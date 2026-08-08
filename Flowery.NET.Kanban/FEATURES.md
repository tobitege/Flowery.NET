# Flowery.NET Kanban Feature Overview

`Flowery.NET.Kanban` is a reusable task-board package for Avalonia applications.
It includes the board interface, dialogs, data models, storage support, localization, and accessibility support.
The host app can use the complete interface or control the board through public methods, commands, and events.

## Package Contents

- `FlowKanban` provides the complete board and board-home interface.
- `FlowKanbanManager` provides direct access to board, column, lane, task, archive, selection, and storage operations.
- The data models support binding, change notifications, JSON storage, and custom fields.
- The package includes editors for boards, columns, tasks, lanes, filters, and settings.
- The default styles use the Flowery.NET theme and its Daisy controls.
- The package includes text for 12 languages.
- Keyboard and screen-reader users receive names and actions for the main controls.

## Basic Use

Add the Kanban styles after `DaisyUITheme`:

```xml
<Application.Styles>
    <daisy:DaisyUITheme />
    <StyleInclude Source="avares://Flowery.NET.Kanban/Themes/Generic.axaml" />
</Application.Styles>
```

Add the control to a view:

```xml
xmlns:kanban="clr-namespace:Flowery.NET.Kanban.Controls;assembly=Flowery.NET.Kanban"

<kanban:FlowKanban Board="{Binding Board}" />
```

`FlowKanbanData` contains the complete board data. The host app can bind an existing board or let the control create one.

## Board Home

The board home provides a starting page for saved boards.

- It lists all available boards with their names and last-change dates.
- It searches the board list by name.
- It sorts boards by name or last-change date in either direction.
- It opens, renames, duplicates, removes, and exports boards.
- It creates an empty board through the board editor.
- It creates a demo board with sample data.
- If no saved boards exist, it shows an empty state.
- It can show a custom welcome title and message.
- It remembers the last board and the last view.

## Board Layouts

The control has three board layouts.

- The standard layout shows all columns in a horizontal board.
- If the available width is small, the compact layout shows one selected column.
- The swimlane layout groups tasks into named horizontal lanes.
- If tasks have no lane, the swimlane layout includes an Unassigned lane.

The compact layout starts automatically on narrow windows and on small mobile screens.
The host app can also set a compact-layout override.

Compact columns can use an adaptive card count or a fixed card count from 1 through 20.
The adaptive mode uses the available height and the measured card height.

## Board Display Controls

- Users can zoom the complete board through the supported Flowery.NET sizes.
- Users can resize all columns with the pointer or the keyboard.
- The host app can set the minimum, maximum, and current column width.
- Users can collapse individual columns into narrow rails.
- A collapsed column releases its unused board space.
- Users can show or hide the board status bar.
- The status bar shows the column count, zoom size, and archive action.
- Users can show or hide the archive column.
- The toolbar provides search, filters, board actions, task creation, settings, statistics, and keyboard help.
- The add-task control can appear above tasks, below tasks, or in both positions.
- Task details can expand automatically.
- Flowery.NET theme changes update the Kanban controls.

## Board Data

Each board stores the following main information:

- A stable board ID, title, description, creator, creation date, and last-change date.
- A list of board tags.
- Columns, lanes, tasks, and saved view grouping.
- The archive column and the Done column.
- Automatic archive settings for old Done tasks.
- The next sequential work-item number.
- A schema version for older saved boards.
- Custom board fields for host-specific data.

Each column stores the following information:

- A stable ID and title.
- Its ordered task list.
- A work-in-progress limit for the complete column.
- Optional work-in-progress limits for individual lanes.
- Policy text, such as entry rules or a definition of done.
- Its collapsed state.
- Custom column fields for host-specific data.

Each lane stores a stable ID, title, optional description, and custom fields.
The board editor can add, rename, reorder, and remove lanes.
When a lane is removed, its tasks can move to another lane or become unassigned.

## Task Data

A task appears as a card on the board. Each task can contain the following information:

