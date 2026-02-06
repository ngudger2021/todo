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
        private readonly List<Guid> _dependencies = new();
        private readonly ObservableCollection<string> _dependencyDisplay = new();
        private RecurrenceRule? _recurrence;
        private const string AttachmentsDir = "attachments";

        public AddEditTaskWindow(TaskItem? task, ObservableCollection<TaskItem> allTasks)
        {
            InitializeComponent();
            _existingTask = task;
            _allTasks = allTasks;
            Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AttachmentsDir));
            if (task != null)
            {
                Title = "Edit Task";
                TitleTextBox.Text = task.Title;
                DescriptionTextBox.Text = task.Description;
                DescriptionMarkdownCheckBox.IsChecked = task.DescriptionIsMarkdown;
                var existingTags = task.Tags ?? new List<string>();
                TagsTextBox.Text = existingTags.Count == 0 ? string.Empty : string.Join(", ", existingTags);
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

                // Load recurrence
                _recurrence = task.Recurrence;
                if (_recurrence != null && _recurrence.Type != RecurrenceType.None)
                {
                    RecurrenceTypeComboBox.SelectedIndex = (int)_recurrence.Type;
                    RecurrenceIntervalTextBox.Text = _recurrence.Interval.ToString();
                }

                // Load dependencies
                if (task.DependsOn != null)
                {
                    foreach (var depId in task.DependsOn)
                    {
                        var depTask = _allTasks.FirstOrDefault(t => t.Id == depId);
                        if (depTask != null)
                        {
                            _dependencies.Add(depId);
                            _dependencyDisplay.Add(depTask.Title);
                        }
                    }
                }

                // Load note
                NoteTextBox.Text = task.Note;
                NoteMarkdownCheckBox.IsChecked = task.NoteIsMarkdown;
                UpdateDescriptionPreview();
                UpdateNotePreview();
            }
            else
            {
                Title = "Add Task";
                PriorityComboBox.SelectedIndex = 1;
                TagsTextBox.Text = string.Empty;
                NoteTextBox.Text = string.Empty;
                DescriptionMarkdownCheckBox.IsChecked = false;
                NoteMarkdownCheckBox.IsChecked = true;
            }
            AttachmentsListBox.ItemsSource = _attachmentDisplay;
            SubtasksListBox.ItemsSource = _subTasks;
            DependenciesListBox.ItemsSource = _dependencyDisplay;
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
                _existingTask.DescriptionIsMarkdown = DescriptionMarkdownCheckBox.IsChecked == true;
                _existingTask.DueDate = dueDate;
                _existingTask.Priority = priority;
                _existingTask.Attachments = attachmentsDest;
                _existingTask.SubTasks = newSubTasks;
                _existingTask.Tags = tags;
                _existingTask.Note = NoteTextBox.Text.Trim();
                _existingTask.NoteIsMarkdown = NoteMarkdownCheckBox.IsChecked == true;
                _existingTask.Recurrence = BuildRecurrenceRule();
                _existingTask.DependsOn = new List<Guid>(_dependencies);
            }
            else
            {
                var newTask = new TaskItem
                {
                    Id = Guid.NewGuid(),
                    Title = title,
                    Description = description,
                    DescriptionIsMarkdown = DescriptionMarkdownCheckBox.IsChecked == true,
                    CreatedAt = DateTime.Now,
                    DueDate = dueDate,
                    Priority = priority,
                    Completed = false,
                    Attachments = attachmentsDest,
                    SubTasks = newSubTasks,
                    Tags = tags,
                    Note = NoteTextBox.Text.Trim(),
                    NoteIsMarkdown = NoteMarkdownCheckBox.IsChecked == true,
                    Recurrence = BuildRecurrenceRule(),
                    DependsOn = new List<Guid>(_dependencies)
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
            if (RecurrenceTypeComboBox == null || RecurrenceIntervalPanel == null)
                return;

            var selectedIndex = RecurrenceTypeComboBox.SelectedIndex;
            if (selectedIndex > 0) // Not "None"
            {
                RecurrenceIntervalPanel.Visibility = Visibility.Visible;
                RecurrenceIntervalLabel.Text = selectedIndex switch
                {
                    1 => "day(s)",
                    2 => "week(s)",
                    3 => "month(s)",
                    _ => "unit(s)"
                };
            }
            else
            {
                RecurrenceIntervalPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void AddDependency_Click(object sender, RoutedEventArgs e)
        {
            // Show a list of available tasks to select as dependency
            var availableTasks = _allTasks
                .Where(t => t != _existingTask) // Can't depend on self
                .ToList();

            if (!availableTasks.Any())
            {
                MessageBox.Show("No other tasks available to add as dependency.");
                return;
            }

            var selectionWindow = new Window
            {
                Title = "Select Dependency",
                Width = 400,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var listBox = new ListBox
            {
                ItemsSource = availableTasks,
                DisplayMemberPath = "Title",
                Margin = new Thickness(10)
            };

            var okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5)
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5)
            };

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            var mainPanel = new DockPanel();
            DockPanel.SetDock(buttonPanel, Dock.Bottom);
            mainPanel.Children.Add(buttonPanel);
            mainPanel.Children.Add(listBox);

            selectionWindow.Content = mainPanel;

            TaskItem? selectedTask = null;
            okButton.Click += (s, args) =>
            {
                selectedTask = listBox.SelectedItem as TaskItem;
                selectionWindow.DialogResult = true;
                selectionWindow.Close();
            };
            cancelButton.Click += (s, args) =>
            {
                selectionWindow.DialogResult = false;
                selectionWindow.Close();
            };

            if (selectionWindow.ShowDialog() == true && selectedTask != null)
            {
                if (!_dependencies.Contains(selectedTask.Id))
                {
                    _dependencies.Add(selectedTask.Id);
                    _dependencyDisplay.Add(selectedTask.Title);
                }
            }
        }

        private void RemoveDependency_Click(object sender, RoutedEventArgs e)
        {
            int index = DependenciesListBox.SelectedIndex;
            if (index >= 0 && index < _dependencies.Count)
            {
                _dependencies.RemoveAt(index);
                _dependencyDisplay.RemoveAt(index);
            }
        }

        private RecurrenceRule? BuildRecurrenceRule()
        {
            var selectedIndex = RecurrenceTypeComboBox.SelectedIndex;
            if (selectedIndex == 0) // None
                return null;

            int interval = 1;
            if (!int.TryParse(RecurrenceIntervalTextBox.Text, out interval) || interval < 1)
                interval = 1;

            return new RecurrenceRule
            {
                Type = (RecurrenceType)selectedIndex,
                Interval = interval
            };
        }

        private void NoteMarkdownCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateNotePreview();
        }

        private void NoteTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateNotePreview();
        }

        private void DescriptionMarkdownCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateDescriptionPreview();
        }

        private void DescriptionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateDescriptionPreview();
        }

        private void UpdateDescriptionPreview()
        {
            if (DescriptionMarkdownPreview == null || DescriptionMarkdownCheckBox == null || DescriptionTextBox == null)
            {
                return;
            }

            bool isMarkdown = DescriptionMarkdownCheckBox.IsChecked == true;
            DescriptionMarkdownPreview.Visibility = isMarkdown ? Visibility.Visible : Visibility.Collapsed;
            if (isMarkdown)
            {
                DescriptionMarkdownPreview.Document = MarkdownRenderer.ToFlowDocument(DescriptionTextBox.Text);
            }
        }

        private void UpdateNotePreview()
        {
            if (NoteMarkdownPreview == null || NoteMarkdownCheckBox == null || NoteTextBox == null)
            {
                return;
            }

            bool isMarkdown = NoteMarkdownCheckBox.IsChecked == true;
            NoteMarkdownPreview.Visibility = isMarkdown ? Visibility.Visible : Visibility.Collapsed;
            if (isMarkdown)
            {
                NoteMarkdownPreview.Document = MarkdownRenderer.ToFlowDocument(NoteTextBox.Text);
            }
        }

    }
}
