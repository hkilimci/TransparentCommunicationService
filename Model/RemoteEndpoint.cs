using System.Net;

namespace TransparentCommunicationService.Model;

public class RemoteEndpoint(IPAddress ipAddress, int port)
{
    public IPAddress IpAddress { get; set; } = ipAddress;
    public int Port { get; set; } = port;
}
