using WfpTrafficControl.Shared.Policy;
using WfpTrafficControl.UI.Models;

namespace WfpTrafficControl.UI.Services;

/// <summary>
/// Provides pre-configured policy templates for common use cases.
/// </summary>
public interface IPolicyTemplateProvider
{
    /// <summary>
    /// Gets all available policy templates.
    /// </summary>
    IReadOnlyList<PolicyTemplate> GetTemplates();

    /// <summary>
    /// Gets templates filtered by category.
    /// </summary>
    IReadOnlyList<PolicyTemplate> GetTemplatesByCategory(string category);

    /// <summary>
    /// Gets available template categories.
    /// </summary>
    IReadOnlyList<string> GetCategories();
}

/// <summary>
/// Default implementation of IPolicyTemplateProvider with built-in templates.
/// </summary>
public class PolicyTemplateProvider : IPolicyTemplateProvider
{
    private readonly List<PolicyTemplate> _templates;

    public PolicyTemplateProvider()
    {
        _templates = CreateTemplates();
    }

    public IReadOnlyList<PolicyTemplate> GetTemplates() => _templates;

    public IReadOnlyList<PolicyTemplate> GetTemplatesByCategory(string category) =>
        _templates.Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();

    public IReadOnlyList<string> GetCategories() =>
        _templates.Select(t => t.Category).Distinct().OrderBy(c => c).ToList();

    private static List<PolicyTemplate> CreateTemplates()
    {
        return new List<PolicyTemplate>
        {
            CreateBlockCloudflareDnsTemplate(),
            CreateBlockGoogleServicesTemplate(),
            CreateBlockWindowsTelemetryTemplate(),
            CreateBlockSocialMediaTemplate(),
            CreateBlockAllTrafficTemplate(),
            CreateAllowWebBrowsingOnlyTemplate(),
            CreateDevelopmentLockdownTemplate(),
            CreateBlockAdsAndTrackersTemplate(),
        };
    }

