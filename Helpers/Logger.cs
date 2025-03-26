using System.Text;
using System.Globalization;
using TransparentCommunicationService.Model;

namespace TransparentCommunicationService.Helpers;

internal static class Logger
{
    private static ProxyConfiguration? _config;
    private static readonly object LockObject = new object();
    private static readonly object DataLockObject = new object();

    public static void Initialize(ProxyConfiguration config)
    {
        _config = config;

        if (!config.EnableFileLogging)
        {
            return;
        }

        EnsureLogDirectoryExists(config.LogFilePath);
            
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        WriteToFile($"=== Transparent Communication Service Log Started at {timestamp} ===\n");
            
        // Initialize data log file if separate logging is enabled
        if (!config.SeparateDataLogs)
        {
            return;
        }

        EnsureLogDirectoryExists(config.DataLogFilePath);
        WriteToDataFile($"=== Transparent Communication Service Data Log Started at {timestamp} ===\n");
    }

    public static void LogData(string direction, byte[] buffer, int bytesRead)
    {
        var sb = new StringBuilder();
        var formattedMessage = string.Format(CultureInfo.InvariantCulture, 
            "{0} [DATA] {1} [{2} bytes]: ", 
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture), 
            direction,
            bytesRead);
        
        sb.AppendLine(formattedMessage);

        // Display all data in hex format if payload logging is enabled
        if (_config?.LogDataPayload ?? true)
        {
            for (var i = 0; i < bytesRead; i++)
            {
                sb.Append(CultureInfo.InvariantCulture, $"{buffer[i]:X2} ");

                // Add a newline every 16 bytes for better readability
                if ((i + 1) % 16 != 0 || i >= bytesRead - 1)
                {
                    continue;
                }

                sb.AppendLine();
                sb.Append("                                      "); // Align with the start of hex data
            }
        }
        else
        {
            sb.AppendLine("<LogDataPayload=false>");
        }

        var logMessage = sb.ToString();
        Console.WriteLine(logMessage);
        
        // If separate data logs are enabled, write to the data log file
        if (_config?.SeparateDataLogs ?? false)
        {
            WriteToDataFile(logMessage);            
        }

        // Write to the main log file
        WriteToFile(logMessage);
    }
        
    public static void LogInfo(string message)
    {
        var formattedMessage = string.Format(CultureInfo.InvariantCulture, 
            "{0} [INFO] {1}", 
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture), 
            message);
        
        Console.WriteLine(formattedMessage);
        WriteToFile(formattedMessage);
    }
    
    public static void LogError(string message, Exception? ex = null)
    {
        var formattedMessage = string.Format(CultureInfo.InvariantCulture, 
            "{0} [ERROR] {1}", 
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture), 
            message);
            
        if (ex != null)
        {
            formattedMessage += $" - {ex.Message}";
            if (ex.StackTrace != null)
            {
                formattedMessage += $"\n{ex.StackTrace}";
            }
        }
        
        Console.WriteLine(formattedMessage);
        WriteToFile(formattedMessage);
    }
        
    public static void DisplayWelcomeMessage()
    {
        Console.WriteLine("Transparent TCP Proxy - Virtual Modem Relay");
        Console.WriteLine("-------------------------------------------");
    }
        
    public static void DisplayServerStartInfo(ProxyConfiguration config)
    {
        var sb = new StringBuilder();
        sb.AppendLine("\n=== Transparent Communication Service Started ===");
        sb.Append(CultureInfo.InvariantCulture, $"Listening on: localhost:{config.LocalPort}\n");
        sb.Append(CultureInfo.InvariantCulture, $"Forwarding to: {config.RemoteIpAddress}:{config.RemotePort}\n\n");
        sb.AppendLine("Active Configuration:");
        sb.Append(CultureInfo.InvariantCulture, $"  Local Port: {config.LocalPort}\n");
        sb.Append(CultureInfo.InvariantCulture, $"  Remote Endpoint: {config.RemoteIpAddress}:{config.RemotePort}\n");
        sb.Append(CultureInfo.InvariantCulture, $"  Buffer Size: {config.BufferSize} bytes\n");
        sb.Append(CultureInfo.InvariantCulture, $"  Timeout: {config.Timeout} seconds\n");
        sb.Append(CultureInfo.InvariantCulture, $"  File Logging: {(config.EnableFileLogging ? "Enabled" : "Disabled")}");
                      
        if (config.EnableFileLogging)
        {
            sb.Append(CultureInfo.InvariantCulture, $"\n  Log File: {Path.GetFullPath(config.LogFilePath)}");
            
            if (config.SeparateDataLogs)
            {
                sb.Append(CultureInfo.InvariantCulture, $"\n  Data Log File: {Path.GetFullPath(config.DataLogFilePath)}");
            }
        }
        
        sb.AppendLine("\n\nPress Ctrl+C to exit.\n");
        
        var message = sb.ToString();
        
        Console.WriteLine(message);
        WriteToFile(message);
    }
    
    private static void WriteToFile(string message)
    {
        // Skip file writing if file logging is disabled
        if (_config is not { EnableFileLogging: true })
        {
            return;
        }

        try
        {
            var logFilePath = _config.LogFilePath;
            
            // Ensure log directory exists before writing
            EnsureLogDirectoryExists(logFilePath);
            
            lock (LockObject)
            {
                File.AppendAllText(logFilePath, message + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            // Don't use LogError to avoid infinite recursion
            Console.WriteLine($"Error writing to log file: {ex.Message}");
        }
    }
    
    private static void WriteToDataFile(string message)
    {
        // Skip data file writing if file logging is disabled or separate data logs are disabled
        if (_config is not { EnableFileLogging: true, SeparateDataLogs: true })
        {
            return;
        }
        
        try
        {
            var dataLogFilePath = _config.DataLogFilePath;
            
            // Ensure log directory exists before writing
            EnsureLogDirectoryExists(dataLogFilePath);
            
            lock (DataLockObject)
            {
                File.AppendAllText(dataLogFilePath, message + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            // Don't use LogError to avoid infinite recursion
            Console.WriteLine($"Error writing to data log file: {ex.Message}");
        }
    }
    
    private static void EnsureLogDirectoryExists(string logFilePath)
    {
        var directory = Path.GetDirectoryName(logFilePath);
        
        if (string.IsNullOrEmpty(directory) || Directory.Exists(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
    }
}