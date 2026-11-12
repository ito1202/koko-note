namespace KOKONote.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var sid = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value;
        using var instance = new KOKONote.Core.InstanceLease(@"Local\KOKONote.2.0." + sid);
        if (!instance.IsFirst)
        {
            MessageBox.Show("KOKO Note は既に起動しています。トレイから開いてください。", "KOKO Note");
            return;
        }
        var metrics = args.Length == 2 && args[0] == "--measure" ? new PerformanceRecorder(Path.GetFullPath(args[1])) : null;
        Application.Run(new ResidentContext(metrics));
    }
}
