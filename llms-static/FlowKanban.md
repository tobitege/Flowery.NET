# Overview

FlowKanban is the board control in Flowery.NET.Kanban.
It displays columns and task cards, with a home view for board selection.

## Loading and errors

A pending board load cannot replace a newer selection, a home navigation request, a detached board, or a changed BoardStore.
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

As in the apps of the platforms, a tap opens a card's editor, and holding a card selects it; while cards are selected, a tap adds a card to the selection or takes it out.
Taps are decided on release and only those that did not move count, so swipes over a card scroll the board and never open, select or drag it.
A tap or click on a free place of the board (not on a card or a control) clears the selection, on touch and with the mouse; a swipe or a mouse drag there does not.
After a hold, the finger drags the card: a copy follows the finger, the column under it shows the drop position, and the release moves the card as a drop does (WIP limits, lanes).
Near an edge of the board or a column, the view scrolls along. Avalonia has no platform drag and drop on Android, so the board moves the card itself.
The sidebar scrolls when the board is lower than its buttons.

## Pinch to Zoom

On a touch screen, on Android, iOS and in the browser, spreading two fingers zooms the board in and pinching them zooms it out, one size tier per quarter of their distance, within the same tiers as the zoom buttons and `Ctrl + +` / `Ctrl + -` (`ExtraSmall` to `ExtraLarge`), in the standard, compact and swimlane layouts.
Once the second finger lands, the pinch takes both fingers, so the board does not scroll while they pinch. On Android and iOS a pinch neither moves nor selects cards: a pending hold of the first finger selects nothing, and a card it drags goes back without a drop.
In the browser the page does not zoom, since Avalonia keeps touch gestures on its canvas. One finger works as before.

## Neumorphic Effect

The board root sets `DaisyNeumorphic.ScopeEnabled="False"`, so columns, cards and their Daisy controls stay flat when a global [neumorphic](DaisyNeumorphic.md) mode is active.
