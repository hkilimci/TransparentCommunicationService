using System.Globalization;
using System.Net;
using System.Text.Json;
using TransparentCommunicationService.Model;

namespace TransparentCommunicationService.Helpers;

/// <summary>
/// Manages configuration from multiple sources: command line args, settings file, and console input
/// </summary>
internal static class Configuration
{
    private const string DefaultSettingsFileName = "settings.json";
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
    {
        WriteIndented = true
    };


    /// <summary>
    /// Loads configuration from all sources in priority order: args > file > console
    /// </summary>
    public static ProxyConfiguration LoadConfiguration(string[] args)
    {
        // Start with default configuration
        var config = new ProxyConfiguration
        {
            BufferSize = Constants.Configuration.DefaultBufferSize,
            LocalPort = Constants.Configuration.DefaultLocalPort,
            Timeout = Constants.Configuration.DefaultTimeout,
            EnableFileLogging = Constants.Configuration.DefaultEnableFileLogging,
            SeparateDataLogs = Constants.Configuration.DefaultSeparateDataLogs,
            LogDataPayload = Constants.Configuration.DefaultLogDataPayload
        };

        if (args.Length == 0)
        {
            // Try to load from settings file first (lowest priority)
            LoadFromSettingsFile(ref config);
        }
        else
        {
            // Then try to parse command line arguments (higher priority)
            TryParseCommandLineArguments(args, ref config);
        }
        
        // Finally, prompt for any missing required values (if no command line args provided)
        if (args.Length == 0 || config.RemoteIpAddress == null || config.RemotePort == 0)
        {
            PromptForMissingValues(ref config);
        }

        return config;
    }

