using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TodoWpfApp.Models
{
    public class TaskDataContainer
    {
        [JsonPropertyName("tasks")]
        public List<TaskItem> Tasks { get; set; } = new();

        [JsonPropertyName("history")]
        public List<TaskHistoryEntry> History { get; set; } = new();

        [JsonPropertyName("kanban_columns")]
        public List<KanbanColumn> KanbanColumns { get; set; } = new();

        [JsonPropertyName("general_notes")]
        public List<GeneralNote> GeneralNotes { get; set; } = new();
    }
}
