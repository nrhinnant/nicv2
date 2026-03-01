using System.Runtime.InteropServices;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace WfpTrafficControl.UI.Services;

/// <summary>
/// System tray icon service using Windows Forms NotifyIcon.
/// Provides status visibility, quick actions, and notifications.
/// </summary>
public sealed class TrayIconService : ITrayIconService
{
    private WinForms.NotifyIcon? _notifyIcon;
    private WinForms.ContextMenuStrip? _contextMenu;
    private WinForms.ToolStripMenuItem? _statusItem;
    private WinForms.ToolStripMenuItem? _filterCountItem;
    private bool _disposed;

    /// <inheritdoc />
    public bool IsVisible => _notifyIcon?.Visible ?? false;

    /// <inheritdoc />
    public TrayIconState CurrentState { get; private set; } = TrayIconState.Disconnected;

    /// <inheritdoc />
    public event EventHandler? ShowWindowRequested;

    /// <inheritdoc />
    public event EventHandler? ExitRequested;

    /// <inheritdoc />
    public event EventHandler? RefreshRequested;

    /// <inheritdoc />
    public void Initialize()
    {
        if (_notifyIcon != null)
            return;

        _contextMenu = CreateContextMenu();

        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = CreateIcon(TrayIconState.Disconnected),
            Text = "WFP Traffic Control - Connecting...",
            Visible = true,
            ContextMenuStrip = _contextMenu
        };

        _notifyIcon.DoubleClick += OnNotifyIconDoubleClick;
    }

    /// <inheritdoc />
    public void UpdateState(bool isConnected, int filterCount, string serviceVersion)
    {
        if (_notifyIcon == null)
            return;

        TrayIconState newState;
        string tooltipText;

        if (!isConnected)
        {
            newState = TrayIconState.Disconnected;
            tooltipText = "WFP Traffic Control - Offline";
        }
        else if (filterCount == 0)
        {
            newState = TrayIconState.NoPolicy;
            tooltipText = $"WFP Traffic Control - No Policy\nService v{serviceVersion}";
        }
        else
        {
            newState = TrayIconState.Active;
            tooltipText = $"WFP Traffic Control - Active\n{filterCount} filter(s)\nService v{serviceVersion}";
        }

        // Update icon if state changed
        if (newState != CurrentState)
        {
            CurrentState = newState;
            _notifyIcon.Icon?.Dispose();
            _notifyIcon.Icon = CreateIcon(newState);
        }

        // NotifyIcon.Text has a 63 character limit
        _notifyIcon.Text = tooltipText.Length > 63 ? tooltipText[..63] : tooltipText;

        // Update context menu status items
        UpdateContextMenuStatus(isConnected, filterCount, serviceVersion);
    }

    /// <inheritdoc />
    public void ShowNotification(string title, string message, bool isError = false)
    {
        _notifyIcon?.ShowBalloonTip(
            3000,
            title,
            message,
            isError ? WinForms.ToolTipIcon.Error : WinForms.ToolTipIcon.Info);
    }

    /// <inheritdoc />
    public void Hide()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.DoubleClick -= OnNotifyIconDoubleClick;
            _notifyIcon.Icon?.Dispose();
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        // Dispose menu items explicitly (they are IDisposable)
        _statusItem?.Dispose();
        _statusItem = null;
        _filterCountItem?.Dispose();
        _filterCountItem = null;

        _contextMenu?.Dispose();
        _contextMenu = null;
    }

    private WinForms.ContextMenuStrip CreateContextMenu()
    {
        var menu = new WinForms.ContextMenuStrip();

        // Status header (disabled, just for display)
        _statusItem = new WinForms.ToolStripMenuItem("Status: Checking...")
        {
            Enabled = false,
            Font = new Drawing.Font(menu.Font, Drawing.FontStyle.Bold)
        };
        menu.Items.Add(_statusItem);

        // Filter count
        _filterCountItem = new WinForms.ToolStripMenuItem("Filters: 0")
        {
            Enabled = false
        };
        menu.Items.Add(_filterCountItem);

        menu.Items.Add(new WinForms.ToolStripSeparator());

        // Show Window
        var showItem = new WinForms.ToolStripMenuItem("Show Window");
        showItem.Click += (_, _) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(showItem);

        // Refresh
        var refreshItem = new WinForms.ToolStripMenuItem("Refresh Status");
        refreshItem.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(refreshItem);

        menu.Items.Add(new WinForms.ToolStripSeparator());

        // Exit
        var exitItem = new WinForms.ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(exitItem);

        return menu;
    }

    private void UpdateContextMenuStatus(bool isConnected, int filterCount, string serviceVersion)
    {
        if (_statusItem != null)
        {
            _statusItem.Text = isConnected
                ? $"Status: Connected (v{serviceVersion})"
                : "Status: Offline";
        }

        if (_filterCountItem != null)
        {
            _filterCountItem.Text = $"Filters: {filterCount}";
        }
    }

    private void OnNotifyIconDoubleClick(object? sender, EventArgs e)
    {
        ShowWindowRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Creates a simple colored icon for the given state.
    /// Icons are created programmatically to avoid external dependencies.
    /// </summary>
    private static Drawing.Icon CreateIcon(TrayIconState state)
    {
        var color = state switch
        {
            TrayIconState.Active => Drawing.Color.FromArgb(76, 175, 80),      // Green
            TrayIconState.NoPolicy => Drawing.Color.FromArgb(255, 193, 7),    // Amber/Yellow
            TrayIconState.Disconnected => Drawing.Color.FromArgb(158, 158, 158), // Gray
            TrayIconState.Error => Drawing.Color.FromArgb(244, 67, 54),       // Red
            _ => Drawing.Color.Gray
        };

        return CreateCircleIcon(color, 16);
    }

    /// <summary>
    /// Creates a circular icon with the specified color.
    /// </summary>
    private static Drawing.Icon CreateCircleIcon(Drawing.Color fillColor, int size)
    {
        using var bitmap = new Drawing.Bitmap(size, size);
        using var graphics = Drawing.Graphics.FromImage(bitmap);

        // Enable anti-aliasing for smooth circle
        graphics.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // Draw filled circle
        using var brush = new Drawing.SolidBrush(fillColor);
        graphics.FillEllipse(brush, 1, 1, size - 2, size - 2);

        // Draw border for better visibility
        using var pen = new Drawing.Pen(Drawing.Color.FromArgb(100, 0, 0, 0), 1);
        graphics.DrawEllipse(pen, 1, 1, size - 3, size - 3);

        // Convert bitmap to icon - we must properly manage the GDI handle
        var hIcon = bitmap.GetHicon();
        try
        {
            // Create icon from handle
            using var tempIcon = Drawing.Icon.FromHandle(hIcon);
            // Clone it so we own the copy (Icon.FromHandle doesn't take ownership)
            return (Drawing.Icon)tempIcon.Clone();
        }
        finally
        {
            // Destroy the original handle to prevent GDI leak
            _ = DestroyIcon(hIcon);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
