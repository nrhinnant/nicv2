# 034 - Rule Simulation ("What If" Testing)

## Overview

The Rule Simulator allows users to test hypothetical connections against the current policy without actually making any network connections. This "What If" testing helps validate that rules are configured correctly before they affect real traffic.

## Features

### Simulation Dialog

A dialog window accessible from the main toolbar that allows users to:

1. **Specify Connection Parameters:**
   - Direction: outbound or inbound
   - Protocol: TCP or UDP
   - Remote IP address (required)
   - Remote port (optional)
   - Process path (optional) - with Browse and Pick buttons
   - Local IP address (optional)
   - Local port (optional)

2. **View Simulation Results:**
   - Clear ALLOWED/BLOCKED verdict with color coding (green/red)
   - Summary of why the connection was allowed/blocked
   - Details of the matched rule (if any)
   - Option to view the full evaluation trace

3. **Evaluation Trace:**
   - Shows all rules that were evaluated
   - Indicates which rule matched (if any)
   - Shows the reason for each match/non-match
   - Helps debug complex rule configurations

### Simulation Logic

The simulator evaluates rules in priority order (highest priority first) and returns the first matching rule. If no rule matches, the policy's default action is applied.

**Matching Criteria:**
- **Direction:** "both" matches any direction; otherwise must match exactly
- **Protocol:** "any" matches any protocol; otherwise must match exactly
- **Process:** Full path match or executable name match
- **Remote IP:** Supports individual IPs and CIDR notation
- **Remote Ports:** Supports single ports, ranges (80-443), and lists (80,443,8080)
- **Local IP/Ports:** Same as remote, when specified

## Implementation Details

### Files Created/Modified

**New Files:**
- `src/shared/Ipc/SimulateMessages.cs` - IPC request/response classes
- `src/shared/Policy/RuleSimulator.cs` - Core simulation logic
- `src/ui/WfpTrafficControl.UI/ViewModels/RuleSimulatorViewModel.cs` - ViewModel
- `src/ui/WfpTrafficControl.UI/Views/RuleSimulatorView.xaml` - Dialog UI
- `src/ui/WfpTrafficControl.UI/Views/RuleSimulatorView.xaml.cs` - Code-behind
- `src/ui/WfpTrafficControl.UI/Converters/NullToVisibilityConverter.cs` - New converters
- `src/ui/WfpTrafficControl.UI/Converters/BooleanToToggleTextConverter.cs` - New converter

**Modified Files:**
- `src/shared/Ipc/IpcMessages.cs` - Added ParseSimulateRequest
- `src/service/Ipc/PipeServer.cs` - Added ProcessSimulateRequest handler
- `src/ui/WfpTrafficControl.UI/Services/IServiceClient.cs` - Added SimulateAsync
- `src/ui/WfpTrafficControl.UI/Services/ServiceClient.cs` - Implemented SimulateAsync
- `src/ui/WfpTrafficControl.UI/ViewModels/MainViewModel.cs` - Added OpenRuleSimulatorCommand
- `src/ui/WfpTrafficControl.UI/MainWindow.xaml` - Added Simulate button
- `src/ui/WfpTrafficControl.UI/App.xaml.cs` - Registered RuleSimulatorViewModel

### IPC Protocol

**Request:**
```json
{
  "type": "simulate",
  "direction": "outbound",
  "protocol": "tcp",
  "remoteIp": "192.168.1.100",
  "remotePort": 443,
  "processPath": "C:\\Program Files\\App\\app.exe",
  "localIp": null,
  "localPort": null
}
```

**Response:**
```json
{
  "ok": true,
  "wouldAllow": false,
  "matchedRuleId": "block-https",
  "matchedAction": "block",
  "matchedRuleComment": "Block HTTPS to internal network",
  "usedDefaultAction": false,
  "defaultAction": "allow",
  "evaluationTrace": [
    {
      "ruleId": "block-https",
      "action": "block",
      "matched": true,
      "reason": "All criteria matched",
      "priority": 100
    }
  ],
  "rulesEvaluated": 1,
  "policyLoaded": true,
  "policyVersion": "1.0.0"
}
```

## Usage

### Opening the Simulator

1. Click the **Simulate** button in the main window header
2. The Rule Simulator dialog opens

### Performing a Simulation

1. Select the direction (outbound/inbound)
2. Select the protocol (TCP/UDP)
3. Enter the remote IP address (required)
4. Optionally enter:
   - Remote port
   - Process path (use Browse or Pick buttons)
   - Local IP/port
5. Click **Simulate**
6. View the result

### Understanding Results

**ALLOWED (Green Banner):**
- The connection would be permitted
- Either a matching "allow" rule was found, or no rule matched and default action is "allow"

**BLOCKED (Red Banner):**
- The connection would be denied
- A matching "block" rule was found, or default action is "block"

**Evaluation Trace:**
- Click "Show Trace" to see all evaluated rules
- Each row shows a rule and why it did/didn't match
- Useful for understanding rule priority and matching logic

## Security Considerations

- Simulation is read-only and does not affect WFP filters
- Only uses the currently loaded policy from LKG store
- Does not make any actual network connections
- Process path is not validated for existence (allows testing paths that don't exist yet)

## Testing

### Manual Testing

1. Load a policy with various rules
2. Open the Rule Simulator
3. Test a connection that should be blocked:
   - Verify BLOCKED result
   - Verify correct rule is shown as matching
4. Test a connection that should be allowed:
   - Verify ALLOWED result
   - If no rule matches, verify default action is shown
5. Test with process path:
   - Use Pick button to select a running process
   - Verify process matching works correctly
6. View evaluation trace:
   - Expand the trace
   - Verify all rules are listed with correct match status

### Automated Tests

Unit tests verify:
- Simulation logic for various rule combinations
- IP/CIDR matching
- Port range matching
- Process path matching
- Default action behavior

## Known Limitations

- Simulation uses the LKG (Last Known Good) policy, not pending editor changes
- Local IP/port matching is simulated but may have WFP limitations in actual enforcement
- IPv6 addresses are supported in simulation but actual WFP support may vary
- Process paths must be absolute paths for reliable matching
