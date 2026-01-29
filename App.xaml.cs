using System.Windows;
using TodoWpfApp.Models;

namespace TodoWpfApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private ReminderService? _reminderService;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();

            _reminderService = new ReminderService(mainWindow.Tasks, mainWindow.GetReminderSettings, mainWindow.HandleReminderNotification);
            _reminderService.Start();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _reminderService?.Dispose();
            base.OnExit(e);
        }
    }
}
