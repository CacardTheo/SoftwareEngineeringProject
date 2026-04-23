using System.Collections.Concurrent;
using System.Security.Authentication.ExtendedProtection;

namespace SoftwareEngineeringProject.ViewModels
{
    public class MainViewModel
    {
        private readonly LanguageManager _languageManager;
        private BackupProcessor _backupProcessor;

        private List<BackupJob> _jobs = new List<BackupJob>();
        
        public MainViewModel()
        {
            _languageManager = LanguageManager.GetInstance();
            _backupProcessor = new BackupProcessor(new StateManager());
        }

        public string GetText(string key)
        {
            // Use the singleton instance retrieved at initialization
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
                return false; // Returns false if language change fails
            }
        }

        public bool CreateJob(string name, string source, string target, string type)
        {
            // Can't have more than 5 jobs, and name must be unique
            if (_jobs.Count >= 5) return false;
            if (_jobs.Any(j => j.Name == name)) return false;
            _jobs.Add(new BackupJob
            {
                Name = name,
                SourceDir = source,
                TargetDir = target,
                Type = type.Equals("Full", StringComparison.OrdinalIgnoreCase) ? BackupType.Full : BackupType.Differential
            });
            return true; // Returns true if job creation is successful
        }

        public bool DeleteJob(int index)
        {
            // Logic to delete a backup job based on the provided index
            if (index >= 0 && index < _jobs.Count)
            {
                _jobs.RemoveAt(index);
                return true; // Returns true if deletion is successful
            }
            return false; // Returns false if index is out of range
        }

        public bool RunJob(string input)
        {
            if (_jobs.Count == 0) return false;
            List<int> indicesToRun = ParseIndices(input, _jobs.Count);

            foreach (int index in indicesToRun)
            {
                var job = _jobs[index];

                _backupProcessor.Execute(job);
            }
            return true;
        }

        public List<BackupJob> GetJobs() {
            return _jobs; // Returns the list of backup jobs
        }

        private List<int> ParseIndices(string input, int maxCount)
        {
            var indices = new HashSet<int>(); // Hasset to avoid duplicates

            // Management of range "1-3"
            if (input.Contains('-'))
            {
                var parts = input.Split('-');
                if (parts.Length == 2 && int.TryParse(parts[0], out int start) && int.TryParse(parts[1], out int end))
                {
                    // We do -1 because the user inputs are 1-based, but our list is 0-based
                    for (int i = start; i <= end; i++)
                    {
                        if (i > 0 && i <= maxCount) indices.Add(i - 1);
                    }
                }
            }
            // Management of multiple indices "1;3;5"
            else if (input.Contains(';'))
            {
                var parts = input.Split(';');
                foreach (var part in parts)
                {
                    if (int.TryParse(part, out int id) && id > 0 && id <= maxCount)
                    {
                        indices.Add(id - 1);
                    }
                }
            }
            // Management of single index "1"
            else
            {
                if (int.TryParse(input, out int id) && id > 0 && id <= maxCount)
                {
                    indices.Add(id - 1);
                }
            }

            return indices.OrderBy(i => i).ToList();
        }
    }
}