# WFP Traffic Control System - Architecture Diagrams

This file contains Mermaid diagrams that can be:
- Previewed in VS Code with the "Mermaid Preview" extension
- Converted to images at https://mermaid.live
- Embedded directly in GitHub/Azure DevOps markdown
- Used in documentation tools (Sphinx, MkDocs, Docusaurus)

---

## 1. High-Level Architecture

```mermaid
graph TB
    subgraph UserSpace["USER SPACE"]
        subgraph CLI["CLI (wfpctl.exe)"]
            A1[status/validate]
            A2[apply/lkg revert]
            A3[rollback/teardown]
            A4[logs]
            A5[watch]
        end

        A1 & A2 & A3 & A4 & A5 -->|Named Pipe<br/>Admin ACL| B1

        subgraph Service["Windows Service (LocalSystem)"]
            B1[PipeServer<br/>IPC Handler]
            B2[PolicyFileWatcher<br/>Hot Reload]
            B3[WfpEngine<br/>Filter Manager]

            B1 --> B3
            B1 --> B2
            B2 --> B3

            subgraph Core["Core Components"]
                C1[PolicyValidator]
                C2[RuleCompiler]
                C3[WfpInterop<br/>P/Invoke]
            end

            B3 --> C1
            B3 --> C2
            B3 --> C3
        end

        subgraph Storage["Policy Store<br/>%ProgramData%\WfpTrafficControl"]
            D1[(lkg-policy.json<br/>SHA256 checksum)]
            D2[(audit.log<br/>JSON Lines)]
        end

        B1 --> D1
        B1 --> D2
        B2 --> D1
    end

    subgraph KernelSpace["KERNEL SPACE"]
        E1[Base Filtering Engine]

        subgraph WFP["Windows Filtering Platform"]
            F1[Provider<br/>7A3F8E2D...]
            F2[Sublayer<br/>B2C4D6E8...]
            F3[Filters<br/>ALE_AUTH_CONNECT_V4<br/>ALE_AUTH_RECV_ACCEPT_V4]

            F1 --> F2
            F2 --> F3
        end

        E1 --> F1
    end

    C3 -->|Fwpm* APIs| E1
    F3 -->|Enforce| G1[Network Stack<br/>TCPIP.sys]

    style CLI fill:#e1f5ff
    style Service fill:#fff4e1
    style Storage fill:#f0f0f0
    style KernelSpace fill:#ffe1e1
    style WFP fill:#ffcccc
```

---

## 2. Policy Apply Flow (Sequence Diagram)

```mermaid
sequenceDiagram
    actor User
    participant CLI as CLI<br/>(wfpctl)
    participant Pipe as PipeServer
    participant Validator as PolicyValidator
    participant Compiler as RuleCompiler
    participant Engine as WfpEngine
    participant WFP as Windows<br/>Filtering Platform
    participant LKG as LKG Store
    participant Audit as Audit Log

    User->>CLI: wfpctl apply policy.json
    CLI->>CLI: Read policy file
    CLI->>Pipe: ApplyRequest (JSON via pipe)

    rect rgb(255, 240, 240)
        Note over Pipe: Security Checks
        Pipe->>Pipe: 1. Authorize (admin check)
        Pipe->>Pipe: 2. Rate limit check
        Pipe->>Pipe: 3. Protocol version check
    end

    Pipe->>Pipe: Read file (atomic, TOCTOU-safe)
    Audit->>Audit: Log: apply-started

    Pipe->>Validator: ValidateJson(policyJson)
    Validator->>Validator: Check schema, constraints, duplicates
    Validator-->>Pipe: ValidationResult

    alt Validation Failed
        Pipe-->>CLI: Error: validation failed
        CLI-->>User: Display errors
    end

    Pipe->>Compiler: Compile(policy)
    Compiler->>Compiler: For each rule:<br/>Parse IP/CIDR, ports<br/>Generate deterministic GUID
    Compiler-->>Pipe: CompilationResult<br/>(List<CompiledFilter>)

    alt Compilation Failed
        Pipe-->>CLI: Error: compilation failed
        CLI-->>User: Display errors
    end

    Pipe->>Engine: ApplyFilters(filters)

    rect rgb(240, 255, 240)
        Note over Engine,WFP: Idempotent Reconciliation
        Engine->>WFP: Enumerate existing filters
        WFP-->>Engine: Current filter list
        Engine->>Engine: Compute diff<br/>(ToAdd, ToRemove, Unchanged)

        alt Diff is empty
            Engine-->>Pipe: Success (0 changes)
        else Has changes
            Engine->>WFP: Begin transaction
            Engine->>WFP: Delete obsolete filters
            Engine->>WFP: Create new filters
            Engine->>WFP: Commit transaction
            WFP-->>Engine: Success
            Engine-->>Pipe: ApplyResult<br/>(created, removed, unchanged)
        end
    end

    Pipe->>LKG: Save(policyJson, sourcePath)
    LKG->>LKG: Compute SHA256<br/>Atomic write (temp + rename)
    LKG-->>Pipe: Success (non-fatal)

    Audit->>Audit: Log: apply-finished<br/>(success, filter counts)

    Pipe-->>CLI: ApplyResponse (success)
    CLI-->>User: Display: 3 created, 1 removed, 5 unchanged
```

