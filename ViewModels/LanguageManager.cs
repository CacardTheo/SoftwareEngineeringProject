using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System;

namespace EasySaveWpf.ViewModels
{
    public class LanguageManager
    {
        private static LanguageManager _instance;
        private static readonly object _lock = new object();

        private Dictionary<string, string> _translations;
        private string _currentLanguage = "en";

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
            _currentLanguage = lang;
            LoadTranslations();
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
                    var files = Directory.GetFiles(dirPath, "*.json");
                    languages.AddRange(files.Select(Path.GetFileNameWithoutExtension));
                }
                else
                {
                    string fallbackDirPath = Path.Combine("Resources", "Languages");
                    if (Directory.Exists(fallbackDirPath))
                    {
                        var files = Directory.GetFiles(fallbackDirPath, "*.json");
                        languages.AddRange(files.Select(Path.GetFileNameWithoutExtension));
                    }
                }
            }
            catch { }

            if (languages.Count == 0)
            {
                languages.Add("en");
            }

            return languages.Distinct().ToList();
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
    }
}