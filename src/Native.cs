using System.Runtime.InteropServices;

namespace LimitedLauncher;

/// <summary>Raw netapi32 / crypt32 P/Invoke declarations. Everything ships with Windows, no NuGet needed.</summary>
internal static class Native
{
    public const int NERR_Success = 0;
    public const int NERR_UserNotFound = 2221;
    public const int NERR_UserExists = 2224;
    public const int NERR_PasswordTooShort = 2245;
    public const int ERROR_MORE_DATA = 234;
    public const int ERROR_ACCESS_DENIED = 5;

    public const uint UF_SCRIPT = 0x0001;
    public const uint UF_ACCOUNTDISABLE = 0x0002;
    public const uint UF_PASSWD_CANT_CHANGE = 0x0040;
    public const uint UF_DONT_EXPIRE_PASSWD = 0x10000;
    public const uint USER_PRIV_USER = 1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct USER_INFO_0
    {
        public string Name;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct USER_INFO_1
    {
        public string Name;
        public string? Password;
        public uint PasswordAge;
        public uint Priv;
        public string? HomeDir;
        public string? Comment;
        public uint Flags;
        public string? ScriptPath;
    }

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    public static extern int NetUserAdd(string? server, int level, ref USER_INFO_1 buf, out int parmErr);

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    public static extern int NetUserDel(string? server, string name);

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    public static extern int NetUserGetInfo(string? server, string name, int level, out IntPtr buf);

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    public static extern int NetUserEnum(string? server, int level, uint filter, out IntPtr buf, int prefMaxLen,
        out int entriesRead, out int totalEntries, ref int resumeHandle);

    [DllImport("netapi32.dll")]
    public static extern int NetApiBufferFree(IntPtr buf);

    [StructLayout(LayoutKind.Sequential)]
    public struct DATA_BLOB
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool CryptProtectData(ref DATA_BLOB dataIn, string? description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, int flags, out DATA_BLOB dataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool CryptUnprotectData(ref DATA_BLOB dataIn, IntPtr description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, int flags, out DATA_BLOB dataOut);

    [DllImport("kernel32.dll")]
    public static extern IntPtr LocalFree(IntPtr mem);
}
