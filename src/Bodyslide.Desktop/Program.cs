using Bodyslide.Core;
using System.Windows.Forms;

namespace Bodyslide.Desktop;

internal static class Program
{
    private static string? _startupDiagnosticsPath;

    [STAThread]
    private static int Main(string[] args)
    {
        ExecutionEnvironment.TryNormalizeCurrentDirectoryToExecutionRoot(
            Environment.ProcessPath,
            AppContext.BaseDirectory);
        _startupDiagnosticsPath = ShouldEnableStartupDiagnostics(args)
            ? ResolveStartupDiagnosticsPath(args)
            : null;
        WriteStartupDiagnostics(
            _startupDiagnosticsPath,
            $"desktop-startup: exe={Environment.ProcessPath ?? "(unknown)"}, cwd={Environment.CurrentDirectory}, args=[{string.Join(", ", args)}]");
        RegisterGlobalExceptionHandlers();

        if (args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase))
        {
            return RunSmokeTest();
        }

        try
        {
            var launchOptions = DesktopWorkflowSupport.ParseLaunchOptions(args);
            WriteStartupDiagnostics(
                _startupDiagnosticsPath,
                $"desktop-startup: launcher={launchOptions.FromModOrganizerLauncher}, startup-output={launchOptions.StartupOutputDirectory ?? "(none)"}, startup-input={launchOptions.StartupInputPath ?? "(none)"}");
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm(launchOptions));
            WriteStartupDiagnostics(_startupDiagnosticsPath, "desktop-startup: ui exited normally");
            return 0;
        }
        catch (Exception ex)
        {
            ShowFatalError(ex, _startupDiagnosticsPath);
            return 1;
        }
    }

    private static void RegisterGlobalExceptionHandlers()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += OnThreadException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    private static int RunSmokeTest()
    {
        try
        {
            ApplicationConfiguration.Initialize();
            using var form = new MainForm();
            form.CreateControl();
            var summary = form.GetSmokeTestSummary();
            Console.WriteLine(DesktopSmokeTestContract.Serialize(summary));
            return summary.Status == DesktopSmokeTestContract.ReadyStatus ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"SlideSmith desktop smoke test failed: {ex}");
            return 1;
        }
    }

    private static void OnThreadException(object sender, System.Threading.ThreadExceptionEventArgs e) =>
        ShowFatalError(e.Exception, _startupDiagnosticsPath);

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            ShowFatalError(ex, _startupDiagnosticsPath);
        }
    }

    private static void ShowFatalError(Exception ex, string? startupDiagnosticsPath)
    {
        var crashLogPath = TryWriteCrashLog(ex);
        WriteStartupDiagnostics(
            startupDiagnosticsPath,
            $"desktop-startup: fatal {ex.GetType().FullName}: {ex.Message}; crash-log={crashLogPath ?? "(not written)"}");
        var crashDetails = BuildCrashDetails(ex, crashLogPath);
        using var dialog = new Form
        {
            Text = "SlideSmith — Fatal Error",
            Width = 760,
            Height = 460,
            StartPosition = FormStartPosition.CenterScreen,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = true
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        dialog.Controls.Add(layout);

        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Text = crashLogPath is null
                ? "SlideSmith encountered a fatal error and could not start.\nYou can copy the details below for bug reports."
                : $"SlideSmith encountered a fatal error and could not start.\nA startup crash log was written to:\n{crashLogPath}",
        }, 0, 0);

        var detailsTextBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9f),
            Text = crashDetails
        };
        layout.Controls.Add(detailsTextBox, 0, 1);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
        };
        var closeButton = new Button
        {
            Text = "Close",
            AutoSize = true,
            DialogResult = DialogResult.OK,
        };
        var copyButton = new Button
        {
            Text = "Copy crash details",
            AutoSize = true,
        };
        copyButton.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(crashDetails);
                MessageBox.Show(dialog, "Crash details copied to the clipboard.", "SlideSmith", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception clipboardEx)
            {
                System.Diagnostics.Trace.TraceWarning($"Failed to copy crash details to the clipboard: {clipboardEx.Message}");
                MessageBox.Show(
                    dialog,
                    "Clipboard access is unavailable on this machine right now. You can still use the crash log path shown above.",
                    "Clipboard unavailable",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        };
        actions.Controls.Add(closeButton);
        actions.Controls.Add(copyButton);
        layout.Controls.Add(actions, 0, 2);

        dialog.AcceptButton = closeButton;
        dialog.CancelButton = closeButton;
        dialog.ShowDialog();
    }

    private static string BuildCrashDetails(Exception ex, string? crashLogPath)
    {
        return
            $"Message: {ex.Message}{Environment.NewLine}" +
            $"Type: {ex.GetType().FullName}{Environment.NewLine}" +
            $"Timestamp (UTC): {DateTime.UtcNow:O}{Environment.NewLine}" +
            $"Crash log: {crashLogPath ?? "not written"}{Environment.NewLine}{Environment.NewLine}" +
            $"Stack Trace:{Environment.NewLine}{ex}";
    }

    private static string? TryWriteCrashLog(Exception ex)
    {
        foreach (var baseDirectory in EnumerateCrashLogDirectories())
        {
            try
            {
                Directory.CreateDirectory(baseDirectory);
                var crashLogPath = Path.Combine(baseDirectory, "startup-crash.log");
                File.WriteAllText(
                    crashLogPath,
                    $"Timestamp (UTC): {DateTime.UtcNow:O}{Environment.NewLine}" +
                    $"Message: {ex.Message}{Environment.NewLine}" +
                    $"Type: {ex.GetType().FullName}{Environment.NewLine}{Environment.NewLine}" +
                    ex);
                return crashLogPath;
            }
            catch
            {
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateCrashLogDirectories()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            yield return Path.Combine(localAppData, "SlideSmith");
        }

        yield return Path.Combine(Path.GetTempPath(), "SlideSmith");
    }

    private static string? ResolveStartupDiagnosticsPath(IReadOnlyList<string> args)
    {
        for (var index = 0; index < args.Count; index++)
        {
            if (!TryReadOptionToken(args[index], out var optionName, out var inlineValue) ||
                !optionName.Equals("startup-diagnostics", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(inlineValue))
            {
                return NormalizeDiagnosticsPath(inlineValue);
            }

            if (index + 1 < args.Count)
            {
                if (!TryReadOptionToken(args[index + 1], out _, out _))
                {
                    return NormalizeDiagnosticsPath(args[index + 1]);
                }
            }
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            return Path.Combine(localAppData, "SlideSmith", "startup-launch-diagnostics.log");
        }

        return Path.Combine(Path.GetTempPath(), "SlideSmith", "startup-launch-diagnostics.log");
    }

    private static bool ShouldEnableStartupDiagnostics(IReadOnlyList<string> args)
    {
        if (IsDesktopDiagnosticsEnabledByEnvironment())
        {
            return true;
        }

        foreach (var arg in args)
        {
            if (TryReadOptionToken(arg, out var optionName, out _) &&
                optionName.Equals("startup-diagnostics", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDesktopDiagnosticsEnabledByEnvironment()
    {
        var flag = Environment.GetEnvironmentVariable("SLIDESMITH_STARTUP_DIAGNOSTICS");
        return string.Equals(flag, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(flag, "yes", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(flag, "on", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeDiagnosticsPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim().Trim('"');
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static bool TryReadOptionToken(string? arg, out string optionName, out string? inlineValue)
        => StandaloneStartupRouting.TryReadOptionToken(arg, out optionName, out inlineValue);

    private static void WriteStartupDiagnostics(string? diagnosticsPath, string message)
    {
        if (string.IsNullOrWhiteSpace(diagnosticsPath))
        {
            return;
        }

        try
        {
            var path = Path.GetFullPath(diagnosticsPath);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(
                path,
                $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}
