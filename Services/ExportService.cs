using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TodoWpfApp.Models;

namespace TodoWpfApp.Services
{
    /// <summary>
    /// Service for exporting task data to various formats (CSV, PDF).
    /// </summary>
    public class ExportService
    {
        /// <summary>
        /// Export tasks to CSV format for Excel compatibility.
        /// </summary>
        public static void ExportToCsv(IEnumerable<TaskItem> tasks, string filePath)
        {
            var csv = new StringBuilder();

            // Header
            csv.AppendLine("Title,Description,Due Date,Priority,Status,Created At,Completed At,Tags,Subtasks");

            foreach (var task in tasks)
            {
                var title = EscapeCsv(task.Title);
                var description = EscapeCsv(task.Description);
                var dueDate = task.DueDate?.ToString("yyyy-MM-dd HH:mm") ?? "";
                var priority = task.Priority;
                var status = task.Status;
                var createdAt = task.CreatedAt.ToString("yyyy-MM-dd HH:mm");
                var completedAt = task.CompletedAt?.ToString("yyyy-MM-dd HH:mm") ?? "";
                var tags = EscapeCsv(string.Join(", ", task.Tags));
                var subtasks = task.SubTasks.Count.ToString();
                csv.AppendLine($"{title},{description},{dueDate},{priority},{status},{createdAt},{completedAt},{tags},{subtasks}");
            }

            File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Export tasks to a simple text-based PDF format.
        /// Note: This is a simplified implementation that creates a text document.
        /// For production use, consider using a library like iTextSharp or PdfSharp.
        /// </summary>
        public static void ExportToTextPdf(IEnumerable<TaskItem> tasks, string filePath)
        {
            var content = new StringBuilder();

            content.AppendLine("=".PadRight(80, '='));
            content.AppendLine("TASK REPORT");
            content.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}");
            content.AppendLine("=".PadRight(80, '='));
            content.AppendLine();

            var taskList = tasks.ToList();

            // Summary
            content.AppendLine("SUMMARY");
            content.AppendLine("-".PadRight(80, '-'));
            content.AppendLine($"Total Tasks: {taskList.Count}");
            content.AppendLine($"Completed: {taskList.Count(t => t.Completed)}");
            content.AppendLine($"Pending: {taskList.Count(t => !t.Completed)}");
            content.AppendLine($"High Priority: {taskList.Count(t => t.Priority == "High")}");
            content.AppendLine($"Medium Priority: {taskList.Count(t => t.Priority == "Medium")}");
            content.AppendLine($"Low Priority: {taskList.Count(t => t.Priority == "Low")}");
            content.AppendLine();

            // Tasks by Priority
            foreach (var priority in new[] { "High", "Medium", "Low" })
            {
                var priorityTasks = taskList.Where(t => t.Priority == priority).ToList();
                if (!priorityTasks.Any()) continue;

                content.AppendLine($"{priority.ToUpper()} PRIORITY TASKS");
                content.AppendLine("-".PadRight(80, '-'));

                foreach (var task in priorityTasks)
                {
                    content.AppendLine($"[{(task.Completed ? "X" : " ")}] {task.Title}");
                    if (!string.IsNullOrWhiteSpace(task.Description))
                    {
                        content.AppendLine($"    Description: {task.Description}");
                    }
                    if (task.DueDate.HasValue)
                    {
                        content.AppendLine($"    Due Date: {task.DueDate.Value:yyyy-MM-dd HH:mm}");
                    }
                    if (task.Tags.Any())
                    {
                        content.AppendLine($"    Tags: {string.Join(", ", task.Tags)}");
                    }
                    if (task.SubTasks.Any())
                    {
                        content.AppendLine($"    Subtasks: {task.SubTasks.Count(st => st.Completed)}/{task.SubTasks.Count} completed");
                    }
                    content.AppendLine();
                }
            }

            // Overdue tasks
            var overdueTasks = taskList.Where(t => !t.Completed && t.DueDate.HasValue && t.DueDate.Value < DateTime.Now).ToList();
            if (overdueTasks.Any())
            {
                content.AppendLine("OVERDUE TASKS");
                content.AppendLine("-".PadRight(80, '-'));
                foreach (var task in overdueTasks)
                {
                    content.AppendLine($"• {task.Title} (Due: {task.DueDate.Value:yyyy-MM-dd})");
                }
                content.AppendLine();
            }

            content.AppendLine("=".PadRight(80, '='));
            content.AppendLine("END OF REPORT");
            content.AppendLine("=".PadRight(80, '='));

            File.WriteAllText(filePath, content.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Export task history to CSV.
        /// </summary>
        public static void ExportHistoryToCsv(IEnumerable<TaskHistoryEntry> history, string filePath)
        {
            var csv = new StringBuilder();

            csv.AppendLine("Title,Description,Created At,Completed At,Deleted At,Status,Time to Complete (Days)");

            foreach (var entry in history)
            {
                var title = EscapeCsv(entry.Title);
                var description = EscapeCsv(entry.Description);
                var createdAt = entry.CreatedAt.ToString("yyyy-MM-dd HH:mm");
                var completedAt = entry.CompletedAt?.ToString("yyyy-MM-dd HH:mm") ?? "";
                var deletedAt = entry.DeletedAt?.ToString("yyyy-MM-dd HH:mm") ?? "";
                var status = entry.Status;
                var timeToComplete = entry.TimeToComplete.HasValue
                    ? entry.TimeToComplete.Value.TotalDays.ToString("F2")
                    : "";

                csv.AppendLine($"{title},{description},{createdAt},{completedAt},{deletedAt},{status},{timeToComplete}");
            }

            File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            // Escape quotes and wrap in quotes if contains comma, quote, or newline
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }
    }
}
