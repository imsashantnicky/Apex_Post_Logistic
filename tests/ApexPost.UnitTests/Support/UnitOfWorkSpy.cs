namespace ApexPost.UnitTests.Support;

/// <summary>
/// Counts what every unit of work of one harness did, so a test can state how often a use case committed. A commit
/// that was told to fail applies nothing, as a database rollback would.
/// </summary>
internal sealed class UnitOfWorkSpy
{
    private Exception? _nextFailure;

    public int Commits { get; private set; }

    public int FailedCommits { get; private set; }

    public void FailNextCommitWith(Exception failure) => _nextFailure = failure;

    public void Committed() => Commits++;

    public void ThrowIfCommitMustFail()
    {
        if (_nextFailure is not { } failure)
        {
            return;
        }

        _nextFailure = null;
        FailedCommits++;
        throw failure;
    }
}
