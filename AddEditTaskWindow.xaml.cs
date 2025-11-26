using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using TodoWpfApp.Models;
using System.Windows.Controls;

namespace TodoWpfApp
{
    /// <summary>
    /// Interaction logic for AddEditTaskWindow.xaml
    /// </summary>
    public partial class AddEditTaskWindow : Window
    {
        private readonly TaskItem? _existingTask;
        private readonly ObservableCollection<TaskItem> _allTasks;
        // List of attachments where bool indicates if the path refers to a new file (to be copied)
        private readonly List<(bool IsNew, string Path)> _attachments = new();
        private readonly ObservableCollection<string> _attachmentDisplay = new();
        private readonly ObservableCollection<SubTask> _subTasks = new();
        private const string AttachmentsDir = "attachments";

        public AddEditTaskWindow(TaskItem? task, ObservableCollection<TaskItem> allTasks)
        {
            InitializeComponent();
            _existingTask = task;
            _allTasks = allTasks;
            if (task != null)
            {
                Title = "Edit Task";
                TitleTextBox.Text = task.Title;
                DescriptionTextBox.Text = task.Description;
                TagsTextBox.Text = task.Tags.Count == 0 ? string.Empty : string.Join(", ", task.Tags);
                if (task.DueDate.HasValue)
                    DueDatePicker.SelectedDate = task.DueDate.Value;
                foreach (ComboBoxItem item in PriorityComboBox.Items)
                {
                    if ((item.Content?.ToString() ?? "").Equals(task.Priority, StringComparison.OrdinalIgnoreCase))
                    {
                        PriorityComboBox.SelectedItem = item;
                        break;
                    }
                }
                foreach (var relName in task.Attachments)
                {
                    _attachments.Add((false, relName));
                    _attachmentDisplay.Add(System.IO.Path.GetFileName(relName));
                }
                foreach (var st in task.SubTasks)
                {
                    // Clone each subtask so that edits do not modify the original until save
                    _subTasks.Add(new SubTask
                    {
                        Title = st.Title,
                        Completed = st.Completed,
                        Description = st.Description,
                        DueDate = st.DueDate,
                        Priority = st.Priority,
                        Attachments = new List<string>(st.Attachments ?? new List<string>()),
                        Tags = new List<string>(st.Tags ?? new List<string>())
                    });
                }
                SetRecurrenceTypeSelection(task.RecurrenceType);
                RecurrenceIntervalTextBox.Text = Math.Max(1, task.RecurrenceInterval).ToString();
                RecursUntilDatePicker.SelectedDate = task.RecursUntil;
            }
            else
            {
                Title = "Add Task";
                PriorityComboBox.SelectedIndex = 1;
                TagsTextBox.Text = string.Empty;
                SetRecurrenceTypeSelection(RecurrenceType.None);
                RecurrenceIntervalTextBox.Text = "1";
            }
            AttachmentsListBox.ItemsSource = _attachmentDisplay;
            SubtasksListBox.ItemsSource = _subTasks;
            UpdateRecurrenceControlsEnabled(GetSelectedRecurrenceType());
        }

