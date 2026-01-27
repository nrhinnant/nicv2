using WfpTrafficControl.Shared;

Console.WriteLine($"{WfpConstants.ProjectName} CLI");
Console.WriteLine("Usage: wfpctl <command>");
Console.WriteLine();
Console.WriteLine("Commands:");
Console.WriteLine("  status    - Show service and policy status");
Console.WriteLine("  validate  - Validate a policy file");
Console.WriteLine("  apply     - Apply a policy file");
Console.WriteLine("  rollback  - Rollback to previous policy");
Console.WriteLine("  enable    - Enable traffic control");
Console.WriteLine("  disable   - Disable traffic control");
Console.WriteLine("  logs      - Show logs");
Console.WriteLine();
Console.WriteLine("(Not yet implemented)");
