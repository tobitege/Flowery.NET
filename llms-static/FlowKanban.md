# Overview

FlowKanban is the board control in Flowery.NET.Kanban.
It displays columns and task cards, with a home view for board selection.

## Loading and errors

A pending board load cannot replace a newer selection, a home navigation request, a detached board, or a changed BoardStore.
User management cancels obsolete provider requests. A failed request shows a localized error, distinct from an empty result.
Roles are display metadata. Applications enforce their own authorization rules.

## Keyboard and automation

The board exposes multi-selection through its Selection provider, including selected tasks outside the realized viewport.
Cards expose SelectionItem and Invoke providers. Selection changes update the task models and board selection consistently.
Space toggles a focused card. Up/Down retains card navigation.
Disabled controls reject automation changes.

The task editor uses keyboard-accessible color choices and subtask rename buttons.
Enter commits a subtask rename; Escape cancels it. Save reads the current tag and color selections.

See [FlowKanbanDialogBase](FlowKanbanDialogBase.md) for modal focus behavior.

## Touch (Android and iOS)

As in DaisyScheduler, a tap selects a card (with Ctrl, adds it) and a double tap edits it; a long press on its title edits it as well, and swipes over unselected cards scroll the board.
Taps are decided on release and only those that did not move count, so swipes over a card never select or edit it.
A finger on a selected card drags it: a copy follows the finger, the column under it shows the drop position, and the release moves the card as a drop does (WIP limits, lanes).
Near an edge of the board or a column, the view scrolls along. Avalonia has no platform drag and drop on Android, so the board moves the card itself.
The sidebar scrolls when the board is lower than its buttons.

## Neumorphic Effect

The board root sets `DaisyNeumorphic.ScopeEnabled="False"`, so columns, cards and their Daisy controls stay flat when a global [neumorphic](DaisyNeumorphic.md) mode is active.
