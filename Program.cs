using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Collections.Generic;

namespace TransparentCommunicationService
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            DisplayWelcomeMessage();

            // Parse command line arguments
            if (!TryParseCommandLineArguments(args, out var config))
            {
                return; // Error messages and usage info already displayed by the parsing method
            }

            try
            {
                await RunProxyServer(config);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private static void DisplayWelcomeMessage()
        {
            Console.WriteLine("Transparent TCP Proxy - Virtual Modem Relay");
            Console.WriteLine("-------------------------------------------");
        }

        private static bool TryParseCommandLineArguments(string[] args, out ProxyConfiguration config)
        {
            // Initialize with default values
            config = new ProxyConfiguration
            {
                BufferSize = Constants.DefaultBufferSize,
                LocalPort = Constants.DefaultLocalPort,
                Timeout = Constants.DefaultTimeout
            };

            // Handle empty arguments
            if (args.Length == 0)
            {
                ShowUsage();
                return false;
            }

            foreach (string arg in args)
            {
                if (!TryProcessArgument(arg, ref config))
                {
                    return false;
                }
            }

            // Validate required parameters
            if (config.RemoteIpAddress == null)
            {
                Console.WriteLine("Error: Remote IP address is required.");
                ShowUsage();
                return false;
            }

            if (config.RemotePort == 0)
            {
                Console.WriteLine("Error: Remote port is required.");
                ShowUsage();
                return false;
            }

            return true;
        }

        private static bool TryProcessArgument(string arg, ref ProxyConfiguration config)
        {
            arg = arg.ToLower();

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
                        Console.WriteLine($"Error: {string.Format(errorMessage, value)}");
                        ShowUsage();
                        return false;
                    }
                    
                    // Set the value in the configuration
                    setter(config, parsedValue);
                    return true;
                }
            }

            // Handle legacy positional parameters
            if (config.RemoteIpAddress == null && IPAddress.TryParse(arg, out IPAddress? ipAddress))
            {
                config.RemoteIpAddress = ipAddress;
                return true;
            }
            
            if (config.RemotePort == 0 && TryParsePort(arg, out int port))
            {
                config.RemotePort = port;
                return true;
            }

            // If we get here, the argument wasn't recognized
            Console.WriteLine($"Error: Unrecognized or invalid parameter: {arg}");
            ShowUsage();
            return false;
        }

        private static bool TryParsePort(string value, out int port)
        {
            return int.TryParse(value, out port) && port >= 1 && port <= 65535;
        }

        private static async Task RunProxyServer(ProxyConfiguration config)
        {
            // Create TCP listener
            using var listener = new TcpListener(IPAddress.Any, config.LocalPort);
            listener.Start();

            DisplayServerStartInfo(config);

            // Setup cancellation
            using var cts = SetupCancellation();

            // Accept and handle client connections
            await AcceptClientsLoop(listener, config, cts.Token);

            // Cleanup
            listener.Stop();
            Console.WriteLine("Proxy stopped.");
        }

        private static CancellationTokenSource SetupCancellation()
        {
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true; // Prevent the process from terminating immediately
                cts.Cancel();
                Console.WriteLine("Shutting down...");

                // Ensure the application exits after cleanup
                Task.Run(async () =>
                {
                    // Give some time for cleanup operations to complete
                    await Task.Delay(1000);
                    Environment.Exit(0);
                });
            };
            return cts;
        }

        private static void DisplayServerStartInfo(ProxyConfiguration config)
        {
            Console.WriteLine($"Proxy started. Listening on localhost:{config.LocalPort}");
            Console.WriteLine($"Forwarding connections to {config.RemoteIpAddress}:{config.RemotePort}");
            Console.WriteLine($"Buffer size: {config.BufferSize} bytes, Timeout: {config.Timeout} seconds");
            Console.WriteLine("Press Ctrl+C to exit.");
            Console.WriteLine();
        }

        private static async Task AcceptClientsLoop(TcpListener listener, ProxyConfiguration config, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // Accept client connection
                    TcpClient clientConnection = await listener.AcceptTcpClientAsync() ?? throw new Exception("client cannot connect.");
                    Console.WriteLine($"Client connected: {((IPEndPoint?)clientConnection.Client.RemoteEndPoint)?.Address}");

                    // Configure timeout
                    clientConnection.ReceiveTimeout = config.Timeout * 1000;
                    clientConnection.SendTimeout = config.Timeout * 1000;

                    // Handle each client in a separate task
                    _ = HandleClientAsync(clientConnection, config, token);
                }
                catch (Exception ex) when (ex is SocketException || ex is OperationCanceledException)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    Console.WriteLine($"Connection error: {ex.Message}");
                }
            }
        }

        private static async Task HandleClientAsync(TcpClient clientConnection, ProxyConfiguration config, CancellationToken token)
        {
            TcpClient? remoteConnection = null;

            try
            {
                if (config.RemoteIpAddress == null)
                {
                    throw new Exception("Ip address not exist");
                }
                // Connect to remote modem
                remoteConnection = new TcpClient
                {
                    // Configure timeout
                    ReceiveTimeout = config.Timeout * 1000,
                    SendTimeout = config.Timeout * 1000
                };

                await remoteConnection.ConnectAsync(config.RemoteIpAddress, config.RemotePort, token);
                Console.WriteLine("Connected to remote modem. Relaying data...");

                // Get network streams
                NetworkStream clientStream = clientConnection.GetStream();
                NetworkStream remoteStream = remoteConnection.GetStream();

                // Relay data in both directions
                Task clientToRemoteTask = RelayDataAsync(clientStream, remoteStream, "Client => Modem ", config.BufferSize, token);
                Task remoteToClientTask = RelayDataAsync(remoteStream, clientStream, "Modem  => Client", config.BufferSize, token);

                // Wait for either direction to complete (disconnection or error)
                await Task.WhenAny(clientToRemoteTask, remoteToClientTask);
            }
            catch (Exception ex) when (ex is SocketException || ex is OperationCanceledException)
            {
                if (!token.IsCancellationRequested)
                {
                    Console.WriteLine($"Connection error: {ex.Message}");
                }
            }
            finally
            {
                // Clean up resources
                remoteConnection?.Close();
                clientConnection.Close();
                Console.WriteLine("Connection closed.");
            }
        }

        private static async Task RelayDataAsync(NetworkStream source, NetworkStream destination, string direction, int bufferSize, CancellationToken token)
        {
            var buffer = new byte[bufferSize];
            int bytesRead;

            try
            {
                while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                {
                    await destination.WriteAsync(buffer, 0, bytesRead, token);
                    await destination.FlushAsync(token);

                    // Log data for debugging
                    LogData(direction, buffer, bytesRead);
                }
            }
            catch (Exception) when (token.IsCancellationRequested)
            {
                // Cancellation is expected, just exit
            }
        }

        private static void LogData(string direction, byte[] buffer, int bytesRead)
        {
            var sb = new StringBuilder();
            sb.Append($"{direction} [{bytesRead} bytes]: ");

            // Display all data in hex format
            for (int i = 0; i < bytesRead; i++)
            {
                sb.Append($"{buffer[i]:X2} ");

                // Add a newline every 16 bytes for better readability
                if ((i + 1) % 16 == 0 && i < bytesRead - 1)
                {
                    sb.AppendLine();
                    sb.Append("                  "); // Align with the start of hex data
                }
            }

            Console.WriteLine(sb.ToString());
        }

        private static void ShowUsage()
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
            Console.WriteLine($"\nAlso supports legacy format:");
            Console.WriteLine($"  tcs <RemoteIPAddress> <RemotePort>");
        }
    }
}
