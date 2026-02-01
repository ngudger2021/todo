using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using TodoWpfApp.Models;

namespace TodoWpfApp
{
    public partial class NotificationCenterWindow : Window
    {
        private readonly ObservableCollection<NotificationItem> _notifications;

        public event Action<Guid>? OpenTaskRequested;
        public event Action? NotificationsChanged;

        public NotificationCenterWindow(ObservableCollection<NotificationItem> notifications)
        {
            InitializeComponent();
            _notifications = notifications;
            NotificationsList.ItemsSource = _notifications;

            UpdateCount();
        }

        private void UpdateCount()
        {
            int unread = _notifications.Count(n => !n.Read);
            CountText.Text = $"{unread} unread notification{(unread != 1 ? "s" : "")}";
        }

        private void MarkAsRead_Click(object sender, RoutedEventArgs e)
        {
            if (NotificationsList.SelectedItem is NotificationItem notification)
            {
                notification.Read = true;
                UpdateCount();
                NotificationsChanged?.Invoke();
            }
        }

        private void OpenTask_Click(object sender, RoutedEventArgs e)
        {
            if (NotificationsList.SelectedItem is NotificationItem notification)
            {
                notification.Read = true;
                UpdateCount();
                NotificationsChanged?.Invoke();
                OpenTaskRequested?.Invoke(notification.TaskId);
            }
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Clear all notifications?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _notifications.Clear();
                UpdateCount();
                NotificationsChanged?.Invoke();
            }
        }

    }
}
