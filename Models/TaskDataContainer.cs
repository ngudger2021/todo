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
    }
}
