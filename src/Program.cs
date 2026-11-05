using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace TomatoClock
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool preview = args != null && args.Length >= 2 &&
                           string.Equals(args[0], "--screenshot", StringComparison.OrdinalIgnoreCase);

            string mutexName = preview
                ? "TomatoClock.Preview.Instance"
                : "TomatoClock.SingleInstance.7B1D2F4A";

            bool createdNew;
            using (Mutex mutex = new Mutex(true, mutexName, out createdNew))
            {
                if (!createdNew)
                {
                    if (!preview)
                    {
                        MessageBox.Show("番茄时钟已经在运行啦。", "番茄时钟",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    return;
                }

                Application app = new Application();
                app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                app.DispatcherUnhandledException += OnDispatcherUnhandledException;

                try
                {
                    MainWindow window = new MainWindow();
                    if (preview)
                    {
                        string path = args[1];
                        string state = args.Length >= 3 ? args[2] : "idle";
                        window.EnablePreview(path, state);
                    }
                    app.Run(window);
                }
                catch (Exception ex)
                {
                    LogError(ex);
                    if (!preview)
                    {
                        MessageBox.Show("番茄时钟启动失败：\n" + ex.Message, "番茄时钟",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                finally
                {
                    GC.KeepAlive(mutex);
                }
            }
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogError(e.Exception);
            MessageBox.Show("番茄时钟遇到了一点问题：\n" + e.Exception.Message, "番茄时钟",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            e.Handled = true;
        }

        private static void LogError(Exception ex)
        {
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "番茄时钟");
                Directory.CreateDirectory(folder);
                StringBuilder text = new StringBuilder();
                text.AppendLine("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "]");
                text.AppendLine(ex.ToString());
                text.AppendLine();
                File.AppendAllText(Path.Combine(folder, "error.log"), text.ToString(), Encoding.UTF8);
            }
            catch
            {
                // Logging must never mask the original problem.
            }
        }
    }
}
