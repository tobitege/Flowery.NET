"""Starts the published Flowery Photos app and checks open, edit, save, reopen and a setting across a restart.

Called by the build-photos job of .github/workflows/ci.yml after the Release publish for the runner's own runtime.
Drives the unchanged app from outside, as a user would, on the platform it runs on:

1. Writes a 320 x 200 PNG with a red, a green and a blue band from left to right, starts the app with it, rotates
   it clockwise (R) and saves a copy (Ctrl+S, which asks nothing). The copy must be 200 x 320 with red on top and
   blue at the bottom. The shipped samples are square, so their size would not show the rotation.
2. Resizes the window and closes it. Photos saves the window size when it closes and restores it at start.
3. Starts the app again with the saved copy, checks the window size, rotates and saves the copy once more, which
   shows that the copy opened in the editor, and closes the app.

Each start uses a new FLOWERY_STATE_ROOT in --output, so the runner's own settings and Pictures stay untouched.
Windows uses Win32 (ctypes); Linux uses xdotool and wmctrl, which need a window manager on the display, such as
openbox on Xvfb; macOS uses System Events through osascript, which needs the accessibility permission that the
hosted runners grant. The app logs, screenshots and photos go to --output.

    python3 scripts/check_photos_app.py --app publish --output artifacts/photos
"""

import argparse
import math
import os
from pathlib import Path
import struct
import subprocess
import sys
import time
import zlib

import ci_png

# Opening the photo waits for the library scan of an empty library; keys before that change nothing.
SETTLE_SECONDS = 8
RED = (220, 40, 40)
GREEN = (40, 180, 60)
BLUE = (40, 60, 220)


def write_png(path, width, height, color):
    """Writes an 8-bit RGB PNG; color(x, y) returns the (red, green, blue) of each pixel."""
    rows = b"".join(b"\x00" + bytes(channel for x in range(width) for channel in color(x, y)) for y in range(height))

    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))

    path.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
                     + chunk(b"IDAT", zlib.compress(rows)) + chunk(b"IEND", b""))


