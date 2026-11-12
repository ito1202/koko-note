using System.Security.AccessControl;
using System.Security.Principal;

namespace KOKONote.App;

internal static class SecureDirectory
{
    public static string Prepare()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KOKONote");
        var user = WindowsIdentity.GetCurrent().User ?? throw new UnauthorizedAccessException();
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var info = new DirectoryInfo(path);
        if (info.Exists && info.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new UnauthorizedAccessException();
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(true, false);
        foreach (var sid in new[] { user, system })
            security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        if (!info.Exists) info.Create(security);
        else info.SetAccessControl(security);
        foreach (var file in info.EnumerateFileSystemInfos())
        {
            if (file.Attributes.HasFlag(FileAttributes.ReparsePoint) || file is DirectoryInfo)
                throw new UnauthorizedAccessException();
            var acl = new FileSecurity();
            acl.SetAccessRuleProtection(true, false);
            foreach (var sid in new[] { user, system })
                acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, AccessControlType.Allow));
            ((FileInfo)file).SetAccessControl(acl);
        }
        return Path.Combine(path, "notes.db");
    }
}