    /// <summary>
    /// Loads configuration from a settings file if it exists
    /// </summary>
    private static void LoadFromSettingsFile(ref ProxyConfiguration config)
    {
        // Check if settings file exists
        if (!File.Exists(DefaultSettingsFileName))
        {
            Console.WriteLine($"Settings file not found: {DefaultSettingsFileName}");
            return;
        }

        try
        {
            var jsonContent = File.ReadAllText(DefaultSettingsFileName);
            var settingsDto = JsonSerializer.Deserialize<SettingsFileDto>(jsonContent);

            if (settingsDto == null)
            {
                Console.WriteLine("Settings file is empty or invalid");
                return;
            }

            // Apply settings from file if they exist
            if (!string.IsNullOrEmpty(settingsDto.Endpoint) && IPAddress.TryParse(settingsDto.Endpoint, out var ipAddress))
            {
                config.RemoteIpAddress = ipAddress;
            }

            if (settingsDto.Port is >= 1 and <= 65535)
            {
                config.RemotePort = settingsDto.Port.Value;
            }

            if (settingsDto.LocalPort is >= 1 and <= 65535)
            {
                config.LocalPort = settingsDto.LocalPort.Value;
            }

            if (settingsDto.BufferSize is > 0)
            {
                config.BufferSize = settingsDto.BufferSize.Value;
            }

            if (settingsDto.Timeout is >= 0)
            {
                config.Timeout = settingsDto.Timeout.Value;
            }
            
            // Apply logging settings if available
            if (settingsDto.EnableFileLogging.HasValue)
            {
                config.EnableFileLogging = settingsDto.EnableFileLogging.Value;
            }
            
            if (settingsDto.SeparateDataLogs.HasValue)
            {
                config.SeparateDataLogs = settingsDto.SeparateDataLogs.Value;
            }
            
            if (settingsDto.LogDataPayload.HasValue)
            {
                config.LogDataPayload = settingsDto.LogDataPayload.Value;
            }

            Console.WriteLine($"Configuration loaded from settings file: {DefaultSettingsFileName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading settings file: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves the current configuration to a settings file
    /// </summary>
    private static void SaveToSettingsFile(ProxyConfiguration config)
    {
        try
        {
            var settingsDto = new SettingsFileDto
            {
                Endpoint = config.RemoteIpAddress?.ToString(),
                Port = config.RemotePort,
                LocalPort = config.LocalPort,
                BufferSize = config.BufferSize,
                Timeout = config.Timeout,
                EnableFileLogging = config.EnableFileLogging,
                SeparateDataLogs = config.SeparateDataLogs,
                LogDataPayload = config.LogDataPayload
            };

            var jsonContent = JsonSerializer.Serialize(settingsDto, Options);
            File.WriteAllText(DefaultSettingsFileName, jsonContent);

            Console.WriteLine($"Configuration saved to: {DefaultSettingsFileName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving settings file: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses command line arguments and updates the configuration
    /// </summary>
    private static void TryParseCommandLineArguments(string[] args, ref ProxyConfiguration config)
    {
        // Check for legacy format (2 or 3 positional arguments)
        if (args.Length >= 2 && !args[0].Contains('=') && !args[1].Contains('='))
        {
            // First argument should be IP address
            if (IPAddress.TryParse(args[0], out var ipAddress))
            {
                config.RemoteIpAddress = ipAddress;
            }

            // Second argument should be remote port
            if (TryParsePort(args[1], out var remotePort))
            {
                config.RemotePort = remotePort;
            }

            return;
        }

        // Modern format with named parameters
        foreach (var arg in args)
        {
            ProcessArgument(arg.ToLower(CultureInfo.InvariantCulture), ref config);
        }
    }

    /// <summary>
    /// Processes a single command line argument
    /// </summary>
    private static void ProcessArgument(string arg, ref ProxyConfiguration config)
    {
        // Parameter validation definitions
        var parameterHandlers = new Dictionary<string, (Func<string, object?> parser, Action<ProxyConfiguration, object> setter, string errorMessage)>
        {
            // IP Address parameter
            [Constants.App.Endpoint] = (
                value => IPAddress.TryParse(value, out var ip) ? ip : null,
                (cfg, val) => cfg.RemoteIpAddress = (IPAddress?)val,
                // Error message
                "Invalid remote IP address format: {0}"
            ),
                
            // Remote port parameter
            [Constants.App.Port] = (
                value => TryParsePort(value, out var remotePort) ? remotePort : null,
                (cfg, val) => cfg.RemotePort = (int)val,
                "Remote port must be between 1 and 65535: {0}"
            ),
                
            // Local port parameter
            [Constants.App.LocalPort] = (
                value => TryParsePort(value, out var localPort) ? localPort : null,
                (cfg, val) => cfg.LocalPort = (int)val,
                "Local port must be between 1 and 65535: {0}"
            ),
                
            // Buffer size parameter
            [Constants.App.BufferSize] = (
                value => int.TryParse(value, out var size) && size > 0 ? size : null,
                (cfg, val) => cfg.BufferSize = (int)val,
                "Buffer size must be a positive integer: {0}"
            ),
                
            // Timeout parameter
            [Constants.App.Timeout] = (
                value => int.TryParse(value, out var timeout) && timeout >= 0 ? timeout : null,
                (cfg, val) => cfg.Timeout = (int)val,
                "Timeout must be a non-negative integer: {0}"
            ),
            
            // EnableFileLogging parameter
            [Constants.App.EnableFileLogging] = (
                value => bool.TryParse(value, out var enableLogging) ? enableLogging : null,
                (cfg, val) => cfg.EnableFileLogging = (bool)val,
                "EnableFileLogging must be true or false: {0}"
            ),
            
            // SeparateDataLogs parameter
            [Constants.App.SeparateDataLogs] = (
                value => bool.TryParse(value, out var separateLogs) ? separateLogs : null,
                (cfg, val) => cfg.SeparateDataLogs = (bool)val,
                "SeparateDataLogs must be true or false: {0}"
            ),
            
            // LogDataPayload parameter 
            [Constants.App.LogDataPayload] = (
                value => bool.TryParse(value, out var logPayload) ? logPayload : null,
                (cfg, val) => cfg.LogDataPayload = (bool)val,
                "LogDataPayload must be true or false: {0}"
            )
        };

        // Try to process as a named parameter
        foreach (var handler in parameterHandlers)
        {
            var prefix = $"{handler.Key}=";
            
            if (!arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = arg[prefix.Length..];
            var (parser, setter, errorMessage) = handler.Value;
                    
            // Parse the value
            var parsedValue = parser(value);
            if (parsedValue == null)
            {
                Console.WriteLine($"Warning: {string.Format(CultureInfo.InvariantCulture, errorMessage, value)}");
                return;
            }
                    
            // Set the value in the configuration
            setter(config, parsedValue);
            return;
        }

        // Handle legacy positional parameters
        if (config.RemoteIpAddress == null && IPAddress.TryParse(arg, out var ipAddress))
        {
            config.RemoteIpAddress = ipAddress;
            return;
        }
            
        if (config.RemotePort == 0 && TryParsePort(arg, out var port))
        {
            config.RemotePort = port;
            return;
        }

        Console.WriteLine($"Warning: Unrecognized parameter: {arg}");
    }

    /// <summary>
    /// Prompts the user for any missing required configuration values
    /// </summary>
    private static void PromptForMissingValues(ref ProxyConfiguration config)
    {
        // Display current configuration values
        Console.WriteLine("\nCurrent Configuration:");
        Console.WriteLine($"  Remote Ip Address: {config.RemoteIpAddress}");
        Console.WriteLine($"  Remote Port: {config.RemotePort}");
        Console.WriteLine($"  Local Port: {config.LocalPort} (default: {Constants.Configuration.DefaultLocalPort})");
        Console.WriteLine($"  Buffer Size: {config.BufferSize} bytes (default: {Constants.Configuration.DefaultBufferSize})");
        Console.WriteLine($"  Timeout: {config.Timeout} seconds (default: {Constants.Configuration.DefaultTimeout})");
        Console.WriteLine($"  File Logging: {(config.EnableFileLogging ? "Enabled" : "Disabled")}");
        
        if (config.EnableFileLogging)
        {
            Console.WriteLine($"  Separate Data Logs: {(config.SeparateDataLogs ? "Enabled" : "Disabled")}");
            Console.WriteLine($"  Log Data Payload: {(config.LogDataPayload ? "Enabled" : "Disabled")}");
        }
        
        Console.WriteLine();

        var hasRemotePointChanged = false;

        if (config is { RemoteIpAddress: not null, RemotePort: > 0 })
        {
            Console.Write("Would you like to use it with a new remote point? (y/n): ");

            if (Console.ReadLine()?.ToLower(CultureInfo.InvariantCulture) == "y")
            {
                config.RemoteIpAddress = null;
                config.RemotePort = 0;
            }
        }

        // Prompt for remote IP if not provided or using new remote point
        if (config.RemoteIpAddress == null)
        {
            while (config.RemoteIpAddress == null)
            {
                Console.Write("Enter remote IP address: ");
                var input = Console.ReadLine();
                    
                if (string.IsNullOrWhiteSpace(input))
                {
                    Console.WriteLine("Remote IP address is required.");
                    continue;
                }
                    
                if (IPAddress.TryParse(input, out var ipAddress))
                {
                    config.RemoteIpAddress = ipAddress;
                    hasRemotePointChanged = true;
                }
                else
                {
                    Console.WriteLine("Invalid IP address format. Please try again.");
                }
            }
        }

        // Prompt for remote port if not provided
        if (config.RemotePort == 0)
        {
            while (config.RemotePort == 0)
            {
                Console.Write("Enter remote port: ");
                var input = Console.ReadLine();
                    
                if (string.IsNullOrWhiteSpace(input))
                {
                    Console.WriteLine("Remote port is required.");
                    continue;
                }
                    
                if (TryParsePort(input, out var port))
                {
                    config.RemotePort = port;
                    hasRemotePointChanged = true;
                }
                else
                {
                    Console.WriteLine("Invalid port. Port must be between 1 and 65535.");
                }
            }
        }

        if (!hasRemotePointChanged)
        {
            return;
        }

        // Handle settings file options
        if (File.Exists(DefaultSettingsFileName))
        {
            Console.Write("Remote point has changed. Would you like to update the settings file with current configuration? (y/n): ");

            if (Console.ReadLine()?.ToLower(CultureInfo.InvariantCulture) == "y")
            {
                SaveToSettingsFile(config);
            }
        }
        else
        {
            Console.Write("Save this configuration to settings file? (y/n): ");

            if (Console.ReadLine()?.ToLower(CultureInfo.InvariantCulture) == "y")
            {
                SaveToSettingsFile(config);
            }
        }
    }

    private static bool TryParsePort(string value, out int port)
    {
        return int.TryParse(value, out port) && port is >= 1 and <= 65535;
    }

    /// <summary>
    /// Displays usage information for the application
    /// </summary>
    public static void ShowUsage()
    {
        Console.WriteLine($"\nUsage:");
        Console.WriteLine($"  tcs {Constants.App.Endpoint}=<RemoteIPAddress> {Constants.App.Port}=<RemotePort> [{Constants.App.LocalPort}=<LocalPort>] [{Constants.App.BufferSize}=<BufferSize>] [{Constants.App.Timeout}=<TimeoutSeconds>] [{Constants.App.EnableFileLogging}=<EnableFileLogging>] [{Constants.App.SeparateDataLogs}=<SeparateDataLogs>] [{Constants.App.LogDataPayload}=<LogDataPayload>]");
        Console.WriteLine($"\nParameters:");
        Console.WriteLine($"  {Constants.App.Endpoint}=<RemoteIPAddress>\t- The IP address of the target modem");
        Console.WriteLine($"  {Constants.App.Port}=<RemotePort>\t\t- The port of the modem to forward traffic to");
        Console.WriteLine($"  {Constants.App.LocalPort}=<LocalPort>\t\t- (Optional) The local port to listen on (default: {Constants.Configuration.DefaultLocalPort})");
        Console.WriteLine($"  {Constants.App.BufferSize}=<BufferSize>\t\t- (Optional) Buffer size for data transmission (default: {Constants.Configuration.DefaultBufferSize})");
        Console.WriteLine($"  {Constants.App.Timeout}=<TimeoutSeconds>\t- (Optional) Connection timeout in seconds (default: {Constants.Configuration.DefaultTimeout})");
        Console.WriteLine($"  {Constants.App.EnableFileLogging}=<EnableFileLogging>\t- (Optional) Enable file logging (default: {Constants.Configuration.DefaultEnableFileLogging})");
        Console.WriteLine($"  {Constants.App.SeparateDataLogs}=<SeparateDataLogs>\t- (Optional) Separate data logs (default: {Constants.Configuration.DefaultSeparateDataLogs})");
        Console.WriteLine($"  {Constants.App.LogDataPayload}=<LogDataPayload>\t- (Optional) Log data payload (default: {Constants.Configuration.DefaultLogDataPayload})");
        Console.WriteLine($"\nExample:");
        Console.WriteLine($"  tcs {Constants.App.Endpoint}=192.168.1.45 {Constants.App.Port}=4545 {Constants.App.LocalPort}=1209 {Constants.App.Timeout}=60 {Constants.App.BufferSize}=16384 {Constants.App.EnableFileLogging}=true {Constants.App.SeparateDataLogs}=true {Constants.App.LogDataPayload}=true");
        Console.WriteLine($"\nConfiguration Sources (in priority order):");
        Console.WriteLine($"  1. Command-line arguments (highest priority)");
        Console.WriteLine($"  2. Settings file (tcs-settings.json in the current directory)");
        Console.WriteLine($"  3. Console input prompts (if required parameters are missing)");
        Console.WriteLine($"\nAlso supports legacy format:");
        Console.WriteLine($"  tcs <RemoteIPAddress> <RemotePort>");
    }
}