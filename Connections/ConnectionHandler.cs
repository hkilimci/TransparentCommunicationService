using System.Net.Sockets;
using TransparentCommunicationService.Helpers;
using TransparentCommunicationService.Model;

namespace TransparentCommunicationService.Connections;

internal static class ConnectionHandler
{
    public static async Task HandleClientAsync(TcpClient clientConnection, ProxyConfiguration config, CancellationToken token)
    {
        TcpClient? remoteConnection = null;

        try
        {
            if (config.RemoteIpAddress == null)
            {
                throw new ArgumentNullException(nameof(config), "RemoteIpAddress cannot be null");
            }
            
            // Connect to remote modem
            remoteConnection = new TcpClient
            {
                // Configure timeout
                ReceiveTimeout = config.Timeout * 1000,
                SendTimeout = config.Timeout * 1000
            };

            await remoteConnection.ConnectAsync(config.RemoteIpAddress, config.RemotePort, token);
            
            Logger.LogInfo("Connected to remote modem. Relaying data...");

            // Get network streams
            await using var clientStream = clientConnection.GetStream();
            await using var remoteStream = remoteConnection.GetStream();

            // Relay data in both directions
            var clientToRemoteTask = RelayDataAsync(clientStream, remoteStream, "Client => Remote", config.BufferSize, token);
            var remoteToClientTask = RelayDataAsync(remoteStream, clientStream, "Remote => Client", config.BufferSize, token);

            // Wait for either direction to complete (disconnection or error)
            await Task.WhenAny(clientToRemoteTask, remoteToClientTask);
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            if (!token.IsCancellationRequested)
            {
                Logger.LogError("Connection error", ex);
            }
        }
        finally
        {
            // Clean up resources
            remoteConnection?.Close();
            clientConnection.Close();
            Logger.LogInfo("Connection closed.");
        }
    }

    private static async Task RelayDataAsync(NetworkStream source, NetworkStream destination, string direction, int bufferSize, CancellationToken token)
    {
        var buffer = new byte[bufferSize];

        try
        {
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, token)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                await destination.FlushAsync(token);

                // Log data for debugging
                Logger.LogData(direction, buffer, bytesRead);
            }
        }
        catch (Exception ex) when (token.IsCancellationRequested)
        {
            // Cancellation is expected, just exit
            Logger.LogError($"Request cancelled {direction}", ex);
        }
        catch (Exception ex)
        {
            // Log non-cancellation errors
            Logger.LogError($"Error relaying data in direction {direction}", ex);
        }
    }
}