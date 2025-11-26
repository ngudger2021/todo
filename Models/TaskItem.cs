using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace TodoWpfApp.Models
{
    public enum RecurrenceType
    {
        None,
        Daily,
        Weekly,
        Monthly
    }

    /// <summary>
    /// Represents a single task in the to‑do list.  Tasks hold metadata
    /// including title, description, due date, priority, completion state,
    /// attachments and a collection of subtasks.  Attachment names are stored
    /// as relative filenames inside the application’s attachments folder.
    /// </summary>
    public class TaskItem : INotifyPropertyChanged
    {
        private string _title = string.Empty;
        private string _description = string.Empty;
        private DateTime? _dueDate;
        private string _priority = "Medium";
        private bool _completed;
        private DateTime _createdAt = DateTime.Now;
        private DateTime? _completedAt;
        private List<string> _tags = new();
        private RecurrenceType _recurrenceType = RecurrenceType.None;
        private int _recurrenceInterval = 1;
        private DateTime? _recursUntil;

        [JsonPropertyName("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

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
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CompletedAt)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
                }
            }
        }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt
        {
            get => _createdAt;
            set
            {
                if (_createdAt != value)
                {
                    _createdAt = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CreatedAt)));
                }
            }
        }

        [JsonPropertyName("completed_at")]
        public DateTime? CompletedAt
        {
            get => _completedAt;
            set
            {
                if (_completedAt != value)
                {
                    _completedAt = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CompletedAt)));
                }
            }
        }

        /// <summary>
        /// List of relative attachment file names stored under the attachments
        /// directory.  Only the file name (not the full path) is stored so
        /// that the application remains portable.
        /// </summary>
        [JsonPropertyName("attachments")]
        public List<string> Attachments { get; set; } = new List<string>();

        /// <summary>
        /// List of subtasks associated with this task.  Each subtask can be
        /// marked complete independently.
        /// </summary>
        [JsonPropertyName("subtasks")]
        public List<SubTask> SubTasks { get; set; } = new List<SubTask>();

        /// <summary>
        /// Tags that categorise the task.  Stored as simple strings and persisted in the
        /// JSON representation.  Defaults to an empty list so callers can freely add tags
        /// without null checks.
        /// </summary>
        [JsonPropertyName("tags")]
        public List<string> Tags
        {
            get => _tags;
            set
            {
                _tags = value ?? new List<string>();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Tags)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TagsDisplay)));
            }
        }

        [JsonPropertyName("recurrence_type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public RecurrenceType RecurrenceType
        {
            get => _recurrenceType;
            set
            {
                if (_recurrenceType != value)
                {
                    _recurrenceType = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RecurrenceType)));
                }
            }
        }

        [JsonPropertyName("recurrence_interval")]
        public int RecurrenceInterval
        {
            get => _recurrenceInterval;
            set
            {
                if (_recurrenceInterval != value)
                {
                    _recurrenceInterval = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RecurrenceInterval)));
                }
            }
        }

        [JsonPropertyName("recurs_until")]
        public DateTime? RecursUntil
        {
            get => _recursUntil;
            set
            {
                if (_recursUntil != value)
                {
                    _recursUntil = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RecursUntil)));
                }
            }
        }

        /// <summary>
        /// Computed property for displaying status text.
        /// </summary>
        [JsonIgnore]
        public string Status => Completed ? "Completed" : "Pending";

        /// <summary>
        /// Convenience string for displaying tags as a comma-separated list.
        /// </summary>
        [JsonIgnore]
        public string TagsDisplay => Tags.Count == 0 ? string.Empty : string.Join(", ", Tags);

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}