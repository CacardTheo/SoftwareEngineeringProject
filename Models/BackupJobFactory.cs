using SoftwareEngineeringProject;

public static class BackupJobFactory
    {
        public static IBackupStrategy CreateStrategy(string type)
        {
            return type.ToLower() switch
            {
                "full" => new FullBackupStrategy(),
                "differential" => new DifferentialBackupStrategy(),
                _ => throw new ArgumentException("Invalid backup type. Use 'full' or 'differential'.")
            }; 
        }
    }