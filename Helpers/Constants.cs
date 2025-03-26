namespace TransparentCommunicationService.Helpers;

internal static class Constants
{
    public static class Configuration
    {
        // Default values for configurable parameters
        public const int DefaultBufferSize = 8192;
        public const int DefaultLocalPort = 1209;
        public const int DefaultTimeout = 30; // Default timeout in seconds
        
        // Default logging values
        public const bool DefaultEnableFileLogging = true;
        public const bool DefaultSeparateDataLogs = false;
        public const bool DefaultLogDataPayload = true;
    }
    
    public static class App
    {
        public const string Endpoint = "endpoint";
        public const string Port = "port";
        public const string LocalPort = "localport";
        public const string Timeout = "timeout";
        public const string BufferSize = "buffer";
        
        public const string LogsDir = "logs";
        // Logging parameters
        public const string EnableFileLogging = "enablefilelogging";
        public const string SeparateDataLogs = "separatedatalogs";
        public const string LogDataPayload = "logdatapayload";
    }
}