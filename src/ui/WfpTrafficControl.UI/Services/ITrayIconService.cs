namespace WfpTrafficControl.UI.Services;

/// <summary>
/// Represents the visual state of the tray icon.
/// </summary>
public enum TrayIconState
{
    /// <summary>
    /// Service is offline or disconnected (gray icon).
    /// </summary>
    Disconnected,

    /// <summary>
    /// Service is connected but no policy is applied (yellow icon).
    /// </summary>
    NoPolicy,

    /// <summary>
    /// Service is connected and policy is active (green icon).
    /// </summary>
    Active,

    /// <summary>
    /// An error has occurred (red icon).
    /// </summary>
    Error
}

/// <summary>
/// Service for managing the system tray icon.
/// </summary>
public interface ITrayIconService : IDisposable
{
    /// <summary>
    /// Gets whether the tray icon is currently visible.
    /// </summary>
    bool IsVisible { get; }

    /// <summary>
    /// Gets the current state of the tray icon.
    /// </summary>
    TrayIconState CurrentState { get; }

    /// <summary>
    /// Initializes and shows the tray icon.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Updates the tray icon state based on service status.
    /// </summary>
    /// <param name="isConnected">Whether the service is connected.</param>
    /// <param name="filterCount">The number of active filters.</param>
    /// <param name="serviceVersion">The service version string.</param>
    void UpdateState(bool isConnected, int filterCount, string serviceVersion);

    /// <summary>
    /// Shows a balloon notification in the system tray.
    /// </summary>
    /// <param name="title">Notification title.</param>
    /// <param name="message">Notification message.</param>
    /// <param name="isError">Whether this is an error notification.</param>
    void ShowNotification(string title, string message, bool isError = false);

    /// <summary>
    /// Hides the tray icon.
    /// </summary>
    void Hide();

    /// <summary>
    /// Event raised when the user clicks "Show Window" from the context menu.
    /// </summary>
    event EventHandler? ShowWindowRequested;

    /// <summary>
    /// Event raised when the user clicks "Exit" from the context menu.
    /// </summary>
    event EventHandler? ExitRequested;

    /// <summary>
    /// Event raised when the user clicks "Refresh" from the context menu.
    /// </summary>
    event EventHandler? RefreshRequested;
}
