using System.Text.Json;

namespace EasySave.Views.Utils
{
    public class LanguageManager
    {
        private Dictionary<string, string> _strings = new();
        public string CurrentLanguage { get; private set; }

        public LanguageManager(string defaultLang = "en")
        {
            SetLanguage(defaultLang);
        }

        public void SetLanguage(string lang)
        {
            CurrentLanguage = lang;
            // Chemin dynamique pour trouver les JSON dans le dossier d'exécution
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Languages", $"{lang}.json");

            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                _strings = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            }
        }

        public string GetString(string key)
        {
            return _strings.GetValueOrDefault(key, $"Missing_{key}");
        }
    }
}