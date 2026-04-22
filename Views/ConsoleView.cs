using SoftwareEngineeringProject.ViewModels;

public class ConsoleView
{
    private readonly MainViewModel _viewModel;

    public ConsoleView()
    {
        _viewModel = new MainViewModel(); // Le ViewModel créera ses services (Processor, etc.)
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
        Console.WriteLine(_viewModel.GetText("menu_title"));
        Console.WriteLine(_viewModel.GetText("menu_change_lang"));
        Console.WriteLine(_viewModel.GetText("menu_create"));
        Console.WriteLine(_viewModel.GetText("menu_delete"));
        Console.WriteLine(_viewModel.GetText("menu_show"));
        Console.WriteLine(_viewModel.GetText("menu_run_one"));
        Console.WriteLine(_viewModel.GetText("menu_run_part"));
        Console.WriteLine(_viewModel.GetText("menu_run_all"));
        Console.WriteLine(_viewModel.GetText("menu_exit"));
        Console.Write(_viewModel.GetText("prompt_choice"));
    }

    private void HandleUserInput()
    {
        string choice = Console.ReadLine() ?? "";
        switch (choice)
        {
            case "1": // Changer de langue
                Console.Write("Language (en/fr): ");
                string lang = Console.ReadLine() ?? "en";
                _viewModel.ChangeLanguage(lang);
                break;

            case "2": // Créer un bJOB
                Console.Write("Name: ");
                string name = Console.ReadLine() ?? "";
                Console.Write("Source Path: ");
                string source = Console.ReadLine() ?? "";
                Console.Write("Target Path: ");
                string target = Console.ReadLine() ?? "";
                Console.Write("Type (Full/Differential): ");
                string type = Console.ReadLine() ?? "";
                _viewModel.CreateJob(name, source, target, type);
                break;

            case "3": // Supprimer un bJOB
                DisplayJobs(); // On affiche pour que l'user voie l'ID
                Console.Write("ID to delete: ");
                if (int.TryParse(Console.ReadLine(), out int idDel))
                    _viewModel.DeleteJob(idDel - 1); // -1 car l'affichage commence à 1
                break;

            case "4": // Visualiser les bJOBs
                DisplayJobs();
                break;

            case "5": // Exécuter UN bJOB
                DisplayJobs();
                Console.Write("Job ID to run: ");
                _viewModel.RunJob(Console.ReadLine() ?? ""); 
                break;

            case "6": // Exécuter une PARTIE
                Console.Write(_viewModel.GetText("prompt_indices")); 
                _viewModel.RunJob(Console.ReadLine() ?? ""); // "1;3" ou "1-3"
                break;

            case "7": // Exécuter TOUS les bJOBs
                _viewModel.RunJob("1-5"); 
                break;

            case "8": // Quitter
                Environment.Exit(0);
                break;

            default:
                Console.WriteLine("Invalid choice.");
                break;
        }
    }

    private void DisplayJobs()
    {
        var jobs = _viewModel.GetJobs(); // Le VM récupère la liste
        for (int i = 0; i < jobs.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {jobs[i].Name} [{jobs[i].Type}]");
        }
    }
}