using System.Globalization;
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
        var config = CreateDefaultConfiguration();

        // Always try to load from settings file first (lowest priority)
        LoadFromSettingsFile(ref config);

        // Apply command line arguments on top (higher priority)
        if (args.Length > 0)
        {
            TryParseCommandLineArguments(args, ref config);
        }

        // Prompt for any missing required values (interactive if no args, or if endpoints still missing)
        if (args.Length == 0 || config.RemoteEndpoints.Count == 0)
        {
            PromptForMissingValues(ref config);
        }

        return config;
    }

    /// <summary>
    /// Loads configuration from settings and optional args without interactive console prompts.
    /// </summary>
    public static ProxyConfiguration LoadConfigurationForGui(string[]? args = null)
    {
        var config = CreateDefaultConfiguration();
        LoadFromSettingsFile(ref config);

        if (args is { Length: > 0 })
        {
            TryParseCommandLineArguments(args, ref config);
        }

        return config;
    }

    /// <summary>
    /// Loads configuration from a specific settings file.
    /// </summary>
    public static ProxyConfiguration LoadConfigurationFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A settings file path is required.", nameof(filePath));
        }

        var config = CreateDefaultConfiguration();
        ApplySettingsDto(ref config, ReadSettingsFile(filePath));
        return config;
    }

    /// <summary>
    /// Saves configuration to the default settings file.
    /// </summary>
    public static void SaveConfigurationToFile(ProxyConfiguration config)
    {
        SaveConfigurationToFile(config, DefaultSettingsFileName);
    }

    /// <summary>
    /// Saves configuration to a specific settings file.
    /// </summary>
    public static void SaveConfigurationToFile(ProxyConfiguration config, string filePath)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A settings file path is required.", nameof(filePath));
        }

        var settingsDto = CreateSettingsFileDto(config);
        var jsonContent = JsonSerializer.Serialize(settingsDto, Options);
        File.WriteAllText(filePath, jsonContent);
    }

    /// <summary>
    /// Public wrapper used by GUI input validation.
    /// </summary>
    public static bool TryParseEndpointValue(string input, out RemoteEndpoint endpoint)
    {
        return TryParseEndpoint(input, out endpoint);
    }

    /// <summary>
    /// Public wrapper used by GUI input validation.
    /// </summary>
    public static bool TryParsePortValue(string? input, out int port)
    {
        return TryParsePort(input, out port);
    }

    private static ProxyConfiguration CreateDefaultConfiguration()
    {
        return new ProxyConfiguration
        {
            BufferSize = Constants.Configuration.DefaultBufferSize,
            LocalPort = Constants.Configuration.DefaultLocalPort,
            Timeout = Constants.Configuration.DefaultTimeout,
            EnableFileLogging = Constants.Configuration.DefaultEnableFileLogging,
            SeparateDataLogs = Constants.Configuration.DefaultSeparateDataLogs,
            LogDataPayload = Constants.Configuration.DefaultLogDataPayload
        };
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
            ApplySettingsDto(ref config, ReadSettingsFile(DefaultSettingsFileName));

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
            SaveConfigurationToFile(config);
            Console.WriteLine($"Configuration saved to: {DefaultSettingsFileName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving settings file: {ex.Message}");
        }
    }

    private static SettingsFileDto ReadSettingsFile(string filePath)
    {
        var jsonContent = File.ReadAllText(filePath);
        var settingsDto = JsonSerializer.Deserialize<SettingsFileDto>(jsonContent);

        return settingsDto ?? throw new InvalidDataException("Settings file is empty or invalid.");
    }

    private static void ApplySettingsDto(ref ProxyConfiguration config, SettingsFileDto settingsDto)
    {
        if (settingsDto.Endpoints != null)
        {
            var endpoints = new List<RemoteEndpoint>();
            foreach (var endpointStr in settingsDto.Endpoints)
            {
                if (TryParseEndpoint(endpointStr, out var endpoint))
                {
                    endpoints.Add(endpoint);
                }
                else
                {
                    Console.WriteLine($"Warning: Invalid endpoint format in settings file: {endpointStr}");
                }
            }

            config.RemoteEndpoints = endpoints;
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
    }

    private static SettingsFileDto CreateSettingsFileDto(ProxyConfiguration config)
    {
        return new SettingsFileDto
        {
            Endpoints = config.RemoteEndpoints.Select(e => $"{e.Host}:{e.Port}").ToList(),
            LocalPort = config.LocalPort,
            BufferSize = config.BufferSize,
            Timeout = config.Timeout,
            EnableFileLogging = config.EnableFileLogging,
            SeparateDataLogs = config.SeparateDataLogs,
            LogDataPayload = config.LogDataPayload
        };
    }

    /// <summary>
    /// Parses command line arguments and updates the configuration
    /// </summary>
    private static void TryParseCommandLineArguments(string[] args, ref ProxyConfiguration config)
    {
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
            // Endpoints parameter (comma-separated list)
            ["endpoints"] = (
                value =>
                {
                    var endpoints = new List<RemoteEndpoint>();
                    var endpointStrings = value.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var endpointStr in endpointStrings)
                    {
                        if (TryParseEndpoint(endpointStr, out var endpoint))
                        {
                            endpoints.Add(endpoint);
                        }
                        else
                        {
                            Console.WriteLine($"Warning: Invalid endpoint format in command line: {endpointStr}");
                        }
                    }
                    return endpoints.Count != 0 ? endpoints : null;
                },
                (cfg, val) => cfg.RemoteEndpoints.AddRange((List<RemoteEndpoint>)val!),
                "Invalid format for endpoints: {0}"
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

        Console.WriteLine($"Warning: Unrecognized parameter: {arg}");
    }

    /// <summary>
    /// Prompts the user for any missing required configuration values
    /// </summary>
    private static void PromptForMissingValues(ref ProxyConfiguration config)
    {
        // Display current configuration values
        Console.WriteLine("\nCurrent Configuration:");
        Console.WriteLine($"  Remote Endpoints: {(config.RemoteEndpoints.Count != 0 ? string.Join(", ", config.RemoteEndpoints.Select(e => $"{e.Host}:{e.Port}")) : "Not set")}");
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

        if (config.RemoteEndpoints.Count != 0)
        {
            Console.Write("Would you like to clear existing remote endpoints and add new ones? (y/n): ");

            if (Console.ReadLine()?.ToLower(CultureInfo.InvariantCulture) == "y")
            {
                config.RemoteEndpoints.Clear();
                hasRemotePointChanged = true;
            }
        }

        // Prompt for remote endpoints if none are configured
        if (config.RemoteEndpoints.Count == 0)
        {
            Console.WriteLine("Enter remote endpoints in 'ip:port' format. Press Enter on an empty line to finish.");
            while (true)
            {
                Console.Write("Enter remote endpoint: ");
                var input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                {
                    if (config.RemoteEndpoints.Count != 0) break; // Exit if at least one endpoint was added
                    Console.WriteLine("At least one remote endpoint is required.");
                    continue;
                }

                if (TryParseEndpoint(input, out var endpoint))
                {
                    config.RemoteEndpoints.Add(endpoint);
                    hasRemotePointChanged = true;
                }
                else
                {
                    Console.WriteLine("Invalid format. Please use 'ip:port' (e.g., 127.0.0.1:8080).");
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
            Console.Write("Remote endpoints have changed. Would you like to update the settings file with the current configuration? (y/n): ");

            if (Console.ReadLine()?.ToLower(CultureInfo.InvariantCulture) == "y")
            {
                SaveToSettingsFile(config);
            }
        }
        else
        {
            Console.Write("Would you like to save this configuration to a new settings.json file? (y/n): ");
            if (Console.ReadLine()?.ToLower(CultureInfo.InvariantCulture) == "y")
            {
                SaveToSettingsFile(config);
            }
        }
    }

    /// <summary>
    /// Shows application usage instructions
    /// </summary>
    public static void ShowUsage()
    {
        Console.WriteLine("\nUsage: TransparentCommunicationService [options]");
        Console.WriteLine("\nOptions:");
        Console.WriteLine("  endpoints=<ip1:port1>,<ip2:port2>,...   (e.g., endpoints=192.168.1.100:5000,10.0.0.1:5001)");
        Console.WriteLine($"  localport=<port>                          (default: {Constants.Configuration.DefaultLocalPort})");
        Console.WriteLine($"  buffersize=<bytes>                      (default: {Constants.Configuration.DefaultBufferSize})");
        Console.WriteLine($"  timeout=<seconds>                       (default: {Constants.Configuration.DefaultTimeout})");
        Console.WriteLine("  enablefilelogging=<true|false>            (default: true)");
        Console.WriteLine("  separatedatalogs=<true|false>             (default: false)");
        Console.WriteLine("  logdatapayload=<true|false>               (default: true)");
        Console.WriteLine("\nExample:");
        Console.WriteLine("  TransparentCommunicationService endpoints=127.0.0.1:8080 localport=9000");
        Console.WriteLine();
    }

    /// <summary>
    /// Tries to parse a string into a port number
    /// </summary>
    private static bool TryParsePort(string? input, out int port)
    {
        if (int.TryParse(input, out port) && port is >= 1 and <= 65535)
        {
            return true;
        }

        port = 0;
        return false;
    }
    
    /// <summary>
    /// Tries to parse an endpoint string into a RemoteEndpoint object.
    /// Supported formats:
    ///   host:port         (e.g., "127.0.0.1:8080", "myserver.local:8080")
    ///   [ipv6]:port       (e.g., "[::1]:8080")
    /// </summary>
    private static bool TryParseEndpoint(string input, out RemoteEndpoint endpoint)
    {
        endpoint = null!;
        input = input.Trim();

        string host;
        string portStr;

        // Handle IPv6 bracketed format: [::1]:8080
        if (input.StartsWith('['))
        {
            var closingBracket = input.IndexOf(']');
            if (closingBracket < 0 || closingBracket + 1 >= input.Length || input[closingBracket + 1] != ':')
            {
                return false;
            }

            host = input.Substring(1, closingBracket - 1);
            portStr = input.Substring(closingBracket + 2);
        }
        else
        {
            // Use LastIndexOf so that plain IPv4 and hostnames work; the port is always last
            var lastColon = input.LastIndexOf(':');
            if (lastColon < 0)
            {
                return false;
            }

            host = input.Substring(0, lastColon);
            portStr = input.Substring(lastColon + 1);
        }

        if (string.IsNullOrWhiteSpace(host) || !TryParsePort(portStr, out var port))
        {
            return false;
        }

        endpoint = new RemoteEndpoint(host, port);
        return true;
    }
}
