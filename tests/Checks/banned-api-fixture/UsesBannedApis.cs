using System.Diagnostics;

namespace BannedApiFixture;

public static class UsesBannedApis
{
    public static object[] All(Process process, WaitHandle handle, Task task) =>
    [
        DateTime.Now,
        DateTime.UtcNow,
        DateTimeOffset.UtcNow,
        Stopwatch.StartNew(),
        Task.Delay(1),
        process.WaitForExit(1),
        handle.WaitOne(1),
        task.Wait(1),
        new CancellationTokenSource(1),
    ];

    public static void Sleep() => Thread.Sleep(1);
}
