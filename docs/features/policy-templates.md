# Policy Templates Feature

## Overview

Policy Templates provide pre-configured firewall policies that users can load directly from the UI. This feature enables quick setup of common security configurations without requiring manual rule creation.

## Feature Description

Templates are available in two locations in the Policy Editor:
1. **Toolbar dropdown** - A "Templates" dropdown in the toolbar allows quick selection and loading of templates
2. **Empty state** - When no policy is loaded, template cards are displayed with descriptions and one-click loading

## Available Templates

### Privacy Templates

| Template | Description |
|----------|-------------|
| **Block Cloudflare DNS** | Blocks connections to Cloudflare's DNS servers (1.1.1.1, 1.0.0.1) and their family-safe variants. Use this to prevent applications from using Cloudflare DNS. |
| **Block Google Services** | Blocks major Google services including Search, Gmail, YouTube, and Google APIs. Covers Google's primary IP ranges used for consumer services. |
| **Block Windows Telemetry** | Blocks known Microsoft telemetry and diagnostics endpoints. May affect Windows Update and error reporting. |
| **Block Ads and Trackers** | Blocks common advertising networks and tracking services (DoubleClick, Google Analytics, Facebook Pixel, etc.). |

### Security Templates

| Template | Description |
|----------|-------------|
| **Block All Traffic (Default Deny)** | Creates a default-deny policy that blocks all network traffic except local loopback. Use as a starting point and add explicit allow rules. |
| **Allow Web Browsing Only** | Restricts outbound traffic to only HTTP (80), HTTPS (443), and DNS (53) ports. Blocks other protocols. |

### Productivity Templates

| Template | Description |
|----------|-------------|
| **Block Social Media** | Blocks access to major social media platforms including Facebook, Instagram, Twitter/X, TikTok, and Snapchat. |

### Development Templates

| Template | Description |
|----------|-------------|
| **Development Lockdown** | A security-focused policy for development environments. Allows common development ports (HTTP, HTTPS, SSH, Git) and local development servers (ports 3000-9999) while blocking other traffic. |

## Template Structure

Each template includes:
- **Name**: Display name for the template
- **Category**: Grouping category (Privacy, Security, Productivity, Development)
- **Description**: Detailed explanation of what the template does
- **Warning** (optional): Warning message shown before loading potentially disruptive templates
- **Policy**: The pre-configured policy with rules

## Usage

### Loading a Template from Toolbar

1. In the Policy Editor, locate the "Templates" dropdown in the toolbar
2. Select a template from the dropdown
3. Click the "Load" button
4. If the template has a warning, review it and confirm
5. The template rules are loaded into the editor
6. Review and customize rules as needed
7. Click "Save" to save to a file, or "Apply to Service" to activate

### Loading a Template from Empty State

1. When no policy is loaded, the Policy Editor shows template cards
2. Browse the available templates
3. Click "Load Template" on your chosen template
4. Review warning (if any) and confirm
5. Customize and apply as needed

## Customization

After loading a template, you can:
- Add new rules using the "+" button
- Edit existing rules by selecting them
- Disable rules without removing them (uncheck the checkbox)
- Delete rules you don't need
- Adjust rule priorities
- Change the default action (allow/block)

## Safety Features

1. **Unsaved Changes Warning**: If you have unsaved changes, you'll be prompted before loading a template
2. **Template Warnings**: Potentially disruptive templates (e.g., Block All Traffic) show a warning before loading
3. **No Automatic Apply**: Templates only load into the editor - you must explicitly apply to activate
4. **Review Before Apply**: All rules are visible and editable before application
5. **Rollback**: Standard rollback mechanism works if applied policy causes issues

## Adding Custom Templates

To add new templates, modify `PolicyTemplateProvider.cs`:

```csharp
private static PolicyTemplate CreateMyCustomTemplate()
{
    return new PolicyTemplate
    {
        Id = "my-custom-template",
        Name = "My Custom Template",
        Category = "Custom",
        Description = "Description of what this template does.",
        Warning = null, // Optional warning message
        CreatePolicy = () => new Policy
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            UpdatedAt = DateTime.UtcNow,
            Rules = new List<Rule>
            {
                // Add your rules here
            }
        }
    };
}
```

Then add your template to the `CreateTemplates()` method.

## Known Limitations

1. **IP-based blocking**: Templates use IP addresses/CIDR ranges which may not be comprehensive. Services often use multiple IP ranges that can change. For complete blocking, consider DNS-based solutions.

2. **No automatic updates**: Template IP ranges are static. Google, Facebook, etc. may add new IP ranges over time.

3. **Process paths**: Some templates block specific Windows processes (e.g., CompatTelRunner.exe). These paths may vary across Windows versions.

## Files Changed

- `src/ui/WfpTrafficControl.UI/Models/PolicyTemplate.cs` - Template model class
- `src/ui/WfpTrafficControl.UI/Services/PolicyTemplateProvider.cs` - Template provider with built-in templates
- `src/ui/WfpTrafficControl.UI/ViewModels/PolicyEditorViewModel.cs` - Template loading command
- `src/ui/WfpTrafficControl.UI/Views/PolicyEditorView.xaml` - Template UI (toolbar and empty state)
- `src/ui/WfpTrafficControl.UI/Converters/ObjectToBoolConverter.cs` - Helper converter
- `src/ui/WfpTrafficControl.UI/App.xaml.cs` - DI registration
