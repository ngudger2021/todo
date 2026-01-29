using System;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using TodoWpfApp.Models;

namespace TodoWpfApp
{
    public class ReminderService : IDisposable
    {
        private readonly ObservableCollection<TaskItem> _tasks;
        private readonly Func<ReminderSettings> _settingsProvider;
        private readonly Action<TaskItem> _notificationSink;
        private readonly DispatcherTimer _timer;
        private readonly HashSet<Guid> _notifiedTasks = new();

        public ReminderService(ObservableCollection<TaskItem> tasks, Func<ReminderSettings> settingsProvider, Action<TaskItem> notificationSink)
        {
            _tasks = tasks;
            _settingsProvider = settingsProvider;
            _notificationSink = notificationSink;
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(1)
            };
            _timer.Tick += Timer_Tick;
        }

        public void Start()
        {
            _timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            var settings = _settingsProvider();
            if (!settings.RemindersEnabled)
            {
                return;
            }

            var now = DateTime.Now;
            var leadTime = TimeSpan.FromHours(Math.Max(0, settings.LeadTimeHours));
            var windowEnd = now + leadTime;

            // Allow tasks to be reminded again if their due date is pushed out of the
            // current reminder window or if they have been completed since the last
            // check. This prevents permanent suppression after a user snoozes a task.
            foreach (var taskId in _notifiedTasks.ToList())
            {
                var task = _tasks.FirstOrDefault(t => t.Id == taskId);
                if (task == null || task.Completed || !task.DueDate.HasValue || task.DueDate.Value > windowEnd)
                {
                    _notifiedTasks.Remove(taskId);
                }
            }

            var dueSoon = _tasks
                .Where(t => t.DueDate.HasValue && !t.Completed)
                .Where(t => t.DueDate.Value >= now && t.DueDate.Value <= windowEnd)
                .Where(t => !_notifiedTasks.Contains(t.Id))
                .OrderBy(t => t.DueDate)
                .ToList();

            foreach (var task in dueSoon)
            {
                _notifiedTasks.Add(task.Id);
                _notificationSink(task);
            }
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Tick -= Timer_Tick;
        }
    }
}
