using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using TodoWpfApp.Models;

namespace TodoWpfApp
{
    public partial class StatisticsWindow : Window
    {
        private readonly IEnumerable<TaskItem> _tasks;
        private readonly IEnumerable<TaskHistoryEntry> _history;
        private static readonly string[] StatusTags = { "New", "In Progress", "On Hold", "Complete" };

        public StatisticsWindow(IEnumerable<TaskItem> tasks, IEnumerable<TaskHistoryEntry> history)
        {
            InitializeComponent();
            _tasks = tasks;
            _history = history;
            CalculateStatistics();
        }

        private void CalculateStatistics()
        {
            var taskList = _tasks.ToList();
            var historyList = _history.ToList();

            // Overall stats
            int totalTasks = taskList.Count;
            int completedTasks = taskList.Count(t => t.Completed);
            int pendingTasks = totalTasks - completedTasks;

            TotalTasksText.Text = totalTasks.ToString();
            CompletedTasksText.Text = completedTasks.ToString();
            PendingTasksText.Text = pendingTasks.ToString();

            // Completion rate
            double completionRate = totalTasks > 0 ? (completedTasks * 100.0 / totalTasks) : 0;
            CompletionRateText.Text = $"{completionRate:F1}%";

            // Average time to complete
            var completedHistory = historyList.Where(h => h.TimeToComplete.HasValue).ToList();
            if (completedHistory.Any())
            {
                double avgDays = completedHistory.Average(h => h.TimeToComplete!.Value.TotalDays);
                if (avgDays < 1)
                {
                    AvgTimeText.Text = $"{avgDays * 24:F1}h";
                }
                else
                {
                    AvgTimeText.Text = $"{avgDays:F1}d";
                }
            }
            else
            {
                AvgTimeText.Text = "N/A";
            }

            // Priority breakdown
            int highPriority = taskList.Count(t => t.Priority == "High");
            int mediumPriority = taskList.Count(t => t.Priority == "Medium");
            int lowPriority = taskList.Count(t => t.Priority == "Low");

            HighPriorityText.Text = highPriority.ToString();
            MediumPriorityText.Text = mediumPriority.ToString();
            LowPriorityText.Text = lowPriority.ToString();

            if (totalTasks > 0)
            {
                HighPriorityBar.Value = (highPriority * 100.0 / totalTasks);
                HighPriorityBar.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));

                MediumPriorityBar.Value = (mediumPriority * 100.0 / totalTasks);
                MediumPriorityBar.Foreground = new SolidColorBrush(Color.FromRgb(255, 152, 0));

                LowPriorityBar.Value = (lowPriority * 100.0 / totalTasks);
                LowPriorityBar.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
            }

            // Status breakdown
            var statusCounts = taskList
                .GroupBy(GetStatusTag)
                .ToDictionary(g => g.Key, g => g.Count());

            int newCount = statusCounts.TryGetValue("New", out var n) ? n : 0;
            int inProgressCount = statusCounts.TryGetValue("In Progress", out var ip) ? ip : 0;
            int onHoldCount = statusCounts.TryGetValue("On Hold", out var oh) ? oh : 0;
            int completeCount = statusCounts.TryGetValue("Complete", out var c) ? c : 0;

            NewStatusText.Text = newCount.ToString();
            InProgressStatusText.Text = inProgressCount.ToString();
            OnHoldStatusText.Text = onHoldCount.ToString();
            CompleteStatusText.Text = completeCount.ToString();

