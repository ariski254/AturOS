using System;
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

            // Suppress non-critical framework/telemetry/MSAA/popup exceptions so they don't interrupt user navigation
            if (args.Exception is System.IO.FileNotFoundException fnf &&
                (fnf.FileName?.Contains("Accessibility", StringComparison.OrdinalIgnoreCase) == true ||
                 fnf.FileName?.Contains("Tracing", StringComparison.OrdinalIgnoreCase) == true ||
                 fnf.FileName?.Contains("PresentationFramework", StringComparison.OrdinalIgnoreCase) == true))
            {
                return;
            }
        };
    }
}

