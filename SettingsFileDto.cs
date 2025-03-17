using System.Text.Json.Serialization;

namespace TransparentCommunicationService
{
    /// <summary>
    /// Data transfer object for settings file serialization/deserialization
    /// </summary>
    internal class SettingsFileDto
    {
        [JsonPropertyName("endpoint")]
        public string? Endpoint { get; set; }
        
        [JsonPropertyName("port")]
        public int? Port { get; set; }
        
        [JsonPropertyName("localPort")]
        public int? LocalPort { get; set; }
        
        [JsonPropertyName("bufferSize")]
        public int? BufferSize { get; set; }
        
        [JsonPropertyName("timeout")]
        public int? Timeout { get; set; }
    }
}