    private static PolicyTemplate CreateBlockCloudflareDnsTemplate()
    {
        return new PolicyTemplate
        {
            Id = "block-cloudflare-dns",
            Name = "Block Cloudflare DNS",
            Category = "Privacy",
            Description = "Blocks connections to Cloudflare's DNS servers (1.1.1.1, 1.0.0.1) and their family-safe variants. Use this if you want to prevent applications from using Cloudflare DNS instead of your preferred DNS provider.",
            CreatePolicy = () => new Policy
            {
                Version = "1.0.0",
                DefaultAction = "allow",
                UpdatedAt = DateTime.UtcNow,
                Rules = new List<Rule>
                {
                    new()
                    {
                        Id = "block-cloudflare-dns-primary-1",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "any",
                        Remote = new EndpointFilter { Ip = "1.1.1.1/32", Ports = "53,443,853" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Cloudflare primary DNS (1.1.1.1)"
                    },
                    new()
                    {
                        Id = "block-cloudflare-dns-primary-2",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "any",
                        Remote = new EndpointFilter { Ip = "1.0.0.1/32", Ports = "53,443,853" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Cloudflare secondary DNS (1.0.0.1)"
                    },
                    new()
                    {
                        Id = "block-cloudflare-dns-malware-1",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "any",
                        Remote = new EndpointFilter { Ip = "1.1.1.2/32", Ports = "53,443,853" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Cloudflare malware-blocking DNS (1.1.1.2)"
                    },
                    new()
                    {
                        Id = "block-cloudflare-dns-malware-2",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "any",
                        Remote = new EndpointFilter { Ip = "1.0.0.2/32", Ports = "53,443,853" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Cloudflare malware-blocking DNS (1.0.0.2)"
                    },
                    new()
                    {
                        Id = "block-cloudflare-dns-family-1",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "any",
                        Remote = new EndpointFilter { Ip = "1.1.1.3/32", Ports = "53,443,853" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Cloudflare family DNS (1.1.1.3)"
                    },
                    new()
                    {
                        Id = "block-cloudflare-dns-family-2",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "any",
                        Remote = new EndpointFilter { Ip = "1.0.0.3/32", Ports = "53,443,853" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Cloudflare family DNS (1.0.0.3)"
                    }
                }
            }
        };
    }

    private static PolicyTemplate CreateBlockGoogleServicesTemplate()
    {
        return new PolicyTemplate
        {
            Id = "block-google-services",
            Name = "Block Google Services",
            Category = "Privacy",
            Description = "Blocks connections to major Google services including Search, Gmail, YouTube, and Google APIs. This covers Google's primary IP ranges used for most consumer services.",
            Warning = "This will block access to Google Search, Gmail, YouTube, Google Drive, and many other Google services. Some applications may not function correctly.",
            CreatePolicy = () => new Policy
            {
                Version = "1.0.0",
                DefaultAction = "allow",
                UpdatedAt = DateTime.UtcNow,
                Rules = new List<Rule>
                {
                    new()
                    {
                        Id = "block-google-dns-1",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "any",
                        Remote = new EndpointFilter { Ip = "8.8.8.8/32" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Google Public DNS primary"
                    },
                    new()
                    {
                        Id = "block-google-dns-2",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "any",
                        Remote = new EndpointFilter { Ip = "8.8.4.4/32" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Google Public DNS secondary"
                    },
                    new()
                    {
                        Id = "block-google-range-1",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ip = "142.250.0.0/15", Ports = "80,443" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Google services (142.250.0.0/15)"
                    },
                    new()
                    {
                        Id = "block-google-range-2",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ip = "172.217.0.0/16", Ports = "80,443" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Google services (172.217.0.0/16)"
                    },
                    new()
                    {
                        Id = "block-google-range-3",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ip = "216.58.0.0/16", Ports = "80,443" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Google services (216.58.0.0/16)"
                    },
                    new()
                    {
                        Id = "block-google-range-4",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ip = "74.125.0.0/16", Ports = "80,443" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Google services (74.125.0.0/16)"
                    },
                    new()
                    {
                        Id = "block-youtube-range",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ip = "208.65.152.0/22", Ports = "80,443" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block YouTube (208.65.152.0/22)"
                    }
                }
            }
        };
    }

    private static PolicyTemplate CreateBlockWindowsTelemetryTemplate()
    {
        return new PolicyTemplate
        {
            Id = "block-windows-telemetry",
            Name = "Block Windows Telemetry",
            Category = "Privacy",
            Description = "Blocks connections to known Microsoft telemetry and diagnostics endpoints. This can reduce data sent to Microsoft but may affect some Windows features like automatic updates and error reporting.",
            Warning = "Blocking telemetry may prevent Windows Update from functioning correctly and disable some diagnostic features.",
            CreatePolicy = () => new Policy
            {
                Version = "1.0.0",
                DefaultAction = "allow",
                UpdatedAt = DateTime.UtcNow,
                Rules = new List<Rule>
                {
                    new()
                    {
                        Id = "block-ms-telemetry-1",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ip = "13.107.4.50/32", Ports = "443" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Microsoft telemetry endpoint"
                    },
                    new()
                    {
                        Id = "block-ms-telemetry-2",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ip = "65.55.252.0/24", Ports = "443" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Microsoft telemetry (watson)"
                    },
                    new()
                    {
                        Id = "block-ms-telemetry-3",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ip = "65.52.100.0/24", Ports = "443" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Microsoft telemetry (vortex)"
                    },
                    new()
                    {
                        Id = "block-ms-telemetry-4",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ip = "204.79.197.200/32", Ports = "443" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Microsoft telemetry (bing telemetry)"
                    },
                    new()
                    {
                        Id = "block-ms-telemetry-5",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ip = "23.218.212.0/24", Ports = "443" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Microsoft telemetry (diagnostics)"
                    },
                    new()
                    {
                        Id = "block-ms-compat-telemetry",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Process = "C:\\Windows\\System32\\CompatTelRunner.exe",
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Windows Compatibility Telemetry process"
                    },
                    new()
                    {
                        Id = "block-ms-diag-track",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Process = "C:\\Windows\\System32\\DiagTrack\\DiagTrackRunner.exe",
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Windows Diagnostic Tracking process"
                    }
                }
            }
        };
    }

    private static PolicyTemplate CreateBlockSocialMediaTemplate()
    {
        return new PolicyTemplate
        {
            Id = "block-social-media",
            Name = "Block Social Media",
            Category = "Productivity",
            Description = "Blocks access to major social media platforms including Facebook, Instagram, Twitter/X, TikTok, and Snapchat. Useful for reducing distractions or parental controls.",
            CreatePolicy = () => new Policy
            {
                Version = "1.0.0",
                DefaultAction = "allow",
                UpdatedAt = DateTime.UtcNow,
                Rules = new List<Rule>
                {
                    // Facebook / Meta
                    new()
                    {
                        Id = "block-facebook-1",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ip = "157.240.0.0/16", Ports = "80,443" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Facebook/Meta (157.240.0.0/16)"
                    },
                    new()
                    {
                        Id = "block-facebook-2",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ip = "31.13.24.0/21", Ports = "80,443" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Facebook/Meta (31.13.24.0/21)"
                    },
                    new()
                    {
                        Id = "block-facebook-3",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ip = "31.13.64.0/18", Ports = "80,443" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Facebook/Meta (31.13.64.0/18)"
                    },
                    new()
                    {
                        Id = "block-instagram",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ip = "185.60.216.0/22", Ports = "80,443" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Instagram CDN"
                    },
                    // Twitter / X
                    new()
                    {
                        Id = "block-twitter-1",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ip = "104.244.42.0/24", Ports = "80,443" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Twitter/X (104.244.42.0/24)"
                    },
                    new()
                    {
                        Id = "block-twitter-2",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ip = "199.59.148.0/22", Ports = "80,443" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Twitter/X (199.59.148.0/22)"
                    },
                    // TikTok
                    new()
                    {
                        Id = "block-tiktok-1",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ip = "161.117.0.0/16", Ports = "80,443" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block TikTok (ByteDance)"
                    },
                    new()
                    {
                        Id = "block-tiktok-2",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ip = "45.136.0.0/16", Ports = "80,443" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block TikTok CDN"
                    },
                    // Snapchat
                    new()
                    {
                        Id = "block-snapchat",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ip = "54.84.0.0/16", Ports = "80,443" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Snapchat"
                    }
                }
            }
        };
    }

    private static PolicyTemplate CreateBlockAllTrafficTemplate()
    {
        return new PolicyTemplate
        {
            Id = "block-all-traffic",
            Name = "Block All Traffic (Default Deny)",
            Category = "Security",
            Description = "Creates a default-deny policy that blocks all network traffic except local loopback. Use this as a starting point and add explicit allow rules for applications you trust.",
            Warning = "This will block ALL network traffic including internet access. Only apply this if you understand the implications and have planned for recovery.",
            CreatePolicy = () => new Policy
            {
                Version = "1.0.0",
                DefaultAction = "block",
                UpdatedAt = DateTime.UtcNow,
                Rules = new List<Rule>
                {
                    new()
                    {
                        Id = "allow-loopback-ipv4",
                        Action = "allow",
                        Direction = "both",
                        Protocol = "any",
                        Remote = new EndpointFilter { Ip = "127.0.0.0/8" },
                        Priority = 200,
                        Enabled = true,
                        Comment = "Allow localhost/loopback traffic (IPv4)"
                    },
                    new()
                    {
                        Id = "allow-loopback-local",
                        Action = "allow",
                        Direction = "both",
                        Protocol = "any",
                        Local = new EndpointFilter { Ip = "127.0.0.0/8" },
                        Priority = 200,
                        Enabled = true,
                        Comment = "Allow localhost/loopback traffic (local endpoint)"
                    }
                }
            }
        };
    }

    private static PolicyTemplate CreateAllowWebBrowsingOnlyTemplate()
    {
        return new PolicyTemplate
        {
            Id = "allow-web-only",
            Name = "Allow Web Browsing Only",
            Category = "Security",
            Description = "Restricts outbound traffic to only HTTP (80) and HTTPS (443) ports. This allows normal web browsing but blocks other protocols like email clients, games, P2P, etc.",
            Warning = "This will block many applications that don't use standard web ports, including some email clients, games, VPNs, and other software.",
            CreatePolicy = () => new Policy
            {
                Version = "1.0.0",
                DefaultAction = "block",
                UpdatedAt = DateTime.UtcNow,
                Rules = new List<Rule>
                {
                    new()
                    {
                        Id = "allow-loopback",
                        Action = "allow",
                        Direction = "both",
                        Protocol = "any",
                        Remote = new EndpointFilter { Ip = "127.0.0.0/8" },
                        Priority = 200,
                        Enabled = true,
                        Comment = "Allow localhost/loopback traffic"
                    },
                    new()
                    {
                        Id = "allow-dns-udp",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "udp",
                        Remote = new EndpointFilter { Ports = "53" },
                        Priority = 150,
                        Enabled = true,
                        Comment = "Allow DNS queries (UDP)"
                    },
                    new()
                    {
                        Id = "allow-dns-tcp",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ports = "53" },
                        Priority = 150,
                        Enabled = true,
                        Comment = "Allow DNS queries (TCP)"
                    },
                    new()
                    {
                        Id = "allow-http",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ports = "80" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Allow HTTP (port 80)"
                    },
                    new()
                    {
                        Id = "allow-https",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ports = "443" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Allow HTTPS (port 443)"
                    }
                }
            }
        };
    }

    private static PolicyTemplate CreateDevelopmentLockdownTemplate()
    {
        return new PolicyTemplate
        {
            Id = "development-lockdown",
            Name = "Development Lockdown",
            Category = "Development",
            Description = "A security-focused policy for development environments. Allows common development ports and tools while blocking unnecessary traffic. Includes allowances for Git, npm/NuGet, Docker, and local development servers.",
            CreatePolicy = () => new Policy
            {
                Version = "1.0.0",
                DefaultAction = "block",
                UpdatedAt = DateTime.UtcNow,
                Rules = new List<Rule>
                {
                    new()
                    {
                        Id = "allow-loopback",
                        Action = "allow",
                        Direction = "both",
                        Protocol = "any",
                        Remote = new EndpointFilter { Ip = "127.0.0.0/8" },
                        Priority = 200,
                        Enabled = true,
                        Comment = "Allow localhost/loopback traffic"
                    },
                    new()
                    {
                        Id = "allow-dns",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "any",
                        Remote = new EndpointFilter { Ports = "53" },
                        Priority = 150,
                        Enabled = true,
                        Comment = "Allow DNS queries"
                    },
                    new()
                    {
                        Id = "allow-https",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ports = "443" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Allow HTTPS for package managers and Git"
                    },
                    new()
                    {
                        Id = "allow-http",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ports = "80" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Allow HTTP"
                    },
                    new()
                    {
                        Id = "allow-ssh",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ports = "22" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Allow SSH/Git over SSH"
                    },
                    new()
                    {
                        Id = "allow-git-protocol",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ports = "9418" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Allow Git protocol"
                    },
                    new()
                    {
                        Id = "allow-local-dev-servers",
                        Action = "allow",
                        Direction = "both",
                        Protocol = "tcp",
                        Local = new EndpointFilter { Ports = "3000-9999" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Allow local development servers (ports 3000-9999)"
                    }
                }
            }
        };
    }

    private static PolicyTemplate CreateBlockAdsAndTrackersTemplate()
    {
        return new PolicyTemplate
        {
            Id = "block-ads-trackers",
            Name = "Block Ads and Trackers",
            Category = "Privacy",
            Description = "Blocks connections to common advertising networks and tracking services. Note that this IP-based blocking is not comprehensive - consider using browser extensions or DNS-based blocking for better coverage.",
            CreatePolicy = () => new Policy
            {
                Version = "1.0.0",
                DefaultAction = "allow",
                UpdatedAt = DateTime.UtcNow,
                Rules = new List<Rule>
                {
                    // DoubleClick (Google Ads)
                    new()
                    {
                        Id = "block-doubleclick",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ip = "216.58.209.0/24", Ports = "80,443" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block DoubleClick/Google Ads"
                    },
                    // Google Analytics
                    new()
                    {
                        Id = "block-google-analytics-1",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ip = "142.251.0.0/16", Ports = "80,443" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Google Analytics range"
                    },
                    // Facebook Pixel / Tracking
                    new()
                    {
                        Id = "block-fb-tracking",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ip = "157.240.1.0/24", Ports = "80,443" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Facebook tracking"
                    },
                    // Adobe Analytics / Omniture
                    new()
                    {
                        Id = "block-adobe-analytics",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ip = "66.235.128.0/17", Ports = "80,443" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Adobe Analytics/Omniture"
                    },
                    // Amazon Advertising
                    new()
                    {
                        Id = "block-amazon-ads",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ip = "54.239.98.0/24", Ports = "80,443" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Amazon Advertising"
                    },
                    // Criteo
                    new()
                    {
                        Id = "block-criteo",
                        Action = "block",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Remote = new EndpointFilter { Ip = "178.250.0.0/16", Ports = "80,443" },
                        Priority = 100,
                        Enabled = true,
                        Comment = "Block Criteo ad network"
                    }
                }
            }
        };
    }
}
