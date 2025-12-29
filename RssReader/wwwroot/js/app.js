// Theme management
const THEME_KEY = 'app-theme';

function applyTheme(theme) {
    const effectiveTheme = theme === 'auto'
        ? (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light')
        : theme;
    document.documentElement.setAttribute('data-bs-theme', effectiveTheme);
}

// Apply theme immediately and on every Blazor enhanced navigation
function ensureTheme() {
    const theme = localStorage.getItem(THEME_KEY) || 'auto';
    applyTheme(theme);
}

// Apply on initial load
ensureTheme();

// Listen for Blazor enhanced navigation
if (typeof Blazor !== 'undefined') {
    Blazor.addEventListener('enhancedload', ensureTheme);
} else {
    // If Blazor isn't loaded yet, wait for it
    document.addEventListener('DOMContentLoaded', function() {
        if (typeof Blazor !== 'undefined') {
            Blazor.addEventListener('enhancedload', ensureTheme);
        }
    });
}

// Also re-apply after Blazor reconnects (for SignalR reconnection scenarios)
document.addEventListener('blazor:reconnected', ensureTheme);

// Listen for system theme changes when in auto mode
window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
    const currentTheme = localStorage.getItem(THEME_KEY) || 'auto';
    if (currentTheme === 'auto') {
        applyTheme('auto');
    }
});

window.getStoredTheme = function () {
    return localStorage.getItem(THEME_KEY);
};

window.setStoredTheme = function (theme) {
    localStorage.setItem(THEME_KEY, theme);
};

window.getPreferredTheme = function () {
    const storedTheme = localStorage.getItem(THEME_KEY);
    if (storedTheme) {
        return storedTheme;
    }
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
};

window.setTheme = function (theme) {
    localStorage.setItem(THEME_KEY, theme);
    applyTheme(theme);
};

window.downloadFile = function (fileName, base64Content, contentType) {
    const byteCharacters = atob(base64Content);
    const byteNumbers = new Array(byteCharacters.length);
    for (let i = 0; i < byteCharacters.length; i++) {
        byteNumbers[i] = byteCharacters.charCodeAt(i);
    }
    const byteArray = new Uint8Array(byteNumbers);
    const blob = new Blob([byteArray], { type: contentType });
    
    const link = document.createElement('a');
    link.href = window.URL.createObjectURL(blob);
    link.download = fileName;
    link.click();
    
    window.URL.revokeObjectURL(link.href);
};
