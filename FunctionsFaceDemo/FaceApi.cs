using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace FunctionsFaceDemo
{
    public static class FaceApi
    {
        static HttpClient GetHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", Environment.GetEnvironmentVariable("CognitiveServicesApiKey"));
            return client;
        }

        public static async Task EnsureFaceListExists(string faceListId)
        {
            using (var client = GetHttpClient())
            {
                var url = $"{Environment.GetEnvironmentVariable("CognitiveServicesHost")}/face/v1.0/facelists/{faceListId}";
                if ((await client.GetAsync(url)).StatusCode == HttpStatusCode.NotFound)
                {
                    await client.PutAsJsonAsync(url, new
                    {
                        name = faceListId
                    });
                }
            }
        }

        public static async Task<IEnumerable<PersistedFace>> FindSimilarFaces(string faceId, string faceListId)
        {
            using (var client = GetHttpClient())
            {
                var url = $"{Environment.GetEnvironmentVariable("CognitiveServicesHost")}/face/v1.0/findsimilars";
                var httpResponse = await client.PostAsJsonAsync(url, new
                {
                    faceId = faceId,
                    faceListId = faceListId
                });

                if (httpResponse.StatusCode == HttpStatusCode.OK)
                {
                    return JsonConvert.DeserializeObject<List<PersistedFace>>(await httpResponse.Content.ReadAsStringAsync())
                        .Where(f => f.Confidence > 0.6);
                }
            }
            return null;
        }

        public static async Task<PersistedFace> AddFaceToFaceList(Stream imageStream, string faceListId, FaceRectangle targetFace)
        {
            using (var client = GetHttpClient())
            using (var memStream = new MemoryStream())
            {
                imageStream.CopyTo(memStream);
                imageStream.Position = 0;
                memStream.Position = 0;
                var content = new StreamContent(memStream);
                var targetFaceString = $"{targetFace.Left},{targetFace.Top},{targetFace.Width},{targetFace.Height}";
                var url = $"{Environment.GetEnvironmentVariable("CognitiveServicesHost")}/face/v1.0/facelists/{faceListId}/persistedFaces?targetFace={targetFaceString}";
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                var httpResponse = await client.PostAsync(url, content);

                if (httpResponse.StatusCode == HttpStatusCode.OK)
                {
                    return JsonConvert.DeserializeObject<PersistedFace>(await httpResponse.Content.ReadAsStringAsync());
                }
            }
            return null;
        }

        public static async Task<List<Face>> GetFaces(Stream imageStream)
        {
            using (var client = GetHttpClient())
            using (var memStream = new MemoryStream())
            {
                imageStream.CopyTo(memStream);
                if (imageStream.CanSeek)
                {
                    imageStream.Position = 0;
                }
                memStream.Position = 0;
                var content = new StreamContent(memStream);
                var url = $"{Environment.GetEnvironmentVariable("CognitiveServicesHost")}/face/v1.0/detect?returnFaceId=true&returnFaceLandmarks=false";
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                var httpResponse = await client.PostAsync(url, content);

                if (httpResponse.StatusCode == HttpStatusCode.OK)
                {
                    return JsonConvert.DeserializeObject<List<Face>>(await httpResponse.Content.ReadAsStringAsync());
                }
            }
            return null;
        }
    }
}
