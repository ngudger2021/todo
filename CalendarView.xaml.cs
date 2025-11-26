using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using TodoWpfApp.Models;

namespace TodoWpfApp
{
    /// <summary>
    /// Interaction logic for CalendarView.xaml
    /// </summary>
    public partial class CalendarView : Window, INotifyPropertyChanged
    {
        private readonly ObservableCollection<TaskItem> _tasks;
        private readonly Action _saveAndRefresh;
        private readonly ObservableCollection<CalendarEntry> _selectedDateEntries = new();
        private DateTime _selectedDate = DateTime.Today;

        public ObservableCollection<TaskItem> Tasks => _tasks;

        public ObservableCollection<CalendarEntry> SelectedDateEntries => _selectedDateEntries;

        public string SelectedDateText => $"Selected: {_selectedDate:MMMM d, yyyy}";

        public CalendarView(ObservableCollection<TaskItem> tasks, Action saveAndRefresh)
        {
            InitializeComponent();
            _tasks = tasks;
            _saveAndRefresh = saveAndRefresh;
            DataContext = this;

            TaskCalendar.DisplayDate = _selectedDate;
            TaskCalendar.SelectedDate = _selectedDate;
            RebuildSelectedDateEntries();

            _tasks.CollectionChanged += Tasks_CollectionChanged;
            foreach (var task in _tasks)
            {
                SubscribeToTask(task);
            }
        }

        private void Tasks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.OfType<TaskItem>())
                {
                    SubscribeToTask(item);
                }
            }
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems.OfType<TaskItem>())
                {
                    UnsubscribeFromTask(item);
                }
            }
            RebuildSelectedDateEntries();
        }

        private void SubscribeToTask(TaskItem task)
        {
            task.PropertyChanged += Task_PropertyChanged;
            if (task.SubTasks != null)
            {
                foreach (var sub in task.SubTasks)
                {
                    sub.PropertyChanged += Sub_PropertyChanged;
                }
            }
        }

        private void UnsubscribeFromTask(TaskItem task)
        {
            task.PropertyChanged -= Task_PropertyChanged;
            if (task.SubTasks != null)
            {
                foreach (var sub in task.SubTasks)
                {
                    sub.PropertyChanged -= Sub_PropertyChanged;
                }
            }
        }

        private void Task_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TaskItem.DueDate))
            {
                RebuildSelectedDateEntries();
            }
        }

        private void Sub_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SubTask.DueDate))
            {
                RebuildSelectedDateEntries();
            }
        }

        private void TaskCalendar_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (TaskCalendar.SelectedDate.HasValue)
            {
                _selectedDate = TaskCalendar.SelectedDate.Value.Date;
                OnPropertyChanged(nameof(SelectedDateText));
                RebuildSelectedDateEntries();
            }
        }

        private void RebuildSelectedDateEntries()
        {
            _selectedDateEntries.Clear();

            foreach (var task in _tasks)
            {
                if (task.DueDate.HasValue && task.DueDate.Value.Date == _selectedDate.Date)
                {
                    _selectedDateEntries.Add(CalendarEntry.FromTask(task));
                }

                foreach (var sub in task.SubTasks)
                {
                    if (sub.DueDate.HasValue && sub.DueDate.Value.Date == _selectedDate.Date)
                    {
                        _selectedDateEntries.Add(CalendarEntry.FromSubTask(task, sub));
                    }
                }
            }
            OnPropertyChanged(nameof(SelectedDateEntries));
            OnPropertyChanged(nameof(SelectedDateText));
            TaskCalendar.InvalidateVisual();
        }

        private void TaskEntry_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            if (sender is FrameworkElement element && element.DataContext is CalendarEntry entry)
            {
                DragDrop.DoDragDrop(element, entry, DragDropEffects.Move);
            }
        }

        private void DayButton_Drop(object sender, DragEventArgs e)
        {
            if (sender is not CalendarDayButton button || button.DataContext is not DateTime date)
            {
                return;
            }

            var entry = e.Data.GetData(typeof(CalendarEntry)) as CalendarEntry;
            if (entry == null)
            {
                return;
            }

            entry.UpdateDueDate(date.Date);
            _saveAndRefresh?.Invoke();
            RebuildSelectedDateEntries();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class CalendarEntry
    {
        public TaskItem Task { get; }
        public SubTask? SubTask { get; }

        private CalendarEntry(TaskItem task, SubTask? subTask)
        {
            Task = task;
            SubTask = subTask;
        }

        public static CalendarEntry FromTask(TaskItem task) => new(task, null);

        public static CalendarEntry FromSubTask(TaskItem parent, SubTask subTask) => new(parent, subTask);

        public string DisplayText => SubTask == null ? Task.Title : $"{Task.Title} • {SubTask.Title}";

        public string Details
        {
            get
            {
                if (SubTask == null)
                {
                    return Task.Description;
                }
                return string.IsNullOrWhiteSpace(SubTask.Description) ? "Subtask" : SubTask.Description;
            }
        }

        public void UpdateDueDate(DateTime newDate)
        {
            if (SubTask == null)
            {
                Task.DueDate = newDate;
            }
            else
            {
                SubTask.DueDate = newDate;
            }
        }
    }

    public class TasksForDateConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values.Length < 2)
            {
                return Array.Empty<CalendarEntry>();
            }

            if (values[0] is not DateTime date || values[1] is not ObservableCollection<TaskItem> tasks)
            {
                return Array.Empty<CalendarEntry>();
            }

            var entries = new List<CalendarEntry>();
            foreach (var task in tasks)
            {
                if (task.DueDate.HasValue && task.DueDate.Value.Date == date.Date)
                {
                    entries.Add(CalendarEntry.FromTask(task));
                }

                foreach (var sub in task.SubTasks)
                {
                    if (sub.DueDate.HasValue && sub.DueDate.Value.Date == date.Date)
                    {
                        entries.Add(CalendarEntry.FromSubTask(task, sub));
                    }
                }
            }
            return entries;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
            => throw new NotSupportedException();
    }
}
