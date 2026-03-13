namespace TransparentCommunicationService.Model;

internal sealed class RemoteEndpoint(string host, int port)
{
    public string Host { get; set; } = host;
    public int Port { get; set; } = port;
}
