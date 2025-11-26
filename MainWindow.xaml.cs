using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Globalization;
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

            RefreshTagFilterOptions();
            LoadReminderSettings();
            ApplyReminderSettingsToUi();
        }

        public ReminderSettings GetReminderSettings() => _reminderSettings;

        internal void SaveTasksAndRefresh()
        {
            SaveTasks();
            _tasksView?.Refresh();
            UpdateDetails();
        }

        /// <summary>
        /// Predicate for filtering tasks based on the selected filter option.
        /// </summary>
        private bool TaskFilter(object obj)
        {
            if (obj is not TaskItem task)
                return false;
            string filter = (StatusFilterComboBox?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All";
            if (!(filter == "All" || (filter == "Pending" && !task.Completed) || (filter == "Completed" && task.Completed)))
            {
                return false;
            }

            string selectedTag = TagFilterComboBox?.SelectedItem as string ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(selectedTag) && selectedTag != "All tags" && !task.Tags.Any(t => string.Equals(t, selectedTag, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            string search = SearchTextBox?.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(search))
            {
                var comparison = StringComparison.OrdinalIgnoreCase;
                bool matches = (task.Title?.IndexOf(search, comparison) >= 0)
                    || (task.Description?.IndexOf(search, comparison) >= 0)
                    || task.Tags.Any(t => t.IndexOf(search, comparison) >= 0);
                if (!matches)
                {
                    return false;
                }
            }

            return true;
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
                    options.Converters.Add(new JsonStringEnumConverter());
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
                                st.Tags ??= new List<string>();
                            }
                            t.Tags ??= new List<string>();
                            if (t.RecurrenceInterval <= 0)
                            {
                                t.RecurrenceInterval = 1;
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
                options.Converters.Add(new JsonStringEnumConverter());
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
                TagsPanel.Children.Clear();
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
            // Tags
            TagsPanel.Children.Clear();
            if (task.Tags.Count > 0)
            {
                foreach (var tag in task.Tags)
                {
                    TagsPanel.Children.Add(new Border
                    {
                        Background = System.Windows.Media.Brushes.LightGray,
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(5, 2, 5, 2),
                        Margin = new Thickness(0, 0, 5, 5),
                        Child = new TextBlock { Text = tag }
                    });
                }
            }
            else
            {
                TagsPanel.Children.Add(new TextBlock { Text = "No tags" });
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
                RefreshTagFilterOptions();
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
                RefreshTagFilterOptions();
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
                var nextTask = CreateNextRecurringInstance(task);
                if (nextTask != null)
                {
                    _tasks.Add(nextTask);
                }
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
            RefreshTagFilterOptions();
            UpdateDetails();
        }

        private void CalendarButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new CalendarView(_tasks, SaveTasksAndRefresh)
            {
                Owner = this
            };
            window.Show();
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

        private void FilterBar_Changed(object sender, SelectionChangedEventArgs e)
        {
            _tasksView?.Refresh();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _tasksView?.Refresh();
        }

        private void RefreshTagFilterOptions()
        {
            if (TagFilterComboBox == null)
                return;

            var selected = TagFilterComboBox.SelectedItem as string;
            TagFilterComboBox.Items.Clear();
            TagFilterComboBox.Items.Add("All tags");
            var tags = _tasks
                .SelectMany(t => t.Tags ?? new List<string>())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t)
                .ToList();
            foreach (var tag in tags)
            {
                TagFilterComboBox.Items.Add(tag);
            }

            if (selected != null && TagFilterComboBox.Items.Contains(selected))
            {
                TagFilterComboBox.SelectedItem = selected;
            }
            else
            {
                TagFilterComboBox.SelectedIndex = 0;
            }
        }

        private void GroupByComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_tasksView == null || GroupByComboBox == null)
                return;

            _tasksView.GroupDescriptions.Clear();
            var selection = (GroupByComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (selection == "Due Date")
            {
                _tasksView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(TaskItem.DueDate), new DueDateGroupConverter()));
            }
            else if (selection == "Tag")
            {
                _tasksView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(TaskItem.Tags), new TagGroupConverter()));
            }

            _tasksView.Refresh();
        }

        private TaskItem? CreateNextRecurringInstance(TaskItem task)
        {
            if (task.RecurrenceType == RecurrenceType.None || !task.DueDate.HasValue)
            {
                return null;
            }

            int interval = Math.Max(1, task.RecurrenceInterval);
            DateTime nextDue = task.DueDate.Value;
            try
            {
                nextDue = task.RecurrenceType switch
                {
                    RecurrenceType.Daily => nextDue.AddDays(interval),
                    RecurrenceType.Weekly => nextDue.AddDays(7 * interval),
                    RecurrenceType.Monthly => nextDue.AddMonths(interval),
                    _ => nextDue
                };
            }
            catch
            {
                return null;
            }

            if (task.RecursUntil.HasValue && nextDue.Date > task.RecursUntil.Value.Date)
            {
                return null;
            }

            return new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = task.Title,
                Description = task.Description,
                DueDate = nextDue,
                Priority = task.Priority,
                Completed = false,
                IsMarkdown = task.IsMarkdown,
                Attachments = new List<string>(task.Attachments ?? new List<string>()),
                SubTasks = task.SubTasks.Select(st => new SubTask
                {
                    Title = st.Title,
                    Description = st.Description,
                    Completed = false,
                    DueDate = st.DueDate,
                    Priority = st.Priority,
                    Attachments = new List<string>(st.Attachments ?? new List<string>()),
                    IsMarkdown = st.IsMarkdown,
                    Tags = new List<string>(st.Tags ?? new List<string>())
                }).ToList(),
                Tags = new List<string>(task.Tags ?? new List<string>()),
                RecurrenceType = task.RecurrenceType,
                RecurrenceInterval = interval,
                RecursUntil = task.RecursUntil
            };
        }

        private class DueDateGroupConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value is DateTime dt)
                {
                    return dt.Date.ToShortDateString();
                }
                return "No due date";
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
        }

        private class TagGroupConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value is List<string> tags && tags.Count > 0)
                {
                    return tags[0];
                }
                return "No tags";
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
        }
    }
}