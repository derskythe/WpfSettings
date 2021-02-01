using System;
using System.IO;
using Newtonsoft.Json;
using NLog;
using WpfSettings.Settings;

namespace WpfSettings
{
    public static class SettingsHelpers
    {
        // ReSharper disable FieldCanBeMadeReadOnly.Local
        // ReSharper disable InconsistentNaming
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        // ReSharper restore InconsistentNaming
        // ReSharper restore FieldCanBeMadeReadOnly.Local

        public static void Save(this ApplicationSettings value, string path)
        {
            try
            {
                var jsonObj = JsonConvert.SerializeObject(value, Formatting.Indented);
                File.WriteAllText(path, jsonObj);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error writing app settings {ex.Message}");
            }
        }
    }
}
