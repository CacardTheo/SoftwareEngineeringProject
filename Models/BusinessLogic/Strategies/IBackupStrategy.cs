using SoftwareEngineeringProject;

public interface IBackupStrategy
    {
        void Execute(string SourceDirectory, string TargetDirectory);
    }