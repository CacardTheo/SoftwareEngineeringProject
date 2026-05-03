using SoftwareEngineeringProject.ViewModels;

namespace SoftwareEngineeringProject.Views
{

public class ConsoleView
{
    private readonly MainViewModel _viewModel;

    public ConsoleView()
    {
        _viewModel = new MainViewModel(); // The ViewModel will create its services (Processor, etc.)
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
        Console.WriteLine(_viewModel.GetText("menu_log_format"));
        Console.WriteLine(_viewModel.GetText("menu_exit"));
        Console.Write(_viewModel.GetText("prompt_choice"));
    }

    private void HandleUserInput()
    {
        string choice = Console.ReadLine() ?? "";
        switch (choice)
        {
            case "1": // Change language
                Console.Write(_viewModel.GetText("prompt_language"));
                string lang = Console.ReadLine() ?? "en";
                _viewModel.ChangeLanguage(lang);
                break;

            case "2": // Create a JOB
                Console.Write(_viewModel.GetText("prompt_name"));
                string name = Console.ReadLine() ?? "";
                Console.Write(_viewModel.GetText("prompt_source"));
                string source = Console.ReadLine() ?? "";
                Console.Write(_viewModel.GetText("prompt_target"));
                string target = Console.ReadLine() ?? "";
                Console.Write(_viewModel.GetText("prompt_type"));
                string type = Console.ReadLine() ?? "";
                RunMethodResult(_viewModel.CreateJob(name, source, target, type), _viewModel.GetText("job_created_success"), _viewModel.GetText("error_job_creation"));
                break;

            case "3": // Delete a job
                DisplayJobs();
                bool jobDeleted = false;
                Console.Write(_viewModel.GetText("prompt_id_delete"));
                if (int.TryParse(Console.ReadLine(), out int idDel))
                    jobDeleted = _viewModel.DeleteJob(idDel - 1);
                RunMethodResult(jobDeleted, _viewModel.GetText("job_deleted_success"), _viewModel.GetText("job_deleted_failure"));
                break;

            case "4": // View jobs
                DisplayJobs();
                Console.WriteLine(_viewModel.GetText("exit"));
                Console.ReadLine();
                break;

            case "5": // Run ONE job
                DisplayJobs();
                Console.Write(_viewModel.GetText("prompt_job_id"));
                RunMethodResult(_viewModel.RunJob(Console.ReadLine() ?? ""), _viewModel.GetText("job_execution_success"), _viewModel.GetText("job_execution_failure"));
                break;

            case "6": // Run a SUBSET
                Console.Write(_viewModel.GetText("prompt_indices"));
                RunMethodResult(_viewModel.RunJob(Console.ReadLine() ?? ""), _viewModel.GetText("job_execution_success"), _viewModel.GetText("job_execution_failure")); // "1;3" or "1-3"
                break;

            case "7": // Run ALL JOBs
                RunMethodResult(_viewModel.RunJob("1-5"), _viewModel.GetText("job_execution_success"), _viewModel.GetText("job_execution_failure"));
                break;

            case "8": // Change log format
                Console.Write(_viewModel.GetText("prompt_log_format"));
                string format = Console.ReadLine() ?? "";
                RunMethodResult(_viewModel.SetLogFormat(format), _viewModel.GetText("log_format_success"), _viewModel.GetText("log_format_failure"));
                break;

            case "9": // Exit
                Environment.Exit(0);
                break;

            default:
                Console.WriteLine(_viewModel.GetText("invalid_choice"));
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

}