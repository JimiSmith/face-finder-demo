using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Blob;
using ImageResizer;
using System.Linq;

namespace FunctionsFaceDemo
{
    public static class FindFaces
    {
        private const string FACE_GROUP = "demofacegroup";

        [FunctionName("FindFaces")]
        public static async Task Run([BlobTrigger("faces/{name}", Connection = "AzureWebJobsStorage")]ICloudBlob imageFile,
                                     string name,
                                     [Table("faces", Connection = "AzureWebJobsStorage")]IAsyncCollector<FaceRectangle> outTable,
                                     Binder binder,
                                     TraceWriter log)
        {
            if (name.Contains("/")) return;
            using (var imageStream = imageFile.OpenRead())
            {
                var faces = await FaceApi.GetFaces(imageStream);

                if (faces == null) return;

                foreach (var face in faces)
                {
                    // get a thumbnail of the face
                    var thumbnailFileName = Guid.NewGuid();
                    var cloudBlob = await binder.BindAsync<CloudBlockBlob>(new BlobAttribute($"faces/thumbnail/{thumbnailFileName}", FileAccess.ReadWrite));
                    using (var inputMemoryStream = new MemoryStream())
                    using (var memoryStream = new MemoryStream())
                    {
                        imageStream.CopyTo(inputMemoryStream);
                        imageStream.Position = 0;
                        inputMemoryStream.Position = 0;
                        var job = new ImageJob(inputMemoryStream, memoryStream, new Instructions
                        {
                            Width = 200,
                            Height = 200,
                            Mode = FitMode.Crop,
                            CropRectangle = new double[4] {
                                face.FaceRectangle.Left,
                                face.FaceRectangle.Top,
                                face.FaceRectangle.Left + face.FaceRectangle.Width,
                                face.FaceRectangle.Top + face.FaceRectangle.Height,
                            },
                            OutputFormat = OutputFormat.Png
                        });
                        job.Build();
                        memoryStream.Position = 0;
                        await cloudBlob.UploadFromStreamAsync(memoryStream).ConfigureAwait(false);
                        cloudBlob.Properties.ContentType = "image/png";
                        await cloudBlob.SetPropertiesAsync().ConfigureAwait(false);
                    }

                    // add the face to the face list
                    await FaceApi.EnsureFaceListExists(FACE_GROUP);
                    var similarFaces = await FaceApi.FindSimilarFaces(face.FaceId.ToString(), FACE_GROUP);
                    if (similarFaces == null || similarFaces.Count() == 0)
                    {
                        // no similar faces in face group, add it
                        var persistedFace = await FaceApi.AddFaceToFaceList(imageStream, FACE_GROUP, face.FaceRectangle);
                        face.FaceId = persistedFace.PersistedFaceId;
                    }
                    else
                    {
                        face.FaceId = similarFaces.First().PersistedFaceId;
                    }

                    var faceRectangle = face.FaceRectangle;
                    faceRectangle.RowKey = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(DateTimeOffset.UtcNow.ToString()));
                    faceRectangle.PartitionKey = face.FaceId.ToString();
                    faceRectangle.FaceUrl = imageFile.Uri.ToString();
                    faceRectangle.FaceThumbnailUrl = $"https://{imageFile.Uri.Host}/faces/thumbnail/{thumbnailFileName}";
                    await outTable.AddAsync(faceRectangle);
                }
            }
        }
    }
}