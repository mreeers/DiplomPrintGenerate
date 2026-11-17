using System;
using System.Windows;
using DiplomPrintGenerate.Services;

namespace DiplomPrintGenerate
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Global exception logging
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                Logger.LogError("КРИТИЧЕСКАЯ НЕОБРАБОТАННАЯ ОШИБКА (AppDomain)", ex);
            };

            this.DispatcherUnhandledException += (s, args) =>
            {
                Logger.LogError("НЕОБРАБОТАННАЯ ОШИБКА ПОТОКА ДИСПЕТЧЕРА", args.Exception);
                // Do not mark handled so crash is visible if critical, but still logged
            };

            if (e.Args != null && Array.Exists(e.Args, a => a.Equals("--screenshot", StringComparison.OrdinalIgnoreCase) || a.Equals("--debug", StringComparison.OrdinalIgnoreCase)))
            {
                ScreenshotService.IsEnabled = true;
                Logger.Log("Режим скриншотов активирован через параметр командной строки.");
            }

            Logger.Log("Приложение успешно инициализировано.");
        }
    }
}