---

## 3. Component Dependencies

```mermaid
graph LR
    subgraph Shared["Shared Library"]
        WfpConstants[WfpConstants<br/>GUIDs, Paths]
        PolicyModels[Policy Models<br/>Rule, Policy, EndpointFilter]
        PolicyValidator[PolicyValidator<br/>Schema validation]
        RuleCompiler[RuleCompiler<br/>Rule → Filter]
        FilterDiff[FilterDiffComputer<br/>Reconciliation]
        LkgStore[LkgStore<br/>Persistence + SHA256]
        AuditLog[AuditLogWriter/Reader<br/>JSON Lines]
        WfpInterop[WfpInterop<br/>P/Invoke to fwpuclnt.dll]
        WfpTransaction[WfpTransaction<br/>RAII wrapper]
        IWfpEngine[IWfpEngine<br/>Interface]
    end

    subgraph Service["Service Assembly"]
        Worker[Worker<br/>Service host]
        PipeServer[PipeServer<br/>IPC handler]
        WfpEngine[WfpEngine<br/>Implementation]
        FileWatcher[PolicyFileWatcher<br/>Hot reload]
        RateLimiter[RateLimiter<br/>Token bucket]
    end

    subgraph CLI["CLI Assembly"]
        Program[Program<br/>CLI parser]
        PipeClient[PipeClient<br/>IPC client]
    end

    Worker --> PipeServer
    Worker --> FileWatcher
    Worker --> WfpEngine
    PipeServer --> WfpEngine
    PipeServer --> FileWatcher
    PipeServer --> RateLimiter
    PipeServer --> LkgStore
    PipeServer --> AuditLog
    FileWatcher --> WfpEngine
    FileWatcher --> LkgStore
    WfpEngine --> PolicyValidator
    WfpEngine --> RuleCompiler
    WfpEngine --> FilterDiff
    WfpEngine --> WfpInterop
    WfpEngine --> WfpTransaction
    WfpEngine -.implements.-> IWfpEngine

    Program --> PipeClient

    style Shared fill:#e1f5ff
    style Service fill:#fff4e1
    style CLI fill:#e1ffe1
```

---

## 4. Idempotent Reconciliation Process

```mermaid
flowchart TD
    Start([User: wfpctl apply policy.json])

    Start --> Load[Load & Validate Policy]
    Load --> Compile[Compile Rules to Filters<br/>Generate Deterministic GUIDs]
    Compile --> Enum[Enumerate Existing Filters<br/>from WFP Sublayer]

    Enum --> Diff{Compute Diff}

    Diff --> ToAdd[ToAdd:<br/>GUIDs in desired<br/>but not in current]
    Diff --> ToRemove[ToRemove:<br/>GUIDs in current<br/>but not in desired]
    Diff --> Unchanged[Unchanged:<br/>GUIDs in both]

    ToAdd --> Empty{Diff Empty?}
    ToRemove --> Empty
    Unchanged --> Empty

    Empty -->|Yes| NoOp[Skip Transaction<br/>Return: 0 changes]
    Empty -->|No| Begin[Begin WFP Transaction]

    Begin --> Delete[Delete Filters<br/>in ToRemove]
    Delete --> Create[Create Filters<br/>in ToAdd]
    Create --> Commit[Commit Transaction]

    Commit --> SaveLKG[Save as LKG<br/>SHA256 + Atomic Write]
    NoOp --> SaveLKG

    SaveLKG --> End([Return: ApplyResult])

    style Start fill:#e1ffe1
    style NoOp fill:#ffffcc
    style Commit fill:#e1ffe1
    style End fill:#e1ffe1
```

---

## 5. Safety Mechanisms

```mermaid
graph TD
    subgraph Mechanisms["Safety Mechanisms"]
        direction TB

        S1[🔒 Transactional Updates<br/>WfpTransaction RAII<br/>All-or-nothing commits]

        S2[🛡️ Fail-Open Behavior<br/>LKG corrupt → allow all<br/>Apply fails → keep existing]

        S3[🚨 Panic Rollback<br/>wfpctl rollback<br/>Delete all filters instantly]

        S4[💾 LKG Recovery<br/>SHA256 integrity<br/>wfpctl lkg revert]

        S5[🔄 Idempotent Apply<br/>Same input → zero changes<br/>Deterministic GUIDs]

        S6[🔍 Input Validation<br/>Schema checks<br/>Path traversal protection<br/>Rate limiting]
    end

    Policy[Policy Apply Request] --> S6
    S6 --> S2
    S2 --> S5
    S5 --> S1
    S1 --> WFP[Windows Filtering Platform]

    Error[Error/Failure] --> S3
    Error --> S4
    S3 --> WFP
    S4 --> WFP

    WFP --> Network[Network Stack<br/>Connection Enforcement]

    style Mechanisms fill:#fff4e1
    style WFP fill:#ffe1e1
    style Network fill:#e1ffe1
```

