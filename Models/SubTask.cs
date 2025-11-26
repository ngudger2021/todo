using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace TodoWpfApp.Models
{
    /// <summary>
    /// Represents a subtask attached to a parent task.  Each subtask can be
    /// marked complete independently of the parent task.  Subtasks are
    /// persisted inside the parent task’s JSON representation.
    /// </summary>
    public class SubTask : INotifyPropertyChanged
    {
        private string _title = string.Empty;
        private bool _completed;
        private string _description = string.Empty;
        private DateTime? _dueDate;
        private string _priority = "Medium";
        private bool _isMarkdown;
        private List<string> _tags = new();

        /// <summary>
        /// Title of the subtask.  This should be a short summary of what needs to be done.
        /// </summary>
        [JsonPropertyName("title")]
        public string Title
        {
            get => _title;
            set
            {
                if (_title != value)
                {
                    _title = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
                }
            }
        }

        /// <summary>
        /// Indicates whether the subtask has been completed.  Checking or unchecking this
        /// flag will automatically persist changes back to the parent task when the task
        /// collection is saved.
        /// </summary>
        [JsonPropertyName("completed")]
        public bool Completed
        {
            get => _completed;
            set
            {
                if (_completed != value)
                {
                    _completed = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Completed)));
                }
            }
        }

        /// <summary>
        /// Detailed description of the subtask.  This field can contain arbitrary text or
        /// markdown depending on the value of <see cref="IsMarkdown"/>.  The editing
        /// interface provides a resizable text box for entering long descriptions.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
                }
            }
        }

        /// <summary>
        /// Optional due date for the subtask.  When set, this can be used to sort or
        /// prioritise subtasks relative to one another.
        /// </summary>
        [JsonPropertyName("due_date")]
        public DateTime? DueDate
        {
            get => _dueDate;
            set
            {
                if (_dueDate != value)
                {
                    _dueDate = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DueDate)));
                }
            }
        }

        /// <summary>
        /// Priority level of the subtask.  Acceptable values are High, Medium or Low.
        /// </summary>
        [JsonPropertyName("priority")]
        public string Priority
        {
            get => _priority;
            set
            {
                if (_priority != value)
                {
                    _priority = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Priority)));
                }
            }
        }

        /// <summary>
        /// List of attachment file names stored relative to the attachments directory.  Only
        /// file names are persisted so that the application remains portable.
        /// </summary>
        [JsonPropertyName("attachments")]
        public List<string> Attachments { get; set; } = new List<string>();

        /// <summary>
        /// When true, the Description is treated as markdown text rather than plain
        /// formatting.  Markdown descriptions are currently stored as plain text but can
        /// be rendered differently in the future.
        /// </summary>
        [JsonPropertyName("is_markdown")]
        public bool IsMarkdown
        {
            get => _isMarkdown;
            set
            {
                if (_isMarkdown != value)
                {
                    _isMarkdown = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMarkdown)));
                }
            }
        }

        /// <summary>
        /// Tags that categorise the subtask.  Optional but always initialised to an empty list
        /// to simplify filtering and display logic.
        /// </summary>
        [JsonPropertyName("tags")]
        public List<string> Tags
        {
            get => _tags;
            set
            {
                _tags = value ?? new List<string>();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Tags)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}