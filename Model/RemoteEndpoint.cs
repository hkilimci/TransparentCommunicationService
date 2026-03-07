namespace TransparentCommunicationService.Model;

internal class RemoteEndpoint(string host, int port)
{
    public string Host { get; set; } = host;
    public int Port { get; set; } = port;
}
