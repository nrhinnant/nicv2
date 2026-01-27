using WfpTrafficControl.Service;
using WfpTrafficControl.Shared;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = WfpConstants.ServiceName;
});
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
