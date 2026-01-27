using WfpTrafficControl.Cli;
using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Ipc;

// Parse command line arguments
var command = args.Length > 0 ? args[0].ToLowerInvariant() : null;

switch (command)
{
    case "status":
    case "ping":
        return RunStatusCommand();

    case "bootstrap":
        return RunBootstrapCommand();

    case "teardown":
        return RunTeardownCommand();

    case "validate":
    case "apply":
    case "rollback":
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
    Console.WriteLine("  status    - Check if the service is running and show info");
    Console.WriteLine("  bootstrap - Create WFP provider and sublayer (idempotent)");
    Console.WriteLine("  teardown  - Remove WFP provider and sublayer (panic rollback)");
    Console.WriteLine("  validate  - Validate a policy file (not yet implemented)");
    Console.WriteLine("  apply     - Apply a policy file (not yet implemented)");
    Console.WriteLine("  rollback  - Rollback to previous policy (not yet implemented)");
    Console.WriteLine("  enable    - Enable traffic control (not yet implemented)");
    Console.WriteLine("  disable   - Disable traffic control (not yet implemented)");
    Console.WriteLine("  logs      - Show logs (not yet implemented)");
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
