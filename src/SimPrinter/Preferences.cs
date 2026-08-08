using System.Text.Json;

namespace SimPrinter
{
    public class Preferences
    {
        public string SimBriefId { get; set; } = "";
        public bool UseSerial { get; set; } = true;
        public string? ComPort { get; set; }
        public int BaudRate { get; set; } = 9600;
        public string? PrinterName { get; set; }
        public bool RandomizeFinalLoadsheet { get; set; } = false;
        public string SiApiKey { get; set; } = "";
        public bool DarkMode { get; set; } = false;
        public bool UseVatsimWeather { get; set; } = false;
        public bool EnableBrowserPrintServer { get; set; } = false;
        public bool AutoPrintOoOiReport { get; set; } = false;

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SimPrinter", "preferences.json");

        public static Preferences Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    var prefs = JsonSerializer.Deserialize<Preferences>(json);
                    if (prefs != null) return prefs;
                }
            }
            catch
            {
                // Corrupt or unreadable preferences file: fall back to defaults instead of crashing startup.
            }
            return new Preferences();
        }

        public void Save()
        {
            string dir = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(dir);
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
    }
}
