using WfpTrafficControl.Cli;
using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Ipc;
using WfpTrafficControl.Shared.Policy;

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
        return RunValidateCommand(args.Length > 1 ? args[1] : null);

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
    Console.WriteLine("  validate <file>    - Validate a policy JSON file");
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

static int RunValidateCommand(string? filePath)
{
    // Step 1: Check if file path was provided
    if (string.IsNullOrWhiteSpace(filePath))
    {
        Console.Error.WriteLine("Error: No policy file specified.");
        Console.Error.WriteLine("Usage: wfpctl validate <policy.json>");
        return 1;
    }

    // Step 2: Check if file exists
    if (!File.Exists(filePath))
    {
        Console.Error.WriteLine($"Error: File not found: {filePath}");
        return 1;
    }

    // Step 3: Check file size
    var fileInfo = new FileInfo(filePath);
    if (fileInfo.Length > PolicyValidator.MaxPolicyFileSize)
    {
        Console.Error.WriteLine($"Error: Policy file exceeds maximum size ({PolicyValidator.MaxPolicyFileSize / 1024} KB)");
        return 1;
    }

    // Step 4: Read file content
    string json;
    try
    {
        json = File.ReadAllText(filePath);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error reading file: {ex.Message}");
        return 1;
    }

    // Step 5: Validate the policy locally (no service needed for validation)
    var result = PolicyValidator.ValidateJson(json);

    // Step 6: Display results
    if (result.IsValid)
    {
        var policy = Policy.FromJson(json);
        Console.WriteLine("Policy is valid.");
        Console.WriteLine();
        Console.WriteLine("Policy Summary:");
        Console.WriteLine($"  Version:        {policy?.Version ?? "unknown"}");
        Console.WriteLine($"  Default Action: {policy?.DefaultAction ?? "unknown"}");
        Console.WriteLine($"  Updated At:     {policy?.UpdatedAt:O}");
        Console.WriteLine($"  Rule Count:     {policy?.Rules.Count ?? 0}");

        if (policy?.Rules.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Rules:");
            var enabledCount = policy.Rules.Count(r => r.Enabled);
            var disabledCount = policy.Rules.Count - enabledCount;
            Console.WriteLine($"  Enabled:  {enabledCount}");
            Console.WriteLine($"  Disabled: {disabledCount}");

            // Show first few rules
            Console.WriteLine();
            Console.WriteLine("First 5 rules (preview):");
            foreach (var rule in policy.Rules.Take(5))
            {
                var status = rule.Enabled ? "+" : "-";
                var direction = rule.Direction.ToUpperInvariant()[0];
                Console.WriteLine($"  [{status}] {rule.Id}: {rule.Action} {rule.Protocol} {direction} {FormatEndpoints(rule)}");
            }
            if (policy.Rules.Count > 5)
            {
                Console.WriteLine($"  ... and {policy.Rules.Count - 5} more rules");
            }
        }

        return 0;
    }
    else
    {
        Console.Error.WriteLine(result.GetSummary());
        return 1;
    }
}

static string FormatEndpoints(Rule rule)
{
    var parts = new List<string>();

    if (rule.Remote != null)
    {
        var remote = new List<string>();
        if (!string.IsNullOrEmpty(rule.Remote.Ip)) remote.Add(rule.Remote.Ip);
        if (!string.IsNullOrEmpty(rule.Remote.Ports)) remote.Add($":{rule.Remote.Ports}");
        if (remote.Count > 0) parts.Add($"to {string.Join("", remote)}");
    }

    if (rule.Local != null)
    {
        var local = new List<string>();
        if (!string.IsNullOrEmpty(rule.Local.Ip)) local.Add(rule.Local.Ip);
        if (!string.IsNullOrEmpty(rule.Local.Ports)) local.Add($":{rule.Local.Ports}");
        if (local.Count > 0) parts.Add($"from {string.Join("", local)}");
    }

    if (!string.IsNullOrEmpty(rule.Process))
    {
        parts.Add($"({Path.GetFileName(rule.Process)})");
    }

    return parts.Count > 0 ? string.Join(" ", parts) : "(any)";
}
