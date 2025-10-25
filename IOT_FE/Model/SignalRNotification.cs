using System.Text.Json.Serialization;

namespace IOT_FE.Model
{
    public class SignalRNotification
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;

        [JsonPropertyName("FillLevel")]
        public double FillLevel { get; set; }

        [JsonPropertyName("imageUrl")]
        public string ImageUrl { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public DateTime? Timestamp { get; set; }


    }
}