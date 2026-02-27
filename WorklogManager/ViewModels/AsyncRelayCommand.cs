using System.Windows.Input;

namespace WorklogManager.ViewModels;

/// <summary>
/// Async ICommand implementation. Prevents re-entrant execution while running.
/// Propagates OperationCanceledException silently; other exceptions bubble up.
/// </summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<CancellationToken, Task> _execute;
    private readonly Func<bool>? _canExecute;

    private bool _isExecuting;
    private CancellationTokenSource? _cts;

    public AsyncRelayCommand(
        Func<CancellationToken, Task> execute,
        Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool IsExecuting => _isExecuting;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) =>
        !_isExecuting && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;

        _isExecuting = true;
        _cts = new CancellationTokenSource();
        CommandManager.InvalidateRequerySuggested();

        try
        {
            await _execute(_cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected — swallow silently
        }
        finally
        {
            _isExecuting = false;
            _cts?.Dispose();
            _cts = null;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>Cancels the currently running execution, if any.</summary>
    public void Cancel() => _cts?.Cancel();
}
