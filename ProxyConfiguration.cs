using System.Net;

namespace TransparentCommunicationService
{
    /// <summary>
    /// Configuration class to hold all proxy settings
    /// </summary>
    internal class ProxyConfiguration
    {
        public IPAddress? RemoteIpAddress { get; set; }
        public int RemotePort { get; set; }
        public int LocalPort { get; set; }
        public int BufferSize { get; set; }
        public int Timeout { get; set; }
    }
}
