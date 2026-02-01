# To-Do 2.0

To-Do 2.0 is a Windows desktop task manager with a fast main list, a Kanban view, and a clean stats dashboard. It focuses on practical workflows: quick capture, tags, reminders, and exports.

## Highlights
- Kanban board with status columns: New, In Progress, On Hold, Complete
- Status tags sync automatically between Kanban and the main list
- Color-coded status badges for quick scanning
- Enhanced statistics dashboard
- Markdown-optional notes with live preview

## Core Features
- Task list with filtering, tagging, and dependencies
- Attachments and subtasks
- Reminders and notifications
- Quick Add with natural language parsing
- Export to CSV or text report

## Screens
- Main list view with details panel
- Kanban board view
- Statistics dashboard

## Install
1. Download the latest ZIP from GitHub Releases.
2. Extract the folder.
3. Run `TodoWpfApp.exe`.

If prompted, install the .NET Desktop Runtime.

## Build From Source
Prerequisites:
- Windows 10/11
- .NET SDK (Windows Desktop)

Build:
```
dotnet build .\TodoWpfApp.csproj
```

Run:
```
dotnet run --project .\TodoWpfApp.csproj
```

## Usage Notes
- Use the View toggle to switch between Main Form and Kanban.
- Status is controlled by Kanban status tags: New, In Progress, On Hold, Complete.
- Notes can be plain text or Markdown (per task or general note).

## Release Notes
See `RELEASE_NOTES.md` for the current release.

## License
Add a license file if you plan to open-source this repository.
