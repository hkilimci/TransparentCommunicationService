using System.Net.Sockets;
using System.Net;
using System.Text;

namespace TransparentCommunicationService
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            DisplayWelcomeMessage();

            // Show usage if --help or -h is specified
            if (args.Length > 0 && (args[0].Equals("--help", StringComparison.OrdinalIgnoreCase) || args[0].Equals("-h", StringComparison.OrdinalIgnoreCase)))
            {
                ConfigurationManager.ShowUsage();
                return;
            }

            try
            {
                // Load configuration from all sources (args > file > console)
                var config = ConfigurationManager.LoadConfiguration(args);
                
                // Run the proxy server with the loaded configuration
                await RunProxyServer(config);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                ConfigurationManager.ShowUsage();
            }
        }

        private static void DisplayWelcomeMessage()
        {
            Console.WriteLine("Transparent TCP Proxy - Virtual Modem Relay");
            Console.WriteLine("-------------------------------------------");
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
            Console.WriteLine("\n=== Transparent Communication Service Started ===");
            Console.WriteLine($"Listening on: localhost:{config.LocalPort}");
            Console.WriteLine($"Forwarding to: {config.RemoteIpAddress}:{config.RemotePort}");
            Console.WriteLine("\nActive Configuration:");
            Console.WriteLine($"  Local Port: {config.LocalPort}");
            Console.WriteLine($"  Remote Endpoint: {config.RemoteIpAddress}:{config.RemotePort}");
            Console.WriteLine($"  Buffer Size: {config.BufferSize} bytes");
            Console.WriteLine($"  Timeout: {config.Timeout} seconds");
            Console.WriteLine("\nPress Ctrl+C to exit.");
            Console.WriteLine("");
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
    }
}
