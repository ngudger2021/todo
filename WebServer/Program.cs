using System.Text.Json;
using TodoWpfApp.Models;
using TodoWebServer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.WriteIndented = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<DataStore>>();
    var env = sp.GetRequiredService<IHostEnvironment>();
    var configured = builder.Configuration["DataFile"];
    var defaultPath = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "todo_data.json"));
    var dataPath = string.IsNullOrWhiteSpace(configured) ? defaultPath : Path.GetFullPath(configured);
    return new DataStore(dataPath, logger);
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/tasks", async (DataStore store) => Results.Ok(await store.GetTasksAsync()));

app.MapGet("/api/tasks/{id:guid}", async (Guid id, DataStore store) =>
{
    var tasks = await store.GetTasksAsync();
    var task = tasks.FirstOrDefault(t => t.Id == id);
    return task is null ? Results.NotFound() : Results.Ok(task);
});

app.MapGet("/api/history", async (DataStore store) => Results.Ok(await store.GetHistoryAsync()));

app.MapPost("/api/tasks", async (TaskItem task, DataStore store) =>
{
    var created = await store.AddTaskAsync(task);
    return Results.Created($"/api/tasks/{created.Id}", created);
});

app.MapPut("/api/tasks/{id:guid}", async (Guid id, TaskItem task, DataStore store) =>
{
    var updated = await store.UpdateTaskAsync(id, task);
    return updated is null ? Results.NotFound() : Results.Ok(updated);
});

app.MapDelete("/api/tasks/{id:guid}", async (Guid id, DataStore store) =>
{
    var removed = await store.DeleteTaskAsync(id);
    return removed ? Results.NoContent() : Results.NotFound();
});

app.MapFallbackToFile("index.html");

app.Run();
