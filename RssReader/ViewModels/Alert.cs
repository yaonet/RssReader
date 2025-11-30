namespace RssReader.ViewModels
{
    /// <summary>
    /// Represents an alert message to be displayed to the user.
    /// </summary>
    public class Alert
    {
        public string? Text { get; set; }
        public AlertType Type { get; set; }
        public bool IsVisible => !string.IsNullOrEmpty(Text);

        public static Alert Success(string message) => new()
        {
            Text = message,
            Type = AlertType.Success
        };

        public static Alert Error(string message) => new()
        {
            Text = message,
            Type = AlertType.Error
        };

        public static Alert Info(string message) => new()
        {
            Text = message,
            Type = AlertType.Info
        };

        public static Alert Warning(string message) => new()
        {
            Text = message,
            Type = AlertType.Warning
        };

        public void Clear()
        {
            Text = null;
        }

        public string GetCssClass() => Type switch
        {
            AlertType.Success => "alert-success",
            AlertType.Error => "alert-danger",
            AlertType.Warning => "alert-warning",
            AlertType.Info => "alert-info",
            _ => "alert-secondary"
        };

        public string GetIcon() => Type switch
        {
            AlertType.Success => "bi-check-circle-fill",
            AlertType.Error => "bi-exclamation-circle-fill",
            AlertType.Warning => "bi-exclamation-triangle-fill",
            AlertType.Info => "bi-info-circle-fill",
            _ => "bi-info-circle"
        };
    }

    public enum AlertType
    {
        Success,
        Error,
        Warning,
        Info
    }
}
