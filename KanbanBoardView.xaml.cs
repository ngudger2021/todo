using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using TodoWpfApp.Models;

namespace TodoWpfApp
{
    /// <summary>
    /// Interaction logic for KanbanBoardView.xaml
    /// </summary>
    public partial class KanbanBoardView : UserControl
    {
        private static readonly string[] StatusTags = { "New", "In Progress", "On Hold", "Complete" };
        private readonly ObservableCollection<KanbanColumn> _columns;
        private readonly ObservableCollection<TaskItem> _tasks;
        private readonly Action _onDataChanged;
        private TaskItem? _draggedTask;
        private KanbanColumn? _selectedColumn;

        public ObservableCollection<KanbanColumn> Columns => _columns;

        public KanbanBoardView(ObservableCollection<TaskItem> tasks, ObservableCollection<KanbanColumn> columns, Action onDataChanged)
        {
            InitializeComponent();
            _tasks = tasks;
            _onDataChanged = onDataChanged;
            _columns = columns; // Use the shared collection directly

            EnsureStatusColumns();

            ColumnsControl.ItemsSource = _columns;
            Loaded += KanbanBoardView_Loaded;
        }

        private void KanbanBoardView_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshAllColumns();
        }

        /// <summary>
        /// Public method to refresh the board from external callers (e.g., when switching views)
        /// </summary>
        public void RefreshBoard()
        {
            RefreshAllColumns();
        }

        private void RefreshAllColumns()
        {
            // Force update the layout to ensure visual tree is ready
            ColumnsControl.UpdateLayout();

            // Find all TasksList controls by name
            var tasksLists = FindVisualChildren<ItemsControl>(ColumnsControl).Where(ic => ic.Name == "TasksList").ToList();

            foreach (var tasksControl in tasksLists)
            {
                if (tasksControl.DataContext is KanbanColumn column)
                {
                    var columnStatus = column.Name;
                    var columnTasks = _tasks
                        .Where(t => string.Equals(GetOrAssignStatusTag(t), columnStatus, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(t => t.SortOrder)
                        .ToList();

                    tasksControl.ItemsSource = columnTasks;

                    // Update task count in the header
                    var columnBorder = FindVisualParent<Border>(tasksControl);
                    if (columnBorder != null)
                    {
                        var headerBorder = FindVisualChildren<Border>(columnBorder).FirstOrDefault();
                        if (headerBorder != null)
                        {
                            var countText = FindVisualChildren<TextBlock>(headerBorder).Skip(1).FirstOrDefault();
                            if (countText != null)
                            {
                                countText.Text = $"{columnTasks.Count} task{(columnTasks.Count != 1 ? "s" : "")}";
                            }
                        }
                    }
                }
            }
        }

        private static bool IsCompletedColumn(KanbanColumn column)
        {
            return string.Equals(column.Name, "Complete", StringComparison.OrdinalIgnoreCase);
        }

        private void AddColumnButton_Click(object sender, RoutedEventArgs e)
        {
            // Columns are fixed to match task status.
        }

        private void EditColumnButton_Click(object sender, RoutedEventArgs e)
        {
            // Columns are fixed to match task status.
        }

        private void DeleteColumnButton_Click(object sender, RoutedEventArgs e)
        {
            // Columns are fixed to match task status.
        }

        private void AddTaskButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new AddEditTaskWindow(null, _tasks);
            if (window.ShowDialog() == true)
            {
                _onDataChanged?.Invoke();
                RefreshAllColumns();
            }
        }

        private void ColumnHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is KanbanColumn column)
            {
                _selectedColumn = column;

                // Visual feedback for selection
                foreach (var item in ColumnsControl.Items)
                {
                    var container = ColumnsControl.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
                    var headerBorder = FindVisualChildren<Border>(container).FirstOrDefault(b => b.DataContext == item);
                    if (headerBorder != null)
                    {
                        headerBorder.BorderBrush = item == column ? Brushes.Yellow : null;
                        headerBorder.BorderThickness = item == column ? new Thickness(2) : new Thickness(0);
                    }
                }
            }
        }

        private void TaskCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is TaskItem task)
            {
                if (e.ClickCount >= 2)
                {
                    var window = new AddEditTaskWindow(task, _tasks);
                    if (window.ShowDialog() == true)
                    {
                        _onDataChanged?.Invoke();
                        RefreshAllColumns();
                    }
                    e.Handled = true;
                    return;
                }

                _draggedTask = task;
                DragDrop.DoDragDrop(border, task, DragDropEffects.Move);
            }
        }

        private void TasksList_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void TasksList_Drop(object sender, DragEventArgs e)
        {
            if (_draggedTask == null)
                return;

            var targetColumn = (sender as FrameworkElement)?.DataContext as KanbanColumn;
            if (targetColumn != null)
            {
                var newStatus = targetColumn.Name;
                SetStatusTag(_draggedTask, newStatus);
                bool moveToCompleted = IsCompletedColumn(targetColumn);
                _draggedTask.Completed = moveToCompleted;
                _draggedTask.CompletedAt = moveToCompleted ? DateTime.Now : null;
                _draggedTask.ColumnId = targetColumn.Id;
                _onDataChanged?.Invoke();
                RefreshAllColumns();
            }

            _draggedTask = null;
        }

        private void ClearStatusTags(TaskItem task)
        {
            // Remove all status:* tags when task is removed from board
            task.Tags.RemoveAll(t => t.StartsWith("status:", StringComparison.OrdinalIgnoreCase));
        }

        private void EnsureStatusColumns()
        {
            _columns.Clear();
            _columns.Add(new KanbanColumn { Name = "New", DisplayOrder = 0, Color = "#95a5a6" });
            _columns.Add(new KanbanColumn { Name = "In Progress", DisplayOrder = 1, Color = "#f39c12" });
            _columns.Add(new KanbanColumn { Name = "On Hold", DisplayOrder = 2, Color = "#8e44ad" });
            _columns.Add(new KanbanColumn { Name = "Complete", DisplayOrder = 3, Color = "#27ae60" });
            _onDataChanged?.Invoke();
        }

        private string GetOrAssignStatusTag(TaskItem task)
        {
            var existing = task.Tags.FirstOrDefault(t => StatusTags.Any(s =>
                string.Equals(s, t, StringComparison.OrdinalIgnoreCase)));
            if (!string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }

            SetStatusTag(task, "New");
            return "New";
        }

        private void SetStatusTag(TaskItem task, string newStatus)
        {
            // Remove existing status tags and preserve all other tags
            task.Tags.RemoveAll(t => StatusTags.Any(s => string.Equals(s, t, StringComparison.OrdinalIgnoreCase)));
            task.Tags.Add(newStatus);
        }

        // Helper methods to find visual tree elements
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject? depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T typedChild)
                {
                    yield return typedChild;
                }

                foreach (var childOfChild in FindVisualChildren<T>(child))
                {
                    yield return childOfChild;
                }
            }
        }

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                    return parent;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }
    }

    /// <summary>
    /// Converter to convert hex color string to SolidColorBrush
    /// </summary>
    public class HexToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hexColor && !string.IsNullOrWhiteSpace(hexColor))
            {
                try
                {
                    return (SolidColorBrush)new BrushConverter().ConvertFrom(hexColor)!;
                }
                catch
                {
                    return Brushes.Gray;
                }
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
