# Phase 11: Policy Schema v1

## Overview

This phase introduces the policy schema types and strict validation for the WFP Traffic Control system. The policy model defines the structure of firewall rules that will later be compiled into WFP filters.

**Scope**: Schema definition and validation only. No WFP application in this phase.

## Policy Model

### Policy Object

```json
{
  "version": "1.0.0",
  "defaultAction": "allow",
  "updatedAt": "2024-01-15T10:30:00Z",
  "rules": []
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `version` | string | Yes | Semantic version (e.g., "1.0.0") |
| `defaultAction` | string | Yes | Action when no rules match: `"allow"` or `"block"` |
| `updatedAt` | datetime | Yes | ISO 8601 timestamp of last update |
| `rules` | array | Yes | Ordered list of rules (can be empty) |

### Rule Object

```json
{
  "id": "block-dns-external",
  "action": "block",
  "direction": "outbound",
  "protocol": "udp",
  "process": "C:\\Windows\\System32\\svchost.exe",
  "local": { "ports": "53" },
  "remote": { "ip": "0.0.0.0/0" },
  "priority": 100,
  "enabled": true,
  "comment": "Block external DNS queries"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | string | Yes | Unique identifier (alphanumeric, dashes, underscores) |
| `action` | string | Yes | `"allow"` or `"block"` |
| `direction` | string | Yes | `"inbound"`, `"outbound"`, or `"both"` |
| `protocol` | string | Yes | `"tcp"`, `"udp"`, or `"any"` |
| `process` | string | No | Full path (e.g., `C:\app.exe`) or image name (e.g., `app.exe`) |
| `local` | object | No | Local endpoint filter |
| `remote` | object | No | Remote endpoint filter |
| `priority` | int | Yes | Higher values take precedence |
| `enabled` | bool | Yes | Whether rule is active |
| `comment` | string | No | Human-readable description |

### EndpointFilter Object

```json
{
  "ip": "192.168.1.0/24",
  "ports": "80,443,8080-8090"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `ip` | string | No* | IP address or CIDR notation |
| `ports` | string | No* | Port spec: single, range, or comma-separated |

*At least one of `ip` or `ports` must be specified.

## Validation Rules

### Policy-Level Validation

1. **version** - Must be valid semantic version format (`X.Y.Z` with optional pre-release)
2. **defaultAction** - Must be `"allow"` or `"block"` (case-insensitive)
3. **updatedAt** - Must be set and not in the future (5-minute grace period)
4. **rules** - Required array (can be empty), max 10,000 rules

### Rule-Level Validation

1. **id** - Required, unique within policy, max 128 chars, alphanumeric/dashes/underscores only
2. **action** - Required, must be `"allow"` or `"block"`
3. **direction** - Required, must be `"inbound"`, `"outbound"`, or `"both"`
4. **protocol** - Required, must be `"tcp"`, `"udp"`, or `"any"`
5. **process** - If specified:
   - Max 260 characters
   - Must be full path (`C:\...`) or image name only (`app.exe`)
   - No path traversal (`..` not allowed)
6. **comment** - If specified, max 1024 characters

### Endpoint Filter Validation

1. At least one of `ip` or `ports` must be specified
2. **ip** - Valid IPv4/IPv6 address or CIDR notation
   - IPv4 prefix: 0-32
   - IPv6 prefix: 0-128
3. **ports** - Valid port specification:
   - Single port: `80` (range 1-65535)
   - Range: `80-443` (start <= end)
   - List: `80,443,8080-8090`

### Size Limits

| Limit | Value |
|-------|-------|
| Max policy JSON size | 1 MB |
| Max rules per policy | 10,000 |
| Max rule ID length | 128 chars |
| Max process path length | 260 chars |
| Max comment length | 1024 chars |

## CLI Usage

### Validate Command

```bash
wfpctl validate <policy.json>
```

**Success output:**
```
Policy is valid.

Policy Summary:
  Version:        1.0.0
  Default Action: allow
  Updated At:     2024-01-15T10:30:00.0000000Z
  Rule Count:     5

Rules:
  Enabled:  4
  Disabled: 1

First 5 rules (preview):
  [+] block-dns-external: block udp O to 0.0.0.0/0 (svchost.exe)
  [+] allow-https-out: allow tcp O to :443
  [-] test-rule: block tcp B (disabled)
```

**Error output:**
```
Policy validation failed with 3 error(s):

  - rules[0].id: Rule ID is required
  - rules[1] (id='my-rule').protocol: Invalid protocol: 'icmp'. Must be one of: tcp, udp, any
  - rules[2] (id='dup-rule').id: Duplicate rule ID. First occurrence at rules[1]
```

### Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Policy is valid |
| 1 | Validation failed or file error |

## Example Policies

### Minimal Valid Policy

```json
{
  "version": "1.0.0",
  "defaultAction": "allow",
  "updatedAt": "2024-01-15T10:30:00Z",
  "rules": []
}
```

### Block Specific IP

```json
{
  "version": "1.0.0",
  "defaultAction": "allow",
  "updatedAt": "2024-01-15T10:30:00Z",
  "rules": [
    {
      "id": "block-bad-ip",
      "action": "block",
      "direction": "both",
      "protocol": "any",
      "remote": { "ip": "203.0.113.50" },
      "priority": 100,
      "enabled": true,
      "comment": "Block known malicious IP"
    }
  ]
}
```

### Application-Specific Rules

```json
{
  "version": "1.0.0",
  "defaultAction": "allow",
  "updatedAt": "2024-01-15T10:30:00Z",
  "rules": [
    {
      "id": "chrome-allow-https",
      "action": "allow",
      "direction": "outbound",
      "protocol": "tcp",
      "process": "chrome.exe",
      "remote": { "ports": "443" },
      "priority": 100,
      "enabled": true
    },
    {
      "id": "chrome-block-other",
      "action": "block",
      "direction": "outbound",
      "protocol": "any",
      "process": "chrome.exe",
      "priority": 50,
      "enabled": true,
      "comment": "Block all other Chrome traffic"
    }
  ]
}
```

### Complex Port Rules

```json
{
  "version": "1.0.0",
  "defaultAction": "block",
  "updatedAt": "2024-01-15T10:30:00Z",
  "rules": [
    {
      "id": "allow-web-ports",
      "action": "allow",
      "direction": "outbound",
      "protocol": "tcp",
      "remote": { "ports": "80,443,8080-8090" },
      "priority": 100,
      "enabled": true
    },
    {
      "id": "allow-dns",
      "action": "allow",
      "direction": "outbound",
      "protocol": "udp",
      "remote": { "ip": "8.8.8.8", "ports": "53" },
      "priority": 100,
      "enabled": true
    }
  ]
}
```

## Files Changed

| File | Change |
|------|--------|
| `src/shared/Policy/PolicyModels.cs` | New - Policy, Rule, EndpointFilter types |
| `src/shared/Policy/NetworkUtils.cs` | New - CIDR and port validation utilities |
| `src/shared/Policy/PolicyValidator.cs` | New - Strict validation logic |
| `src/shared/Ipc/ValidateMessages.cs` | New - IPC messages for validate command |
| `src/shared/Ipc/IpcMessages.cs` | Modified - Added validate request parsing |
| `src/cli/Program.cs` | Modified - Implemented validate command |
| `src/service/Ipc/PipeServer.cs` | Modified - Added validate request handler |

## Testing

Run the unit tests:
```bash
dotnet test --filter "FullyQualifiedName~PolicyValidation"
```

Manual validation:
```bash
# Build the CLI
dotnet build src/cli

# Validate a policy file
./src/cli/bin/Debug/net8.0/wfpctl validate path/to/policy.json
```

## Known Limitations

1. **No WFP Application** - This phase only validates policies; actual WFP filter creation is future work
2. **No Policy Storage** - Policies are validated but not persisted
3. **Process Matching** - Process paths are validated for format only; existence is not checked
4. **No Schema Evolution** - Version field is validated but no migration logic exists yet

## Security Considerations

- Policy input is treated as untrusted (per CLAUDE.md requirements)
- Path traversal attempts are blocked
- Size limits prevent resource exhaustion
- Invalid characters are rejected to prevent injection

## Rollback

This phase is validation-only and does not modify WFP state. No rollback needed.
