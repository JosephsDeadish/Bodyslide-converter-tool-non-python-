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
        MessageBox.Show(
            $"SlideSmith encountered a fatal error and could not start:\n\n{ex.Message}\n\n{ex.GetType().FullName}\n\nIf this keeps happening, please report it on GitHub.",
            "SlideSmith — Fatal Error",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
