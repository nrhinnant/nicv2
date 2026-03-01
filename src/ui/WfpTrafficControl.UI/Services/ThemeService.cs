using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace WfpTrafficControl.UI.Services;

/// <summary>
/// Service for managing application theme with system theme detection.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string RegistryValueName = "AppsUseLightTheme";

    private readonly Application _application;
    private ThemeMode _currentMode = ThemeMode.System;
    private bool _isDarkTheme;
    private bool _isInitialized;

    /// <inheritdoc />
    public ThemeMode CurrentMode => _currentMode;

    /// <inheritdoc />
    public bool IsDarkTheme => _isDarkTheme;

    /// <inheritdoc />
    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeService"/> class.
    /// </summary>
    public ThemeService()
    {
        _application = Application.Current;
    }

    /// <summary>
    /// Initializes the theme service with a saved preference or system default.
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        // Load saved preference
        var savedMode = LoadThemePreference();
        ApplyTheme(savedMode);
        _isInitialized = true;

        // Subscribe to system theme changes
        SystemEvents.UserPreferenceChanged += OnSystemThemeChanged;
    }

    /// <inheritdoc />
    public void ApplyTheme(ThemeMode mode)
    {
        _currentMode = mode;

        // Determine actual theme based on mode
        bool useDark = mode switch
        {
            ThemeMode.Light => false,
            ThemeMode.Dark => true,
            ThemeMode.System => DetectSystemDarkMode(),
            _ => false
        };

        if (_isInitialized && useDark == _isDarkTheme)
        {
            // No change needed
            return;
        }

        _isDarkTheme = useDark;

        // Apply the theme
        ApplyThemeResources(useDark);

        // Save preference
        SaveThemePreference(mode);

        // Raise event
        ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(useDark));
    }

    /// <inheritdoc />
    public void ToggleTheme()
    {
        // When toggling, switch to explicit light/dark mode
        var newMode = _isDarkTheme ? ThemeMode.Light : ThemeMode.Dark;
        ApplyTheme(newMode);
    }

    /// <inheritdoc />
    public bool DetectSystemDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            if (key?.GetValue(RegistryValueName) is int value)
            {
                // 0 = dark mode, 1 = light mode
                return value == 0;
            }
        }
        catch
        {
            // If we can't read the registry, default to light theme
        }

        return false;
    }

    /// <summary>
    /// Cleans up resources.
    /// </summary>
    public void Cleanup()
    {
        SystemEvents.UserPreferenceChanged -= OnSystemThemeChanged;
    }

    private void ApplyThemeResources(bool isDark)
    {
        var themePath = isDark
            ? "Themes/DarkTheme.xaml"
            : "Themes/LightTheme.xaml";

        var themeUri = new Uri(themePath, UriKind.Relative);
        var themeDictionary = new ResourceDictionary { Source = themeUri };

        // Find and replace the theme dictionary
        var mergedDictionaries = _application.Resources.MergedDictionaries;

        // Remove existing theme dictionary (first one is always the theme)
        ResourceDictionary? existingTheme = null;
        foreach (var dict in mergedDictionaries)
        {
            if (dict.Source?.OriginalString.Contains("Theme") == true)
            {
                existingTheme = dict;
                break;
            }
        }

        if (existingTheme != null)
        {
            mergedDictionaries.Remove(existingTheme);
        }

        // Insert the new theme at the beginning (before Styles.xaml)
        mergedDictionaries.Insert(0, themeDictionary);
    }

    private void OnSystemThemeChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General && _currentMode == ThemeMode.System)
        {
            // Re-apply system theme
            _application.Dispatcher.Invoke(() => ApplyTheme(ThemeMode.System));
        }
    }

    private static ThemeMode LoadThemePreference()
    {
        try
        {
            var settingsPath = GetSettingsFilePath();
            if (File.Exists(settingsPath))
            {
                var content = File.ReadAllText(settingsPath);
                if (Enum.TryParse<ThemeMode>(content.Trim(), out var mode))
                {
                    return mode;
                }
            }
        }
        catch
        {
            // If we can't read settings, use system default
        }

        return ThemeMode.System;
    }

    private static void SaveThemePreference(ThemeMode mode)
    {
        try
        {
            var settingsPath = GetSettingsFilePath();
            var directory = Path.GetDirectoryName(settingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(settingsPath, mode.ToString());
        }
        catch
        {
            // If we can't save settings, silently continue
        }
    }

    private static string GetSettingsFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "WfpTrafficControl", "theme.txt");
    }
}
