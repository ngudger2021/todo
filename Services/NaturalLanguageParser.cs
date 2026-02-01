using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace TodoWpfApp.Services
{
    /// <summary>
    /// Parses natural language input to extract task details.
    /// Supports patterns like: "Buy groceries tomorrow 3pm #shopping !high"
    /// - Tags start with #
    /// - Priority is !high, !medium, or !low
    /// - Date/time keywords: today, tomorrow, monday-sunday, specific dates
    /// </summary>
    public class NaturalLanguageParser
    {
        public class ParsedTask
        {
            public string Title { get; set; } = string.Empty;
            public DateTime? DueDate { get; set; }
            public List<string> Tags { get; set; } = new();
            public string Priority { get; set; } = "Medium";
        }

        public static ParsedTask Parse(string input)
        {
            var result = new ParsedTask();
            if (string.IsNullOrWhiteSpace(input))
                return result;

            var tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            var titleParts = new List<string>();

            DateTime? parsedDate = null;
            TimeSpan? parsedTime = null;

            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];

                // Check for tag
                if (token.StartsWith("#"))
                {
                    var tag = token.Substring(1);
                    if (!string.IsNullOrWhiteSpace(tag))
                        result.Tags.Add(tag);
                    continue;
                }

                // Check for priority
                if (token.StartsWith("!"))
                {
                    var priority = token.Substring(1).ToLower();
                    result.Priority = priority switch
                    {
                        "high" or "h" => "High",
                        "low" or "l" => "Low",
                        _ => "Medium"
                    };
                    continue;
                }

                // Check for date keywords
                var lowerToken = token.ToLower();
                if (TryParseRelativeDate(lowerToken, out var dateValue))
                {
                    parsedDate = dateValue;
                    continue;
                }

                // Check for time (e.g., 3pm, 14:00, 9:30am)
                if (TryParseTime(token, out var timeValue))
                {
                    parsedTime = timeValue;
                    continue;
                }

                // Check for explicit date (e.g., 2026-01-30, 01/30/2026)
                if (TryParseExplicitDate(token, out var explicitDate))
                {
                    parsedDate = explicitDate;
                    continue;
                }

                // Otherwise, it's part of the title
                titleParts.Add(token);
            }

            result.Title = string.Join(" ", titleParts);

            // Combine date and time if available
            if (parsedDate.HasValue)
            {
                result.DueDate = parsedDate.Value;
                if (parsedTime.HasValue)
                {
                    result.DueDate = result.DueDate.Value.Date + parsedTime.Value;
                }
            }
            else if (parsedTime.HasValue)
            {
                // If only time is specified, assume today
                result.DueDate = DateTime.Today + parsedTime.Value;
            }

            return result;
        }

        private static bool TryParseRelativeDate(string token, out DateTime date)
        {
            date = DateTime.Today;

            switch (token)
            {
                case "today":
                    return true;
                case "tomorrow":
                case "tmr":
                    date = DateTime.Today.AddDays(1);
                    return true;
                case "monday":
                case "mon":
                    date = GetNextDayOfWeek(DayOfWeek.Monday);
                    return true;
                case "tuesday":
                case "tue":
                    date = GetNextDayOfWeek(DayOfWeek.Tuesday);
                    return true;
                case "wednesday":
                case "wed":
                    date = GetNextDayOfWeek(DayOfWeek.Wednesday);
                    return true;
                case "thursday":
                case "thu":
                    date = GetNextDayOfWeek(DayOfWeek.Thursday);
                    return true;
                case "friday":
                case "fri":
                    date = GetNextDayOfWeek(DayOfWeek.Friday);
                    return true;
                case "saturday":
                case "sat":
                    date = GetNextDayOfWeek(DayOfWeek.Saturday);
                    return true;
                case "sunday":
                case "sun":
                    date = GetNextDayOfWeek(DayOfWeek.Sunday);
                    return true;
                default:
                    return false;
            }
        }

        private static DateTime GetNextDayOfWeek(DayOfWeek day)
        {
            var today = DateTime.Today;
            int daysToAdd = ((int)day - (int)today.DayOfWeek + 7) % 7;
            if (daysToAdd == 0)
                daysToAdd = 7; // Next week if it's the same day
            return today.AddDays(daysToAdd);
        }

        private static bool TryParseTime(string token, out TimeSpan time)
        {
            time = TimeSpan.Zero;

            // Match patterns like 3pm, 14:00, 9:30am
            var match = Regex.Match(token, @"^(\d{1,2})(?::(\d{2}))?(am|pm)?$", RegexOptions.IgnoreCase);
            if (!match.Success)
                return false;

            int hour = int.Parse(match.Groups[1].Value);
            int minute = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
            string? meridiem = match.Groups[3].Success ? match.Groups[3].Value.ToLower() : null;

            if (meridiem == "pm" && hour < 12)
                hour += 12;
            else if (meridiem == "am" && hour == 12)
                hour = 0;

            if (hour >= 0 && hour < 24 && minute >= 0 && minute < 60)
            {
                time = new TimeSpan(hour, minute, 0);
                return true;
            }

            return false;
        }

        private static bool TryParseExplicitDate(string token, out DateTime date)
        {
            // Try various date formats
            string[] formats = { "yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy", "M/d/yyyy", "d/M/yyyy" };
            return DateTime.TryParseExact(token, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }
    }
}
