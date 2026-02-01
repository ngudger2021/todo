using System;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace TodoWpfApp.Models
{
    public class NotificationItem : INotifyPropertyChanged
    {
        private bool _read;
        private DateTime? _snoozedUntil;

        [JsonPropertyName("task_id")]
        public Guid TaskId { get; set; }

        [JsonPropertyName("task_title")]
        public string TaskTitle { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("due_date")]
        public DateTime? DueDate { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [JsonPropertyName("read")]
        public bool Read
        {
            get => _read;
            set
            {
                if (_read != value)
                {
                    _read = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Read)));
                }
            }
        }

        [JsonPropertyName("snoozed_until")]
        public DateTime? SnoozedUntil
        {
            get => _snoozedUntil;
            set
            {
                if (_snoozedUntil != value)
                {
                    _snoozedUntil = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SnoozedUntil)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
