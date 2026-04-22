using SoftwareEngineeringProject.ViewModels;

public class ConsoleView
{
    private readonly MainViewModel _viewModel;

    public ConsoleView()
    {
        _viewModel = new MainViewModel(); // Le ViewModel creera ses services (Processor, etc.)
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

            case "2": // Creer un job
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

            case "3": // Supprimer un job
                DisplayJobs();
                Console.Write("ID to delete: ");
                if (int.TryParse(Console.ReadLine(), out int idDel))
                    _viewModel.DeleteJob(idDel - 1);
                break;

            case "4": // Visualiser les jobs
                DisplayJobs();
                break;

            case "5": // Executer UN job
                DisplayJobs();
                Console.Write("Job ID to run: ");
                _viewModel.RunJob(Console.ReadLine() ?? "");
                break;

            case "6": // Executer une PARTIE
                Console.Write(_viewModel.GetText("prompt_indices"));
                _viewModel.RunJob(Console.ReadLine() ?? "");
                break;

            case "7": // Executer TOUS les jobs
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
        var jobs = _viewModel.GetJobs();
        for (int i = 0; i < jobs.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {jobs[i].Name} [{jobs[i].Type}]");
        }
    }
}
