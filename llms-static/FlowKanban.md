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

A double tap on a card toggles its selection, and a long press on its title edits it; a press starts no drag, so swipes scroll the board.
The double tap is decided on release and counts only taps that did not move, so swipes over a card never select it.
The sidebar scrolls when the board is lower than its buttons.

## Neumorphic Effect

The board root sets `DaisyNeumorphic.ScopeEnabled="False"`, so columns, cards and their Daisy controls stay flat when a global [neumorphic](DaisyNeumorphic.md) mode is active.
