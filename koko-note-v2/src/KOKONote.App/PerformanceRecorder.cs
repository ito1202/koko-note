using System.Diagnostics;
using System.Text.Json;

namespace KOKONote.App;

// Opt-in local diagnostics only. No text, titles, paths to notes, or external communication.
internal sealed class PerformanceRecorder(string destination)
{
    private readonly List<double> shows = [];
    private readonly List<double> switches = [];
    private readonly CancellationTokenSource stopping = new();
    private int interactions;
    private double? startup;
    private object? idle;
    public void Interaction() => interactions++;
    public void Show(double milliseconds) => shows.Add(milliseconds);
    public void Switch(double milliseconds) { Interaction(); switches.Add(milliseconds); }
    public void Ready()
    {
        using var process = Process.GetCurrentProcess();
        startup = (DateTime.UtcNow - process.StartTime.ToUniversalTime()).TotalMilliseconds;
    }
    public async Task SampleIdleAsync(Func<bool> visible)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stopping.Token);
            var count = interactions;
            var hidden = !visible();
            using var p = Process.GetCurrentProcess();
            var cpu = p.TotalProcessorTime;
            var start = Stopwatch.GetTimestamp();
            await Task.Delay(TimeSpan.FromSeconds(10), stopping.Token);
            p.Refresh();
            var seconds = Stopwatch.GetElapsedTime(start).TotalSeconds;
            idle = new
            {
                ValidHiddenSample = hidden && !visible() && interactions == count,
                SampleSeconds = seconds,
                CpuPercentOfMachine = (p.TotalProcessorTime - cpu).TotalSeconds / seconds / Environment.ProcessorCount * 100,
                CpuPercentOfOneCore = (p.TotalProcessorTime - cpu).TotalSeconds / seconds * 100,
                PrivateBytes = p.PrivateMemorySize64, WorkingSetBytes = p.WorkingSet64
            };
        }
        catch (OperationCanceledException) { }
    }
    public void Write()
    {
        stopping.Cancel();
        using var p = Process.GetCurrentProcess();
        var report = new
        {
            OS = Environment.OSVersion.ToString(), Runtime = Environment.Version.ToString(),
            Architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
#if DEBUG
            Configuration = "Debug (do not use for acceptance)",
#else
            Configuration = "Release",
#endif
            LogicalProcessors = Environment.ProcessorCount,
            StartupToReadyMs = startup,
            FirstShowMs = shows.Count > 0 ? (double?)shows[0] : null,
            SubsequentShowMs = shows.Skip(1).ToArray(), NoteSwitchMs = switches.ToArray(),
            Idle = idle, FinalPrivateBytes = p.PrivateMemorySize64, FinalWorkingSetBytes = p.WorkingSet64,
            Method = "UI handler entry through synchronous Update; excludes physical key delivery and desktop compositor."
        };
        // Never overwrite an arbitrary existing file with a diagnostic report.
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(output, report, new JsonSerializerOptions { WriteIndented = true });
    }
}
