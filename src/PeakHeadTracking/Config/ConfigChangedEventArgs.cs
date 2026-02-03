using System;

namespace PeakHeadTracking.Config
{
    /// <summary>
    /// Configuration change event arguments
    /// </summary>
    public class ConfigChangedEventArgs : EventArgs
    {
        public string SettingName { get; set; }
        public object OldValue { get; set; }
        public object NewValue { get; set; }
        public string Category { get; set; }
    }
}
