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
        Console.Clear();
        Console.WriteLine(_langManager.GetString("Menu_Title"));
        Console.WriteLine(_langManager.GetString("Menu_Option_Run"));
        Console.WriteLine(_langManager.GetString("Menu_Option_Language"));
        Console.WriteLine(_langManager.GetString("Menu_Option_Quit"));
        Console.Write(_langManager.GetString("Selection_Prompt"));
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

    private void HandleLanguageSelection()
    {
        Console.WriteLine("Select Language: 1. English | 2. Français");
        var choice = Console.ReadLine();

        if (choice == "2") _langManager.SetLanguage("fr");
        else _langManager.SetLanguage("en");

        DisplayMenu();
    }
}