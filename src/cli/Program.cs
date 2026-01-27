using WfpTrafficControl.Cli;
using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Ipc;

// Parse command line arguments
var command = args.Length > 0 ? args[0].ToLowerInvariant() : null;

// Handle multi-word commands like "demo-block enable"
var subCommand = args.Length > 1 ? args[1].ToLowerInvariant() : null;

switch (command)
{
    case "status":
    case "ping":
        return RunStatusCommand();

    case "bootstrap":
        return RunBootstrapCommand();

    case "teardown":
        return RunTeardownCommand();

    case "demo-block":
        return RunDemoBlockCommand(subCommand);

    case "rollback":
        return RunRollbackCommand();

    case "validate":
    case "apply":
    case "enable":
    case "disable":
    case "logs":
        Console.WriteLine($"Command '{command}' is not yet implemented.");
        return 1;

    case "--help":
    case "-h":
    case "help":
        PrintUsage();
        return 0;

    case "--version":
    case "-v":
        Console.WriteLine($"{WfpConstants.ProjectName} CLI v1.0.0");
        return 0;

    default:
        PrintUsage();
        return command == null ? 0 : 1;
}

static void PrintUsage()
{
    Console.WriteLine($"{WfpConstants.ProjectName} CLI");
    Console.WriteLine();
    Console.WriteLine("Usage: wfpctl <command>");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  status             - Check if the service is running and show info");
    Console.WriteLine("  bootstrap          - Create WFP provider and sublayer (idempotent)");
    Console.WriteLine("  teardown           - Remove WFP provider and sublayer (panic rollback)");
    Console.WriteLine("  demo-block enable  - Enable demo block filter (blocks TCP to 1.1.1.1:443)");
    Console.WriteLine("  demo-block disable - Disable demo block filter");
    Console.WriteLine("  demo-block status  - Show demo block filter status");
    Console.WriteLine("  rollback           - Remove all filters (keeps provider/sublayer)");
    Console.WriteLine("  validate           - Validate a policy file (not yet implemented)");
    Console.WriteLine("  apply              - Apply a policy file (not yet implemented)");
    Console.WriteLine("  enable             - Enable traffic control (not yet implemented)");
    Console.WriteLine("  disable            - Disable traffic control (not yet implemented)");
    Console.WriteLine("  logs               - Show logs (not yet implemented)");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --help, -h     Show this help message");
    Console.WriteLine("  --version, -v  Show version information");
}

static int RunStatusCommand()
{
    using var client = new PipeClient();

    // Step 1: Connect to the service
    var connectResult = client.Connect();
    if (connectResult.IsFailure)
    {
        Console.Error.WriteLine($"Error: {connectResult.Error.Message}");
        return 1;
    }

    // Step 2: Send ping request
    var request = new PingRequest();
    var result = client.SendRequest<PingResponse>(request);

    if (result.IsFailure)
    {
        Console.Error.WriteLine($"Error: {result.Error.Message}");
        return 1;
    }

    var response = result.Value;

    // Step 3: Check response status
    if (!response.Ok)
    {
        Console.Error.WriteLine($"Error: {response.Error ?? "Unknown error from service"}");
        return 1;
    }

    // Step 4: Display success information
    Console.WriteLine($"{WfpConstants.ServiceName} Service is running");
    Console.WriteLine($"  Version: {response.ServiceVersion}");
    Console.WriteLine($"  Time:    {response.Time}");

    return 0;
}

static int RunBootstrapCommand()
{
    using var client = new PipeClient();

    // Step 1: Connect to the service
    var connectResult = client.Connect();
    if (connectResult.IsFailure)
    {
        Console.Error.WriteLine($"Error: {connectResult.Error.Message}");
        return 1;
    }

    // Step 2: Send bootstrap request
    var request = new BootstrapRequest();
    var result = client.SendRequest<BootstrapResponse>(request);

    if (result.IsFailure)
    {
        Console.Error.WriteLine($"Error: {result.Error.Message}");
        return 1;
    }

    var response = result.Value;

    // Step 3: Check response status
    if (!response.Ok)
    {
        Console.Error.WriteLine($"Bootstrap failed: {response.Error ?? "Unknown error"}");
        return 1;
    }

    // Step 4: Display success information
    Console.WriteLine("WFP bootstrap completed successfully");
    Console.WriteLine($"  Provider exists: {response.ProviderExists}");
    Console.WriteLine($"  Sublayer exists: {response.SublayerExists}");

    return 0;
}

static int RunTeardownCommand()
{
    using var client = new PipeClient();

    // Step 1: Connect to the service
    var connectResult = client.Connect();
    if (connectResult.IsFailure)
    {
        Console.Error.WriteLine($"Error: {connectResult.Error.Message}");
        return 1;
    }

    // Step 2: Send teardown request
    var request = new TeardownRequest();
    var result = client.SendRequest<TeardownResponse>(request);

    if (result.IsFailure)
    {
        Console.Error.WriteLine($"Error: {result.Error.Message}");
        return 1;
    }

    var response = result.Value;

    // Step 3: Check response status
    if (!response.Ok)
    {
        Console.Error.WriteLine($"Teardown failed: {response.Error ?? "Unknown error"}");
        return 1;
    }

    // Step 4: Display success information
    Console.WriteLine("WFP teardown completed successfully");
    Console.WriteLine($"  Provider removed: {response.ProviderRemoved}");
    Console.WriteLine($"  Sublayer removed: {response.SublayerRemoved}");

    return 0;
}

