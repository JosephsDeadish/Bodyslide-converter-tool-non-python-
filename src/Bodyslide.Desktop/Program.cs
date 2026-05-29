using System.Windows.Forms;

namespace Bodyslide.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += OnThreadException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            ShowFatalError(ex);
        }
    }

    private static void OnThreadException(object sender, System.Threading.ThreadExceptionEventArgs e) =>
        ShowFatalError(e.Exception);

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            ShowFatalError(ex);
        }
    }

    private static void ShowFatalError(Exception ex)
    {
        var crashDetails = BuildCrashDetails(ex);
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
            Text = "SlideSmith encountered a fatal error and could not start.\nYou can copy the details below for bug reports.",
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
            }
            catch
            {
            }
        };
        actions.Controls.Add(closeButton);
        actions.Controls.Add(copyButton);
        layout.Controls.Add(actions, 0, 2);

        dialog.AcceptButton = closeButton;
        dialog.CancelButton = closeButton;
        dialog.ShowDialog();
    }

    private static string BuildCrashDetails(Exception ex)
    {
        return
            $"Message: {ex.Message}{Environment.NewLine}" +
            $"Type: {ex.GetType().FullName}{Environment.NewLine}" +
            $"Timestamp (UTC): {DateTime.UtcNow:O}{Environment.NewLine}{Environment.NewLine}" +
            $"Stack Trace:{Environment.NewLine}{ex}";
    }
}
