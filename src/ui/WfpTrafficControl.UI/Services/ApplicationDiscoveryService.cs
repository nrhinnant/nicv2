using System.IO;
using Microsoft.Win32;

namespace WfpTrafficControl.UI.Services;

/// <summary>
/// Service for discovering installed applications and analyzing rule coverage.
/// </summary>
public interface IApplicationDiscoveryService
{
    /// <summary>
    /// Discovers installed applications that may use the network.
    /// </summary>
    Task<List<DiscoveredApplication>> DiscoverApplicationsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets suggested rules for a specific application.
    /// </summary>
    List<SuggestedRule> GetSuggestedRules(string applicationId);

    /// <summary>
    /// Gets all application signatures (known apps with suggested rules).
    /// </summary>
    List<ApplicationSignature> GetApplicationSignatures();
}

/// <summary>
/// Represents a discovered application on the system.
/// </summary>
public class DiscoveredApplication
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string? InstallPath { get; set; }
    public string? ExecutablePath { get; set; }
    public string? Version { get; set; }
    public DateTime? InstallDate { get; set; }
    public ApplicationCoverageStatus CoverageStatus { get; set; } = ApplicationCoverageStatus.Unknown;
    public bool IsKnownApplication { get; set; }
    public string? KnownApplicationId { get; set; }
    public List<string> MatchingRules { get; set; } = new();
    public List<SuggestedRule> SuggestedRules { get; set; } = new();
}

/// <summary>
/// Coverage status for an application.
/// </summary>
public enum ApplicationCoverageStatus
{
    Unknown,
    Uncovered,
    PartiallyCovered,
    FullyCovered
}

/// <summary>
/// A rule suggestion for an application.
/// </summary>
public class SuggestedRule
{
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Action { get; set; } = "allow";
    public string Direction { get; set; } = "outbound";
    public string Protocol { get; set; } = "tcp";
    public string? RemoteIp { get; set; }
    public List<int>? RemotePorts { get; set; }
    public string? Comment { get; set; }
    public SuggestionPriority Priority { get; set; } = SuggestionPriority.Normal;
}

/// <summary>
/// Priority for rule suggestions.
/// </summary>
public enum SuggestionPriority
{
    Low,
    Normal,
    High,
    Critical
}

/// <summary>
/// Known application signature with typical network behavior.
/// </summary>
public class ApplicationSignature
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public List<string> ExecutablePatterns { get; set; } = new();
    public List<SuggestedRule> DefaultRules { get; set; } = new();
    public string? Description { get; set; }
    public string? Category { get; set; }
}

/// <summary>
/// Implementation of application discovery service.
/// </summary>
public class ApplicationDiscoveryService : IApplicationDiscoveryService
{
    private static readonly List<ApplicationSignature> _signatures = InitializeSignatures();

    /// <inheritdoc />
    public Task<List<DiscoveredApplication>> DiscoverApplicationsAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var applications = new List<DiscoveredApplication>();

