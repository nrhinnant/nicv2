using WfpTrafficControl.Service.Ipc;
using WfpTrafficControl.Service.Wfp;
using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Native;

namespace WfpTrafficControl.Service;

/// <summary>
/// Background worker service that manages the WFP policy controller lifecycle.
/// Currently a placeholder that demonstrates clean start/stop behavior.
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IWfpEngine _wfpEngine;
    private PipeServer? _pipeServer;

    public Worker(ILogger<Worker> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _wfpEngine = new WfpEngine(_loggerFactory.CreateLogger<WfpEngine>());
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        var version = GetVersion();

        _logger.LogInformation(
            "{ServiceName} v{Version} starting at {Time}",
            WfpConstants.ServiceName,
            version,
            DateTimeOffset.Now);

        // Start the IPC pipe server
        _pipeServer = new PipeServer(_loggerFactory.CreateLogger<PipeServer>(), version, _wfpEngine);
        _pipeServer.Start();

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{ServiceName} is now running", WfpConstants.ServiceName);

        try
        {
            // Main service loop
            while (!stoppingToken.IsCancellationRequested)
            {
                // Placeholder: WFP policy controller logic will go here
                // For now, just idle and stay responsive to stop signals
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested - not an error
            _logger.LogDebug("Service execution cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in service execution");
            // Don't rethrow - let the service stop gracefully
            // The lifetime will be stopped in StopAsync
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "{ServiceName} stopping at {Time}",
            WfpConstants.ServiceName,
            DateTimeOffset.Now);

        // Stop the IPC pipe server
        if (_pipeServer != null)
        {
            await _pipeServer.StopAsync();
            _pipeServer.Dispose();
            _pipeServer = null;
        }

        await base.StopAsync(cancellationToken);

        _logger.LogInformation("{ServiceName} stopped", WfpConstants.ServiceName);
    }

    private static string GetVersion()
    {
        var assembly = typeof(Worker).Assembly;
        var version = assembly.GetName().Version;
        return version?.ToString(3) ?? "0.0.0";
    }
}
