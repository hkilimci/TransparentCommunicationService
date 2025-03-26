using System.Net;
using TransparentCommunicationService.Helpers;

namespace TransparentCommunicationService.Model;

/// <summary>
/// Configuration class to hold all proxy settings
/// </summary>
internal class ProxyConfiguration
{
    public IPAddress? RemoteIpAddress { get; set; }
    public int RemotePort { get; set; }
    public int LocalPort { get; set; }
    public int BufferSize { get; set; }
    public int Timeout { get; set; }
        
    // Logging configuration
    public bool EnableFileLogging { get; set; } = true;
    public string LogFilePath 
    { 
        get
        {
            if (RemoteIpAddress == null)
            {
                return Path.Combine(Constants.App.LogsDir, $"tcs.log");
            }
                
            // Create a safe filename by replacing any invalid characters
            var safeIp = RemoteIpAddress.ToString().Replace('.', '-');
            return Path.Combine(Constants.App.LogsDir, $"tcs_{safeIp}_{RemotePort}.log");
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
                
            if (RemoteIpAddress == null)
            {
                return Path.Combine(Constants.App.LogsDir, "tcs_data.log");
            }
                
            // Create a safe filename by replacing any invalid characters
            var safeIp = RemoteIpAddress.ToString().Replace('.', '-');
            return Path.Combine(Constants.App.LogsDir, $"tcs_data_{safeIp}_{RemotePort}.log");
        }
    }
        
    public bool LogDataPayload { get; set; } = true;
    public bool SeparateDataLogs { get; set; }
}