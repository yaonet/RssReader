namespace RssReader.Models
{
    /// <summary>
    /// Represents application settings stored in the database
    /// </summary>
    public class AppSetting
    {
        public int Id { get; set; }
        
        /// <summary>
        /// Setting key (unique identifier)
        /// </summary>
        public string Key { get; set; } = string.Empty;
        
        /// <summary>
        /// Setting value
        /// </summary>
        public string Value { get; set; } = string.Empty;
        
        /// <summary>
        /// Description of the setting
        /// </summary>
        public string? Description { get; set; }
        
        /// <summary>
        /// Last updated timestamp
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
