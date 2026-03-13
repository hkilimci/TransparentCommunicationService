using System.Text;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using TransparentCommunicationService.Helpers;
using TransparentCommunicationService.Model;
using TransparentCommunicationService.Server;

namespace TransparentCommunicationService;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1001", Justification = "Window lifecycle ensures CTS disposal via StopProxyAsync and OnClosed.")]
public partial class MainWindow : Window
{
    private static readonly FilePickerFileType JsonFileType = new("JSON Files")
    {
        Patterns = ["*.json"]
    };

    private readonly StringBuilder _logBuilder = new StringBuilder();
    private CancellationTokenSource? _serverCts;
    private Task? _serverTask;

    public MainWindow()
    {
        InitializeComponent();
        Logger.LogWritten += OnLogWritten;
        LoadInitialConfiguration();
        Closed += OnClosed;
    }

    private void LoadInitialConfiguration()
    {
        var config = Configuration.LoadConfigurationForGui();
        ApplyConfigurationToUi(config);
    }

    private void OnStartClicked(object? sender, RoutedEventArgs e)
    {
        if (_serverTask is { IsCompleted: false })
        {
            return;
        }

        if (!TryBuildConfiguration(out var config, out var error))
        {
            StatusTextBlock.Text = error;
            return;
        }

        try
        {
            Configuration.SaveConfigurationToFile(config!);
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Save failed: {ex.Message}";
            return;
        }

        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        ImportButton.IsEnabled = false;
        ExportButton.IsEnabled = false;
        StatusTextBlock.Text = "Starting...";

        _serverCts = new CancellationTokenSource();
        _serverTask = Task.Run(async () =>
        {
            await ProxyServer.RunProxyServer(config!, _serverCts.Token);
        });

        _ = _serverTask.ContinueWith(t =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (t.IsFaulted)
                {
                    StatusTextBlock.Text = $"Failed: {t.Exception?.GetBaseException().Message}";
                }
                else if (t.IsCanceled)
                {
                    StatusTextBlock.Text = "Stopped";
                }
                else
                {
                    StatusTextBlock.Text = "Stopped";
                }

                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                ImportButton.IsEnabled = true;
                ExportButton.IsEnabled = true;

                if (ReferenceEquals(_serverTask, t))
                {
                    _serverCts?.Dispose();
                    _serverCts = null;
                    _serverTask = null;
                }
            });
        }, TaskScheduler.Default);

        StatusTextBlock.Text = "Running";
    }

    private async void OnStopClicked(object? sender, RoutedEventArgs e)
    {
        await StopProxyAsync();
    }

    private async Task StopProxyAsync()
    {
        if (_serverCts == null || _serverTask == null)
        {
            return;
        }

        StatusTextBlock.Text = "Stopping...";

        try
        {
            await _serverCts.CancelAsync();
            await _serverTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping.
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Stop failed: {ex.Message}";
            return;
        }
        finally
        {
            _serverCts.Dispose();
            _serverCts = null;
            _serverTask = null;
        }

        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        ImportButton.IsEnabled = true;
        ExportButton.IsEnabled = true;
        StatusTextBlock.Text = "Stopped";
    }

    private async void OnImportClicked(object? sender, RoutedEventArgs e)
    {
        if (!StorageProvider.CanOpen)
        {
            StatusTextBlock.Text = "Import is not supported on this platform.";
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Settings",
            AllowMultiple = false,
            FileTypeFilter = [JsonFileType]
        });

        var file = files.FirstOrDefault();
        if (file == null)
        {
            return;
        }

        var filePath = file.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            StatusTextBlock.Text = "Only local settings files are supported.";
            return;
        }

        try
        {
            var config = Configuration.LoadConfigurationFromFile(filePath);
            ApplyConfigurationToUi(config);
            StatusTextBlock.Text = $"Imported {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Import failed: {ex.Message}";
        }
    }

    private async void OnExportClicked(object? sender, RoutedEventArgs e)
    {
        if (!TryBuildConfiguration(out var config, out var error))
        {
            StatusTextBlock.Text = $"Export failed: {error}";
            return;
        }

        if (!StorageProvider.CanSave)
        {
            StatusTextBlock.Text = "Export is not supported on this platform.";
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Settings",
            SuggestedFileName = "settings.json",
            DefaultExtension = "json",
            ShowOverwritePrompt = true,
            FileTypeChoices = [JsonFileType]
        });

        if (file == null)
        {
            return;
        }

        var filePath = file.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            StatusTextBlock.Text = "Only local settings files are supported.";
            return;
        }

        try
        {
            Configuration.SaveConfigurationToFile(config!, filePath);
            StatusTextBlock.Text = $"Exported {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Export failed: {ex.Message}";
        }
    }

    private bool TryBuildConfiguration(out ProxyConfiguration? config, out string error)
    {
        config = null;
        error = string.Empty;

        var endpoints = ParseEndpoints(EndpointsTextBox.Text);
        if (endpoints.Count == 0)
        {
            error = "Provide at least one valid endpoint.";
            return false;
        }

        if (!Configuration.TryParsePortValue(LocalPortTextBox.Text, out var localPort))
        {
            error = "Local port must be between 1 and 65535.";
            return false;
        }

        if (!int.TryParse(BufferSizeTextBox.Text, out var bufferSize) || bufferSize <= 0)
        {
            error = "Buffer size must be a positive integer.";
            return false;
        }

        if (!int.TryParse(TimeoutTextBox.Text, out var timeout) || timeout < 0)
        {
            error = "Timeout must be a non-negative integer.";
            return false;
        }

        config = new ProxyConfiguration
        {
            RemoteEndpoints = endpoints,
            LocalPort = localPort,
            BufferSize = bufferSize,
            Timeout = timeout,
            EnableFileLogging = EnableFileLoggingCheckBox.IsChecked ?? true,
            SeparateDataLogs = SeparateDataLogsCheckBox.IsChecked ?? false,
            LogDataPayload = LogDataPayloadCheckBox.IsChecked ?? true
        };

        return true;
    }

    private void ApplyConfigurationToUi(ProxyConfiguration config)
    {
        EndpointsTextBox.Text = string.Join(Environment.NewLine, config.RemoteEndpoints.Select(e => $"{e.Host}:{e.Port}"));
        LocalPortTextBox.Text = config.LocalPort.ToString(CultureInfo.InvariantCulture);
        BufferSizeTextBox.Text = config.BufferSize.ToString(CultureInfo.InvariantCulture);
        TimeoutTextBox.Text = config.Timeout.ToString(CultureInfo.InvariantCulture);
        EnableFileLoggingCheckBox.IsChecked = config.EnableFileLogging;
        SeparateDataLogsCheckBox.IsChecked = config.SeparateDataLogs;
        LogDataPayloadCheckBox.IsChecked = config.LogDataPayload;
    }

    private static List<RemoteEndpoint> ParseEndpoints(string? rawValue)
    {
        var results = new List<RemoteEndpoint>();
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return results;
        }

        var items = rawValue
            .Split([',', '\n', '\r', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var item in items)
        {
            if (Configuration.TryParseEndpointValue(item, out var endpoint))
            {
                results.Add(endpoint);
            }
        }

        return results;
    }

    private void OnLogWritten(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _logBuilder.AppendLine(message);
            LogTextBox.Text = _logBuilder.ToString();
            LogTextBox.CaretIndex = LogTextBox.Text?.Length ?? 0;
        });
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        Logger.LogWritten -= OnLogWritten;
        await StopProxyAsync();
    }
}
