using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using TodoWpfApp.Models;

namespace TodoWpfApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<TaskItem> _tasks = new();
        private ICollectionView? _tasksView;
        private const string DataFile = "todo_data.json";
        private const string AttachmentsDir = "attachments";
        private const string SettingsFile = "user_settings.json";
        private ReminderSettings _reminderSettings = ReminderSettings.CreateDefault();

        public ObservableCollection<TaskItem> Tasks => _tasks;

        public MainWindow()
        {
            InitializeComponent();
            // Ensure attachments directory exists
            Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AttachmentsDir));
            // Load tasks from disk
            LoadTasks();
            // Bind the data grid
            TasksGrid.ItemsSource = _tasks;
            // Setup a view for filtering
            _tasksView = CollectionViewSource.GetDefaultView(_tasks);
            if (_tasksView != null)
            {
                _tasksView.Filter = TaskFilter;
                _tasksView.SortDescriptions.Clear();
                _tasksView.SortDescriptions.Add(new SortDescription(nameof(TaskItem.DueDate), ListSortDirection.Ascending));
                _tasksView.Refresh();
            }

            LoadReminderSettings();
            ApplyReminderSettingsToUi();
        }

        public ReminderSettings GetReminderSettings() => _reminderSettings;

        /// <summary>
        /// Predicate for filtering tasks based on the selected filter option.
        /// </summary>
        private bool TaskFilter(object obj)
        {
            if (obj is not TaskItem task)
                return false;
            var filterItem = FilterComboBox.SelectedItem as ComboBoxItem;
            var filter = filterItem?.Content?.ToString() ?? "All";
            return filter == "All" || (filter == "Pending" && !task.Completed) || (filter == "Completed" && task.Completed);
        }

        /// <summary>
        /// Load tasks from the JSON data file.  If the file does not exist or
        /// cannot be parsed, start with an empty collection.
        /// </summary>
        private void LoadTasks()
        {
            _tasks.Clear();
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DataFile);
            try
            {
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                    };
                    var loaded = JsonSerializer.Deserialize<List<TaskItem>>(json, options);
                    if (loaded != null)
                    {
                        foreach (var t in loaded)
                        {
                            t.Attachments ??= new List<string>();
                            t.SubTasks ??= new List<SubTask>();
                            // Ensure subtasks have non-null attachments lists
                            foreach (var st in t.SubTasks)
                            {
                                st.Attachments ??= new List<string>();
                            }
                            _tasks.Add(t);
                        }
                    }
                }
            }
            catch
            {
                // ignore errors and start with empty list
            }
        }

        /// <summary>
        /// Save the current tasks collection to disk as JSON.
        /// </summary>
        private void SaveTasks()
        {
            try
            {
                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DataFile);
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_tasks, options);
                File.WriteAllText(filePath, json);
            }
            catch
            {
                // optionally notify user of save error
            }
        }

        private void LoadReminderSettings()
        {
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFile);
            try
            {
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var loaded = JsonSerializer.Deserialize<ReminderSettings>(json, options);
                    if (loaded != null)
                    {
                        _reminderSettings = loaded;
                    }
                }
            }
            catch
            {
                _reminderSettings = ReminderSettings.CreateDefault();
            }

            if (_reminderSettings.LeadTimeHours <= 0)
            {
                _reminderSettings.LeadTimeHours = 24;
            }
        }

        private void SaveReminderSettings()
        {
            try
            {
                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFile);
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_reminderSettings, options);
                File.WriteAllText(filePath, json);
            }
            catch
            {
                // ignore
            }
        }

        private void ApplyReminderSettingsToUi()
        {
            ReminderToggle.IsChecked = _reminderSettings.RemindersEnabled;
            ReminderLeadTimeTextBox.Text = _reminderSettings.LeadTimeHours.ToString();
        }

        private void UpdateReminderSettingsFromUi()
        {
            var leadText = ReminderLeadTimeTextBox.Text;
            if (!int.TryParse(leadText, out var leadHours) || leadHours <= 0)
            {
                leadHours = _reminderSettings.LeadTimeHours;
            }

            _reminderSettings.RemindersEnabled = ReminderToggle.IsChecked == true;
            _reminderSettings.LeadTimeHours = leadHours;
            ReminderLeadTimeTextBox.Text = leadHours.ToString();
            SaveReminderSettings();
        }

        private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _tasksView?.Refresh();
        }

        private void TasksGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateDetails();
        }

        /// <summary>
        /// Updates the details pane for the currently selected task.
        /// </summary>
        private void UpdateDetails()
        {
            if (TasksGrid.SelectedItem is not TaskItem task)
            {
                DescriptionText.Text = string.Empty;
                DescriptionMarkdown.Markdown = string.Empty;
                DescriptionText.Visibility = Visibility.Visible;
                DescriptionMarkdown.Visibility = Visibility.Collapsed;
                AttachmentsPanel.Children.Clear();
                SubtasksDetailsPanel.ItemsSource = null;
                return;
            }
            // Description
            bool isMarkdown = task.IsMarkdown;
            string description = string.IsNullOrWhiteSpace(task.Description) ? "No description" : task.Description;
            if (isMarkdown)
            {
                DescriptionMarkdown.Markdown = description;
                DescriptionMarkdown.Visibility = Visibility.Visible;
                DescriptionText.Visibility = Visibility.Collapsed;
            }
            else
            {
                DescriptionText.Text = description;
                DescriptionText.Visibility = Visibility.Visible;
                DescriptionMarkdown.Visibility = Visibility.Collapsed;
            }
            // Attachments
            AttachmentsPanel.Children.Clear();
            if (task.Attachments.Count > 0)
            {
                foreach (var relName in task.Attachments)
                {
                    var fileName = System.IO.Path.GetFileName(relName);
                    var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AttachmentsDir, relName);
                    var link = new TextBlock
                    {
                        Text = fileName,
                        Foreground = System.Windows.Media.Brushes.Blue,
                        TextDecorations = TextDecorations.Underline,
                        Cursor = Cursors.Hand,
                        Tag = fullPath,
                        Margin = new Thickness(0, 0, 0, 2)
                    };
                    link.MouseLeftButtonUp += Attachment_Click;
                    AttachmentsPanel.Children.Add(link);
                }
            }
            else
            {
                var noLbl = new TextBlock { Text = "No attachments" };
                AttachmentsPanel.Children.Add(noLbl);
            }
            // Subtasks
            if (task.SubTasks != null && task.SubTasks.Count > 0)
            {
                SubtasksDetailsPanel.ItemsSource = task.SubTasks;
            }
            else
            {
                SubtasksDetailsPanel.ItemsSource = null;
            }
        }

        /// <summary>
        /// Opens an attachment using the default associated application.
        /// </summary>
        private void Attachment_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock tb && tb.Tag is string path)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = path,
                            UseShellExecute = true
                        });
                    }
                }
                catch
                {
                    MessageBox.Show($"Unable to open attachment: {System.IO.Path.GetFileName(path)}");
                }
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new AddEditTaskWindow(null, _tasks);
            window.Owner = this;
            if (window.ShowDialog() == true)
            {
                SaveTasks();
                _tasksView?.Refresh();
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (TasksGrid.SelectedItem is not TaskItem task)
            {
                MessageBox.Show("Please select a task to edit.");
                return;
            }
            var window = new AddEditTaskWindow(task, _tasks);
            window.Owner = this;
            if (window.ShowDialog() == true)
            {
                SaveTasks();
                _tasksView?.Refresh();
                UpdateDetails();
            }
        }

        private void CompleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (TasksGrid.SelectedItem is not TaskItem task)
            {
                MessageBox.Show("Please select a task to mark complete.");
                return;
            }
            if (!task.Completed)
            {
                task.Completed = true;
                SaveTasks();
                _tasksView?.Refresh();
                UpdateDetails();
            }
            else
            {
                MessageBox.Show("Selected task is already marked as complete.");
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (TasksGrid.SelectedItem is not TaskItem task)
            {
                MessageBox.Show("Please select a task to delete.");
                return;
            }
            var confirm = MessageBox.Show($"Delete task '{task.Title}'? This will remove the task permanently.", "Delete Task", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
                return;
            // Remove task-level attachments that are no longer referenced anywhere else
            foreach (var relName in task.Attachments)
            {
                bool stillUsed = false;
                foreach (var t in _tasks)
                {
                    if (t == task) continue;
                    if (t.Attachments.Contains(relName)) { stillUsed = true; break; }
                    foreach (var st in t.SubTasks)
                    {
                        if (st.Attachments.Contains(relName)) { stillUsed = true; break; }
                    }
                    if (stillUsed) break;
                }
                if (stillUsed)
                    continue;
                if (Path.IsPathRooted(relName))
                    continue;
                var attachPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AttachmentsDir, relName);
                if (File.Exists(attachPath))
                {
                    try { File.Delete(attachPath); } catch { }
                }
            }
            // Remove attachments from subtasks that are no longer referenced anywhere else
            foreach (var st in task.SubTasks)
            {
                foreach (var relName in st.Attachments)
                {
                    bool stillUsed = false;
                    foreach (var t in _tasks)
                    {
                        if (t == task) continue;
                        if (t.Attachments.Contains(relName)) { stillUsed = true; break; }
                        foreach (var st2 in t.SubTasks)
                        {
                            if (st2.Attachments.Contains(relName)) { stillUsed = true; break; }
                        }
                        if (stillUsed) break;
                    }
                    if (stillUsed)
                        continue;
                    if (Path.IsPathRooted(relName))
                        continue;
                    var attachPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AttachmentsDir, relName);
                    if (File.Exists(attachPath))
                    {
                        try { File.Delete(attachPath); } catch { }
                    }
                }
            }
            _tasks.Remove(task);
            SaveTasks();
            _tasksView?.Refresh();
            UpdateDetails();
        }

        private void Subtask_CheckChanged(object sender, RoutedEventArgs e)
        {
            // Persist subtask state changes immediately
            SaveTasks();
        }

        private void ReminderToggle_Changed(object sender, RoutedEventArgs e)
        {
            UpdateReminderSettingsFromUi();
        }

        private void ReminderLeadTimeTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdateReminderSettingsFromUi();
        }

        private void ReminderLeadTimeTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                UpdateReminderSettingsFromUi();
                e.Handled = true;
            }
        }
    }
}