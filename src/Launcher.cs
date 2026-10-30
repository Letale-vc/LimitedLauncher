using System.ComponentModel;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;

namespace LimitedLauncher;

internal static class Launcher
{
    /// <summary>Gives the user read+execute on a folder tree so the limited account can load the target.</summary>
    public static void GrantRead(string user, string folder)
    {
        var dir = new DirectoryInfo(folder);
        var security = dir.GetAccessControl();
        var account = new NTAccount(Environment.MachineName, user);
        security.AddAccessRule(new FileSystemAccessRule(account,
            FileSystemRights.ReadAndExecute | FileSystemRights.Synchronize,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, AccessControlType.Allow));
        dir.SetAccessControl(security);
    }

    public static Process Run(string exe, string arguments, string? workDir, string user, string password)
    {
        string full = Path.GetFullPath(exe);
        if (!File.Exists(full)) throw new AppException($"File not found: {full}");

        var psi = new ProcessStartInfo(full)
        {
            WorkingDirectory = workDir is null ? Path.GetDirectoryName(full)! : Path.GetFullPath(workDir),
            UseShellExecute = false,
            UserName = user,
            Domain = ".",
            PasswordInClearText = password,
            LoadUserProfile = true,
        };
        psi.Arguments = arguments;

        try
        {
            return Process.Start(psi) ?? throw new AppException("Process failed to start.");
        }
        catch (Win32Exception ex)
        {
            throw new AppException($"Could not start '{full}' as '{user}': {ex.Message} (code {ex.NativeErrorCode}). " +
                "Check the password and that the user can read the folder (tick 'Grant folder access' to fix folder permissions).");
        }
    }
}
