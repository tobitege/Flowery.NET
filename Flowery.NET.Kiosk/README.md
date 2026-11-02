# Flowery Kiosk

Flowery Kiosk is a desktop demo for Windows, Linux and macOS that runs on its own, for example on a screen at a fair.
It cycles through six slides, each with its own theme:

| Slide | Theme | Controls |
| --- | --- | --- |
| Authentication | Halloween | Inputs, OTP input, buttons, avatars, divider, status indicator |
| Data Visualization | Synthwave | Radial progress, number flow, stats, progress bars, live status indicators |
| Communication | Nord | Chat bubble, avatar stack, alerts, rating, twelve loading animations, badges |
| Enhanced Controls | Dark | Weather card, color wheel |
| Runtime Scaling | Dracula | `FloweryScaleManager` from 50 to 200 percent, size tiers of input, buttons and badges |
| Background Patterns | Coffee | Six `DaisyPatternedCard` patterns |

The app ports the [Flowery.WinUI Kiosk](https://github.com/tobitege/Flowery.WinUI/tree/main/Flowery.WinUI.Kiosk).
`Flowery.NET.Kiosk` holds the app and its slides, `Flowery.NET.Kiosk.Desktop` starts it on the desktop.

```powershell
dotnet run --project Flowery.NET.Kiosk.Desktop
```

## Use

The slides change every eight seconds with a fade; the bar at the top shows the time to the next slide, and stays empty while the slides change only by hand.
Right, Space and N show the next slide, Left and P the previous one; both wrap around.
A slide changed by hand gets the full eight seconds again.
With reduced motion turned on in the system, the slides change only by hand.

## Command line

| Option | Effect |
| --- | --- |
| `--manual` | No autoplay; the slides change only by hand |
| `--smoke-test <absolute XML path>` | Runs the smoke checks in the window, writes the report to the path and exits with 0 when every check passed |

`scripts/test_kiosk.ps1` runs the smoke checks and reads the report; see [scripts/README.md](../scripts/README.md#test_kioskps1).

The app keeps its state in `Flowery.NET.Kiosk` in the local application data, or in the absolute path of `FLOWERY_STATE_ROOT`.
A failure ends the app with exit code 1; outside a smoke run it writes the error to `crash.log` in that folder.

## Assets

The Noto fonts of the Gallery ship in `Assets/Fonts` next to the executable, with their license in `Assets/Fonts/OFL.txt`.
`Assets/Kiosk` holds the app icon and splash screen of Flowery.WinUI as SVG; the window uses the Gallery's `app_icon.ico`.

## Differences from Flowery.WinUI

- The slides name Flowery.NET and Avalonia where WinUI names itself.
- Flowery.NET has no `InvoiceStamp` loading animation; the communication slide shows `DocumentStamp` instead.
- The scalable controls of Flowery.NET scale their fonts by the factor of `FloweryScaleManager`. The scaling slide sets the size tier of its demo controls itself, as WinUI does, so their card does not take part in the scaling.
- The smoke checks test the Avalonia counterparts of WinUI internals: the text presenter of an input instead of the native TextBox, the window icon instead of Win32 icon messages, and the drawn pattern of each card instead of the count of the SVG pattern files, which Flowery.NET embeds in its assembly.
