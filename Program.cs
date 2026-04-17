using System;
using System.IO;
using SoftwareEngineeringProject; // Assure-toi que le namespace correspond au tien

class Program
{
    static void Main(string[] args)
    {
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string sourceDir = Path.Combine(desktopPath, "EasySave_Source");
        string targetDir = Path.Combine(desktopPath, "EasySave_Target");

        SetupTestEnvironment(sourceDir, targetDir);

        BackupEngine engine = new BackupEngine();

        Console.WriteLine("\n--- TEST 1 : FULL BACKUP ---");
        BackupJob fullJob = new BackupJob("Job_Full", sourceDir, targetDir, BackupType.Full);
        engine.Execute(fullJob);

        Console.WriteLine("\n--- TEST 2 : DIFFERENTIAL (No changes) ---");
        BackupJob diffJob = new BackupJob("Job_Diff", sourceDir, targetDir, BackupType.Differential);
        engine.Execute(diffJob);

        Console.WriteLine("\n--- TEST 3 : DIFFERENTIAL (After 1 modification) ---");
        File.AppendAllText(Path.Combine(sourceDir, "file1.txt"), " - Modification !");
        engine.Execute(diffJob);

        Console.WriteLine("\nTests terminés. Appuyez sur une touche pour quitter.");
        Console.ReadKey();
    }

    static void SetupTestEnvironment(string source, string target)
    {
        if (Directory.Exists(source)) Directory.Delete(source, true);
        if (Directory.Exists(target)) Directory.Delete(target, true);

        Directory.CreateDirectory(source);

        File.WriteAllText(Path.Combine(source, "file1.txt"), "Contenu initial du fichier 1");
        File.WriteAllText(Path.Combine(source, "file2.txt"), "Contenu initial du fichier 2");

        Console.WriteLine($"Environnement de test créé sur le Bureau.");
        Console.WriteLine($"Source: {source}");
        Console.WriteLine($"Target: {target}");
    }
}