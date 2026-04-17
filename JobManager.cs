namespace SoftwareEngineeringProject;

public sealed class JobManager
{
    private static JobManager _instance = null;
    private static readonly object _lock = new object();
    
    // Liste des jobs (limitation à 5 dans la logique)
    private List<BackupJob> _jobs = new List<BackupJob>();

    private JobManager() { }

    public static JobManager GetInstance()
    {
        lock (_lock)
        {
            if (_instance == null) _instance = new JobManager();
            return _instance;
        }
    }

    public void AddJob(string name, string src, string dest, string type)
    {
        if (_jobs.Count < 5)
        {
            // On utilise la Factory pour obtenir la stratégie
            var strategy = BackupJobFactory.CreateStrategy(type);
            _jobs.Add(new BackupJob(name, src, dest, strategy));
        }
    }

    public void ExecuteJob(int index)
    {
        if (index >= 0 && index < _jobs.Count)
        {
            _jobs[index].Execute();
        }
    }
}