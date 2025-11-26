using System;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace TodoWpfApp.Models
{
    public class TaskHistoryEntry : INotifyPropertyChanged
    {
        private string _title = string.Empty;
        private string _description = string.Empty;
        private DateTime _createdAt = DateTime.Now;
        private DateTime? _completedAt;
        private DateTime? _deletedAt;

        [JsonPropertyName("task_id")]
        public Guid TaskId { get; set; }

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
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TimeToComplete)));
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
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TimeToComplete)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
                }
            }
        }

        [JsonPropertyName("deleted_at")]
        public DateTime? DeletedAt
        {
            get => _deletedAt;
            set
            {
                if (_deletedAt != value)
                {
                    _deletedAt = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DeletedAt)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
                }
            }
        }

        [JsonIgnore]
        public string Status
        {
            get
            {
                if (DeletedAt.HasValue)
                {
                    return "Deleted";
                }
                if (CompletedAt.HasValue)
                {
                    return "Completed";
                }
                return "Active";
            }
        }

        [JsonIgnore]
        public TimeSpan? TimeToComplete
        {
            get
            {
                if (CompletedAt.HasValue)
                {
                    return CompletedAt.Value - CreatedAt;
                }
                return null;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public static TaskHistoryEntry FromTask(TaskItem task)
        {
            return new TaskHistoryEntry
            {
                TaskId = task.Id,
                Title = task.Title,
                Description = task.Description,
                CreatedAt = task.CreatedAt,
                CompletedAt = task.CompletedAt,
            };
        }
    }
}