---

## 6. Security Layers (Defense in Depth)

```mermaid
graph LR
    Client[CLI Client] -->|1. OS ACL Check| ACL{Named Pipe ACL<br/>Admins + LocalSystem only}

    ACL -->|Denied| Reject1[Access Denied]
    ACL -->|Allowed| Auth{2. App Authorization<br/>Impersonate + Check Role}

    Auth -->|Not Admin| Reject2[Access Denied]
    Auth -->|Admin| Version{3. Protocol Version<br/>Check Compatibility}

    Version -->|Mismatch| Reject3[Version Error]
    Version -->|Match| Size{4. Size Limit<br/>Max 64 KB}

    Size -->|Too Large| Reject4[Message Too Large]
    Size -->|OK| Rate{5. Rate Limiter<br/>Token Bucket}

    Rate -->|Exceeded| Reject5[Rate Limit Exceeded]
    Rate -->|OK| Process[Process Request]

    Process --> Validate{6. Input Validation<br/>Schema, Path, CIDR}

    Validate -->|Invalid| Reject6[Validation Error]
    Validate -->|Valid| Execute[Execute Operation]

    style Reject1 fill:#ffe1e1
    style Reject2 fill:#ffe1e1
    style Reject3 fill:#ffe1e1
    style Reject4 fill:#ffe1e1
    style Reject5 fill:#ffe1e1
    style Reject6 fill:#ffe1e1
    style Execute fill:#e1ffe1
```

---

## 7. Data Flow: User Space to Kernel Space

```mermaid
flowchart TD
    subgraph User["User Space"]
        A[User Policy JSON File]
        B[CLI: wfpctl apply]
        C[Named Pipe IPC]
        D[Service: PipeServer]
        E[PolicyValidator]
        F[RuleCompiler]
        G[WfpEngine]
        H[WfpInterop<br/>P/Invoke]
    end

    subgraph Kernel["Kernel Space"]
        I[fwpuclnt.dll]
        J[Base Filtering Engine]
        K[Provider: 7A3F8E2D...]
        L[Sublayer: B2C4D6E8...]
        M[Filters with Conditions]
    end

    subgraph Network["Network Layer"]
        N[TCPIP.sys]
        O[Network Stack]
    end

    A --> B
    B -->|ApplyRequest| C
    C --> D
    D --> E
    E -->|Valid| F
    F -->|CompiledFilters| G
    G -->|Fwpm* APIs| H
    H -->|System Calls| I
    I --> J
    J --> K
    K --> L
    L --> M
    M -->|Enforce| N
    N --> O

    style User fill:#e1f5ff
    style Kernel fill:#ffe1e1
    style Network fill:#e1ffe1
```

---

## Converting Diagrams to Images

### Method 1: Online (Easiest)
1. Go to https://mermaid.live
2. Paste any diagram code from above
3. Click "Download PNG" or "Download SVG"

### Method 2: VS Code
1. Install extension: "Mermaid Preview" or "Markdown Preview Mermaid Support"
2. Open this file in VS Code
3. Right-click diagram → "Export to PNG/SVG"

### Method 3: Command Line
```bash
# Install mermaid-cli
npm install -g @mermaid-js/mermaid-cli

# Convert to PNG
mmdc -i ARCHITECTURE_DIAGRAMS.md -o architecture.png

# Convert to SVG
mmdc -i ARCHITECTURE_DIAGRAMS.md -o architecture.svg
```

### Method 4: Python
```bash
pip install mermaid-py
python -c "from mermaid import Mermaid; Mermaid('diagram.mmd').to_png('output.png')"
```

---

## Using in Documentation

### GitHub Markdown
Just paste the mermaid code blocks directly - GitHub renders them automatically.

### Azure DevOps Wiki
Paste the mermaid code blocks - Azure DevOps supports Mermaid natively.

### Confluence
Use the "Mermaid Diagrams" app from Atlassian Marketplace.

### PowerPoint/Word
1. Export as PNG/SVG using methods above
2. Insert as image

### Sphinx Documentation
Add `sphinxcontrib-mermaid` extension to conf.py.

### MkDocs
Add `pymdown-extensions` to mkdocs.yml.
