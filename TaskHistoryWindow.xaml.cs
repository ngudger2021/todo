using System.Collections.ObjectModel;
using System.Windows;
using TodoWpfApp.Models;

namespace TodoWpfApp
{
    public partial class TaskHistoryWindow : Window
    {
        public TaskHistoryWindow(ObservableCollection<TaskHistoryEntry> entries)
        {
            InitializeComponent();
            HistoryGrid.ItemsSource = entries;
        }
    }
}
