using System.Text.Json;
using Microsoft.Extensions.Logging;
using TodoWpfApp.Models;

namespace TodoWebServer;

public class DataStore
{
    private readonly string _dataFile;
    private readonly ILogger<DataStore> _logger;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public DataStore(string dataFile, ILogger<DataStore> logger)
    {
        _dataFile = dataFile;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TaskItem>> GetTasksAsync()
    {
        var container = await ReadContainerAsync();
        return container.Tasks;
    }

    public async Task<IReadOnlyList<TaskHistoryEntry>> GetHistoryAsync()
    {
        var container = await ReadContainerAsync();
        return container.History;
    }

    public async Task<TaskItem> AddTaskAsync(TaskItem task)
    {
        task.Id = task.Id == Guid.Empty ? Guid.NewGuid() : task.Id;
        NormalizeTask(task);

        return await UpdateContainerAsync(container =>
        {
            container.Tasks.Add(task);
            EnsureHistory(container, task);
            return task;
        });
    }

    public async Task<TaskItem?> UpdateTaskAsync(Guid id, TaskItem updated)
    {
        NormalizeTask(updated);

        return await UpdateContainerAsync(container =>
        {
            var existing = container.Tasks.FirstOrDefault(t => t.Id == id);
            if (existing == null)
            {
                return null;
            }

            updated.Id = id;
            var index = container.Tasks.IndexOf(existing);
            container.Tasks[index] = updated;
            EnsureHistory(container, updated);
            return updated;
        });
    }

    public async Task<bool> DeleteTaskAsync(Guid id)
    {
        return await UpdateContainerAsync(container =>
        {
            var existing = container.Tasks.FirstOrDefault(t => t.Id == id);
            if (existing == null)
            {
                return false;
            }

            container.Tasks.Remove(existing);
            var history = container.History.FirstOrDefault(h => h.TaskId == id) ?? TaskHistoryEntry.FromTask(existing);
            history.DeletedAt = DateTime.Now;
            if (!container.History.Contains(history))
            {
                container.History.Add(history);
            }
            else
            {
                var idx = container.History.IndexOf(history);
                container.History[idx] = history;
            }

            return true;
        });
    }

    private async Task<TaskDataContainer> ReadContainerAsync()
    {
        await _mutex.WaitAsync();
        try
        {
            if (!File.Exists(_dataFile))
            {
                return new TaskDataContainer();
            }

            await using var stream = File.OpenRead(_dataFile);
            if (stream.Length == 0)
            {
                return new TaskDataContainer();
            }

            var container = await JsonSerializer.DeserializeAsync<TaskDataContainer>(stream, _jsonOptions);
            if (container != null && container.Tasks.Count > 0)
            {
                EnsureDefaults(container);
                return container;
            }

            stream.Position = 0;
            var legacy = await JsonSerializer.DeserializeAsync<List<TaskItem>>(stream, _jsonOptions);
            if (legacy != null)
            {
                var converted = new TaskDataContainer { Tasks = legacy };
                EnsureDefaults(converted);
                await SaveContainerAsync(converted);
                return converted;
            }

            return new TaskDataContainer();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read data file {File}", _dataFile);
            return new TaskDataContainer();
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<T> UpdateContainerAsync<T>(Func<TaskDataContainer, T> update)
    {
        await _mutex.WaitAsync();
        try
        {
            var container = await ReadContainerNoLockAsync();
            var result = update(container);
            await SaveContainerAsync(container);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update data file {File}", _dataFile);
            throw;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<TaskDataContainer> ReadContainerNoLockAsync()
    {
        if (!File.Exists(_dataFile))
        {
            return new TaskDataContainer();
        }

        await using var stream = File.OpenRead(_dataFile);
        if (stream.Length == 0)
        {
            return new TaskDataContainer();
        }

        var container = await JsonSerializer.DeserializeAsync<TaskDataContainer>(stream, _jsonOptions);
        if (container != null)
        {
            EnsureDefaults(container);
            return container;
        }

        stream.Position = 0;
        var legacy = await JsonSerializer.DeserializeAsync<List<TaskItem>>(stream, _jsonOptions);
        if (legacy != null)
        {
            var converted = new TaskDataContainer { Tasks = legacy };
            EnsureDefaults(converted);
            return converted;
        }

        return new TaskDataContainer();
    }

    private async Task SaveContainerAsync(TaskDataContainer container)
    {
        var directory = Path.GetDirectoryName(_dataFile);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        EnsureDefaults(container);

        await using var stream = File.Create(_dataFile);
        await JsonSerializer.SerializeAsync(stream, container, _jsonOptions);
    }

    private static void EnsureDefaults(TaskDataContainer container)
    {
        container.Tasks ??= new List<TaskItem>();
        container.History ??= new List<TaskHistoryEntry>();

        foreach (var task in container.Tasks)
        {
            NormalizeTask(task);
        }

        foreach (var task in container.Tasks)
        {
            EnsureHistory(container, task);
        }
    }

    private static void NormalizeTask(TaskItem task)
    {
        task.Attachments ??= new List<string>();
        task.SubTasks ??= new List<SubTask>();
        task.Tags ??= new List<string>();

        task.Title ??= string.Empty;
        task.Description ??= string.Empty;

        if (task.CreatedAt == default)
        {
            task.CreatedAt = DateTime.Now;
        }

        if (task.Completed)
        {
            task.CompletedAt ??= DateTime.Now;
        }
        else
        {
            task.CompletedAt = null;
        }

        foreach (var sub in task.SubTasks)
        {
            sub.Attachments ??= new List<string>();
            sub.Tags ??= new List<string>();
            sub.Title ??= string.Empty;
            sub.Description ??= string.Empty;
            if (sub.Completed && sub.DueDate == null)
            {
                // no-op: placeholder for future metadata
            }
        }
    }

    private static void EnsureHistory(TaskDataContainer container, TaskItem task)
    {
        var entry = container.History.FirstOrDefault(h => h.TaskId == task.Id);
        if (entry == null)
        {
            container.History.Add(TaskHistoryEntry.FromTask(task));
            return;
        }

        entry.Title = task.Title;
        entry.Description = task.Description;
        entry.CreatedAt = task.CreatedAt;
        entry.CompletedAt = task.CompletedAt;
        if (!task.Completed)
        {
            entry.CompletedAt = null;
        }
        entry.DeletedAt = entry.DeletedAt; // keep deletion timestamp if set
    }
}
