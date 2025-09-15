# Transparent Communication Service (TCS)

A flexible, high-performance TCP proxy application for transparent network communication.

## Overview

Transparent Communication Service (TCS) is a lightweight TCP proxy tool written in C# using .NET 8.0. It allows you to forward network connections between local and remote endpoints with configurable parameters such as buffer size and connection timeout. It acts as a transparent TCP proxy, allowing client applications to communicate with remote modems as if they were directly connected. It listens on a local port and forwards all incoming traffic to a specified remote modem, relaying responses back seamlessly. So, it allows existing applications to communicate with modems behind firewalls or VPNs without modification.

- ✅ Plug-and-play – No installation or configuration needed.
- ✅ Portable – Runs as a standalone executable.
- ✅ Flexible – Supports dynamic remote IP and port selection.
- ✅ Seamless communication – Behaves just like a real modem.
- ✅ Enhanced logging – File logging with configurable options.

Ideal for developers, system integrators, and debugging modem communications in restricted environments. 🚀

---

### ✨ What's New in v1.1.0

- **Multi-Destination Fan-Out**: The biggest new feature! You can now forward a single client's data to **multiple remote servers simultaneously**. This transforms the tool from a simple proxy into a powerful data distributor, perfect for data replication, load testing, or system integration.
- **Simplified Configuration**: The command-line arguments and `settings.json` file have been updated to use a clear and flexible `endpoints` list.

---

## Features

- **TCP Fan-Out Proxy**: Forward TCP traffic from a single client to multiple remote endpoints simultaneously.
- **Flexible Configuration**: Configure remote endpoints, local port, buffer size, and connection timeout.
- **Multiple Configuration Sources**: Load settings from command-line, a settings file, or interactive console input.
- **Enhanced Logging System**:
  - Console and file-based logging.
  - Configurable data payload logging.
  - Option for separate data transmission logs.
  - Organized logs in a dedicated directory.
- **Performance Optimized**: Asynchronously handles multiple connections for efficient, non-blocking data transmission.
- **Command-Line Interface**: Easy to use from terminal or scripts.

## Requirements

- .NET 8.0 SDK or Runtime
- Windows, Linux, or macOS (any platform supporting .NET 8.0)

## Installation

### Option 1: Download Release

Download the latest release from the [Releases](https://github.com/hkilimci/TransparentCommunicationService/releases) page.

### Option 2: Build from Source

```bash
# Clone the repository
git clone https://github.com/hkilimci/TransparentCommunicationService.git

# Navigate to the project directory
cd TransparentCommunicationService

# Build the project
dotnet build -c Release

# Run the application
dotnet run --project TransparentCommunicationService.csproj
```

## Usage

### Command Format

```
tcs endpoints=<ip1:port1>,<ip2:port2> [localport=<port>] [buffer=<bytes>] [timeout=<seconds>] [enablefilelogging=<true|false>] [separatedatalogs=<true|false>] [logdatapayload=<true|false>]
```

### Configuration Sources

TCS supports multiple ways to provide configuration parameters, in the following priority order:

1. **Command-line arguments** (highest priority)
2. **Settings file** (`settings.json` in the application directory)
3. **Interactive console input** (for missing required parameters)

If a parameter is specified in multiple sources, the highest priority source will be used.

### Parameters

| Parameter | Description | Default |
|-----------|-------------|---------|
| `endpoints` | A comma-separated list of remote endpoints to forward traffic to (e.g., `1.1.1.1:80,2.2.2.2:8080`) | Required |
| `localport` | The local port to listen on for incoming client connections | 1209 |
| `buffer` | Buffer size for data transmission (in bytes) | 8192 |
| `timeout` | Connection timeout in seconds | 30 |
| `enablefilelogging` | Enable logging to a file | true |
| `separatedatalogs` | Create separate log files for data transmissions | false |
| `logdatapayload` | Include the raw data payload in logs | true |

### Settings File Format

TCS can read configuration from a JSON settings file named `settings.json` in the application directory:

```json
{
  "endpoints": [
    "192.168.1.100:5000",
    "10.0.0.50:5001"
  ],
  "localPort": 1209,
  "bufferSize": 8192,
  "timeout": 30,
  "enableFileLogging": true,
  "logDataPayload": true,
  "separateDataLogs": false
}
```

The settings file is automatically created when you choose to save your configuration during the interactive console setup.

### Logging

TCS includes an enhanced logging system with the following features:

- **Console Logging**: All events are logged to the console.
- **File Logging**: When enabled, logs are written to files in a `logs` directory.
- **Main Log File**: Contains general information, errors, and connection events. If multiple endpoints are configured, a generic filename like `tcs_multi_endpoint.log` is used.
- **Data Log File**: When separate data logging is enabled, data transmissions are stored in a dedicated file.
- **Data Payload Logging**: Configure whether to include the full data payload in logs for debugging.

### Examples

#### Basic Usage (Single Destination)

Forward connections from the local port to a single remote endpoint at `192.168.1.45:4545`:

```
tcs endpoints=192.168.1.45:4545
```

#### Multi-Destination Fan-Out

Listen on local port `8080` and forward all incoming data to two different remote endpoints simultaneously:

```
tcs endpoints=192.168.1.45:4545,10.20.30.40:9000 localport=8080
```

#### Advanced Configuration

Configure multiple endpoints with a larger buffer, longer timeout, and custom logging options:

```
tcs endpoints=192.168.1.45:4545,10.20.30.40:9000 localport=1209 timeout=60 buffer=16384 enablefilelogging=true separatedatalogs=true logdatapayload=true
```

## How It Works

1. TCS starts a TCP listener on the specified local port.
2. When a client connects to the local port, TCS establishes a separate connection to **every** remote endpoint defined in the configuration.
3. Data received from the client is **fanned out** and sent to all connected remote endpoints simultaneously.
4. Data received from **any** of the remote endpoints is relayed back to the original client.
5. All traffic can be logged for debugging purposes.
6. If file logging is enabled, logs are stored in the `logs` directory.

## Use Cases

- **Data Replication**: Send the same stream of data to multiple backup or processing servers in real-time.
- **Load Testing**: Simulate a single client sending requests to multiple servers behind a load balancer.
- **System Integration**: Bridge a legacy application to multiple modern services at once.
- **Overcome VPN limitations** by broadcasting data to different network segments.
- **Debugging and Protocol Analysis**: Monitor communication between a client and multiple endpoints.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments

- Built with .NET 8.0
- Inspired by the need for simple, configurable network proxies


<a href="https://www.buymeacoffee.com/hhklmc" target="_blank"><img src="https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png" alt="Buy Me A Coffee" style="height: 41px !important;width: 174px !important;box-shadow: 0px 3px 2px 0px rgba(190, 190, 190, 0.5) !important;-webkit-box-shadow: 0px 3px 2px 0px rgba(190, 190, 190, 0.5) !important;" ></a>
