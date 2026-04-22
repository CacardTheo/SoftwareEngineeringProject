using System;
using SoftwareEngineeringProject.ViewModels;

namespace SoftwareEngineeringProject.Views
{
    public class ConsoleView
    {
        public void ShowMenu()
        {
            var configManager = new ConfigManager();
            var jobs = configManager.LoadJobs();

            while (true)
            {
                Console.WriteLine("\n--- EasySave v1.0 ---");
                Console.WriteLine("1. List jobs");
                Console.WriteLine("2. Run a job");
                Console.WriteLine("3. Exit");
                Console.Write("Choice: ");

                string choice = Console.ReadLine() ?? "";

                switch (choice)
                {
                    case "1":
                        for (int i = 0; i < jobs.Count; i++)
                        {
                            Console.WriteLine($"- ID: {i + 1} | Name: {jobs[i].Name} | Type: {jobs[i].Type}");
                        }
                        break;

                    case "2":
                        Console.Write("Enter Job ID: ");
                        if (int.TryParse(Console.ReadLine(), out int id) && id > 0 && id <= jobs.Count)
                        {
                            var job = jobs[id - 1];
                            var processor = new BackupProcessor();

                            IBackupStrategy strategy = job.Type == BackupType.Full
                                ? new FullBackupStrategy()
                                : new DifferentialBackupStrategy();

                            processor.Execute(job, strategy);
                            Console.WriteLine(">>> Backup successful!");
                        }
                        else
                        {
                            Console.WriteLine("Invalid ID.");
                        }
                        break;

                    case "3":
                        return;
                }
            }
        }
    }
}