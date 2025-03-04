# Transparent Communication Service (TCS)

A flexible, high-performance TCP proxy application for transparent network communication.

## Overview

Transparent Communication Service (TCS) is a lightweight TCP proxy tool written in C# using .NET 8.0. It allows you to forward network connections between local and remote endpoints with configurable parameters such as buffer size and connection timeout.

## Features

- **Simple TCP Forwarding**: Easily forward TCP traffic from a local port to a remote endpoint
- **Flexible Configuration**: Configure remote IP, remote port, local port, buffer size, and connection timeout
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
tcs endpoint=<RemoteIPAddress> port=<RemotePort> [localport=<LocalPort>] [buffer=<BufferSize>] [timeout=<TimeoutSeconds>]
```

### Parameters

| Parameter | Description | Default |
|-----------|-------------|---------|
| `endpoint` | The IP address of the target endpoint | Required |
| `port` | The port of the remote endpoint to forward traffic to | Required |
| `localport` | The local port to listen on | 1209 |
| `buffer` | Buffer size for data transmission (in bytes) | 8192 |
| `timeout` | Connection timeout in seconds | 30 |

### Examples

#### Basic Usage

Forward connections from local port 1209 to remote endpoint 192.168.1.45:4545:

```
tcs endpoint=192.168.1.45 port=4545
```

#### Custom Local Port

Listen on local port 8080 and forward to 192.168.1.45:4545:

```
tcs endpoint=192.168.1.45 port=4545 localport=8080
```

#### Advanced Configuration

Configure with larger buffer and longer timeout:

```
tcs endpoint=192.168.1.45 port=4545 localport=1209 timeout=60 buffer=16384
```

#### Legacy Format

The application also supports a simpler legacy format:

```
tcs <RemoteIPAddress> <RemotePort>
```

## How It Works

1. TCS starts a TCP listener on the specified local port
2. When a client connects to the local port, TCS establishes a connection to the remote endpoint
3. Data is forwarded bidirectionally between the client and the remote endpoint
4. All traffic is logged (optionally in hex format) for debugging purposes

## Use Cases

- Testing network applications
- Debugging network communication
- Port forwarding in development environments
- Network traffic inspection
- Creating transparent proxies for legacy applications

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