        private void AddAttachment_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Title = "Select attachments",
                Multiselect = true
            };
            if (ofd.ShowDialog() == true)
            {
                foreach (var file in ofd.FileNames)
                {
                    _attachments.Add((true, file));
                    _attachmentDisplay.Add(System.IO.Path.GetFileName(file));
                }
            }
        }

        private void RemoveAttachment_Click(object sender, RoutedEventArgs e)
        {
            int index = AttachmentsListBox.SelectedIndex;
            if (index >= 0 && index < _attachments.Count)
            {
                _attachments.RemoveAt(index);
                _attachmentDisplay.RemoveAt(index);
            }
        }

        private void AddSubtask_Click(object sender, RoutedEventArgs e)
        {
            // Create a new subtask with default values; initially no details
            _subTasks.Add(new SubTask { Title = string.Empty, Completed = false, Description = string.Empty, DueDate = null, Priority = "Medium", Attachments = new List<string>(), Tags = new List<string>() });
        }

        private void RemoveSubtask_Click(object sender, RoutedEventArgs e)
        {
            if (SubtasksListBox.SelectedItem is SubTask st)
            {
                _subTasks.Remove(st);
            }
        }

        /// <summary>
        /// Handles editing of a selected subtask.  Opens a dedicated dialog that allows
        /// editing of all subtask metadata including description, due date, priority
        /// and attachments.  The dialog modifies the subtask in place or returns
        /// a new subtask which is added to the collection.
        /// </summary>
        private void EditSubtask_Click(object sender, RoutedEventArgs e)
        {
            if (SubtasksListBox.SelectedItem is not SubTask selected)
            {
                MessageBox.Show("Please select a subtask to edit.");
                return;
            }
            // Open the subtask editor.  We pass the existing subtask and the full task list for
            // attachment reference checking.  New tasks have no identifier yet but the list
            // still contains the parent's attachments and subtasks as clones.
            var subtaskWindow = new AddEditSubtaskWindow(selected, _allTasks)
            {
                Owner = this
            };
            if (subtaskWindow.ShowDialog() == true)
            {
                // If a new subtask is created, add it to our collection.  When editing,
                // the selected object is modified in place.
                if (subtaskWindow.CreatedSubtask != null)
                {
                    _subTasks.Add(subtaskWindow.CreatedSubtask);
                }
                // Changes to the ObservableCollection automatically update the UI.  No explicit refresh required.
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string title = TitleTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                MessageBox.Show("Task title cannot be empty.");
                return;
            }
            string description = DescriptionTextBox.Text.Trim();
            DateTime? dueDate = DueDatePicker.SelectedDate;
            string priority = (PriorityComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Medium";
            List<string> tags = ParseTags(TagsTextBox.Text);
            RecurrenceType recurrenceType = GetSelectedRecurrenceType();
            if (!int.TryParse(RecurrenceIntervalTextBox.Text, out var recurrenceInterval) || recurrenceInterval <= 0)
            {
                MessageBox.Show("Recurrence interval must be a positive number.");
                return;
            }
            DateTime? recursUntil = RecursUntilDatePicker.SelectedDate;
            if (recurrenceType != RecurrenceType.None)
            {
                if (!dueDate.HasValue)
                {
                    MessageBox.Show("A due date is required for recurring tasks.");
                    return;
                }
                if (recursUntil.HasValue && recursUntil.Value.Date < DateTime.Today)
                {
                    MessageBox.Show("Recurrence end date cannot be in the past.");
                    return;
                }
                if (recursUntil.HasValue && recursUntil.Value.Date < dueDate.Value.Date)
                {
                    MessageBox.Show("Recurrence end date must be on or after the first due date.");
                    return;
                }
            }
            else
            {
                recursUntil = null;
            }
            List<string> attachmentsDest = new();
            string attachmentsBaseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AttachmentsDir);
            foreach (var entry in _attachments)
            {
                bool isNew = entry.IsNew;
                string path = entry.Path;
                if (isNew)
                {
                    string baseName = System.IO.Path.GetFileName(path);
                    string uniqueName = $"{Guid.NewGuid()}_{baseName}";
                    string destPath = Path.Combine(attachmentsBaseDir, uniqueName);
                    try
                    {
                        File.Copy(path, destPath, overwrite: true);
                        attachmentsDest.Add(uniqueName);
                    }
                    catch
                    {
                        MessageBox.Show($"Failed to copy attachment '{baseName}'.");
                        return;
                    }
                }
                else
                {
                    attachmentsDest.Add(System.IO.Path.GetFileName(path));
                }
            }
            var newSubTasks = _subTasks.Select(st => new SubTask
            {
                Title = st.Title,
                Completed = st.Completed,
                Description = st.Description,
                DueDate = st.DueDate,
                Priority = st.Priority,
                Attachments = new List<string>(st.Attachments ?? new List<string>()),
                Tags = new List<string>(st.Tags ?? new List<string>())
            }).ToList();
            string attachmentsDirFull = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AttachmentsDir);
            if (_existingTask != null)
            {
                // Remove attachments no longer referenced
                foreach (var oldRel in _existingTask.Attachments.ToList())
                {
                    if (!attachmentsDest.Contains(oldRel))
                    {
                        bool stillUsed = _allTasks.Any(t =>
                            t != _existingTask &&
                            (t.Attachments.Contains(oldRel) || t.SubTasks.Any(st => st.Attachments.Contains(oldRel))));

                        // Preserve if any of the updated subtasks still reference the attachment
                        if (!stillUsed)
                        {
                            stillUsed = newSubTasks.Any(st => st.Attachments.Contains(oldRel));
                        }
                        if (!stillUsed && !Path.IsPathRooted(oldRel))
                        {
                            string oldPath = Path.Combine(attachmentsDirFull, oldRel);
                            if (File.Exists(oldPath))
                            {
                                try { File.Delete(oldPath); } catch { }
                            }
                        }
                    }
                }
                _existingTask.Title = title;
                _existingTask.Description = description;
                _existingTask.DueDate = dueDate;
                _existingTask.Priority = priority;
                _existingTask.Attachments = attachmentsDest;
                _existingTask.SubTasks = newSubTasks;
                _existingTask.Tags = tags;
                _existingTask.RecurrenceType = recurrenceType;
                _existingTask.RecurrenceInterval = recurrenceInterval;
                _existingTask.RecursUntil = recursUntil;
            }
            else
            {
                var newTask = new TaskItem
                {
                    Id = Guid.NewGuid(),
                    Title = title,
                    Description = description,
                    CreatedAt = DateTime.Now,
                    DueDate = dueDate,
                    Priority = priority,
                    Completed = false,
                    Attachments = attachmentsDest,
                    SubTasks = newSubTasks,
                    Tags = tags,
                    RecurrenceType = recurrenceType,
                    RecurrenceInterval = recurrenceInterval,
                    RecursUntil = recursUntil
                };
                _allTasks.Add(newTask);
            }
            DialogResult = true;
            Close();
        }

        private static List<string> ParseTags(string input)
        {
            return input
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void RecurrenceTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateRecurrenceControlsEnabled(GetSelectedRecurrenceType());
        }

        private RecurrenceType GetSelectedRecurrenceType()
        {
            if (RecurrenceTypeComboBox.SelectedItem is ComboBoxItem item && item.Tag is RecurrenceType recurrenceType)
            {
                return recurrenceType;
            }

            return RecurrenceType.None;
        }

        private void SetRecurrenceTypeSelection(RecurrenceType recurrenceType)
        {
            foreach (ComboBoxItem item in RecurrenceTypeComboBox.Items)
            {
                if (item.Tag is RecurrenceType rt && rt == recurrenceType)
                {
                    RecurrenceTypeComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private void UpdateRecurrenceControlsEnabled(RecurrenceType recurrenceType)
        {
            bool enabled = recurrenceType != RecurrenceType.None;
            RecurrenceIntervalTextBox.IsEnabled = enabled;
            RecursUntilDatePicker.IsEnabled = enabled;
        }
    }
}