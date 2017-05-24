using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;

namespace FunctionsFaceDemo
{
    public class PersistedFace
    {
        [JsonProperty("persistedFaceId")]
        public Guid PersistedFaceId { get; set; }

        [JsonProperty("confidence")]
        public double Confidence { get; set; }
    }

    public class Face
    {
        [JsonProperty("faceId")]
        public Guid FaceId { get; set; }

        [JsonProperty("faceRectangle")]
        public FaceRectangle FaceRectangle { get; set; }
    }

    public class FaceRectangle : TableEntity
    {
        [JsonProperty("faceUrl")]
        public string FaceUrl { get; set; }

        [JsonProperty("faceThumbnailUrl")]
        public string FaceThumbnailUrl { get; set; }

        [JsonProperty("left")]
        public int Left { get; set; }

        [JsonProperty("top")]
        public int Top { get; set; }

        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }
    }
}