- A stable task ID and a sequential work-item number.
- A title, description, and card color.
- Low, normal, high, or urgent priority.
- Manual progress from 0 through 100 percent.
- Subtasks with their own completion state.
- If subtasks exist, progress is calculated from those subtasks.
- Planned start and end dates.
- Actual start and end dates.
- Estimated and actual effort in hours or days.
- A stable assignee ID and a saved assignee name.
- A current assignee avatar and descriptive roles from the host app.
- Tags and an optional lane.
- A blocked state, reason, start date, and blocked-day count.
- An overdue state based on the planned end date.
- Archive dates and the original column position.
- Custom task fields for host-specific data.

Avatars and roles are display data. They are not written to the board JSON.
The stable assignee ID and the last known assignee name are written to the board JSON.

## Task Editing and Movement

- Users can add tasks through the full editor or the inline quick-add field.
- Users can edit or remove a task from its card.
- The editor covers all task dates, effort, priority, progress, assignee, tags, blocked state, and subtasks.
- Date fields use the date order and format from the current region.
- Users can drag tasks between columns and between swimlanes.
- Users can reorder tasks inside a column or lane.
- Keyboard shortcuts can move the focused task in all four directions.
- The `CardMoving` event lets the host app cancel a move and provide a reason.
- The `CardMoved` event reports a completed move.
- A move reports the target work limit and the resulting task count.

## Columns, Lanes, and Work Limits

- Users can add, rename, reorder, collapse, resize, and remove columns.
- Column reordering is available in the standard and swimlane layouts.
- Each column can contain policy text.
- Each column can have one work-in-progress limit.
- Each swimlane cell can override the column limit.
- The interface shows current counts and exceeded limits.
- If a move exceeds a limit, normal pointer and keyboard moves show a warning.
- If the host app needs a hard limit, the manager API can reject a move.
- The host app can cancel any move through `CardMoving`.

## Search and Filters

The simple search field finds text in task titles, descriptions, tags, and assignee names.

The expanded filter supports the following criteria:

- One or more priority levels.
- Overdue tasks.
- Blocked tasks.
- Planned start and end date ranges.
- Tasks due today.
- Tasks due during the current week.
- Selected assignee IDs.

`FlowKanbanFilterCriteria` also supports selected columns and inclusion or exclusion of archived tasks.

The filter bar shows the number of active criteria.
Users can clear all criteria with one action.
Users can save, load, and remove named filter presets.
Preset names are limited to 50 characters and exclude control characters.

## Selection and Bulk Actions

Users can select one or more tasks for a group action.

- Move the selected tasks to another column.
- Set one priority for all selected tasks.
- Set one tag list for all selected tasks.
- Set or clear one due date for all selected tasks.
- Archive the selected tasks.
- Remove the selected tasks.
- Clear the complete selection.

The manager API can also set or clear the blocked state for all selected tasks.

## Done and Archive Workflows

- One column can be the Done column.
- When a task enters the Done column, it receives a completion date.
- The board can archive Done tasks after a selected number of days.
- One column can be the archive column.
- Users can archive and restore individual tasks.
- If the original location still exists, restored tasks return to their original column and position.
- The manager can archive all completed tasks from a column.
- The manager can remove all archived tasks permanently from the board data.
- The archive column can stay hidden during normal work.

## Undo and Redo

Undo and redo are optional.
The history keeps up to 50 board commands.
The interface shows whether an undo or redo action is available.
The history also provides a description of the next undo or redo action.

## Board Statistics

The statistics dialog shows the following information:

- Total, active, and archived task counts.
- Blocked, overdue, and unassigned task counts.
- Work-limit use and the number of columns over their limits.
- Active and total task counts for each column.
- Active, blocked, and overdue task counts for each lane.

The manager API also provides column counts, task counts, empty-column counts, and task counts by column.

## Storage, Import, and Export