            // Scan registry for installed applications
            ScanRegistry(applications, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", ct);
            ScanRegistry(applications, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", ct);

            // Remove duplicates by executable path
            var uniqueApps = applications
                .GroupBy(a => a.ExecutablePath?.ToLowerInvariant() ?? a.Name.ToLowerInvariant())
                .Select(g => g.First())
                .ToList();

            // Match against known signatures and add suggestions
            foreach (var app in uniqueApps)
            {
                ct.ThrowIfCancellationRequested();
                MatchSignature(app);
            }

            // Sort by name
            return uniqueApps.OrderBy(a => a.Name).ToList();
        }, ct);
    }

    /// <inheritdoc />
    public List<SuggestedRule> GetSuggestedRules(string applicationId)
    {
        var signature = _signatures.FirstOrDefault(s => s.Id == applicationId);
        return signature?.DefaultRules ?? new List<SuggestedRule>();
    }

    /// <inheritdoc />
    public List<ApplicationSignature> GetApplicationSignatures()
    {
        return _signatures.ToList();
    }

    private static void ScanRegistry(List<DiscoveredApplication> applications, string keyPath, CancellationToken ct)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath);
            if (key == null)
                return;

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    using var subKey = key.OpenSubKey(subKeyName);
                    if (subKey == null)
                        continue;

                    var displayName = subKey.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(displayName))
                        continue;

                    // Skip system components and updates
                    var systemComponent = subKey.GetValue("SystemComponent");
                    if (systemComponent is int sc && sc == 1)
                        continue;

                    var parentKey = subKey.GetValue("ParentKeyName") as string;
                    if (!string.IsNullOrEmpty(parentKey))
                        continue; // Skip updates/patches

                    var app = new DiscoveredApplication
                    {
                        Id = subKeyName,
                        Name = displayName,
                        Publisher = subKey.GetValue("Publisher") as string ?? string.Empty,
                        InstallPath = subKey.GetValue("InstallLocation") as string,
                        Version = subKey.GetValue("DisplayVersion") as string
                    };

                    // Try to find executable path
                    var displayIcon = subKey.GetValue("DisplayIcon") as string;
                    if (!string.IsNullOrEmpty(displayIcon))
                    {
                        // Remove icon index if present
                        var iconPath = displayIcon.Split(',')[0].Trim('"');
                        if (iconPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                            File.Exists(iconPath))
                        {
                            app.ExecutablePath = iconPath;
                        }
                    }

                    // Fall back to install path if executable not found
                    if (string.IsNullOrEmpty(app.ExecutablePath) && !string.IsNullOrEmpty(app.InstallPath))
                    {
                        var possibleExe = Path.Combine(app.InstallPath, displayName + ".exe");
                        if (File.Exists(possibleExe))
                        {
                            app.ExecutablePath = possibleExe;
                        }
                    }

                    // Try install date
                    var installDate = subKey.GetValue("InstallDate") as string;
                    if (!string.IsNullOrEmpty(installDate) && installDate.Length == 8)
                    {
                        if (DateTime.TryParseExact(installDate, "yyyyMMdd", null,
                            System.Globalization.DateTimeStyles.None, out var date))
                        {
                            app.InstallDate = date;
                        }
                    }

                    applications.Add(app);
                }
                catch (Exception)
                {
                    // Skip applications we can't read
                }
            }
        }
        catch (Exception)
        {
            // Registry access may fail
        }
    }

    private static void MatchSignature(DiscoveredApplication app)
    {
        foreach (var sig in _signatures)
        {
            // Match by executable pattern
            if (!string.IsNullOrEmpty(app.ExecutablePath))
            {
                foreach (var pattern in sig.ExecutablePatterns)
                {
                    if (app.ExecutablePath.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        app.IsKnownApplication = true;
                        app.KnownApplicationId = sig.Id;
                        app.SuggestedRules = sig.DefaultRules.ToList();
                        return;
                    }
                }
            }

            // Match by name
            if (app.Name.Contains(sig.Name, StringComparison.OrdinalIgnoreCase))
            {
                app.IsKnownApplication = true;
                app.KnownApplicationId = sig.Id;
                app.SuggestedRules = sig.DefaultRules.ToList();
                return;
            }
        }
    }

    private static List<ApplicationSignature> InitializeSignatures()
    {
        return new List<ApplicationSignature>
        {
            // Web Browsers
            new ApplicationSignature
            {
                Id = "chrome",
                Name = "Google Chrome",
                Publisher = "Google",
                Category = "Web Browser",
                ExecutablePatterns = new List<string> { "chrome.exe" },
                DefaultRules = new List<SuggestedRule>
                {
                    new SuggestedRule
                    {
                        Id = "chrome-https",
                        Description = "Allow Chrome HTTPS",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "tcp",
                        RemotePorts = new List<int> { 443 },
                        Priority = SuggestionPriority.High
                    },
                    new SuggestedRule
                    {
                        Id = "chrome-http",
                        Description = "Allow Chrome HTTP",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "tcp",
                        RemotePorts = new List<int> { 80 },
                        Priority = SuggestionPriority.Normal
                    }
                }
            },
            new ApplicationSignature
            {
                Id = "firefox",
                Name = "Mozilla Firefox",
                Publisher = "Mozilla",
                Category = "Web Browser",
                ExecutablePatterns = new List<string> { "firefox.exe" },
                DefaultRules = new List<SuggestedRule>
                {
                    new SuggestedRule
                    {
                        Id = "firefox-https",
                        Description = "Allow Firefox HTTPS",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "tcp",
                        RemotePorts = new List<int> { 443 },
                        Priority = SuggestionPriority.High
                    },
                    new SuggestedRule
                    {
                        Id = "firefox-http",
                        Description = "Allow Firefox HTTP",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "tcp",
                        RemotePorts = new List<int> { 80 },
                        Priority = SuggestionPriority.Normal
                    }
                }
            },
            new ApplicationSignature
            {
                Id = "edge",
                Name = "Microsoft Edge",
                Publisher = "Microsoft",
                Category = "Web Browser",
                ExecutablePatterns = new List<string> { "msedge.exe" },
                DefaultRules = new List<SuggestedRule>
                {
                    new SuggestedRule
                    {
                        Id = "edge-https",
                        Description = "Allow Edge HTTPS",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "tcp",
                        RemotePorts = new List<int> { 443 },
                        Priority = SuggestionPriority.High
                    },
                    new SuggestedRule
                    {
                        Id = "edge-http",
                        Description = "Allow Edge HTTP",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "tcp",
                        RemotePorts = new List<int> { 80 },
                        Priority = SuggestionPriority.Normal
                    }
                }
            },

            // Communication
            new ApplicationSignature
            {
                Id = "discord",
                Name = "Discord",
                Publisher = "Discord Inc.",
                Category = "Communication",
                ExecutablePatterns = new List<string> { "Discord.exe", "discord\\app-" },
                DefaultRules = new List<SuggestedRule>
                {
                    new SuggestedRule
                    {
                        Id = "discord-api",
                        Description = "Allow Discord API",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "tcp",
                        RemotePorts = new List<int> { 443 },
                        Priority = SuggestionPriority.High
                    },
                    new SuggestedRule
                    {
                        Id = "discord-voice",
                        Description = "Allow Discord Voice",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "udp",
                        Comment = "Voice communication uses UDP on various ports",
                        Priority = SuggestionPriority.High
                    }
                }
            },
            new ApplicationSignature
            {
                Id = "slack",
                Name = "Slack",
                Publisher = "Slack Technologies",
                Category = "Communication",
                ExecutablePatterns = new List<string> { "slack.exe" },
                DefaultRules = new List<SuggestedRule>
                {
                    new SuggestedRule
                    {
                        Id = "slack-https",
                        Description = "Allow Slack HTTPS",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "tcp",
                        RemotePorts = new List<int> { 443 },
                        Priority = SuggestionPriority.High
                    }
                }
            },
            new ApplicationSignature
            {
                Id = "teams",
                Name = "Microsoft Teams",
                Publisher = "Microsoft",
                Category = "Communication",
                ExecutablePatterns = new List<string> { "Teams.exe", "ms-teams.exe" },
                DefaultRules = new List<SuggestedRule>
                {
                    new SuggestedRule
                    {
                        Id = "teams-https",
                        Description = "Allow Teams HTTPS",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "tcp",
                        RemotePorts = new List<int> { 443 },
                        Priority = SuggestionPriority.High
                    },
                    new SuggestedRule
                    {
                        Id = "teams-media",
                        Description = "Allow Teams Media",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "udp",
                        Comment = "Audio/Video uses UDP",
                        Priority = SuggestionPriority.High
                    }
                }
            },
            new ApplicationSignature
            {
                Id = "zoom",
                Name = "Zoom",
                Publisher = "Zoom Video Communications",
                Category = "Communication",
                ExecutablePatterns = new List<string> { "Zoom.exe" },
                DefaultRules = new List<SuggestedRule>
                {
                    new SuggestedRule
                    {
                        Id = "zoom-https",
                        Description = "Allow Zoom HTTPS",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "tcp",
                        RemotePorts = new List<int> { 443 },
                        Priority = SuggestionPriority.High
                    },
                    new SuggestedRule
                    {
                        Id = "zoom-media",
                        Description = "Allow Zoom Media",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "udp",
                        Comment = "Audio/Video uses UDP",
                        Priority = SuggestionPriority.High
                    }
                }
            },

            // Development
            new ApplicationSignature
            {
                Id = "vscode",
                Name = "Visual Studio Code",
                Publisher = "Microsoft",
                Category = "Development",
                ExecutablePatterns = new List<string> { "Code.exe" },
                DefaultRules = new List<SuggestedRule>
                {
                    new SuggestedRule
                    {
                        Id = "vscode-https",
                        Description = "Allow VS Code HTTPS (extensions, updates)",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "tcp",
                        RemotePorts = new List<int> { 443 },
                        Priority = SuggestionPriority.Normal
                    }
                }
            },
            new ApplicationSignature
            {
                Id = "git",
                Name = "Git",
                Publisher = "Git",
                Category = "Development",
                ExecutablePatterns = new List<string> { "git.exe", "git-remote-https.exe" },
                DefaultRules = new List<SuggestedRule>
                {
                    new SuggestedRule
                    {
                        Id = "git-https",
                        Description = "Allow Git HTTPS",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "tcp",
                        RemotePorts = new List<int> { 443 },
                        Priority = SuggestionPriority.High
                    },
                    new SuggestedRule
                    {
                        Id = "git-ssh",
                        Description = "Allow Git SSH",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "tcp",
                        RemotePorts = new List<int> { 22 },
                        Priority = SuggestionPriority.Normal
                    }
                }
            },
            new ApplicationSignature
            {
                Id = "docker",
                Name = "Docker Desktop",
                Publisher = "Docker Inc.",
                Category = "Development",
                ExecutablePatterns = new List<string> { "Docker Desktop.exe", "dockerd.exe" },
                DefaultRules = new List<SuggestedRule>
                {
                    new SuggestedRule
                    {
                        Id = "docker-registry",
                        Description = "Allow Docker Registry (HTTPS)",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "tcp",
                        RemotePorts = new List<int> { 443 },
                        Priority = SuggestionPriority.High
                    }
                }
            },

            // Cloud Storage
            new ApplicationSignature
            {
                Id = "dropbox",
                Name = "Dropbox",
                Publisher = "Dropbox, Inc.",
                Category = "Cloud Storage",
                ExecutablePatterns = new List<string> { "Dropbox.exe" },
                DefaultRules = new List<SuggestedRule>
                {
                    new SuggestedRule
                    {
                        Id = "dropbox-sync",
                        Description = "Allow Dropbox Sync",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "tcp",
                        RemotePorts = new List<int> { 443 },
                        Priority = SuggestionPriority.High
                    }
                }
            },
            new ApplicationSignature
            {
                Id = "onedrive",
                Name = "Microsoft OneDrive",
                Publisher = "Microsoft",
                Category = "Cloud Storage",
                ExecutablePatterns = new List<string> { "OneDrive.exe" },
                DefaultRules = new List<SuggestedRule>
                {
                    new SuggestedRule
                    {
                        Id = "onedrive-sync",
                        Description = "Allow OneDrive Sync",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "tcp",
                        RemotePorts = new List<int> { 443 },
                        Priority = SuggestionPriority.High
                    }
                }
            },
            new ApplicationSignature
            {
                Id = "googledrive",
                Name = "Google Drive",
                Publisher = "Google",
                Category = "Cloud Storage",
                ExecutablePatterns = new List<string> { "GoogleDriveFS.exe", "googledrivesync.exe" },
                DefaultRules = new List<SuggestedRule>
                {
                    new SuggestedRule
                    {
                        Id = "gdrive-sync",
                        Description = "Allow Google Drive Sync",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "tcp",
                        RemotePorts = new List<int> { 443 },
                        Priority = SuggestionPriority.High
                    }
                }
            },

            // Gaming
            new ApplicationSignature
            {
                Id = "steam",
                Name = "Steam",
                Publisher = "Valve",
                Category = "Gaming",
                ExecutablePatterns = new List<string> { "steam.exe", "steamwebhelper.exe" },
                DefaultRules = new List<SuggestedRule>
                {
                    new SuggestedRule
                    {
                        Id = "steam-store",
                        Description = "Allow Steam Store/API",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "tcp",
                        RemotePorts = new List<int> { 443, 80 },
                        Priority = SuggestionPriority.High
                    },
                    new SuggestedRule
                    {
                        Id = "steam-content",
                        Description = "Allow Steam Content Download",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Comment = "Content servers use various ports",
                        Priority = SuggestionPriority.Normal
                    }
                }
            },

            // Utilities
            new ApplicationSignature
            {
                Id = "spotify",
                Name = "Spotify",
                Publisher = "Spotify AB",
                Category = "Media",
                ExecutablePatterns = new List<string> { "Spotify.exe" },
                DefaultRules = new List<SuggestedRule>
                {
                    new SuggestedRule
                    {
                        Id = "spotify-stream",
                        Description = "Allow Spotify Streaming",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "tcp",
                        RemotePorts = new List<int> { 443, 80 },
                        Priority = SuggestionPriority.High
                    }
                }
            },
            new ApplicationSignature
            {
                Id = "vlc",
                Name = "VLC media player",
                Publisher = "VideoLAN",
                Category = "Media",
                ExecutablePatterns = new List<string> { "vlc.exe" },
                DefaultRules = new List<SuggestedRule>
                {
                    new SuggestedRule
                    {
                        Id = "vlc-stream",
                        Description = "Allow VLC Network Streams",
                        Action = "allow",
                        Direction = "outbound",
                        Protocol = "tcp",
                        Comment = "For network streaming (optional)",
                        Priority = SuggestionPriority.Low
                    }
                }
            }
        };
    }
}
