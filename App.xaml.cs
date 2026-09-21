using System.Windows;
using AturOS.Services;

namespace AturOS;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                LoggerService.Instance.Error($"Unhandled Domain Exception: {ex.Message}\n{ex}");
            }
        };

        DispatcherUnhandledException += (sender, args) =>
        {
            var detailed = args.Exception.InnerException != null 
                ? $"{args.Exception.Message}\nDetail: {args.Exception.InnerException.Message}"
                : args.Exception.Message;

            LoggerService.Instance.Error($"Dispatcher Exception: {detailed}\n{args.Exception}");
            args.Handled = true; // Prevent crash, handle gracefully

            MessageBox.Show(
                $"Terjadi kendala pada operasi sistem:\n{detailed}\n\nOperasi telah diamankan dan dicatat ke log aplikasi.",
                "AturOS - Pemberitahuan Sistem",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        };
    }
}
