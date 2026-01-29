using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Globalization;
using System.Windows.Input;
using TodoWpfApp.Models;
using System.Text.Json.Serialization;
using System.Windows.Media;
using System.Windows.Threading;
using TodoWpfApp.Services;
using Microsoft.Win32;
using System.Speech.Recognition;

namespace TodoWpfApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<TaskItem> _tasks = new();
        private readonly ObservableCollection<TaskHistoryEntry> _taskHistory = new();
        private ICollectionView? _tasksView;
        private const string DataFile = "todo_data.json";
        private const string AttachmentsDir = "attachments";
        private const string SettingsFile = "user_settings.json";
        private const string NotificationsFile = "notifications.json";
        private ReminderSettings _reminderSettings = ReminderSettings.CreateDefault();
        private readonly ObservableCollection<NotificationItem> _notifications = new();
        private NotificationCenterWindow? _notificationCenterWindow;
        private DispatcherTimer? _notificationFlashTimer;
        private bool _notificationFlashOn;
        private Brush? _notificationDefaultBackground;
        private Brush? _notificationDefaultForeground;
        private ISyncService? _syncService;
        private SpeechRecognitionEngine? _speechRecognizer;
        private bool _isListening = false;
        private TaskItem? _draggedTask;

        public ObservableCollection<TaskItem> Tasks => _tasks;
        public ObservableCollection<TaskHistoryEntry> TaskHistory => _taskHistory;

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
            LoadNotifications();
            InitializeNotificationIndicator();
            InitializeSyncService();
            InitializeKeyboardCommands();
            InitializeSpeechRecognition();
        }

        private void TasksGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (TasksGrid.SelectedItem is not TaskItem task)
            {
                MessageBox.Show("Please select a task to edit.");
                return;
            }
            EndTasksGridEdit();
            var window = new AddEditTaskWindow(task, _tasks);
            window.Owner = this;
            if (window.ShowDialog() == true)
            {
                SaveTasks();
                RefreshTasksViewSafe();
                UpdateDetails();
                RefreshTagFilterOptions();
            }
        }


        public ReminderSettings GetReminderSettings() => _reminderSettings;

        public void HandleReminderNotification(TaskItem task)
        {
            AddNotificationFromTask(task);
        }

        internal void SaveTasksAndRefresh()
        {
            SyncHistoryMetadata();
            SaveTasks();
            RefreshTasksViewSafe();
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
        /// Load tasks and history from the JSON data file.  If the file does not exist or
        /// cannot be parsed, start with an empty collection.
        /// </summary>
        private void LoadTasks()
        {
            _tasks.Clear();
            _taskHistory.Clear();
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DataFile);
            try
            {
                if (!File.Exists(filePath))
                {
                    return;
                }

                string json = File.ReadAllText(filePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                };

                TaskDataContainer? container = null;
                try
                {
                    container = JsonSerializer.Deserialize<TaskDataContainer>(json, options);
                }
                catch
                {
                    // fallback to old format below
                }

                if (container?.Tasks != null && container.Tasks.Count > 0)
                {
                    LoadTasksIntoCollection(container.Tasks);
                    if (container.History != null)
                    {
                        foreach (var entry in container.History)
                        {
                            _taskHistory.Add(entry);
                        }
                    }
                }
                else
                {
                    // Attempt legacy format: a flat list of tasks
                    var loaded = JsonSerializer.Deserialize<List<TaskItem>>(json, options);
                    if (loaded != null)
                    {
                        LoadTasksIntoCollection(loaded);
                    }
                }
            }
            catch
            {
                // ignore errors and start with empty list
            }

            foreach (var task in _tasks)
            {
                EnsureHistoryEntry(task);
            }
        }

        private void LoadTasksIntoCollection(IEnumerable<TaskItem> tasks)
        {
            foreach (var t in tasks)
            {
                t.Attachments ??= new List<string>();
                t.SubTasks ??= new List<SubTask>();
                foreach (var st in t.SubTasks)
                {
                    st.Attachments ??= new List<string>();
                    st.Tags ??= new List<string>();
                }
                t.Tags ??= new List<string>();
                if (t.CreatedAt == default)
                {
                    t.CreatedAt = DateTime.Now;
                }
                if (t.Completed && !t.CompletedAt.HasValue)
                {
                    t.CompletedAt = t.CreatedAt;
                }
                _tasks.Add(t);
            }
        }

        /// <summary>
        /// Save the current tasks and history collection to disk as JSON.
        /// </summary>
        private void SaveTasks()
        {
            try
            {
                SyncHistoryMetadata();
                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DataFile);
                var options = new JsonSerializerOptions { WriteIndented = true };
                options.Converters.Add(new JsonStringEnumConverter());
                var container = new TaskDataContainer
                {
                    Tasks = _tasks.ToList(),
                    History = _taskHistory.ToList()
                };
                string json = JsonSerializer.Serialize(container, options);
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
                AttachmentsPanel.Children.Clear();
                TagsPanel.Children.Clear();
                SubtasksDetailsPanel.ItemsSource = null;
                return;
            }
            // Description
            string description = string.IsNullOrWhiteSpace(task.Description) ? "No description" : task.Description;
            DescriptionText.Text = description;
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
                        Process.Start(new ProcessStartInfo
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

        /// <summary>
        /// Opens a subtask attachment using the relative file name stored with the subtask.
        /// </summary>
        private void SubtaskAttachment_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock tb && tb.DataContext is string relName)
            {
                var attachPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AttachmentsDir, relName);
                try
                {
                    if (File.Exists(attachPath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = attachPath,
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        MessageBox.Show($"Attachment not found: {relName}");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Unable to open attachment: {relName}\n{ex.Message}");
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
            EndTasksGridEdit();
            var window = new AddEditTaskWindow(task, _tasks);
            window.Owner = this;
            if (window.ShowDialog() == true)
            {
                SaveTasks();
                RefreshTasksViewSafe();
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
            if (task.SubTasks.Any(st => !st.Completed))
            {
                MessageBox.Show("All subtasks must be completed before completing the parent task.");
                return;
            }
            if (!CanCompleteTask(task))
            {
                return;
            }
            if (!task.Completed)
            {
                task.Completed = true;
                if (!task.CompletedAt.HasValue)
                {
                    task.CompletedAt = DateTime.Now;
                }
                var entry = EnsureHistoryEntry(task);
                entry.CompletedAt ??= task.CompletedAt;
                HandleRecurringTaskCompletion(task);
                SaveTasksAndRefresh();
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
            // Remove attachments from the task and all its subtasks
            var attachmentNames = new HashSet<string>(task.Attachments ?? new List<string>());
            foreach (var st in task.SubTasks)
            {
                foreach (var relName in st.Attachments)
                {
                    attachmentNames.Add(relName);
                }
            }
            foreach (var relName in attachmentNames)
            {
                if (Path.IsPathRooted(relName))
                {
                    continue;
                }
                var attachPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AttachmentsDir, relName);
                if (File.Exists(attachPath))
                {
                    try { File.Delete(attachPath); } catch { }
                }
            }
            var historyEntry = EnsureHistoryEntry(task);
            historyEntry.CompletedAt ??= task.CompletedAt;
            historyEntry.DeletedAt = DateTime.Now;
            _tasks.Remove(task);
            SaveTasksAndRefresh();
            RefreshTagFilterOptions();
        }

        private void HistoryButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new TaskHistoryWindow(_taskHistory)
            {
                Owner = this
            };
            window.Show();
        }

        private void NotificationsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_notificationCenterWindow == null)
            {
                _notificationCenterWindow = new NotificationCenterWindow(_notifications)
                {
                    Owner = this
                };
                _notificationCenterWindow.OpenTaskRequested += OpenTaskById;
                _notificationCenterWindow.NotificationsChanged += () =>
                {
                    SaveNotifications();
                    UpdateNotificationIndicator();
                };
                _notificationCenterWindow.Closed += (_, _) => _notificationCenterWindow = null;
            }

            _notificationCenterWindow.Show();
            _notificationCenterWindow.Activate();
        }

        private void Subtask_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (TasksGrid.SelectedItem is TaskItem task)
            {
                if (task.SubTasks.Any(st => !st.Completed) && task.Completed)
                {
                    task.Completed = false;
                    task.CompletedAt = null;
                    var historyEntry = EnsureHistoryEntry(task);
                    historyEntry.CompletedAt = null;
                }
            }
            // Persist subtask state changes immediately
            SaveTasksAndRefresh();
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
            RefreshTasksViewSafe();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshTasksViewSafe();
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

        private void EndTasksGridEdit()
        {
            // Ensure the grid is not mid-edit before we refresh the view.
            if (TasksGrid == null)
            {
                return;
            }
            TasksGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            TasksGrid.CommitEdit(DataGridEditingUnit.Row, true);
        }

        private void RefreshTasksViewSafe()
        {
            if (_tasksView is IEditableCollectionView editable)
            {
                if (editable.IsAddingNew)
                {
                    editable.CommitNew();
                }
                if (editable.IsEditingItem)
                {
                    editable.CommitEdit();
                }
            }
            _tasksView?.Refresh();
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

        private TaskHistoryEntry EnsureHistoryEntry(TaskItem task)
        {
            var existing = _taskHistory.FirstOrDefault(h => h.TaskId == task.Id);
            if (existing == null)
            {
                existing = TaskHistoryEntry.FromTask(task);
                _taskHistory.Add(existing);
            }
            else
            {
                existing.Title = task.Title;
                existing.Description = task.Description;
                if (task.CompletedAt.HasValue && !existing.CompletedAt.HasValue)
                {
                    existing.CompletedAt = task.CompletedAt;
                }
            }

            return existing;
        }

        private void SyncHistoryMetadata()
        {
            foreach (var task in _tasks)
            {
                EnsureHistoryEntry(task);
            }
        }

        private void InitializeNotificationIndicator()
        {
            if (NotificationsButton == null)
            {
                return;
            }

            _notificationDefaultBackground = NotificationsButton.Background;
            _notificationDefaultForeground = NotificationsButton.Foreground;
            _notificationFlashTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _notificationFlashTimer.Tick += (_, _) =>
            {
                if (!_notifications.Any(n => !n.Read))
                {
                    StopNotificationFlashingInternal();
                    return;
                }

                _notificationFlashOn = !_notificationFlashOn;
                if (_notificationFlashOn)
                {
                    NotificationsButton.Background = Brushes.Gold;
                    NotificationsButton.Foreground = Brushes.Black;
                }
                else
                {
                    NotificationsButton.Background = _notificationDefaultBackground;
                    NotificationsButton.Foreground = _notificationDefaultForeground;
                }
            };
            UpdateNotificationIndicator();
        }

        private void UpdateNotificationIndicator()
        {
            if (NotificationsButton == null)
            {
                return;
            }

            var unreadCount = _notifications.Count(n => !n.Read);
            NotificationsButton.Content = unreadCount > 0 ? $"Notifications ({unreadCount})" : "Notifications";

            if (unreadCount > 0)
            {
                StartNotificationFlashing();
            }
            else
            {
                StopNotificationFlashingInternal();
            }
        }

        private void StartNotificationFlashing()
        {
            if (_notificationFlashTimer == null)
            {
                return;
            }

            if (!_notificationFlashTimer.IsEnabled)
            {
                _notificationFlashTimer.Start();
            }
        }

        private void StopNotificationFlashingInternal()
        {
            if (_notificationFlashTimer == null)
            {
                return;
            }

            if (_notificationFlashTimer.IsEnabled)
            {
                _notificationFlashTimer.Stop();
            }

            _notificationFlashOn = false;
            if (NotificationsButton != null)
            {
                NotificationsButton.Background = _notificationDefaultBackground;
                NotificationsButton.Foreground = _notificationDefaultForeground;
            }
        }

        private void AddNotificationFromTask(TaskItem task)
        {
            if (!task.DueDate.HasValue)
            {
                return;
            }

            var now = DateTime.Now;
            var existing = _notifications.FirstOrDefault(n => n.TaskId == task.Id && n.DueDate == task.DueDate);
            if (existing != null)
            {
                if (existing.SnoozedUntil.HasValue && existing.SnoozedUntil.Value > now)
                {
                    return;
                }

                if (!existing.Read)
                {
                    return;
                }

                existing.Message = $"'{task.Title}' is due by {task.DueDate.Value:g}.";
                existing.CreatedAt = now;
                existing.Read = false;
                existing.SnoozedUntil = null;
            }
            else
            {
                _notifications.Add(new NotificationItem
                {
                    TaskId = task.Id,
                    TaskTitle = task.Title,
                    Message = $"'{task.Title}' is due by {task.DueDate.Value:g}.",
                    DueDate = task.DueDate,
                    CreatedAt = now,
                    Read = false
                });
            }

            SaveNotifications();
            UpdateNotificationIndicator();
        }

        private void LoadNotifications()
        {
            _notifications.Clear();
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, NotificationsFile);
            try
            {
                if (!File.Exists(filePath))
                {
                    return;
                }

                var json = File.ReadAllText(filePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var loaded = JsonSerializer.Deserialize<List<NotificationItem>>(json, options);
                if (loaded != null)
                {
                    foreach (var item in loaded)
                    {
                        _notifications.Add(item);
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        private void SaveNotifications()
        {
            try
            {
                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, NotificationsFile);
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_notifications.ToList(), options);
                File.WriteAllText(filePath, json);
            }
            catch
            {
                // ignore
            }
        }

        private void OpenTaskById(Guid taskId)
        {
            var task = _tasks.FirstOrDefault(t => t.Id == taskId);
            if (task == null)
            {
                MessageBox.Show("Task not found.");
                return;
            }

            if (_tasksView != null)
            {
                var inView = _tasksView.Cast<TaskItem>().Any(t => t.Id == taskId);
                if (!inView)
                {
                    MessageBox.Show("Task is currently filtered out. Adjust filters to view it.");
                    return;
                }
            }

            TasksGrid.SelectedItem = task;
            TasksGrid.ScrollIntoView(task);
            UpdateDetails();
        }

        // ==================== NEW FEATURE IMPLEMENTATIONS ====================

        #region Quick Add with Natural Language Parsing

        private void QuickAddTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ProcessQuickAdd();
                e.Handled = true;
            }
        }

        private void QuickAddButton_Click(object sender, RoutedEventArgs e)
        {
            ProcessQuickAdd();
        }

        private void ProcessQuickAdd()
        {
            string input = QuickAddTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(input))
                return;

            var parsed = NaturalLanguageParser.Parse(input);

            if (string.IsNullOrWhiteSpace(parsed.Title))
            {
                MessageBox.Show("Please enter a task title.");
                return;
            }

            var newTask = new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = parsed.Title,
                DueDate = parsed.DueDate,
                Priority = parsed.Priority,
                Tags = parsed.Tags,
                CreatedAt = DateTime.Now,
                Completed = false
            };

            _tasks.Add(newTask);
            EnsureHistoryEntry(newTask);
            SaveTasks();
            _tasksView?.Refresh();
            RefreshTagFilterOptions();

            QuickAddTextBox.Text = string.Empty;
            MessageBox.Show($"Task added: {newTask.Title}", "Quick Add", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Voice Input

        private void InitializeSpeechRecognition()
        {
            try
            {
                _speechRecognizer = new SpeechRecognitionEngine(new CultureInfo("en-US"));
                _speechRecognizer.SetInputToDefaultAudioDevice();

                // Create a grammar for general dictation
                var dictationGrammar = new DictationGrammar();
                _speechRecognizer.LoadGrammar(dictationGrammar);

                _speechRecognizer.SpeechRecognized += SpeechRecognizer_SpeechRecognized;
            }
            catch
            {
                // Speech recognition not available
                if (VoiceInputButton != null)
                {
                    VoiceInputButton.IsEnabled = false;
                    VoiceInputButton.ToolTip = "Speech recognition not available";
                }
            }
        }

        private void VoiceInputButton_Click(object sender, RoutedEventArgs e)
        {
            if (_speechRecognizer == null)
            {
                MessageBox.Show("Speech recognition is not available on this system.");
                return;
            }

            if (!_isListening)
            {
                try
                {
                    _speechRecognizer.RecognizeAsync(RecognizeMode.Multiple);
                    _isListening = true;
                    VoiceInputButton.Content = "🎤 Listening...";
                    VoiceInputButton.Background = Brushes.LightGreen;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to start voice recognition: {ex.Message}");
                }
            }
        }

        private void SpeechRecognizer_SpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                QuickAddTextBox.Text = e.Result.Text;
                _isListening = false;
                VoiceInputButton.Content = "🎤 Voice";
                VoiceInputButton.Background = SystemColors.ControlBrush;

                // Auto-process if confidence is high
                if (e.Result.Confidence > 0.7)
                {
                    ProcessQuickAdd();
                }
            });
        }

        #endregion

        #region Pomodoro Timer

        private void PomodoroButton_Click(object sender, RoutedEventArgs e)
        {
            if (TasksGrid.SelectedItem is not TaskItem task)
            {
                MessageBox.Show("Please select a task to start a Pomodoro session.");
                return;
            }

            var pomodoroWindow = new PomodoroWindow(task, () =>
            {
                SaveTasks();
                _tasksView?.Refresh();
            });
            pomodoroWindow.Owner = this;
            pomodoroWindow.Show();
        }

        #endregion

        #region Gantt Chart

        private void GanttButton_Click(object sender, RoutedEventArgs e)
        {
            var ganttWindow = new GanttChartWindow(_tasks);
            ganttWindow.Owner = this;
            ganttWindow.Show();
        }

        #endregion

        #region Cloud Sync

        private void InitializeSyncService()
        {
            string dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DataFile);
            _syncService = new FileSyncService(dataPath);
        }

        private async void SyncButton_Click(object sender, RoutedEventArgs e)
        {
            if (_syncService == null)
            {
                MessageBox.Show("Sync service is not initialized.");
                return;
            }

            if (!_syncService.IsSyncConfigured())
            {
                // Show configuration dialog
                var folderDialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Select sync folder (e.g., OneDrive, Dropbox folder)",
                    ShowNewFolderButton = true
                };

                if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _syncService.SetSyncFolder(folderDialog.SelectedPath);
                }
                else
                {
                    return;
                }
            }

            SyncButton.IsEnabled = false;
            SyncButton.Content = "Syncing...";

            try
            {
                var result = await _syncService.SyncAsync();

                if (result.Success)
                {
                    MessageBox.Show(result.Message, "Sync Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Reload if data was downloaded
                    if (result.Action == SyncAction.DownloadedFromRemote || result.Action == SyncAction.Merged)
                    {
                        LoadTasks();
                        _tasksView?.Refresh();
                        UpdateDetails();
                        RefreshTagFilterOptions();
                    }
                }
                else
                {
                    MessageBox.Show(result.Message, "Sync Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Sync error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SyncButton.IsEnabled = true;
                SyncButton.Content = "Sync";
            }
        }

        #endregion

        #region Export

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();

            var csvItem = new MenuItem { Header = "Export to CSV" };
            csvItem.Click += (s, args) => ExportToCsv();
            menu.Items.Add(csvItem);

            var pdfItem = new MenuItem { Header = "Export to Text/PDF" };
            pdfItem.Click += (s, args) => ExportToTextPdf();
            menu.Items.Add(pdfItem);

            var historyItem = new MenuItem { Header = "Export History to CSV" };
            historyItem.Click += (s, args) => ExportHistoryToCsv();
            menu.Items.Add(historyItem);

            menu.PlacementTarget = ExportButton;
            menu.IsOpen = true;
        }

        private void ExportToCsv()
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"tasks_{DateTime.Now:yyyy-MM-dd}.csv"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    ExportService.ExportToCsv(_tasks, saveDialog.FileName);
                    MessageBox.Show("Tasks exported successfully!", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportToTextPdf()
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"tasks_report_{DateTime.Now:yyyy-MM-dd}.txt"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    ExportService.ExportToTextPdf(_tasks, saveDialog.FileName);
                    MessageBox.Show("Report exported successfully!", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportHistoryToCsv()
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"task_history_{DateTime.Now:yyyy-MM-dd}.csv"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    ExportService.ExportHistoryToCsv(_taskHistory, saveDialog.FileName);
                    MessageBox.Show("History exported successfully!", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region Keyboard Shortcuts

        private void InitializeKeyboardCommands()
        {
            // Commands are bound in XAML, but we need to provide implementations
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.New, (s, e) => AddButton_Click(s, e)));
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => DeleteButton_Click(s, e)));
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            string shortcuts = @"Keyboard Shortcuts:

Ctrl+N - Add New Task
Ctrl+E - Edit Selected Task
Ctrl+D - Delete Selected Task
Ctrl+F - Focus Search Box
Ctrl+Enter - Complete Selected Task
F5 - Refresh View

Quick Add Format:
Type naturally: 'Buy milk tomorrow 3pm #shopping !high'
- #tag for tags
- !high, !medium, !low for priority
- today, tomorrow, monday-sunday for dates
- 3pm, 14:00, 9:30am for times";

            MessageBox.Show(shortcuts, "Keyboard Shortcuts", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Statistics

        private void StatisticsButton_Click(object sender, RoutedEventArgs e)
        {
            var statsWindow = new StatisticsWindow(_tasks, _taskHistory);
            statsWindow.Owner = this;
            statsWindow.Show();
        }

        #endregion

        #region Drag and Drop

        private void TasksGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement element)
            {
                var row = FindVisualParent<DataGridRow>(element);
                if (row != null && row.Item is TaskItem task)
                {
                    _draggedTask = task;
                    DragDrop.DoDragDrop(TasksGrid, task, DragDropEffects.Move);
                }
            }
        }

        private void TasksGrid_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void TasksGrid_Drop(object sender, DragEventArgs e)
        {
            if (_draggedTask == null)
                return;

            var targetElement = e.OriginalSource as FrameworkElement;
            var targetRow = FindVisualParent<DataGridRow>(targetElement);

            if (targetRow != null && targetRow.Item is TaskItem targetTask && targetTask != _draggedTask)
            {
                int draggedIndex = _tasks.IndexOf(_draggedTask);
                int targetIndex = _tasks.IndexOf(targetTask);

                if (draggedIndex >= 0 && targetIndex >= 0)
                {
                    _tasks.Move(draggedIndex, targetIndex);

                    // Update sort order
                    for (int i = 0; i < _tasks.Count; i++)
                    {
                        _tasks[i].SortOrder = i;
                    }

                    SaveTasks();
                    _tasksView?.Refresh();
                }
            }

            _draggedTask = null;
        }

        private T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                    return parent;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        #endregion

        #region Recurring Tasks Support

        private void HandleRecurringTaskCompletion(TaskItem task)
        {
            if (task.Recurrence == null || task.Recurrence.Type == RecurrenceType.None)
                return;

            var nextDueDate = task.Recurrence.CalculateNextOccurrence(DateTime.Now);
            if (nextDueDate.HasValue)
            {
                var result = MessageBox.Show(
                    $"This is a recurring task. Create next occurrence due on {nextDueDate.Value:d}?",
                    "Recurring Task",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var nextTask = new TaskItem
                    {
                        Id = Guid.NewGuid(),
                        Title = task.Title,
                        Description = task.Description,
                        Priority = task.Priority,
                        Tags = new List<string>(task.Tags),
                        DueDate = nextDueDate,
                        Recurrence = task.Recurrence,
                        CreatedAt = DateTime.Now,
                        Completed = false
                    };

                    _tasks.Add(nextTask);
                    EnsureHistoryEntry(nextTask);
                    SaveTasks();
                    _tasksView?.Refresh();
                }
            }
        }

        #endregion

        #region Task Dependencies Validation

        private bool CanCompleteTask(TaskItem task)
        {
            if (task.DependsOn == null || !task.DependsOn.Any())
                return true;

            var incompleteDeps = task.DependsOn
                .Select(depId => _tasks.FirstOrDefault(t => t.Id == depId))
                .Where(t => t != null && !t.Completed)
                .ToList();

            if (incompleteDeps.Any())
            {
                var depNames = string.Join(", ", incompleteDeps.Select(t => t!.Title));
                MessageBox.Show(
                    $"Cannot complete this task. The following dependencies must be completed first:\n\n{depNames}",
                    "Incomplete Dependencies",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        #endregion
    }
}
