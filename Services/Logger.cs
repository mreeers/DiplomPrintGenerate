using System;
using System.IO;
using System.Text;

namespace DiplomPrintGenerate.Services
{
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static string _logDirectory;
        private static string _currentLogFile;

        static Logger()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                _logDirectory = Path.Combine(baseDir, "logs");
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }

                _currentLogFile = Path.Combine(_logDirectory, $"debug_{DateTime.Now:yyyy-MM-dd}.log");
                Log("=== ЗАПУСК ПРИЛОЖЕНИЯ DiplomPrintGenerate ===");
                Log($"OS: {Environment.OSVersion}, CLR: {Environment.Version}");
            }
            catch { }
        }

        public static string LogDirectory => _logDirectory;
        public static string CurrentLogFile => _currentLogFile;

        public static void Log(string message, string level = "INFO")
        {
            string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";

            // Output to debug console
            System.Diagnostics.Debug.WriteLine(entry);

            // Write to file
            lock (_lock)
            {
                try
                {
                    if (!string.IsNullOrEmpty(_currentLogFile))
                    {
                        File.AppendAllText(_currentLogFile, entry + Environment.NewLine, Encoding.UTF8);
                    }
                }
                catch { }
            }
        }

        public static void LogError(string message, Exception ex = null)
        {
            if (ex != null)
            {
                Log($"{message} | Ошибка: {ex.Message}{Environment.NewLine}StackTrace: {ex.StackTrace}", "ERROR");
            }
            else
            {
                Log(message, "ERROR");
            }
        }

        public static void LogClick(string elementName, string elementType, double x, double y)
        {
            Log($"[CLICK] Элемент: '{elementName}' (Тип: {elementType}) по координатам ({x:0}, {y:0})", "ACTION");
        }
    }
}
