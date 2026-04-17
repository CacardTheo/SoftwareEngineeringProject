using SoftwareEngineeringProject;

public interface IBackupStrategy
    {
        void Copy(string SourceDirectory, string TargetDirectory);
    }