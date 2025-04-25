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

## Features

- **Simple TCP Forwarding**: Easily forward TCP traffic from a local port to a remote endpoint
- **Flexible Configuration**: Configure remote IP, remote port, local port, buffer size, and connection timeout
- **Multiple Configuration Sources**: Load settings from command-line, settings file, or interactive console input
- **Enhanced Logging System**:
  - Console and file-based logging
  - Configurable data payload logging
  - Option for separate data transmission logs
  - Organized logs in a dedicated directory
- **Hex Logging**: View incoming and outgoing network packets in hex format for debugging
- **Performance Optimized**: Designed for efficient data transmission with configurable buffer sizes
- **Command-Line Interface**: Easy to use from terminal or scripts
- **Legacy Format Support**: Maintains backward compatibility with simpler command format

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
tcs endpoint=<RemoteIPAddress> port=<RemotePort> [localport=<LocalPort>] [buffer=<BufferSize>] [timeout=<TimeoutSeconds>] [enablefilelogging=<EnableFileLogging>] [separatedatalogs=<SeparateDataLogs>] [logdatapayload=<LogDataPayload>]
```

### Configuration Sources

TCS supports multiple ways to provide configuration parameters, in the following priority order:

1. **Command-line arguments** (highest priority)
2. **Settings file** (settings.json in the application directory)
3. **Interactive console input** (for missing required parameters)

If a parameter is specified in multiple sources, the highest priority source will be used.

### Parameters

| Parameter | Description | Default |
|-----------|-------------|---------|
| `endpoint` | The IP address of the target endpoint | Required |
| `port` | The port of the remote endpoint to forward traffic to | Required |
| `localport` | The local port to listen on | 1209 |
| `buffer` | Buffer size for data transmission (in bytes) | 8192 |
| `timeout` | Connection timeout in seconds | 30 |
| `enablefilelogging` | Enable logging to file | true |
| `separatedatalogs` | Create separate log files for data transmissions | false |
| `logdatapayload` | Include data payload in logs | true |

### Settings File Format

TCS can read configuration from a JSON settings file named `settings.json` in the application directory:

```json
{
  "endpoint": "127.0.0.1",
  "port": 1987,
  "localPort": 1209,
  "bufferSize": 8192,
  "timeout": 30,
  "enableFileLogging": true,
  "logDataPayload": true,
  "separateDataLogs": false
}
```

The settings file is automatically created when you choose to save your configuration during interactive console setup.

### Logging

TCS includes an enhanced logging system with the following features:

- **Console Logging**: All events are logged to the console
- **File Logging**: When enabled, logs are written to files in a `logs` directory
- **Main Log File**: Contains general information, errors, and connection events
- **Data Log File**: When separate data logging is enabled, data transmissions are stored in a dedicated file
- **Data Payload Logging**: Configure whether to include the full data payload in logs

Log files are named based on the remote endpoint information to make it easy to identify logs for specific connections.

### Examples

#### Basic Usage

Forward connections from local port 1209 to remote endpoint 192.168.1.45:4545:

```
tcs 192.168.1.45 4545
```
or

```
tcs endpoint=192.168.1.45 port=4545
```

#### Custom Local Port

Listen on local port 8080 and forward to 192.168.1.45:4545:

```
tcs endpoint=192.168.1.45 port=4545 localport=8080
```

#### Advanced Configuration

Configure with larger buffer, longer timeout, and custom logging options:

```
tcs endpoint=192.168.1.45 port=4545 localport=1209 timeout=60 buffer=16384 enablefilelogging=true separatedatalogs=true logdatapayload=true
```

#### Legacy Format

The application also supports a simpler legacy format:

```
tcs <RemoteIPAddress> <RemotePort>
```

## How It Works

1. TCS starts a TCP listener on the specified local port.
2. When a client connects to the local port, TCS establishes a connection to the remote endpoint.
3. Data is forwarded bidirectionally between the client and the remote endpoint, making it behave like a virtual modem.
4. All traffic is logged (optionally in hex format) for debugging purposes.
5. If file logging is enabled, logs are stored in the `logs` directory.

## Use Cases

- Overcome local VPN limitations
- Testing network applications
- Debugging network communication
- Port forwarding in development environments
- Network traffic inspection
- Creating transparent proxies for legacy applications
- Protocol analysis with enhanced logging

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
