using System.Net;
using System.Text.Json;

namespace TransparentCommunicationService
{
    /// <summary>
    /// Manages configuration from multiple sources: command line args, settings file, and console input
    /// </summary>
    internal class ConfigurationManager
    {
        private const string DefaultSettingsFileName = "tcs-settings.json";

        /// <summary>
        /// Loads configuration from all sources in priority order: args > file > console
        /// </summary>
        public static ProxyConfiguration LoadConfiguration(string[] args)
        {
            // Start with default configuration
            var config = new ProxyConfiguration
            {
                BufferSize = Constants.DefaultBufferSize,
                LocalPort = Constants.DefaultLocalPort,
                Timeout = Constants.DefaultTimeout
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
            string settingsFilePath = DefaultSettingsFileName;

            // Check if settings file exists
            if (!File.Exists(settingsFilePath))
            {
                Console.WriteLine($"Settings file not found: {settingsFilePath}");
                return;
            }

            try
            {
                string jsonContent = File.ReadAllText(settingsFilePath);
                var settingsDto = JsonSerializer.Deserialize<SettingsFileDto>(jsonContent);

                if (settingsDto == null)
                {
                    Console.WriteLine("Settings file is empty or invalid");
                    return;
                }

                // Apply settings from file if they exist
                if (!string.IsNullOrEmpty(settingsDto.Endpoint) && 
                    IPAddress.TryParse(settingsDto.Endpoint, out IPAddress? ipAddress))
                {
                    config.RemoteIpAddress = ipAddress;
                }

                if (settingsDto.Port.HasValue && settingsDto.Port.Value >= 1 && settingsDto.Port.Value <= 65535)
                {
                    config.RemotePort = settingsDto.Port.Value;
                }

                if (settingsDto.LocalPort.HasValue && settingsDto.LocalPort.Value >= 1 && settingsDto.LocalPort.Value <= 65535)
                {
                    config.LocalPort = settingsDto.LocalPort.Value;
                }

                if (settingsDto.BufferSize.HasValue && settingsDto.BufferSize.Value > 0)
                {
                    config.BufferSize = settingsDto.BufferSize.Value;
                }

                if (settingsDto.Timeout.HasValue && settingsDto.Timeout.Value >= 0)
                {
                    config.Timeout = settingsDto.Timeout.Value;
                }

                Console.WriteLine($"Configuration loaded from settings file: {settingsFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading settings file: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves the current configuration to a settings file
        /// </summary>
        public static void SaveToSettingsFile(ProxyConfiguration config)
        {
            string settingsFilePath = DefaultSettingsFileName;

            try
            {
                var settingsDto = new SettingsFileDto
                {
                    Endpoint = config.RemoteIpAddress?.ToString(),
                    Port = config.RemotePort,
                    LocalPort = config.LocalPort,
                    BufferSize = config.BufferSize,
                    Timeout = config.Timeout
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string jsonContent = JsonSerializer.Serialize(settingsDto, options);
                File.WriteAllText(settingsFilePath, jsonContent);

                Console.WriteLine($"Configuration saved to: {settingsFilePath}");
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
                if (IPAddress.TryParse(args[0], out IPAddress? ipAddress))
                {
                    config.RemoteIpAddress = ipAddress;
                }

                // Second argument should be remote port
                if (TryParsePort(args[1], out int remotePort))
                {
                    config.RemotePort = remotePort;
                }

                return;
            }

            // Modern format with named parameters
            foreach (string arg in args)
            {
                ProcessArgument(arg.ToLower(), ref config);
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
                [Constants.Endpoint] = (
                    // Parser
                    value => IPAddress.TryParse(value, out IPAddress? ip) ? ip : null,
                    // Setter
                    (cfg, val) => cfg.RemoteIpAddress = (IPAddress?)val,
                    // Error message
                    "Invalid remote IP address format: {0}"
                ),
                
                // Remote port parameter
                [Constants.Port] = (
                    value => TryParsePort(value, out int port) ? (object)port : null,
                    (cfg, val) => cfg.RemotePort = (int)val,
                    "Remote port must be between 1 and 65535: {0}"
                ),
                
                // Local port parameter
                [Constants.LocalPort] = (
                    value => TryParsePort(value, out int port) ? (object)port : null,
                    (cfg, val) => cfg.LocalPort = (int)val,
                    "Local port must be between 1 and 65535: {0}"
                ),
                
                // Buffer size parameter
                [Constants.BufferSize] = (
                    value => int.TryParse(value, out int size) && size > 0 ? (object)size : null,
                    (cfg, val) => cfg.BufferSize = (int)val,
                    "Buffer size must be a positive integer: {0}"
                ),
                
                // Timeout parameter
                [Constants.Timeout] = (
                    value => int.TryParse(value, out int timeout) && timeout >= 0 ? (object)timeout : null,
                    (cfg, val) => cfg.Timeout = (int)val,
                    "Timeout must be a non-negative integer: {0}"
                )
            };

            // Try to process as a named parameter
            foreach (var handler in parameterHandlers)
            {
                string prefix = $"{handler.Key}=";
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    string value = arg.Substring(prefix.Length);
                    var (parser, setter, errorMessage) = handler.Value;
                    
                    // Parse the value
                    object? parsedValue = parser(value);
                    if (parsedValue == null)
                    {
                        Console.WriteLine($"Warning: {string.Format(errorMessage, value)}");
                        return;
                    }
                    
                    // Set the value in the configuration
                    setter(config, parsedValue);
                    return;
                }
            }

            // Handle legacy positional parameters
            if (config.RemoteIpAddress == null && IPAddress.TryParse(arg, out IPAddress? ipAddress))
            {
                config.RemoteIpAddress = ipAddress;
                return;
            }
            
            if (config.RemotePort == 0 && TryParsePort(arg, out int port))
            {
                config.RemotePort = port;
                return;
            }

            Console.WriteLine($"Warning: Unrecognized parameter: {arg}");
            return;
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
            Console.WriteLine($"  Local Port: {config.LocalPort} (default: {Constants.DefaultLocalPort})");
            Console.WriteLine($"  Buffer Size: {config.BufferSize} bytes (default: {Constants.DefaultBufferSize})");
            Console.WriteLine($"  Timeout: {config.Timeout} seconds (default: {Constants.DefaultTimeout})");
            Console.WriteLine();

            var hasRemotePointChanged = false;

            if (config.RemoteIpAddress != null && config.RemotePort > 0)
            {
                Console.Write("Would you like to use it with a new remote point? (y/n): ");

                if (Console.ReadLine()?.ToLower() == "y")
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
                    string? input = Console.ReadLine();
                    
                    if (string.IsNullOrWhiteSpace(input))
                    {
                        Console.WriteLine("Remote IP address is required.");
                        continue;
                    }
                    
                    if (IPAddress.TryParse(input, out IPAddress? ipAddress))
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
                    string? input = Console.ReadLine();
                    
                    if (string.IsNullOrWhiteSpace(input))
                    {
                        Console.WriteLine("Remote port is required.");
                        continue;
                    }
                    
                    if (TryParsePort(input, out int port))
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

            if (hasRemotePointChanged)
            {
                // Handle settings file options
                if (File.Exists(DefaultSettingsFileName))
                {
                    Console.Write("Remote point has changed. Would you like to update the settings file with current configuration? (y/n): ");

                    if (Console.ReadLine()?.ToLower() == "y")
                    {
                        SaveToSettingsFile(config);
                    }
                }
                else
                {
                    Console.Write("Save this configuration to settings file? (y/n): ");

                    if (Console.ReadLine()?.ToLower() == "y")
                    {
                        SaveToSettingsFile(config);
                    }
                }

            }
        }

        private static bool TryParsePort(string value, out int port)
        {
            return int.TryParse(value, out port) && port >= 1 && port <= 65535;
        }

        /// <summary>
        /// Displays usage information for the application
        /// </summary>
        public static void ShowUsage()
        {
            Console.WriteLine($"\nUsage:");
            Console.WriteLine($"  tcs {Constants.Endpoint}=<RemoteIPAddress> {Constants.Port}=<RemotePort> [{Constants.LocalPort}=<LocalPort>] [{Constants.BufferSize}=<BufferSize>] [{Constants.Timeout}=<TimeoutSeconds>]");
            Console.WriteLine($"\nParameters:");
            Console.WriteLine($"  {Constants.Endpoint}=<RemoteIPAddress>\t- The IP address of the target modem");
            Console.WriteLine($"  {Constants.Port}=<RemotePort>\t\t- The port of the modem to forward traffic to");
            Console.WriteLine($"  {Constants.LocalPort}=<LocalPort>\t\t- (Optional) The local port to listen on (default: {Constants.DefaultLocalPort})");
            Console.WriteLine($"  {Constants.BufferSize}=<BufferSize>\t\t- (Optional) Buffer size for data transmission (default: {Constants.DefaultBufferSize})");
            Console.WriteLine($"  {Constants.Timeout}=<TimeoutSeconds>\t- (Optional) Connection timeout in seconds (default: {Constants.DefaultTimeout})");
            Console.WriteLine($"\nExample:");
            Console.WriteLine($"  tcs {Constants.Endpoint}=192.168.1.45 {Constants.Port}=4545 {Constants.LocalPort}=1209 {Constants.Timeout}=60 {Constants.BufferSize}=16384");
            Console.WriteLine($"\nConfiguration Sources (in priority order):");
            Console.WriteLine($"  1. Command-line arguments (highest priority)");
            Console.WriteLine($"  2. Settings file (tcs-settings.json in the current directory)");
            Console.WriteLine($"  3. Console input prompts (if required parameters are missing)");
            Console.WriteLine($"\nAlso supports legacy format:");
            Console.WriteLine($"  tcs <RemoteIPAddress> <RemotePort>");
        }
    }
}
