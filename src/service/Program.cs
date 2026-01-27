using WfpTrafficControl.Service;
using WfpTrafficControl.Shared;

var builder = Host.CreateApplicationBuilder(args);

// Configure Windows Service hosting
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = WfpConstants.ServiceName;
});

// Configure logging
builder.Logging.ClearProviders();

// Add console logging (useful when running in console mode for debugging)
builder.Logging.AddConsole();

// Add EventLog logging for Windows service integration
// Events will appear in Windows Event Viewer under Application log
builder.Logging.AddEventLog(settings =>
{
    settings.SourceName = WfpConstants.ServiceName;
    settings.LogName = "Application";
});

// Set minimum log level
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Add the worker service
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
