namespace RssReader.Services;

/// <summary>
/// Service for managing application theme (light/dark/auto)
/// </summary>
public class ThemeService
{
    private string _currentTheme = "auto";

    public event Action? OnThemeChanged;

    public bool IsInitialized { get; private set; }

    public string CurrentTheme
    {
        get => _currentTheme;
        private set
        {
            if (_currentTheme != value)
            {
                _currentTheme = value;
                OnThemeChanged?.Invoke();
            }
        }
    }

    /// <summary>
    /// Initialize the theme from stored value (called once on first load)
    /// </summary>
    public void Initialize(string theme)
    {
        if (!IsInitialized && theme is "light" or "dark" or "auto")
        {
            _currentTheme = theme;
            IsInitialized = true;
        }
    }

    public void SetTheme(string theme)
    {
        if (theme is "light" or "dark" or "auto")
        {
            CurrentTheme = theme;
            IsInitialized = true;
        }
    }
}
