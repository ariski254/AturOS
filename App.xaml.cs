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
                LoggerService.Instance.Error($"Unhandled Domain Exception: {ex.Message}\n{ex.StackTrace}");
            }
        };

        DispatcherUnhandledException += (sender, args) =>
        {
            LoggerService.Instance.Error($"Dispatcher Exception: {args.Exception.Message}\n{args.Exception.StackTrace}");
            args.Handled = true; // Prevent crash, handle gracefully
            MessageBox.Show($"Terjadi kesalahan sistem:\n{args.Exception.Message}", "AturOS - Peringatan", MessageBoxButton.OK, MessageBoxImage.Warning);
        };
    }
}
