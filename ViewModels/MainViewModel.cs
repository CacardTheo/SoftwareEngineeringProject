using System.Collections.Concurrent;
using System.Security.Authentication.ExtendedProtection;

namespace SoftwareEngineeringProject.ViewModels
{
    public class MainViewModel
    {
        private readonly LanguageManager _languageManager;

        public MainViewModel()
        {
            _languageManager = LanguageManager.GetInstance();
        }

        public string GetText(string key)
        {
            // On utilise l'instance unique récupérée au début
            return _languageManager.GetText(key);
        }

        public bool ChangeLanguage(string lang)
        {
            try
            {
                _languageManager.SetLanguage(lang);
                return true;
            }
            catch
            {
                return false; // Retourne false si la langue n'est pas chargée correctement
            }
        }

        public void CreateJob(string name, string source, string target, string type)
        {
            // Logique pour créer un job de sauvegarde
        }

        public void DeleteJob(int index)
        {
            // Logique pour supprimer un job de sauvegarde
        }

        public void ShowJobs()
        {
            // Logique pour afficher les jobs de sauvegarde
        }

        public void RunJob(string name)
        {
            // Logique pour exécuter un job de sauvegarde
        }

        public List<BackupJob> GetJobs() {
            return new List<BackupJob>(); // Retourne la liste des jobs de sauvegarde
        }
    }
}