namespace WfpTrafficControl.UI.Services;

/// <summary>
/// Event arguments for theme change events.
/// </summary>
public sealed class ThemeChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets whether dark theme is now active.
    /// </summary>
    public bool IsDarkTheme { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeChangedEventArgs"/> class.
    /// </summary>
    /// <param name="isDarkTheme">Whether dark theme is now active.</param>
    public ThemeChangedEventArgs(bool isDarkTheme)
    {
        IsDarkTheme = isDarkTheme;
    }
}

/// <summary>
/// Theme mode options.
/// </summary>
public enum ThemeMode
{
    /// <summary>
    /// Follow system theme setting.
    /// </summary>
    System,

    /// <summary>
    /// Always use light theme.
    /// </summary>
    Light,

    /// <summary>
    /// Always use dark theme.
    /// </summary>
    Dark
}

/// <summary>
/// Service for managing application theme.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Gets the current theme mode setting.
    /// </summary>
    ThemeMode CurrentMode { get; }

    /// <summary>
    /// Gets whether dark theme is currently active (resolved from mode).
    /// </summary>
    bool IsDarkTheme { get; }

    /// <summary>
    /// Applies the specified theme mode.
    /// </summary>
    /// <param name="mode">Theme mode to apply.</param>
    void ApplyTheme(ThemeMode mode);

    /// <summary>
    /// Toggles between light and dark themes.
    /// If currently in System mode, switches to the opposite of current system theme.
    /// </summary>
    void ToggleTheme();

    /// <summary>
    /// Detects the current Windows system theme.
    /// </summary>
    /// <returns>True if system is using dark theme.</returns>
    bool DetectSystemDarkMode();

    /// <summary>
    /// Event raised when the theme changes.
    /// </summary>
    event EventHandler<ThemeChangedEventArgs>? ThemeChanged;
}
