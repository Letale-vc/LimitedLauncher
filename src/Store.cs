using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LimitedLauncher;

internal sealed class Config
{
    public string? Default { get; set; }
    public string? LastExe { get; set; }
    public string? LastArgs { get; set; }

    /// <summary>User name -> DPAPI-protected (current Windows user) password, base64.</summary>
    public Dictionary<string, string> Users { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Config))]
internal sealed partial class ConfigContext : JsonSerializerContext { }

/// <summary>Persists limited-user credentials in %APPDATA%, encrypted with DPAPI so no extra package is needed.</summary>
internal static class Store
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LimitedLauncher", "config.json");

    public static Config Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize(File.ReadAllText(FilePath), ConfigContext.Default.Config) ?? new Config();
        }
        catch (JsonException) { }
        return new Config();
    }

    public static void Save(Config cfg)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(cfg, ConfigContext.Default.Config));
    }

    public static string Protect(string secret) =>
        Convert.ToBase64String(Transform(Encoding.UTF8.GetBytes(secret), protect: true));

    public static string? Unprotect(string blob)
    {
        try { return Encoding.UTF8.GetString(Transform(Convert.FromBase64String(blob), protect: false)); }
        catch (Exception ex) when (ex is Win32Exception or FormatException) { return null; }
    }

    private static byte[] Transform(byte[] data, bool protect)
    {
        var input = new Native.DATA_BLOB { cbData = data.Length, pbData = Marshal.AllocHGlobal(data.Length) };
        try
        {
            Marshal.Copy(data, 0, input.pbData, data.Length);
            Native.DATA_BLOB output;
            bool ok = protect
                ? Native.CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out output)
                : Native.CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out output);
            if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                var result = new byte[output.cbData];
                Marshal.Copy(output.pbData, result, 0, output.cbData);
                return result;
            }
            finally { Native.LocalFree(output.pbData); }
        }
        finally { Marshal.FreeHGlobal(input.pbData); }
    }
}
