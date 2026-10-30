using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace LimitedLauncher;

/// <summary>Local account management for the limited user.</summary>
internal static class LocalUsers
{
    private const string UserListKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon\SpecialAccounts\UserList";

    public static bool IsAdministrator()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void Create(string name, string password, bool hideFromLogon = true)
    {
        var info = new Native.USER_INFO_1
        {
            Name = name,
            Password = password,
            Priv = Native.USER_PRIV_USER,
            Comment = "Created by LimitedLauncher",
            Flags = Native.UF_SCRIPT | Native.UF_DONT_EXPIRE_PASSWD | Native.UF_PASSWD_CANT_CHANGE,
        };
        Check(Native.NetUserAdd(null, 1, ref info, out _), $"create user '{name}'");

        if (hideFromLogon)
        {
            try
            {
                using var key = Registry.LocalMachine.CreateSubKey(UserListKey);
                key.SetValue(name, 0, RegistryValueKind.DWord);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
            {
                // cosmetic only, the account is already usable
            }
        }
    }

    public static void Remove(string name)
    {
        Check(Native.NetUserDel(null, name), $"remove user '{name}'");
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(UserListKey, writable: true);
            key?.DeleteValue(name, throwOnMissingValue: false);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException) { }
    }

    public static bool Exists(string name)
    {
        int rc = Native.NetUserGetInfo(null, name, 0, out var buf);
        if (buf != IntPtr.Zero) Native.NetApiBufferFree(buf);
        return rc == Native.NERR_Success;
    }

    public static List<string> List()
    {
        var names = new List<string>();
        int resume = 0;
        int rc;
        do
        {
            rc = Native.NetUserEnum(null, 0, 0, out var buf, -1, out int read, out _, ref resume);
            if (rc != Native.NERR_Success && rc != Native.ERROR_MORE_DATA)
                Check(rc, "list users");
            try
            {
                int size = Marshal.SizeOf<Native.USER_INFO_0>();
                for (int i = 0; i < read; i++)
                {
                    var item = Marshal.PtrToStructure<Native.USER_INFO_0>(buf + i * size);
                    names.Add(item.Name);
                }
            }
            finally
            {
                if (buf != IntPtr.Zero) Native.NetApiBufferFree(buf);
            }
        } while (rc == Native.ERROR_MORE_DATA);
        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    public static (bool Disabled, string Comment) GetInfo(string name)
    {
        Check(Native.NetUserGetInfo(null, name, 1, out var buf), $"read user '{name}'");
        try
        {
            var info = Marshal.PtrToStructure<Native.USER_INFO_1>(buf);
            return ((info.Flags & Native.UF_ACCOUNTDISABLE) != 0, info.Comment ?? "");
        }
        finally
        {
            Native.NetApiBufferFree(buf);
        }
    }

    private static void Check(int rc, string action)
    {
        if (rc == Native.NERR_Success) return;
        string msg = rc switch
        {
            Native.ERROR_ACCESS_DENIED => "access denied (run from an elevated prompt)",
            Native.NERR_UserExists => "user already exists",
            Native.NERR_UserNotFound => "user not found",
            Native.NERR_PasswordTooShort => "password does not meet the local password policy",
            _ => new Win32Exception(rc).Message,
        };
        throw new AppException($"Failed to {action}: {msg} (code {rc}).");
    }
}
