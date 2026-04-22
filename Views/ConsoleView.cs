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
        Console.WriteLine(choice);
        switch (choice)
        {
            case "1": // Change language
                Console.Write("Language (en/fr): ");
                string lang = Console.ReadLine() ?? "en";
                _viewModel.ChangeLanguage(lang);
                break;

            case "2": // Créer un JOB
                Console.Write("Name: ");
                string name = Console.ReadLine() ?? "";
                Console.Write("Source Path: ");
                string source = Console.ReadLine() ?? "";
                Console.Write("Target Path: ");
                string target = Console.ReadLine() ?? "";
                Console.Write("Type (Full/Differential): ");
                string type = Console.ReadLine() ?? "";
                RunMethodResult(_viewModel.CreateJob(name, source, target, type), _viewModel.GetText("job_created_success"), _viewModel.GetText("error_job_creation"));
                break;

            case "3": // Supprimer un job
                DisplayJobs();
                bool jobDeleted = false;
                Console.Write("ID to delete: ");
                if (int.TryParse(Console.ReadLine(), out int idDel))
                    jobDeleted = _viewModel.DeleteJob(idDel - 1);
                RunMethodResult(jobDeleted, _viewModel.GetText("job_deleted_success"), _viewModel.GetText("job_deleted_failure"));
                break;

            case "4": // Visualiser les jobs
                DisplayJobs();
                Console.WriteLine(_viewModel.GetText("exit"));
                Console.ReadLine();
                break;

            case "5": // Executer UN job
                DisplayJobs();
                Console.Write("Job Name to run: ");
                RunMethodResult(_viewModel.RunJob(Console.ReadLine() ?? ""), _viewModel.GetText("job_execution_success"), _viewModel.GetText("job_execution_failure"));
                break;

            case "6": // Exécuter une PARTIE
                Console.Write(_viewModel.GetText("prompt_indices"));
                RunMethodResult(_viewModel.RunJob(Console.ReadLine() ?? ""), _viewModel.GetText("job_execution_success"), _viewModel.GetText("job_execution_failure")); // "1;3" ou "1-3"
                break;

            case "7": // Exécuter TOUS les JOBs
                RunMethodResult(_viewModel.RunJob("1-5"), _viewModel.GetText("job_execution_success"), _viewModel.GetText("job_execution_failure"));
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

    public void RunMethodResult(bool success, string successMessage, string failureMessage )
    {
        if (success)
        {
            Console.WriteLine(successMessage);
        }
        else
        {
            Console.WriteLine(failureMessage);
        }
        Console.WriteLine(_viewModel.GetText("exit"));
        Console.ReadLine();
    }
}