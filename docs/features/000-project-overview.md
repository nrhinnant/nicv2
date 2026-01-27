# 000 — Project Overview

## What We Are Building

A **firewall-style traffic control system for Windows** using the **Windows Filtering Platform (WFP)**.

The system allows/blocks network connections based on configurable policy rules matching:
- Process (path or image name)
- Direction (inbound/outbound)
- Protocol (TCP/UDP)
- Local and remote IP/port

## Components

| Component | Description |
|-----------|-------------|
| **Policy Controller Service** | Windows service (LocalSystem) that owns policy state, applies WFP filters, emits logs, exposes IPC |
| **CLI (`wfpctl`)** | Command-line client for status, apply, rollback, enable/disable, logs, validate |
| **Policy Store** | Local JSON file with strict validation; supports hot reload |

## Key Constraints

### Safety
- Default-allow behavior (explicit block rules)
- Panic rollback mechanism to restore connectivity
- Transactional WFP updates (begin/commit/abort)

### WFP Hygiene
- All objects tagged with our provider GUID
- Dedicated provider and sublayer
- Uninstall removes all artifacts

### Security
- Policy input treated as untrusted; strict validation
- IPC requires local admin authentication
- No arbitrary code execution or shell-outs

## Enforcement Scope

User-mode only using WFP management APIs (`Fwpm*`):
- ALE Outbound Connect Authorization Layer
- ALE Inbound Accept Authorization Layer

No kernel callout driver.

## Out of Scope

- Deep packet inspection (L7)
- Stream modification or injection
- Consumer UI (CLI is sufficient)
- Enterprise management plane

## Development Environment

- Windows VM with snapshots for safe testing
- Run as elevated/LocalSystem for WFP access

---

*See [CLAUDE.md](../../CLAUDE.md) for full project operating guide.*
