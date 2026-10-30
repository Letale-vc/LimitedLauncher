using System.Windows;
using LimitedLauncher;

internal sealed class AppException(string message) : Exception(message);

internal static class Program
{
    [STAThread]
    private static void Main()
    {
#pragma warning disable WPF0001 // Fluent theme (Windows 11 look) is an evaluation API in .NET 9/10
        var app = new Application { ThemeMode = ThemeMode.System };
#pragma warning restore WPF0001
        app.Run(new MainWindow());
    }
}
