using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using CubeAudio.Boost;

namespace CubeAudio;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (ElevatedCommands.IsCommand(e.Args))
        {
            Shutdown(ElevatedCommands.Run(e.Args));
            return;
        }

        DispatcherUnhandledException += OnUnhandledException;

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Debug.WriteLine(e.Exception);
        MessageBox.Show(e.Exception.Message, "CubeAudio", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
