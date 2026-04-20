using System.Collections.Generic;

namespace EasySave.Localization
{
    public class EnLanguage : ILanguage
    {
        private readonly Dictionary<string, string> _texts = new()
        {
            { LanguageKeys.MENU_TITLE, "Main Menu" },
            { LanguageKeys.START_BACKUP, "Start Backup" },
            { LanguageKeys.EXIT, "Exit" },
            { LanguageKeys.ENTER_CHOICE, "Enter your choice:" }
            { LanguageKeys.APP_HELP, "EasySave v1.0 - Use arguments: 1-3 or 1;3" },
            { LanguageKeys.NO_JOB, "No valid job index provided." },
            { LanguageKeys.START_EXECUTION, "Starting execution for jobs: " }
        };

        public string GetText(string key)
        {
            return _texts.TryGetValue(key, out var value) ? value : key;
        }
    }
}