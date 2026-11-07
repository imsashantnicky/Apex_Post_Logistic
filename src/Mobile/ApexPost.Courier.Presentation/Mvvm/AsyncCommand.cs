using System.Windows.Input;

namespace ApexPost.Courier.Presentation.Mvvm;

/// <summary>
/// An <see cref="ICommand"/> for an asynchronous action. While the action runs the command cannot execute again,
/// so a double tap never saves a scan twice or starts two sign-ins. The action must handle its own errors.
/// </summary>
/// <param name="execute">The action to run.</param>
public sealed class AsyncCommand(Func<Task> execute) : ICommand
{
    private bool _isRunning;

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <summary>Whether the action is running now.</summary>
    public bool IsRunning => _isRunning;

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => !_isRunning;

    /// <inheritdoc />
    public void Execute(object? parameter) => _ = ExecuteAsync();

    /// <summary>Runs the action unless it is already running.</summary>
    /// <returns>A task that completes when the action has finished.</returns>
    public async Task ExecuteAsync()
    {
        if (_isRunning)
        {
            return;
        }

        SetRunning(true);
        try
        {
            await execute().ConfigureAwait(true);
        }
        finally
        {
            SetRunning(false);
        }
    }

    private void SetRunning(bool running)
    {
        _isRunning = running;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
