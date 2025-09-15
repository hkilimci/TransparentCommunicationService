using System.Net.Sockets;
using TransparentCommunicationService.Helpers;
using TransparentCommunicationService.Model;

namespace TransparentCommunicationService.Connections;

internal static class ConnectionHandler
{
    public static async Task HandleClientAsync(TcpClient clientConnection, ProxyConfiguration config, CancellationToken token)
    {
        var remoteConnections = new List<TcpClient>();

        try
        {
            if (config.RemoteEndpoints.Count == 0)
            {
                throw new InvalidOperationException("At least one remote endpoint must be configured.");
            }
            
            // Connect to all remote endpoints
            foreach (var endpoint in config.RemoteEndpoints)
            {
                var remoteConnection = new TcpClient
                {
                    ReceiveTimeout = config.Timeout * 1000,
                    SendTimeout = config.Timeout * 1000
                };

                try
                {
                    await remoteConnection.ConnectAsync(endpoint.IpAddress, endpoint.Port, token);
                    remoteConnections.Add(remoteConnection);
                    Logger.LogInfo($"Connected to remote endpoint: {endpoint.IpAddress}:{endpoint.Port}");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to connect to remote endpoint {endpoint.IpAddress}:{endpoint.Port}", ex);
                    remoteConnection.Close();
                }
            }

            if (remoteConnections.Count == 0)
            {
                Logger.LogWarning("No remote connections were successfully established. Closing client connection.");
                return; // Exit if no connections could be made
            }

            Logger.LogInfo("Relaying data between client and remote endpoints...");

            // Get network streams
            await using var clientStream = clientConnection.GetStream();
            var remoteStreams = remoteConnections.Select(rc => rc.GetStream()).ToList();

            // Create tasks for data relay
            var clientToRemotesTask = RelayDataToAllAsync(clientStream, remoteStreams, "Client => Remotes", config.BufferSize, token);
            
            var remoteToClientTasks = remoteStreams.Select((remoteStream, index) => 
                RelayDataAsync(remoteStream, clientStream, $"Remote {remoteConnections[index].Client.RemoteEndPoint} => Client", config.BufferSize, token)
            ).ToList();

            // Wait for the client-to-remotes task or any of the remote-to-client tasks to complete
            var allRelayTasks = new List<Task> { clientToRemotesTask };
            allRelayTasks.AddRange(remoteToClientTasks);
            
            await Task.WhenAny(allRelayTasks);
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException or InvalidOperationException)
        {
            if (!token.IsCancellationRequested)
            {
                Logger.LogError("Connection error", ex);
            }
        }
        finally
        {
            // Clean up all resources
            foreach (var remoteConnection in remoteConnections)
            {
                remoteConnection.Close();
            }
            clientConnection.Close();
            Logger.LogInfo("All connections closed.");
        }
    }

    /// <summary>
    /// Relays data from a single source stream to a single destination stream.
    /// </summary>
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
        catch (IOException ex) when (ex.InnerException is SocketException)
        {
            // This often happens when a connection is closed gracefully by the other party.
            Logger.LogInfo($"Connection closed by remote host in direction: {direction}");
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected on shutdown, just exit.
            Logger.LogInfo($"Relay task cancelled for direction: {direction}");
        }
        catch (Exception ex)
        {
            // Log other non-cancellation errors.
            Logger.LogError($"Error relaying data in direction {direction}", ex);
        }
    }
    
    /// <summary>
    /// Relays data from a single source stream to multiple destination streams (fan-out).
    /// </summary>
    private static async Task RelayDataToAllAsync(NetworkStream source, List<NetworkStream> destinations, string direction, int bufferSize, CancellationToken token)
    {
        var buffer = new byte[bufferSize];

        try
        {
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, token)) > 0)
            {
                // Log the data that is being fanned out
                Logger.LogData(direction, buffer, bytesRead);

                // Write the data to all destination streams concurrently
                var writeTasks = destinations.Select(async destination =>
                {
                    try
                    {
                        await destination.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                        await destination.FlushAsync(token);
                    }
                    catch (Exception ex)
                    {
                        // Log error for a specific destination, but don't stop the whole relay
                        Logger.LogError($"Error writing to a remote destination in direction {direction}", ex);
                        // Optionally, we could remove this stream from the list of destinations here
                    }
                }).ToList();

                await Task.WhenAll(writeTasks);
            }
        }
        catch (IOException ex) when (ex.InnerException is SocketException)
        {
            Logger.LogInfo($"Client connection closed gracefully.");
        }
        catch (OperationCanceledException)
        {
            Logger.LogInfo($"Client relay task cancelled.");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error reading from client in direction {direction}", ex);
        }
    }
}