def edges(image):
    """Returns the colors near the middle of the left, top, right and bottom edges."""
    width, height = image.width, image.height
    return [image.pixel(x, y) for x, y in
            ((8, height // 2), (width // 2, 8), (width - 9, height // 2), (width // 2, height - 9))]


def same_color(first, second):
    return all(abs(a - b) <= 24 for a, b in zip(first, second))


def run(command, check=True, timeout=60, env=None):
    result = subprocess.run(command, capture_output=True, text=True, timeout=timeout, check=False, env=env)
    if check and result.returncode:
        raise RuntimeError(f"{' '.join(command)} failed: {result.stdout}{result.stderr}")
    return result.stdout.strip()


class WindowsDriver:
    def __init__(self):
        import ctypes
        from ctypes import wintypes
        self.ctypes = ctypes
        self.wintypes = wintypes
        try:
            ctypes.windll.shcore.SetProcessDpiAwareness(2)
        except OSError:
            pass
        self.user32 = ctypes.WinDLL("user32", use_last_error=True)
        self.user32.GetForegroundWindow.restype = wintypes.HWND

    def windows(self, pid):
        found = []
        callback_type = self.ctypes.WINFUNCTYPE(self.wintypes.BOOL, self.wintypes.HWND, self.wintypes.LPARAM)

        def callback(hwnd, _):
            owner = self.wintypes.DWORD()
            self.user32.GetWindowThreadProcessId(hwnd, self.ctypes.byref(owner))
            if owner.value == pid and self.user32.IsWindowVisible(hwnd):
                found.append(hwnd)
            return True

        self.user32.EnumWindows(callback_type(callback), 0)
        return found

    def find_window(self, pid):
        return max(self.windows(pid), key=lambda hwnd: math.prod(self.size(hwnd)), default=None)

    def activate(self, window):
        if self.user32.GetForegroundWindow() == window:
            return
        # Windows lets a process take the foreground only in some cases; sharing the input state of the current
        # foreground thread is one of them.
        current = self.user32.GetForegroundWindow()
        own_thread = self.ctypes.windll.kernel32.GetCurrentThreadId()
        foreground_thread = self.user32.GetWindowThreadProcessId(current, None) if current else 0
        attached = foreground_thread and foreground_thread != own_thread \
            and self.user32.AttachThreadInput(own_thread, foreground_thread, True)
        try:
            self.user32.ShowWindow(window, 9)  # SW_RESTORE
            self.user32.BringWindowToTop(window)
            self.user32.SetForegroundWindow(window)
        finally:
            if attached:
                self.user32.AttachThreadInput(own_thread, foreground_thread, False)
        time.sleep(0.5)
        if self.user32.GetForegroundWindow() != window:
            raise RuntimeError("The Photos window did not come to the foreground")

    def key(self, window, key, control=False):
        self.activate(window)
        codes = [0x11] if control else []  # VK_CONTROL
        codes.append(ord(key.upper()))
        for code in codes:
            self.user32.keybd_event(code, 0, 0, 0)
            time.sleep(0.05)
        for code in reversed(codes):
            self.user32.keybd_event(code, 0, 2, 0)  # KEYEVENTF_KEYUP
            time.sleep(0.05)

    def size(self, window):
        rect = self.wintypes.RECT()
        self.user32.GetWindowRect(window, self.ctypes.byref(rect))
        return rect.right - rect.left, rect.bottom - rect.top

    def resize(self, window, width, height):
        # SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE
        self.user32.SetWindowPos(window, None, 0, 0, width, height, 0x0002 | 0x0004 | 0x0010)

    def close(self, window):
        self.user32.PostMessageW(window, 0x0010, 0, 0)  # WM_CLOSE

    def screenshot(self, path):
        # The path goes in through the environment, so no character of it can end the PowerShell string.
        script = ("Add-Type -AssemblyName System.Windows.Forms,System.Drawing; "
                  "$b = [System.Windows.Forms.SystemInformation]::VirtualScreen; "
                  "$bitmap = New-Object System.Drawing.Bitmap $b.Width, $b.Height; "
                  "[System.Drawing.Graphics]::FromImage($bitmap).CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size); "
                  "$bitmap.Save($env:FLOWERY_SCREENSHOT)")
        run(["powershell", "-NoProfile", "-Command", script], check=False,
            env=dict(os.environ, FLOWERY_SCREENSHOT=str(path)))


class LinuxDriver:
    def windows(self, pid):
        return run(["xdotool", "search", "--onlyvisible", "--pid", str(pid)], check=False).split()

    def find_window(self, pid):
        return max(self.windows(pid), key=lambda window: math.prod(self.size(window)), default=None)

    def key(self, window, key, control=False):
        run(["xdotool", "windowactivate", "--sync", window])
        run(["xdotool", "key", "--clearmodifiers", f"ctrl+{key}" if control else key])

    def size(self, window):
        values = dict(line.split("=", 1) for line in run(["xdotool", "getwindowgeometry", "--shell", window]).splitlines())
        return int(values["WIDTH"]), int(values["HEIGHT"])

    def resize(self, window, width, height):
        run(["xdotool", "windowsize", "--sync", window, str(width), str(height)])

    def close(self, window):
        run(["wmctrl", "-i", "-c", hex(int(window))])

    def screenshot(self, path):
        run(["import", "-window", "root", str(path)], check=False)


class MacDriver:
    @staticmethod
    def system_events(pid, commands):
        script = ['tell application "System Events"', f"set p to first process whose unix id is {pid}", *commands,
                  "end tell"]
        return run(["osascript", *sum((["-e", line] for line in script), [])])

    def windows(self, pid):
        # System Events addresses the windows through the process, so the process stands for them.
        try:
            return [pid] * int(self.system_events(pid, ["count windows of p"]))
        except (RuntimeError, ValueError):
            return []

    def find_window(self, pid):
        return pid if self.windows(pid) else None

    def key(self, window, key, control=False):
        self.system_events(window, ["set frontmost of p to true", "delay 0.5",
                                    f'keystroke "{key}"' + (" using control down" if control else "")])

    def size(self, window):
        width, height = self.system_events(window, ["get size of window 1 of p"]).split(", ")
        return int(width), int(height)

    def resize(self, window, width, height):
        self.system_events(window, [f"set size of window 1 of p to {{{width}, {height}}}"])

    def close(self, window):
        self.system_events(window, ['click (first button of window 1 of p whose subrole is "AXCloseButton")'])

    def screenshot(self, path):
        run(["screencapture", "-x", str(path)], check=False)


class Check:
    def __init__(self, app, output):
        self.driver = {"win32": WindowsDriver, "linux": LinuxDriver, "darwin": MacDriver}[sys.platform]()
        self.executable = app / ("Flowery.NET.Photos.Desktop.exe" if sys.platform == "win32" else "Flowery.NET.Photos.Desktop")
        if not self.executable.is_file():
            raise FileNotFoundError(self.executable)
        self.output = output
        self.state = output / "state"
        self.photos = output / "photos"
        self.starts = 0
        self.log = None

    def start(self, photo):
        self.starts += 1
        environment = dict(os.environ, FLOWERY_STATE_ROOT=str(self.state))
        self.log = (self.output / f"app-{self.starts}.log").open("w", encoding="utf-8")
        process = subprocess.Popen([str(self.executable), str(photo)], cwd=self.executable.parent, env=environment,
                                   stdout=self.log, stderr=subprocess.STDOUT)
        deadline = time.monotonic() + 60
        window = None
        while window is None:
            if process.poll() is not None:
                raise RuntimeError(f"Photos exited at start with code {process.returncode}")
            if time.monotonic() > deadline:
                raise RuntimeError("The Photos window did not appear within 60 s")
            time.sleep(1)
            window = self.driver.find_window(process.pid)
        print(f"Start {self.starts}: window after {60 - (deadline - time.monotonic()):.0f} s", flush=True)
        time.sleep(SETTLE_SECONDS)
        return process, window

    def rotate_and_save(self, process, window, source):
        """Rotates the open photo clockwise and saves a copy; returns the copy."""
        before = set(self.photos.iterdir())
        original = ci_png.read(source.read_bytes())
        self.driver.key(window, "r")
        time.sleep(2)
        self.driver.key(window, "s", control=True)
        deadline = time.monotonic() + 30
        while True:
            for path in set(self.photos.iterdir()) - before:
                try:
                    copy = ci_png.read(path.read_bytes())
                except (OSError, ValueError, struct.error, zlib.error):
                    # Not written completely yet.
                    continue
                if (copy.width, copy.height) != (original.height, original.width):
                    raise RuntimeError(f"{path.name} is {copy.width} x {copy.height}, not rotated to "
                                       f"{original.height} x {original.width}")
                # Clockwise, the left edge becomes the top, the top the right, and so on.
                left, top, right, bottom = edges(original)
                if not all(map(same_color, edges(copy), (bottom, left, top, right))):
                    raise RuntimeError(f"{path.name} is not {source.name} rotated clockwise: its edges are "
                                       f"{edges(copy)}, the edges of {source.name} {edges(original)}")
                print(f"Saved the rotated copy {path.name}, {copy.width} x {copy.height}", flush=True)
                # Photos opens the copy after writing it, which ends the edit; a close before that asks whether to
                # save the edit.
                time.sleep(3)
                return path
            if process.poll() is not None:
                raise RuntimeError(f"Photos exited with code {process.returncode} instead of saving a copy")
            if time.monotonic() > deadline:
                raise RuntimeError(f"Photos saved no copy of {source.name} within 30 s. Either R and Ctrl+S came "
                                   f"before the photo was open ({SETTLE_SECONDS} s after the window appeared), or "
                                   "the window did not get them")
            time.sleep(1)

    def stop(self, process, window):
        self.driver.close(window)
        try:
            code = process.wait(timeout=30)
        except subprocess.TimeoutExpired:
            # A dialog is a window of its own: with more than the main window open, Photos asks something, such as
            # whether to save an edit that a close came too early for.
            windows = len(self.driver.windows(process.pid))
            process.kill()
            raise RuntimeError("Photos did not exit within 30 s after its window was closed; " +
                               (f"it shows {windows} windows, so a dialog waits for an answer" if windows > 1 else
                                f"it shows {windows} window(s)"))
        finally:
            self.close_log()
        if code:
            raise RuntimeError(f"Photos exited with code {code}")

    def close_log(self):
        if self.log is not None:
            self.log.close()
            self.log = None


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--app", type=Path, required=True, help="the published app folder")
    parser.add_argument("--output", type=Path, required=True,
                        help="a new or empty folder for logs, screenshots and photos")
    args = parser.parse_args()
    output = args.output.resolve()
    # The check writes its state and photos there; it neither mixes them with other files nor deletes any.
    if output.exists() and any(output.iterdir()):
        parser.error(f"{output} is not empty; pass a new or empty folder")
    output.mkdir(parents=True, exist_ok=True)
    check = Check(args.app.resolve(strict=True), output)
    check.photos.mkdir()
    photo = check.photos / "photo.png"
    write_png(photo, 320, 200, lambda x, y: RED if x < 107 else GREEN if x < 213 else BLUE)

    process = None
    try:
        process, window = check.start(photo)
        copy = check.rotate_and_save(process, window, photo)
        width, height = check.driver.size(window)
        size = (width - 160, height - 100)
        check.driver.resize(window, *size)
        time.sleep(2)
        size = check.driver.size(window)
        print(f"Window resized from {width} x {height} to {size[0]} x {size[1]}", flush=True)
        check.stop(process, window)

        process, window = check.start(copy)
        restored = check.driver.size(window)
        if abs(restored[0] - size[0]) > 2 or abs(restored[1] - size[1]) > 2:
            raise RuntimeError(f"The window opened at {restored[0]} x {restored[1]}, not at the saved {size[0]} x {size[1]}")
        print(f"The window kept its size {restored[0]} x {restored[1]} across the restart", flush=True)
        check.rotate_and_save(process, window, copy)
        check.stop(process, window)
        process = None
    except Exception as error:
        print(f"::error::{error}", flush=True)
        check.driver.screenshot(output / "failure.png")
        return 1
    finally:
        if process is not None and process.poll() is None:
            process.kill()
            process.wait(timeout=30)
        check.close_log()
    print("Photos opens, edits, saves and reopens a photo and keeps its window size across a restart", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
