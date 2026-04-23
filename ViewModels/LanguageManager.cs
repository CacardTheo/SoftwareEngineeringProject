using System.Text.Json;

namespace SoftwareEngineeringProject.ViewModels
{
    public class LanguageManager
    {
        private static LanguageManager _instance;
        private Dictionary<string, string> _translations;
        private string _currentLanguage = "en";

        private LanguageManager() { LoadTranslations(); }

        public static LanguageManager GetInstance()
        {
            if (_instance == null) _instance = new LanguageManager();
            return _instance;
        }

        public void SetLanguage(string lang)
        {
            _currentLanguage = lang;
            LoadTranslations();
        }

        private void LoadTranslations()
        {
            try
            {
                // Get the path where the application is actually running
                string baseDir = AppContext.BaseDirectory;
                string path = Path.Combine(baseDir, "Resources", "Languages", $"{_currentLanguage}.json");

                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    _translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                }
                else
                {
                    // If still not found, try the classic relative path
                    string fallbackPath = Path.Combine("Resources", "Languages", $"{_currentLanguage}.json");
                    if (File.Exists(fallbackPath))
                    {
                        _translations = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(fallbackPath));
                    }
                    else
                    {
                        _translations = new Dictionary<string, string>();
                    }
                }
            }
            catch
            {
                _translations = new Dictionary<string, string>();
            }
        }
        public string GetText(string key) => _translations.ContainsKey(key) ? _translations[key] : key;
    }
}