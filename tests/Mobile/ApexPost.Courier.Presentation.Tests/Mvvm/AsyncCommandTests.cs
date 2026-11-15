using ApexPost.Courier.Presentation.Mvvm;

namespace ApexPost.Courier.Presentation.Tests.Mvvm;

public sealed class AsyncCommandTests
{
    [Fact]
    public async Task The_command_cannot_run_twice_at_the_same_time()
    {
        var gate = new TaskCompletionSource();
        var runs = 0;
        var command = new AsyncCommand(async () =>
        {
            runs++;
            await gate.Task;
        });

        var first = command.ExecuteAsync();
        Assert.True(command.IsRunning);
        Assert.False(command.CanExecute(null));

        await command.ExecuteAsync();
        gate.SetResult();
        await first;

        Assert.Equal(1, runs);
        Assert.False(command.IsRunning);
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public async Task CanExecuteChanged_is_raised_when_the_command_starts_and_ends()
    {
        var raised = 0;
        var command = new AsyncCommand(() => Task.CompletedTask);
        command.CanExecuteChanged += (_, _) => raised++;

        await command.ExecuteAsync();

        Assert.Equal(2, raised);
    }

    [Fact]
    public async Task The_command_can_run_again_after_the_action_throws()
    {
        var command = new AsyncCommand(() => throw new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(command.ExecuteAsync);

        Assert.True(command.CanExecute(null));
    }
}
