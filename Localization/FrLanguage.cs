using System.Collections.Generic;

namespace EasySave.Localization
{
    public class FrLanguage : ILanguage
    {
        private readonly Dictionary<string, string> _texts = new()
        {
            { LanguageKeys.MENU_TITLE, "Menu Principal" },
            { LanguageKeys.START_BACKUP, "Démarrer une sauvegarde" },
            { LanguageKeys.EXIT, "Quitter" },
            { LanguageKeys.ENTER_CHOICE, "Entrez votre choix :" }
            { LanguageKeys.APP_HELP, "EasySave v1.0 - Utilisation: 1-3 ou 1;3" },
            { LanguageKeys.NO_JOB, "Aucun job valide fourni." },
            { LanguageKeys.START_EXECUTION, "Démarrage des jobs : " }
        };

        public string GetText(string key)
        {
            return _texts.TryGetValue(key, out var value) ? value : key;
        }
    }
}