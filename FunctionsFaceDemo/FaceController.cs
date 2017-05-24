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
            var imageStream = await req.Content.ReadAsStreamAsync();
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
            var image = await req.Content.ReadAsStreamAsync();
            using (var writer = await binder.BindAsync<Stream>(new BlobAttribute($"faces/{Guid.NewGuid()}", FileAccess.Write)))
            {
                await image.CopyToAsync(writer);
            }
            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}