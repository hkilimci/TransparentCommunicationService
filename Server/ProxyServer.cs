using System.Net;
using System.Net.Sockets;
using TransparentCommunicationService.Connections;
using TransparentCommunicationService.Helpers;
using TransparentCommunicationService.Model;

namespace TransparentCommunicationService.Server;

internal static class ProxyServer
{
    // Limits the number of connections handled concurrently
    private static readonly SemaphoreSlim ConnectionLimiter = new SemaphoreSlim(
        Constants.Configuration.MaxConcurrentConnections,
        Constants.Configuration.MaxConcurrentConnections);

    public static async Task RunProxyServer(ProxyConfiguration config)
    {
        using var cts = SetupCancellation();
        await RunProxyServer(config, cts.Token);
    }

    public static async Task RunProxyServer(ProxyConfiguration config, CancellationToken token)
    {
        // Initialize logger
        Logger.Initialize(config);

        // Create TCP listener
        using var listener = new TcpListener(IPAddress.Any, config.LocalPort);
        listener.Start();

        Logger.DisplayServerStartInfo(config);

        // Accept and handle client connections
        await AcceptClientsLoop(listener, config, token);

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

            // Fallback: force-exit after 1 second if the main loop hasn't returned
            Task.Run(async () =>
            {
                await Task.Delay(1000); // use no token — we want this to run after cancellation
                Environment.Exit(0);
            });
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

                // Throttle concurrent connections; release the slot when the handler finishes
                await ConnectionLimiter.WaitAsync(token);
                _ = ConnectionHandler.HandleClientAsync(clientConnection, config, token)
                    .ContinueWith(t =>
                    {
                        ConnectionLimiter.Release();
                        if (t.IsFaulted)
                        {
                            Logger.LogError("Unhandled exception in connection handler", t.Exception?.GetBaseException());
                        }
                    }, TaskScheduler.Default);
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
