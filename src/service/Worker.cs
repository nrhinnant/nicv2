using WfpTrafficControl.Shared;

namespace WfpTrafficControl.Service;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{ServiceName} starting", WfpConstants.ServiceName);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Placeholder: WFP policy controller logic will go here
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }

        _logger.LogInformation("{ServiceName} stopping", WfpConstants.ServiceName);
    }
}
