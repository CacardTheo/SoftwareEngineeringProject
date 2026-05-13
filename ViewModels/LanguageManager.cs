using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System;
using System.ComponentModel;

namespace EasySaveWpf.ViewModels
{
    public class LanguageManager : INotifyPropertyChanged
    {
        private static LanguageManager _instance;
        private static readonly object _lock = new object();

        private Dictionary<string, string> _translations;
        private string _currentLanguage = "en";

        public event PropertyChangedEventHandler PropertyChanged;

        public string this[string key] => GetText(key);

        public static LanguageManager Instance => GetInstance();

        private LanguageManager() { LoadTranslations(); }

        public static LanguageManager GetInstance()
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new LanguageManager();
                    }
                }
            }
            return _instance;
        }

        public void SetLanguage(string lang)
        {
            if (_currentLanguage != lang)
            {
                _currentLanguage = lang;
                LoadTranslations();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            }
        }

        public List<string> GetAvailableLanguages()
        {
            var languages = new List<string>();
            try
            {
                string baseDir = AppContext.BaseDirectory;
                string dirPath = Path.Combine(baseDir, "Resources", "Languages");

                if (Directory.Exists(dirPath))
                {
                    foreach (string file in Directory.GetFiles(dirPath, "*.json"))
                        languages.Add(Path.GetFileNameWithoutExtension(file));
                }
                else
                {
                    string fallbackDirPath = Path.Combine("Resources", "Languages");
                    if (Directory.Exists(fallbackDirPath))
                    {
                        foreach (string file in Directory.GetFiles(fallbackDirPath, "*.json"))
                            languages.Add(Path.GetFileNameWithoutExtension(file));
                    }
                }
            }
            catch { }

            if (languages.Count == 0)
                languages.Add("en");

            var unique = new List<string>();
            foreach (string lang in languages)
            {
                if (!unique.Contains(lang))
                    unique.Add(lang);
            }
            return unique;
        }

        private void LoadTranslations()
        {
            try
            {
                string baseDir = AppContext.BaseDirectory;
                string path = Path.Combine(baseDir, "Resources", "Languages", $"{_currentLanguage}.json");

                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    _translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                }
                else
                {
                    string fallbackPath = Path.Combine("Resources", "Languages", $"{_currentLanguage}.json");
                    if (File.Exists(fallbackPath))
                    {
                        string json = File.ReadAllText(fallbackPath);
                        _translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
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
        public string GetText(string key) => _translations != null && _translations.ContainsKey(key) ? _translations[key] : key;

        // Reads a key from a specific language file without changing the current language
        public string GetTextForLanguage(string lang, string key)
        {
            try
            {
                string baseDir = AppContext.BaseDirectory;
                string path = Path.Combine(baseDir, "Resources", "Languages", $"{lang}.json");

                if (!File.Exists(path))
                    path = Path.Combine("Resources", "Languages", $"{lang}.json");

                if (!File.Exists(path))
                    return key;

                string json = File.ReadAllText(path);
                Dictionary<string, string>? translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                if (translations != null && translations.ContainsKey(key))
                    return translations[key];
            }
            catch { }

            return key;
        }
    }
}