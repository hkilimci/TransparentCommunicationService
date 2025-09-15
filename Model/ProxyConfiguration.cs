using TransparentCommunicationService.Helpers;

namespace TransparentCommunicationService.Model;

/// <summary>
/// Configuration class to hold all proxy settings
/// </summary>
internal sealed class ProxyConfiguration
{
    public List<RemoteEndpoint> RemoteEndpoints { get; set; } = new();
    public int LocalPort { get; set; }
    public int BufferSize { get; set; }
    public int Timeout { get; set; }
        
    // Logging configuration
    public bool EnableFileLogging { get; set; } = true;
    public string LogFilePath 
    { 
        get
        {
            // If there is more than one remote endpoint, use a generic log file name
            if (RemoteEndpoints.Count > 1)
            {
                return Path.Combine(Constants.App.LogsDir, "tcs_multi_endpoint.log");
            }

            var firstEndpoint = RemoteEndpoints.FirstOrDefault();
            if (firstEndpoint == null)
            {
                return Path.Combine(Constants.App.LogsDir, "tcs.log");
            }
                
            // Create a safe filename by replacing any invalid characters
            var safeIp = firstEndpoint.IpAddress.ToString().Replace('.', '-');
            return Path.Combine(Constants.App.LogsDir, $"tcs_{safeIp}_{firstEndpoint.Port}.log");
        }
    }
        
    public string DataLogFilePath
    {
        get
        {
            // If separate data logging is disabled, return the main log path
            if (!SeparateDataLogs)
            {
                return LogFilePath;
            }
            
            // If there is more than one remote endpoint, use a generic data log file name
            if (RemoteEndpoints.Count > 1)
            {
                return Path.Combine(Constants.App.LogsDir, "tcs_multi_endpoint_data.log");
            }

            var firstEndpoint = RemoteEndpoints.FirstOrDefault();
            if (firstEndpoint == null)
            {
                return Path.Combine(Constants.App.LogsDir, "tcs_data.log");
            }
                
            // Create a safe filename by replacing any invalid characters
            var safeIp = firstEndpoint.IpAddress.ToString().Replace('.', '-');
            return Path.Combine(Constants.App.LogsDir, $"tcs_data_{safeIp}_{firstEndpoint.Port}.log");
        }
    }
        
    public bool LogDataPayload { get; set; } = true;
    public bool SeparateDataLogs { get; set; }
}