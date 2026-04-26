using System.Windows.Input;

namespace TripleSpaceTranslator.Core.ViewModels;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _executeAsync;
    private readonly Func<bool>? _canExecute;

    public AsyncRelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
    {
        _executeAsync = executeAsync;
        _canExecute = canExecute;
    }

    public bool IsRunning { get; private set; }

    public event EventHandler? CanExecuteChanged;

    public event EventHandler? ExecutionStateChanged;

    public bool CanExecute(object? parameter)
    {
        return !IsRunning && (_canExecute?.Invoke() ?? true);
    }

    public async void Execute(object? parameter)
    {
        await ExecuteAsync();
    }

    public async Task ExecuteAsync()
    {
        if (!CanExecute(null))
        {
            return;
        }

        try
        {
            IsRunning = true;
            RaiseExecutionStateChanged();
            await _executeAsync();
        }
        finally
        {
            IsRunning = false;
            RaiseExecutionStateChanged();
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseExecutionStateChanged()
    {
        ExecutionStateChanged?.Invoke(this, EventArgs.Empty);
        RaiseCanExecuteChanged();
    }
}
