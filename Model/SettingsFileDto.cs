using System.Text.Json.Serialization;

namespace TransparentCommunicationService.Model
{
    /// <summary>
    /// Data transfer object for settings file serialization/deserialization
    /// </summary>
    internal class SettingsFileDto
    {
        [JsonPropertyName("endpoint")]
        public string? Endpoint { get; init; }
        
        [JsonPropertyName("port")]
        public int? Port { get; init; }
        
        [JsonPropertyName("localPort")]
        public int? LocalPort { get; init; }
        
        [JsonPropertyName("bufferSize")]
        public int? BufferSize { get; init; }
        
        [JsonPropertyName("timeout")]
        public int? Timeout { get; init; }
        
        [JsonPropertyName("enableFileLogging")]
        public bool? EnableFileLogging { get; init; }
        
        [JsonPropertyName("separateDataLogs")]
        public bool? SeparateDataLogs { get; init; }
        
        [JsonPropertyName("logDataPayload")]
        public bool? LogDataPayload { get; init; }
    }
}
