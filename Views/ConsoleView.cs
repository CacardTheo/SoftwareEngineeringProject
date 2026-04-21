using System;
using System.Collections.Generic;
using SoftwareEngineeringProject.BusinessLogic;

public class ConsoleView
{
    private readonly BackupManager _manager;
    private readonly CommandParser _parser;

    public ConsoleView()
    {
        _manager = new BackupManager();
        _parser = new CommandParser();
    }

    public void Run(string[] args)
    {
        while (true)
        {
            DisplayMenu();
            HandleUserInput();
        }
    }

    private void DisplayMenu()
    {
        Console.WriteLine("\n--- EasySave Console ---");
        Console.WriteLine("1. List Jobs");
        Console.WriteLine("2. Execute Jobs (ex: 1;3 or 1-5)");
        Console.WriteLine("3. Exit");
    }

    private void HandleUserInput()
    {
        string choice = Console.ReadLine() ?? "";
        switch (choice)
        {
            case "1":
                DisplayJobs();
                break;
            case "2":
                Console.Write("Enter indices: ");
                var indices = _parser.Parse(Console.ReadLine() ?? "");
                _manager.ExecuteJob(indices);
                break;
            case "3":
                Environment.Exit(0);
                break;
        }
    }

    private void DisplayJobs()
    {
        var jobs = _manager.GetJobs();
        for (int i = 0; i < jobs.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {jobs[i].Name} [{jobs[i].Type}]");
        }
    }
}