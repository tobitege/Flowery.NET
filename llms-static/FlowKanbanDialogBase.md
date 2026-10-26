# Overview

FlowKanbanDialogBase derives from DaisyModal and supplies the Kanban overlay dialog behavior.
CreateDialogContent builds the body, header, and footer layout.

## Focus and closing

Opening records the previously focused element. After attachment, the first visible, enabled body tab stop receives focus.
An explicit focus selection by a derived dialog takes precedence.
Override GetInitialFocusTarget to choose another initial control.

IsTabNavigationCycleEnabled defaults to true and cycles Tab/Shift+Tab within the dialog.
IsOutsideClickDismissEnabled and IsCloseOnEnterEnabled control the corresponding close behavior.
Text entry retains its normal handling before dialog-level shortcuts.
Closing restores prior focus if that visual remains attached and the owning top level is not closing.

DialogWidth, DialogHeight, smart sizing, dragging, and optional resizing use the same overlay lifetime.

## On-screen keyboard

The dialog lies in the OverlayLayer, which an on-screen keyboard covers without making it smaller.
While `TopLevel.InputPane` reports a keyboard, the dialog moves into the part above it, at most as high as that part and below the status bar (`InsetsManager.SafeAreaPadding`), and brings the focused field into view.
It returns to its size when the keyboard closes.
The dialog exposes a Window automation peer.