- The default board store keeps multiple named boards through Flowery.NET state storage.
- The control can save changes automatically after edits.
- Users can save and load a board through the built-in actions.
- Users can import and export complete boards as readable JSON.
- The board home can export a saved board without opening it.
- `BoardExportRequested` lets the host app handle an export with the board ID, title, and JSON.
- Without an event handler, export uses a file picker or copies the JSON to the clipboard.
- The host app can replace the default store through `IBoardStore`.
- Storage errors include the failed operation and the original error.
- The control exposes storage errors through `PersistenceFailed`.
- When older board JSON is loaded, it is upgraded.
- Imported board data receives safe IDs, number ranges, collection limits, and valid references.

The `IBoardStore` contract covers board listing, loading, saving, removal, and JSON export.
This contract works with local files, a database, a web service, or another host-owned store.

## Settings

The settings dialog controls the following behavior:

- Confirmation before column removal.
- Confirmation before task removal.
- Automatic saving after edits.
- Automatic expansion of task details.
- Undo and redo.
- Add-task placement.
- Normal column width.
- Adaptive or fixed compact-column sizing.
- The fixed card count for compact columns.
- Archive-column visibility.
- Welcome-message visibility, title, and text.

The control also stores status-bar visibility, the last board, and the last view.
Settings load and save errors are returned to the host app.

## User API: Assignees Without External Account Integration

An assignee is a person who can own a task.
The Kanban package does not sign users in and does not connect to Slack, Microsoft, Google, or another account service.
The host app owns all external connections, login steps, permissions, privacy rules, and access tokens.

The host app gives the board only the data that it needs for display:

- A stable ID for each assignee.
- A display name.
- An optional avatar image.
- Optional descriptive roles, such as `Designer` or `Reviewer`.

Roles are display text only. The Kanban package never uses roles to grant or deny access.

Set `FlowKanban.AssigneeAdapter` to an `IFlowKanbanAssigneeAdapter` implementation.
The included `FlowKanbanAssigneeAdapter` covers direct lists, list callbacks, and single-ID callbacks.
If the host app needs another data source, it can implement the interface.
A custom adapter raises `AssigneesChanged` after its available assignee data changes.

### Why the ID Matters

The same assignee must keep the same ID between app sessions.
The board stores this ID with each task.
Names, avatars, and roles can change without changing task ownership.

IDs are case-sensitive. `person-42` and `Person-42` are different IDs.
An ID cannot be empty.
If a list contains the same ID more than once, the adapter uses the first entry.

When an old task contains an unknown ID, the board keeps that ID and its last known name.
The task receives a new name, avatar, and role list after the host app resolves the ID.

### Use a Direct Assignee List

If the host app already has the complete assignee list, use `FlowKanbanAssigneeAdapter`:

```csharp
var assignees = new FlowKanbanAssigneeAdapter(new[]
{
    new FlowKanbanAssignee(
        id: "person-42",
        displayName: "Mina Singh",
        avatarSource: minaAvatar,
        roles: new[] { "Designer", "Reviewer" })
});

kanban.AssigneeAdapter = assignees;
```

When the host data changes, replace the list:

```csharp
assignees.SetAssignees(latestAssignees);
```

`SetAssignees` updates the list and tells every attached board to reload it.

### Use Host Callbacks

The board calls a callback to get assignee data.
If the host app loads assignees from its own service, use callbacks:

```csharp
kanban.AssigneeAdapter = new FlowKanbanAssigneeAdapter(
    getAssigneesAsync: LoadAssigneesAsync,
    resolveAssigneeAsync: FindAssigneeAsync);
```

The methods have these shapes:

```csharp
Task<IReadOnlyList<FlowKanbanAssignee>> LoadAssigneesAsync(
    CancellationToken cancellationToken);

Task<FlowKanbanAssignee?> FindAssigneeAsync(
    string assigneeId,
    CancellationToken cancellationToken);
```

`LoadAssigneesAsync` returns the choices for the task editor and the assignee filter.
`FindAssigneeAsync` finds one ID that already exists in saved task data.
If the host app does not know the ID, it returns `null`.

If the host app only resolves saved IDs, use the resolver-only adapter:

```csharp
kanban.AssigneeAdapter = new FlowKanbanAssigneeAdapter(FindAssigneeAsync);
```

This form resolves assignees already used by tasks. It does not add other assignees to the editor or filter choices.

