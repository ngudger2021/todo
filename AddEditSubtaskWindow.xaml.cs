using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using TodoWpfApp.Models;
using System.Windows.Controls;
using System.Windows.Data;

namespace TodoWpfApp
{
    /// <summary>
    /// Interaction logic for AddEditSubtaskWindow.xaml
    /// Provides a UI to create or edit a subtask.  Subtasks support a rich set of
    /// metadata including description, due date, priority, attachments and a flag to
    /// indicate whether the description should be interpreted as Markdown.  The
    /// window copies new attachment files into the application's attachments
    /// directory and removes attachments no longer referenced by any other task or
    /// subtask.
    /// </summary>
    public partial class AddEditSubtaskWindow : Window
    {
        private readonly SubTask? _existingSubtask;
        private readonly ObservableCollection<TaskItem> _allTasks;
        // Pair of (isNew, path) for attachments; new attachments need copying
        private readonly List<(bool IsNew, string Path)> _attachments = new();
        private readonly ObservableCollection<string> _attachmentDisplay = new();
        private const string AttachmentsDir = "attachments";
        private readonly SubTask _markdownBinding = new();

        /// <summary>
        /// When creating a new subtask, this property will contain the newly
        /// constructed subtask once the dialog returns true.  When editing an
        /// existing subtask this property will be null because the existing
        /// instance is modified in place.
        /// </summary>
        public SubTask? CreatedSubtask { get; private set; }

        public AddEditSubtaskWindow(SubTask? subtask, ObservableCollection<TaskItem> allTasks)
        {
            InitializeComponent();
            _existingSubtask = subtask;
            _allTasks = allTasks;
            _markdownBinding.IsMarkdown = subtask?.IsMarkdown ?? false;
            MarkdownCheckBox.DataContext = _markdownBinding;
            MarkdownCheckBox.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(SubTask.IsMarkdown))
            {
                Source = _markdownBinding,
                Mode = BindingMode.TwoWay
            });
            // populate priority combo default index
            PriorityComboBox.SelectedIndex = 1;
            if (subtask != null)
            {
                Title = "Edit Subtask";
                TitleTextBox.Text = subtask.Title;
                DescriptionTextBox.Text = subtask.Description;
                if (subtask.DueDate.HasValue)
                    DueDatePicker.SelectedDate = subtask.DueDate.Value;
                // Select priority in combo
                foreach (ComboBoxItem item in PriorityComboBox.Items)
                {
                    if ((item.Content?.ToString() ?? "").Equals(subtask.Priority, StringComparison.OrdinalIgnoreCase))
                    {
                        PriorityComboBox.SelectedItem = item;
                        break;
                    }
                }
                foreach (var relName in subtask.Attachments)
                {
                    _attachments.Add((false, relName));
                    _attachmentDisplay.Add(Path.GetFileName(relName));
                }
            }
            else
            {
                Title = "Add Subtask";
            }
            AttachmentsListBox.ItemsSource = _attachmentDisplay;
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
                    _attachmentDisplay.Add(Path.GetFileName(file));
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

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string title = TitleTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                MessageBox.Show("Subtask title cannot be empty.");
                return;
            }
            string description = DescriptionTextBox.Text.Trim();
            bool isMarkdown = _markdownBinding.IsMarkdown;
            DateTime? dueDate = DueDatePicker.SelectedDate;
            string priority = (PriorityComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Medium";
            List<string> attachmentsDest = new();
            string attachmentsBaseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AttachmentsDir);
            foreach (var entry in _attachments)
            {
                bool isNew = entry.IsNew;
                string path = entry.Path;
                if (isNew)
                {
                    string baseName = Path.GetFileName(path);
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
                    attachmentsDest.Add(Path.GetFileName(path));
                }
            }
            string attachmentsDirFull = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AttachmentsDir);
            if (_existingSubtask != null)
            {
                // Remove attachments no longer referenced
                foreach (var oldRel in _existingSubtask.Attachments.ToList())
                {
                    if (!attachmentsDest.Contains(oldRel))
                    {
                        bool stillUsed = false;
                        // Check if any other task or subtask uses this attachment
                        foreach (var t in _allTasks)
                        {
                            if (t.Attachments.Contains(oldRel))
                            {
                                stillUsed = true;
                                break;
                            }
                            foreach (var st in t.SubTasks)
                            {
                                if (st.Attachments.Contains(oldRel))
                                {
                                    stillUsed = true;
                                    break;
                                }
                            }
                            if (stillUsed) break;
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
                // Update the existing subtask in place
                _existingSubtask.Title = title;
                _existingSubtask.Description = description;
                _existingSubtask.IsMarkdown = isMarkdown;
                _existingSubtask.DueDate = dueDate;
                _existingSubtask.Priority = priority;
                _existingSubtask.Attachments = attachmentsDest;
                CreatedSubtask = null;
            }
            else
            {
                // Create a new subtask but defer adding it to the parent; caller will handle
                var newSubtask = new SubTask
                {
                    Title = title,
                    Completed = false,
                    Description = description,
                    IsMarkdown = isMarkdown,
                    DueDate = dueDate,
                    Priority = priority,
                    Attachments = attachmentsDest
                };
                CreatedSubtask = newSubtask;
            }
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}