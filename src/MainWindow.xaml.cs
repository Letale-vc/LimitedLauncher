using System.ComponentModel;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace LimitedLauncher;

public partial class MainWindow : Window
{
    private const string DefaultUserName = "Limited";

    private readonly Config _cfg = Store.Load();

    public MainWindow()
    {
        InitializeComponent();
        ExeBox.Text = _cfg.LastExe ?? "";
        ArgsBox.Text = _cfg.LastArgs ?? "";
        RefreshUsers(_cfg.Default);
    }

    // ---- handlers ----

    private void Create_Click(object sender, RoutedEventArgs e) => Guard(() =>
    {
        string name = UserBox.Text.Trim();
        if (name.Length == 0) name = DefaultUserName;
        string password = PasswordBox.Text.Length > 0 ? PasswordBox.Text : GeneratePassword();

        LocalUsers.Create(name, password);
        _cfg.Users[name] = Store.Protect(password);
        _cfg.Default = name;
        Store.Save(_cfg);

        Log($"User '{name}' created.");
        PasswordBox.Text = password;
        RefreshUsers(name);
    });

    private void Remove_Click(object sender, RoutedEventArgs e) => Guard(() =>
    {
        string name = UserBox.Text.Trim();
        if (name.Length == 0) throw new AppException("Pick a user to remove.");
        if (MessageBox.Show(this, $"Delete local user '{name}'?", Title,
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        LocalUsers.Remove(name);
        _cfg.Users.Remove(name);
        if (string.Equals(_cfg.Default, name, StringComparison.OrdinalIgnoreCase)) _cfg.Default = _cfg.Users.Keys.FirstOrDefault();
        Store.Save(_cfg);

        Log($"User '{name}' removed.");
        RefreshUsers(_cfg.Default);
    });

    private void Run_Click(object sender, RoutedEventArgs e) => Guard(() =>
    {
        string exe = ExeBox.Text.Trim().Trim('"');
        if (exe.Length == 0) throw new AppException("Choose a program to run.");
        string user = UserBox.Text.Trim();
        if (user.Length == 0) throw new AppException("Choose or create a limited user first.");

        string? password = PasswordBox.Text.Length > 0 ? PasswordBox.Text : null;
        if (password is null)
        {
            if (!_cfg.Users.TryGetValue(user, out var blob))
                throw new AppException($"No saved password for '{user}'. Type it in the Password box.");
            password = Store.Unprotect(blob)
                ?? throw new AppException("The saved password can't be decrypted. Type it in the Password box.");
        }

        string folder = Path.GetDirectoryName(Path.GetFullPath(exe))!;
        try
        {
            Launcher.GrantRead(user, folder);
            Log($"Granted read+execute on '{folder}' to '{user}'.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.Principal.IdentityNotMappedException)
        {
            throw new AppException($"Could not set permissions on '{folder}': {ex.Message}");
        }

        string? workDir = WorkDirBox.Text.Trim().Length > 0 ? WorkDirBox.Text.Trim() : null;
        using var proc = Launcher.Run(exe, ArgsBox.Text, workDir, user, password);
        Log($"Started '{Path.GetFileName(exe)}' as '{user}' (pid {proc.Id}).");

        _cfg.Default = user;
        SaveSettings();
    });

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Programs (*.exe)|*.exe|All files (*.*)|*.*", CheckFileExists = true };
        if (ExeBox.Text.Length > 0) dlg.InitialDirectory = Path.GetDirectoryName(ExeBox.Text) ?? "";
        if (dlg.ShowDialog(this) == true) ExeBox.Text = dlg.FileName;
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshUsers();

    private void UserBox_Changed(object sender, RoutedEventArgs e)
    {
        // SelectionChanged fires before Text updates; read the selected item when there is one.
        string name = (UserBox.SelectedItem as string ?? UserBox.Text).Trim();
        if (_cfg.Users.TryGetValue(name, out var blob) && Store.Unprotect(blob) is { } saved)
        {
            PasswordBox.Text = saved;
            PasswordHint.Text = "Saved password for this user is shown above.";
        }
        else
        {
            PasswordHint.Text = "Leave the password empty to generate one (saved encrypted for your Windows account).";
        }
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files) ExeBox.Text = files[0];
    }

    // A click on empty space should drop focus from whichever input has it.
    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        DependencyObject? root = null;
        for (var d = e.OriginalSource as DependencyObject; d is not null;
             d = d is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
        {
            if (d is System.Windows.Controls.Primitives.TextBoxBase or System.Windows.Controls.Primitives.Selector
                or System.Windows.Controls.Primitives.ButtonBase or System.Windows.Controls.Primitives.ScrollBar) return;
            root = d;
        }
        // A tree that doesn't end at this window is a popup (e.g. the open ComboBox list) - leave it alone.
        if (root is not Window) return;
        Keyboard.ClearFocus();
    }

    private void Window_Closing(object? sender, CancelEventArgs e) => SaveSettings();

    // ---- helpers ----

    private void RefreshUsers(string? select = null)
    {
        string wanted = select ?? (UserBox.Text.Length > 0 ? UserBox.Text : _cfg.Default ?? "");
        try
        {
            UserBox.ItemsSource = LocalUsers.List();
        }
        catch (AppException ex) { Log(ex.Message); }
        UserBox.Text = wanted;
        UserBox_Changed(UserBox, new RoutedEventArgs());
    }

    private void SaveSettings()
    {
        _cfg.LastExe = ExeBox.Text;
        _cfg.LastArgs = ArgsBox.Text;
        Store.Save(_cfg);
    }

    private void Guard(Action action)
    {
        try { action(); }
        catch (AppException ex)
        {
            Log(ex.Message);
            MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Log(string message)
    {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogBox.ScrollToEnd();
    }

    private static string GeneratePassword()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        var chars = new char[24];
        for (int i = 0; i < chars.Length; i++)
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        // guarantee the complexity classes local password policies usually ask for
        chars[0] = 'A'; chars[1] = 'a'; chars[2] = '7';
        return new string(chars);
    }
}