            if (totalTasks > 0)
            {
                NewStatusBar.Value = newCount * 100.0 / totalTasks;
                NewStatusBar.Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166));

                InProgressStatusBar.Value = inProgressCount * 100.0 / totalTasks;
                InProgressStatusBar.Foreground = new SolidColorBrush(Color.FromRgb(243, 156, 18));

                OnHoldStatusBar.Value = onHoldCount * 100.0 / totalTasks;
                OnHoldStatusBar.Foreground = new SolidColorBrush(Color.FromRgb(142, 68, 173));

                CompleteStatusBar.Value = completeCount * 100.0 / totalTasks;
                CompleteStatusBar.Foreground = new SolidColorBrush(Color.FromRgb(39, 174, 96));
            }

            // Tag statistics
            var tagCounts = taskList
                .SelectMany(t => t.Tags)
                .GroupBy(tag => tag)
                .Select(g => new TagStatistic
                {
                    TagName = g.Key,
                    Count = g.Count(),
                    Percentage = totalTasks > 0 ? (g.Count() * 100.0 / totalTasks) : 0
                })
                .OrderByDescending(ts => ts.Count)
                .Take(10)
                .ToList();

            TagStatsList.ItemsSource = tagCounts;

            // Productivity trend
            DrawProductivityTrend(taskList, historyList);

            // Additional metrics
            var now = DateTime.Now;
            int overdueTasks = taskList.Count(t => !t.Completed && t.DueDate.HasValue && t.DueDate.Value < now);
            int dueToday = taskList.Count(t => !t.Completed && t.DueDate.HasValue && t.DueDate.Value.Date == now.Date);
            int dueThisWeek = taskList.Count(t => !t.Completed && t.DueDate.HasValue &&
                t.DueDate.Value.Date >= now.Date && t.DueDate.Value.Date <= now.Date.AddDays(7));
            int recurringTasks = taskList.Count(t => t.Recurrence != null && t.Recurrence.Type != RecurrenceType.None);
            int tasksWithDependencies = taskList.Count(t => t.DependsOn != null && t.DependsOn.Any());

            OverdueTasksText.Text = $"Overdue Tasks: {overdueTasks}";
            DueTodayText.Text = $"Due Today: {dueToday}";
            DueThisWeekText.Text = $"Due This Week: {dueThisWeek}";
            RecurringTasksText.Text = $"Recurring Tasks: {recurringTasks}";
            TasksWithDependenciesText.Text = $"Tasks with Dependencies: {tasksWithDependencies}";
        }

        private void DrawProductivityTrend(List<TaskItem> tasks, List<TaskHistoryEntry> history)
        {
            TrendCanvas.Children.Clear();

            var today = DateTime.Today;
            var days = new List<DateTime>();
            for (int i = 6; i >= 0; i--)
            {
                days.Add(today.AddDays(-i));
            }

            // Count tasks completed per day
            var completionsPerDay = days.Select(day =>
            {
                int count = history.Count(h => h.CompletedAt.HasValue && h.CompletedAt.Value.Date == day);
                return new { Day = day, Count = count };
            }).ToList();

            double canvasWidth = TrendCanvas.ActualWidth > 0 ? TrendCanvas.ActualWidth : 800;
            double canvasHeight = TrendCanvas.ActualHeight > 0 ? TrendCanvas.ActualHeight : 200;

            if (canvasWidth <= 0) canvasWidth = 800;
            if (canvasHeight <= 0) canvasHeight = 200;

            double barWidth = (canvasWidth - 80) / 7;
            double maxCount = completionsPerDay.Max(d => d.Count);
            if (maxCount == 0) maxCount = 1;

            for (int i = 0; i < completionsPerDay.Count; i++)
            {
                var data = completionsPerDay[i];
                double barHeight = (data.Count / (double)maxCount) * (canvasHeight - 40);

                // Bar
                var bar = new Rectangle
                {
                    Width = barWidth - 10,
                    Height = barHeight,
                    Fill = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                    Stroke = Brushes.Black,
                    StrokeThickness = 1
                };

                double x = 40 + i * barWidth;
                double y = canvasHeight - barHeight - 30;

                Canvas.SetLeft(bar, x);
                Canvas.SetTop(bar, y);
                TrendCanvas.Children.Add(bar);

                // Count label
                var countLabel = new System.Windows.Controls.TextBlock
                {
                    Text = data.Count.ToString(),
                    FontSize = 12,
                    Foreground = Brushes.Black
                };
                Canvas.SetLeft(countLabel, x + (barWidth - 10) / 2 - 5);
                Canvas.SetTop(countLabel, y - 20);
                TrendCanvas.Children.Add(countLabel);

                // Day label
                var dayLabel = new System.Windows.Controls.TextBlock
                {
                    Text = data.Day.ToString("MM/dd"),
                    FontSize = 10,
                    Foreground = Brushes.Black
                };
                Canvas.SetLeft(dayLabel, x + (barWidth - 10) / 2 - 15);
                Canvas.SetTop(dayLabel, canvasHeight - 20);
                TrendCanvas.Children.Add(dayLabel);
            }
        }

        private class TagStatistic
        {
            public string TagName { get; set; } = string.Empty;
            public int Count { get; set; }
            public double Percentage { get; set; }
        }

        private static string GetStatusTag(TaskItem task)
        {
            var status = task.Tags.FirstOrDefault(t => StatusTags.Any(s => string.Equals(s, t, StringComparison.OrdinalIgnoreCase)));
            return string.IsNullOrWhiteSpace(status) ? "New" : StatusTags.First(s => string.Equals(s, status, StringComparison.OrdinalIgnoreCase));
        }
    }
}
