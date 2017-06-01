using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using System.IO;
using System.Collections.Generic;
using System;
using ImageResizer;
using Microsoft.WindowsAzure.Storage.Blob;

namespace FunctionsFaceDemo
{
    public static class FaceController
    {
        private const string FACE_GROUP = "demofacegroup";

        internal class FaceIdEqualityComparer : IEqualityComparer<FaceRectangle>
        {
            public bool Equals(FaceRectangle x, FaceRectangle y)
            {
                return x?.PartitionKey == y?.PartitionKey;
            }

            public int GetHashCode(FaceRectangle obj)
            {
                return obj?.PartitionKey.GetHashCode() ?? -1;
            }
        }

        [FunctionName("ListFaceIds")]
        public static HttpResponseMessage ListFaceIds([HttpTrigger(AuthorizationLevel.Function, "get", Route = "faces")]HttpRequestMessage req,
                                                      [Table("faces", Connection = "AzureWebJobsStorage")] IQueryable<FaceRectangle> faces,
                                                      TraceWriter log)
        {
            return req.CreateResponse(HttpStatusCode.OK, faces.ToList().Distinct(new FaceIdEqualityComparer()));
        }

        [FunctionName("ListFaceOccurences")]
        public static HttpResponseMessage ListFaceOccurences([HttpTrigger(AuthorizationLevel.Function, "get", Route = "faces/{faceId}")]HttpRequestMessage req,
                                                             [Table("faces", Connection = "AzureWebJobsStorage")] IQueryable<FaceRectangle> faces,
                                                             string faceId,
                                                             TraceWriter log)
        {
            return req.CreateResponse(HttpStatusCode.OK, faces.Where(f => f.PartitionKey == faceId));
        }

        [FunctionName("ListSimilarFaces")]
        public static async Task<HttpResponseMessage> ListSimilarFaces([HttpTrigger(AuthorizationLevel.Function, "post", Route = "faces/similar")]HttpRequestMessage req,
                                                                       [Table("faces", Connection = "AzureWebJobsStorage")] IQueryable<FaceRectangle> faces,
                                                                       TraceWriter log)
        {
            var formData = await req.Content.ReadAsMultipartAsync();
            var imageStream = await formData.Contents.FirstOrDefault(f => f.Headers.ContentType.MediaType.StartsWith("image/"))?.ReadAsStreamAsync();
            if (imageStream == null)
            {
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, "No image specified");
            }
            if (imageStream.Length > 4 * 1024 * 1024)
            {
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, "Image too large");
            }
            var recognizedFaces = await FaceApi.GetFaces(imageStream);
            var persistedFaces = new List<FaceRectangle>();
            foreach (var face in recognizedFaces)
            {
                var similarFaces = await FaceApi.FindSimilarFaces(face.FaceId.ToString(), FACE_GROUP);
                var firstId = similarFaces.FirstOrDefault()?.PersistedFaceId;
                if (firstId != null)
                {
                    var faceRect = faces.Where(f => f.PartitionKey == firstId.Value.ToString()).FirstOrDefault();
                    if (faceRect != null)
                    {
                        persistedFaces.Add(faceRect);
                    }
                }
            }
            return req.CreateResponse(HttpStatusCode.OK, persistedFaces);
        }

        [FunctionName("UploadFaces")]
        public static async Task<HttpResponseMessage> UploadFaces([HttpTrigger(AuthorizationLevel.Function, "post", Route = "faces")]HttpRequestMessage req,
                                                                  Binder binder,
                                                                  TraceWriter log)
        {
            var formData = await req.Content.ReadAsMultipartAsync();
            var imageStream = await formData.Contents.FirstOrDefault(f => f.Headers.ContentType.MediaType.StartsWith("image/"))?.ReadAsStreamAsync();
            if (imageStream == null)
            {
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, "No image specified");
            }
            if (imageStream.Length > 4 * 1024 * 1024)
            {
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, "Image too large");
            }
            using (var inputMemoryStream = new MemoryStream())
            using (var memoryStream = new MemoryStream())
            {
                imageStream.CopyTo(inputMemoryStream);
                imageStream.Position = 0;
                inputMemoryStream.Position = 0;
                var job = new ImageJob(inputMemoryStream, memoryStream, new Instructions
                {
                    Width = 1440,
                    Height = 1440,
                    Mode = FitMode.Max,
                    AutoRotate = true,
                    OutputFormat = OutputFormat.Png
                });
                job.Build();
                memoryStream.Position = 0;
                var cloudBlob = await binder.BindAsync<CloudBlockBlob>(new BlobAttribute($"faces/{Guid.NewGuid()}", FileAccess.ReadWrite));
                await cloudBlob.UploadFromStreamAsync(memoryStream).ConfigureAwait(false);
                cloudBlob.Properties.ContentType = "image/png";
                await cloudBlob.SetPropertiesAsync().ConfigureAwait(false);
            }
            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}