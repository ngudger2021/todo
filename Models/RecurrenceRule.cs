using System;
using System.Text.Json.Serialization;

namespace TodoWpfApp.Models
{
    /// <summary>
    /// Defines how a task recurs (repeats) after completion.
    /// </summary>
    public class RecurrenceRule
    {
        [JsonPropertyName("type")]
        public RecurrenceType Type { get; set; } = RecurrenceType.None;

        [JsonPropertyName("interval")]
        public int Interval { get; set; } = 1;

        [JsonPropertyName("days_of_week")]
        public List<DayOfWeek> DaysOfWeek { get; set; } = new();

        [JsonPropertyName("day_of_month")]
        public int? DayOfMonth { get; set; }

        /// <summary>
        /// Calculate the next occurrence date based on the completion date.
        /// </summary>
        public DateTime? CalculateNextOccurrence(DateTime completionDate)
        {
            return Type switch
            {
                RecurrenceType.Daily => completionDate.AddDays(Interval),
                RecurrenceType.Weekly => completionDate.AddDays(7 * Interval),
                RecurrenceType.Monthly => completionDate.AddMonths(Interval),
                RecurrenceType.Custom when DaysOfWeek.Count > 0 => GetNextCustomDate(completionDate),
                _ => null
            };
        }

        private DateTime GetNextCustomDate(DateTime from)
        {
            // Find the next occurrence based on days of week
            for (int i = 1; i <= 7; i++)
            {
                var candidate = from.AddDays(i);
                if (DaysOfWeek.Contains(candidate.DayOfWeek))
                {
                    return candidate;
                }
            }
            return from.AddDays(1);
        }

        public string GetDisplayString()
        {
            return Type switch
            {
                RecurrenceType.Daily => $"Every {(Interval == 1 ? "day" : $"{Interval} days")}",
                RecurrenceType.Weekly => $"Every {(Interval == 1 ? "week" : $"{Interval} weeks")}",
                RecurrenceType.Monthly => $"Every {(Interval == 1 ? "month" : $"{Interval} months")}",
                RecurrenceType.Custom when DaysOfWeek.Count > 0 => $"Custom: {string.Join(", ", DaysOfWeek.Select(d => d.ToString().Substring(0, 3)))}",
                _ => "None"
            };
        }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RecurrenceType
    {
        None,
        Daily,
        Weekly,
        Monthly,
        Custom
    }
}
