using TransparentCommunicationService.Helpers;
using TransparentCommunicationService.Server;

namespace TransparentCommunicationService;

internal static class Program
{
    static async Task Main(string[] args)
    {
        Logger.DisplayWelcomeMessage();

        // Show usage if --help or -h is specified
        if (args.Length > 0 && (args[0].Equals("--help", StringComparison.OrdinalIgnoreCase) || args[0].Equals("-h", StringComparison.OrdinalIgnoreCase)))
        {
            Configuration.ShowUsage();
            return;
        }

        try
        {
            // Load configuration from all sources (args > file > console)
            var config = Configuration.LoadConfiguration(args);
                
            // Run the proxy server with the loaded configuration
            await ProxyServer.RunProxyServer(config);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Configuration.ShowUsage();
        }
    }
}