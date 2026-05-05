using System.Windows.Input;

namespace EasySaveWpf.ViewModels;

public class Command : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public Command(Action execute, Func<bool>? canExecute = null)
    {
        _execute = _ => execute();

        if (canExecute == null)
        {
            _canExecute = null;
        }
        else
        {
            _canExecute = _ => canExecute();
        }
    }

    public Command(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        if (_canExecute == null)
            return true;

        return _canExecute(parameter);
    }

    public void Execute(object? parameter)
    {
        _execute(parameter);
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

