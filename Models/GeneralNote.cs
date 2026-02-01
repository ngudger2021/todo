using System;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace TodoWpfApp.Models
{
    /// <summary>
    /// Represents a standalone note that is not linked to any specific task.
    /// General notes can be used for reminders, ideas, or any other freeform content.
    /// </summary>
    public class GeneralNote : INotifyPropertyChanged
    {
        private string _title = string.Empty;
        private string _content = string.Empty;
        private bool _contentIsMarkdown;
        private DateTime _createdAt = DateTime.Now;
        private DateTime _modifiedAt = DateTime.Now;

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
                    _title = value ?? string.Empty;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
                    UpdateModifiedDate();
                }
            }
        }

        [JsonPropertyName("content")]
        public string Content
        {
            get => _content;
            set
            {
                if (_content != value)
                {
                    _content = value ?? string.Empty;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Content)));
                    UpdateModifiedDate();
                }
            }
        }

        [JsonPropertyName("content_is_markdown")]
        public bool ContentIsMarkdown
        {
            get => _contentIsMarkdown;
            set
            {
                if (_contentIsMarkdown != value)
                {
                    _contentIsMarkdown = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ContentIsMarkdown)));
                    UpdateModifiedDate();
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

        [JsonPropertyName("modified_at")]
        public DateTime ModifiedAt
        {
            get => _modifiedAt;
            set
            {
                if (_modifiedAt != value)
                {
                    _modifiedAt = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModifiedAt)));
                }
            }
        }

        private void UpdateModifiedDate()
        {
            ModifiedAt = DateTime.Now;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
