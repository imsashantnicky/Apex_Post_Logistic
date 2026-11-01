namespace ApexPost.UnitTests.Architecture;

/// <summary>Never run: gives the scanner one example of everything the layering tests must catch.</summary>
internal static class BlockingSample
{
    public static int ReadBlocking(Task<int> task)
    {
        task.Wait();
        Thread.Sleep(1);
        return task.Result + task.GetAwaiter().GetResult();
    }

    public static async void FireAndForget(Task task) => await task.ConfigureAwait(false);
}
