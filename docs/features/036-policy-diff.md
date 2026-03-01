# Policy Diff View (P7)

## Overview

The Policy Diff View provides side-by-side comparison of two policies, showing added, removed, and modified rules. This is critical for understanding the impact of changes before applying a new policy.

## Features

### Policy Comparison
- Load two policy files (baseline and new)
- Compare rules by ID
- Detect added, removed, and modified rules
- Show unchanged rules for completeness

### Visual Diff Display
- Color-coded diff items:
  - **Green (+)**: Added rules (in new policy but not baseline)
  - **Red (-)**: Removed rules (in baseline but not new)
  - **Yellow (~)**: Modified rules (same ID, different content)
  - **Gray ( )**: Unchanged rules
- Summary showing counts for each category
- Legend for quick reference

### Change Details
- For modified rules, shows exactly what changed:
  - action, direction, protocol
  - process path
  - remote/local IP and ports
  - priority, enabled state
  - comment

### Metadata Changes
- Detects policy version changes
- Detects default action changes

## Usage

1. Click **Compare** button in the header toolbar
2. Click **Browse...** under "Baseline (Left)" to load the original policy
3. Click **Browse...** under "New (Right)" to load the new policy
4. View the diff results showing all changes
5. Use **Swap** to reverse left/right
6. Use **Clear All** to start over

## Files Created

- `src/ui/WfpTrafficControl.UI/Services/PolicyDiffService.cs` - Core diff algorithm
- `src/ui/WfpTrafficControl.UI/ViewModels/PolicyDiffViewModel.cs` - ViewModel
- `src/ui/WfpTrafficControl.UI/Views/PolicyDiffView.xaml` - Dialog UI
- `src/ui/WfpTrafficControl.UI/Views/PolicyDiffView.xaml.cs` - Code-behind

## Files Modified

- `src/ui/WfpTrafficControl.UI/ViewModels/MainViewModel.cs` - Added OpenPolicyDiffCommand
- `src/ui/WfpTrafficControl.UI/MainWindow.xaml` - Added Compare button
- `src/ui/WfpTrafficControl.UI/App.xaml.cs` - Registered PolicyDiffViewModel

## Implementation Details

### Diff Algorithm

The `PolicyDiffService` compares policies by:

1. Building lookup tables by rule ID for both policies
2. Finding added rules (in right but not left)
3. Finding removed rules (in left but not right)
4. Finding modified rules (same ID, comparing all fields)
5. Identifying unchanged rules

### Change Detection

For modified rules, the service compares:
- action, direction, protocol
- process path
- priority, enabled state
- comment
- remote endpoint (IP and ports)
- local endpoint (IP and ports)

### Data Models

```csharp
public class PolicyDiffResult
{
    public List<RuleDiff> AddedRules { get; }
    public List<RuleDiff> RemovedRules { get; }
    public List<ModifiedRuleDiff> ModifiedRules { get; }
    public List<RuleDiff> UnchangedRules { get; }
    public bool DefaultActionChanged { get; }
    public bool VersionChanged { get; }
    public string Summary { get; }
}

public class ModifiedRuleDiff
{
    public Rule OldRule { get; }
    public Rule NewRule { get; }
    public List<string> ChangedFields { get; }
}
```

## Known Limitations

- Comparison is by rule ID only (renaming a rule counts as remove + add)
- No semantic comparison (e.g., equivalent CIDR ranges)
- Large policies may have slow initial load

## Testing

Run the policy diff tests:
```bash
dotnet test tests/Tests.csproj --filter "FullyQualifiedName~PolicyDiffTests"
```