static int RunDemoBlockCommand(string? subCommand)
{
    switch (subCommand)
    {
        case "enable":
            return RunDemoBlockEnableCommand();
        case "disable":
            return RunDemoBlockDisableCommand();
        case "status":
            return RunDemoBlockStatusCommand();
        default:
            Console.Error.WriteLine("Usage: wfpctl demo-block <enable|disable|status>");
            return 1;
    }
}

static int RunDemoBlockEnableCommand()
{
    using var client = new PipeClient();

    var connectResult = client.Connect();
    if (connectResult.IsFailure)
    {
        Console.Error.WriteLine($"Error: {connectResult.Error.Message}");
        return 1;
    }

    var request = new DemoBlockEnableRequest();
    var result = client.SendRequest<DemoBlockEnableResponse>(request);

    if (result.IsFailure)
    {
        Console.Error.WriteLine($"Error: {result.Error.Message}");
        return 1;
    }

    var response = result.Value;

    if (!response.Ok)
    {
        Console.Error.WriteLine($"Demo block enable failed: {response.Error ?? "Unknown error"}");
        return 1;
    }

    Console.WriteLine("Demo block filter enabled successfully");
    Console.WriteLine($"  Filter active: {response.FilterEnabled}");
    Console.WriteLine($"  Blocking: TCP to 1.1.1.1:443 (Cloudflare)");
    Console.WriteLine();
    Console.WriteLine("To test: curl -v --connect-timeout 5 https://1.1.1.1 (should fail)");

    return 0;
}

static int RunDemoBlockDisableCommand()
{
    using var client = new PipeClient();

    var connectResult = client.Connect();
    if (connectResult.IsFailure)
    {
        Console.Error.WriteLine($"Error: {connectResult.Error.Message}");
        return 1;
    }

    var request = new DemoBlockDisableRequest();
    var result = client.SendRequest<DemoBlockDisableResponse>(request);

    if (result.IsFailure)
    {
        Console.Error.WriteLine($"Error: {result.Error.Message}");
        return 1;
    }

    var response = result.Value;

    if (!response.Ok)
    {
        Console.Error.WriteLine($"Demo block disable failed: {response.Error ?? "Unknown error"}");
        return 1;
    }

    Console.WriteLine("Demo block filter disabled successfully");
    Console.WriteLine($"  Filter removed: {response.FilterDisabled}");
    Console.WriteLine();
    Console.WriteLine("To test: curl -v https://1.1.1.1 (should succeed)");

    return 0;
}

static int RunDemoBlockStatusCommand()
{
    using var client = new PipeClient();

    var connectResult = client.Connect();
    if (connectResult.IsFailure)
    {
        Console.Error.WriteLine($"Error: {connectResult.Error.Message}");
        return 1;
    }

    var request = new DemoBlockStatusRequest();
    var result = client.SendRequest<DemoBlockStatusResponse>(request);

    if (result.IsFailure)
    {
        Console.Error.WriteLine($"Error: {result.Error.Message}");
        return 1;
    }

    var response = result.Value;

    if (!response.Ok)
    {
        Console.Error.WriteLine($"Demo block status check failed: {response.Error ?? "Unknown error"}");
        return 1;
    }

    Console.WriteLine("Demo Block Filter Status");
    Console.WriteLine($"  Active: {response.FilterActive}");
    if (response.FilterActive && response.BlockedTarget != null)
    {
        Console.WriteLine($"  Blocking: {response.BlockedTarget}");
    }

    return 0;
}

static int RunRollbackCommand()
{
    using var client = new PipeClient();

    var connectResult = client.Connect();
    if (connectResult.IsFailure)
    {
        Console.Error.WriteLine($"Error: {connectResult.Error.Message}");
        return 1;
    }

    var request = new RollbackRequest();
    var result = client.SendRequest<RollbackResponse>(request);

    if (result.IsFailure)
    {
        Console.Error.WriteLine($"Error: {result.Error.Message}");
        return 1;
    }

    var response = result.Value;

    if (!response.Ok)
    {
        Console.Error.WriteLine($"Rollback failed: {response.Error ?? "Unknown error"}");
        return 1;
    }

    Console.WriteLine("Panic rollback completed successfully");
    if (response.FiltersRemoved == 0)
    {
        Console.WriteLine("  No filters were present in sublayer");
    }
    else
    {
        Console.WriteLine($"  Filters removed: {response.FiltersRemoved}");
    }
    Console.WriteLine("  Provider and sublayer kept intact");
    Console.WriteLine();
    Console.WriteLine("Use 'wfpctl teardown' to also remove provider and sublayer.");

    return 0;
}
