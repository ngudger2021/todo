using System;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace TodoWpfApp.Models
{
    /// <summary>
    /// Represents a column in the Kanban board (e.g., Backlog, Doing, Done).
    /// Each column has a unique ID, name, and display order.
    /// </summary>
    public class KanbanColumn : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private int _displayOrder;
        private string _color = "#3498db";

        [JsonPropertyName("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [JsonPropertyName("name")]
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value ?? string.Empty;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusTag)));
                }
            }
        }

        [JsonPropertyName("display_order")]
        public int DisplayOrder
        {
            get => _displayOrder;
            set
            {
                if (_displayOrder != value)
                {
                    _displayOrder = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayOrder)));
                }
            }
        }

        [JsonPropertyName("color")]
        public string Color
        {
            get => _color;
            set
            {
                if (_color != value)
                {
                    _color = value ?? "#3498db";
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Color)));
                }
            }
        }

        /// <summary>
        /// Returns the status tag that should be applied to tasks in this column.
        /// Format: "status:ColumnName"
        /// </summary>
        [JsonIgnore]
        public string StatusTag => $"status:{Name}";

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Creates default Kanban columns for a new board.
        /// </summary>
        public static KanbanColumn[] CreateDefaultColumns()
        {
            return new[]
            {
                new KanbanColumn { Name = "Backlog", DisplayOrder = 0, Color = "#95a5a6" },
                new KanbanColumn { Name = "To Do", DisplayOrder = 1, Color = "#3498db" },
                new KanbanColumn { Name = "In Progress", DisplayOrder = 2, Color = "#f39c12" },
                new KanbanColumn { Name = "Done", DisplayOrder = 3, Color = "#27ae60" }
            };
        }
    }
}
