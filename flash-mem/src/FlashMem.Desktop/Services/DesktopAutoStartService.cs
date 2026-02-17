using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace FlashMem.Desktop.Services;

public sealed class DesktopAutoStartService : IAutoStartService
{
    private const string WindowsRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "FlashMem";
    private const string MacLaunchAgentName = "com.flashmem.desktop";
    private readonly string _macLaunchAgentPath;

    public DesktopAutoStartService()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        _macLaunchAgentPath = Path.Combine(home, "Library", "LaunchAgents", $"{MacLaunchAgentName}.plist");
    }

    public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            using var key = Registry.CurrentUser.OpenSubKey(WindowsRunKey, writable: false);
            return Task.FromResult(key?.GetValue(AppName) is string);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Task.FromResult(File.Exists(_macLaunchAgentPath));
        }

        return Task.FromResult(false);
    }

    public Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            using var key = Registry.CurrentUser.CreateSubKey(WindowsRunKey, writable: true);
            if (enabled)
            {
                key?.SetValue(AppName, BuildWindowsCommand());
            }
            else
            {
                key?.DeleteValue(AppName, throwOnMissingValue: false);
            }

            return Task.CompletedTask;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            if (!enabled)
            {
                if (File.Exists(_macLaunchAgentPath))
                {
                    File.Delete(_macLaunchAgentPath);
                }

                return Task.CompletedTask;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_macLaunchAgentPath)!);
            File.WriteAllText(_macLaunchAgentPath, BuildMacPlist());
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    private static string BuildWindowsCommand()
    {
        var command = BuildProgramArguments();
        return string.Join(' ', command.Select(Quote));
    }

    private string BuildMacPlist()
    {
        var args = BuildProgramArguments();
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">");
        sb.AppendLine("<plist version=\"1.0\">");
        sb.AppendLine("<dict>");
        sb.AppendLine($"  <key>Label</key><string>{MacLaunchAgentName}</string>");
        sb.AppendLine("  <key>RunAtLoad</key><true/>");
        sb.AppendLine("  <key>ProgramArguments</key>");
        sb.AppendLine("  <array>");
        foreach (var arg in args)
        {
            sb.AppendLine($"    <string>{EscapeXml(arg)}</string>");
        }

        sb.AppendLine("  </array>");
        sb.AppendLine("</dict>");
        sb.AppendLine("</plist>");
        return sb.ToString();
    }

    private static string[] BuildProgramArguments()
    {
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Unable to resolve process path.");

        if (Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var assemblyPath = Assembly.GetEntryAssembly()?.Location
                ?? throw new InvalidOperationException("Unable to resolve entry assembly path.");
            return [processPath, assemblyPath];
        }

        return [processPath];
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
    }
}
