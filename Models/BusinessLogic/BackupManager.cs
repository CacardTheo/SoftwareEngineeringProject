using SoftwareEngineeringProject;

namespace SoftwareEngineeringProject.BusinessLogic;

public sealed class BackupManager
{
    private List<BackupJob> _jobs;
    private readonly ConfigManager _configManager;
    
    public BackupManager()
    {
        _configManager = new ConfigManager();
        _jobs = _configManager.LoadJobs();
    }

    public void CreateJob(BackupJob job)
    {
        if (_jobs.Count >= 5) throw new Exception("Maximum limit of 5 jobs reached.");
        _jobs.Add(job);
        _configManager.SaveJobs(_jobs);
    }

    public void ExecuteJob(List<int> indices)
    {
        foreach (int index in indices)
        {
            if (index >= 0 && index < _jobs.Count)
            {
                var job = _jobs[index];
                IBackupStrategy strategy = SelectStrategy(job.Type);
            }
        }
    }

    public List<BackupJob> GetJobs() => _jobs;

    private IBackupStrategy SelectStrategy(BackupType type) => type switch
    {
        BackupType.Full => new FullBackupStrategy(),
        BackupType.Differential => new DifferentialBackupStrategy(),
        _ => throw new NotImplementedException()
    };

}