A resolver must return the requested ID without changes.
If a resolver returns a different ID, the board reports an error.

When callback data changes, call `NotifyAssigneesChanged()`:

```csharp
assigneeAdapter.NotifyAssigneesChanged();
```

The host app can also request a reload:

```csharp
await kanban.RefreshAssigneesAsync(cancellationToken);
```

### Avatars and Roles

`AvatarSource` accepts an Avalonia `IImage`.
The host app owns the image and keeps it available while the board displays it.
If an ID cannot be resolved, the board clears the current avatar.

The board shows roles as descriptive text on task cards.
The role list is not saved because the host app remains the source of that data.

### Changes, Delays, and Errors

When the control opens, the board reloads assignees.
When the adapter reports a change, the board reloads them again.
A newer reload replaces an older unfinished reload.
An unfinished reload cannot change the board after the control closes.

Callback errors do not rewrite the assignee IDs or names in task data.
The `AssigneeAdapterFailed` event reports the operation and the original error.
If the assignee ID is available, the event also reports it.

```csharp
kanban.AssigneeAdapterFailed += (_, error) =>
{
    logger.LogError(
        error.Exception,
        "Assignee operation {Operation} failed for {AssigneeId}.",
        error.Operation,
        error.AssigneeId);
};
```

The host app decides how to show or record this error.
The Kanban package does not store external service credentials or retry external service requests.

## Host Control API

`FlowKanbanManager` provides direct operations for hosts that do not use only the built-in buttons.

- Board initialization, settings, loading, saving, listing, renaming, duplication, removal, and export.
- Column creation, insertion, movement, renaming, lookup, policy changes, and removal.
- Lane creation, insertion, movement, renaming, lookup, task reassignment, and removal.
- Task creation, insertion, lookup, editing, movement, reordering, and removal.
- Subtask creation, completion, progress, and removal.
- Work limits, blocked tasks, Done tasks, archive tasks, selection, bulk actions, and statistics.
- Queries for tasks by ID, column, lane, blocked state, archive state, or work-limit state.
- Zoom, lane grouping, board clearing, board replacement, JSON import, JSON export, and default-board creation.

`FlowKanban` also provides bindable commands for the main interface actions.
If the host app needs different behavior, it can replace a command.

The control reports important changes through events:

- Board size, column width, and compact-layout changes.
- Drag completion and task movement.
- Task, column, and board edits.
- Lane-grouping and search-filter changes.
- Storage and assignee-adapter errors.

`FlowKanbanManager` also reports initialization, shutdown, settings, board loading, board saving, and storage errors.

## Keyboard and Accessibility

- Tab and arrow keys move focus through columns, task cards, and add-task controls.
- Enter opens the focused task editor.
- Control+B adds a column.
- If confirmation is active, Control+D removes the last column after confirmation.
- Control+T edits the active column.
- Control+N starts the inline add-task field.
- Control+F moves focus to search.
- Control+Plus and Control+Minus change the board size.
- Control+Alt+Left and Control+Alt+Right move focus between columns.
- Control+Shift with an arrow key moves the focused task.
- Keyboard controls can resize columns with small or large steps and jump to either width limit.

The built-in keyboard-help dialog lists the available actions.

Screen readers receive names, help text, current values, button actions, task-edit actions, and column-collapse state.
The column-width control reports its current, minimum, and maximum values.
Hidden or removed controls do not remain in the accessibility tree.

## Localization

The package includes Arabic, Chinese, English, French, German, Hebrew, Italian, Japanese, Korean, Spanish, Turkish, and Ukrainian text.
The metrics dialog uses right-to-left text direction for right-to-left cultures.
Board labels, dialogs, actions, accessibility names, and keyboard help use the active language.
When the display language changes, date fields keep the current regional date order.

## Deliberate Boundaries

The package supplies a Kanban board, not a complete user-account system.
It does not provide login, identity checks, organization membership, role-based access, or third-party service support.
The host app supplies these features and passes only assignee display data to the board.

The package does not decide where application data belongs.
The host app can use the default state store or provide an `IBoardStore` that matches its data rules.
