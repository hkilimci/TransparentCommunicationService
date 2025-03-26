using System.Net;
using System.Net.Sockets;
using TransparentCommunicationService.Connections;
using TransparentCommunicationService.Helpers;
using TransparentCommunicationService.Model;

namespace TransparentCommunicationService.Server;

internal static class ProxyServer
{
    public static async Task RunProxyServer(ProxyConfiguration config)
    {
        // Initialize logger
        Logger.Initialize(config);
        
        // Create TCP listener
        using var listener = new TcpListener(IPAddress.Any, config.LocalPort);
        listener.Start();

        Logger.DisplayServerStartInfo(config);

        // Setup cancellation
        using var cts = SetupCancellation();

        // Accept and handle client connections
        await AcceptClientsLoop(listener, config, cts.Token);

        // Cleanup
        listener.Stop();
        Logger.LogInfo("Proxy stopped.");
        Logger.LogInfo("");
    }

    private static CancellationTokenSource SetupCancellation()
    {
        var cts = new CancellationTokenSource();
        
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true; // Prevent the process from terminating immediately
            cts.Cancel();
            Logger.LogInfo("Shutting down...");

            // Ensure the application exits after cleanup
            Task.Run(async () =>
            {
                // Give some time for cleanup operations to complete
                await Task.Delay(1000, cts.Token);
                Environment.Exit(0);
            }, cts.Token);
        };
        
        return cts;
    }

    private static async Task AcceptClientsLoop(TcpListener listener, ProxyConfiguration config, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                // Accept client connection
                var clientConnection = await listener.AcceptTcpClientAsync(token) ?? throw new IOException("Failed to accept TCP client connection.");
                var clientEndPoint = (IPEndPoint?)clientConnection.Client.RemoteEndPoint;
                Logger.LogInfo($"Client connected: {clientEndPoint?.Address}:{clientEndPoint?.Port}");

                // Configure timeout
                clientConnection.ReceiveTimeout = config.Timeout * 1000;
                clientConnection.SendTimeout = config.Timeout * 1000;

                // Handle each client in a separate task
                _ = ConnectionHandler.HandleClientAsync(clientConnection, config, token);
            }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                Logger.LogError("Connection error", ex);
            }
        }
    }
}