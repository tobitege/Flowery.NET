"""Starts the published Flowery Photos app and checks open, edit, save, reopen and a setting across a restart.

Called by the build-photos job of .github/workflows/ci.yml after the Release publish for the runner's own runtime.
Drives the unchanged app from outside, as a user would, on the platform it runs on:

1. Starts the app with a copy of a sample photo, rotates it (R) and saves a copy (Ctrl+S, which asks nothing).
   The copy must have the rotated size.
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
import os
from pathlib import Path
import shutil
import struct
import subprocess
import sys
import time

# Opening the photo waits for the library scan of an empty library; keys before that change nothing.
SETTLE_SECONDS = 8


def jpeg_size(path):
    """Returns (width, height) from the frame header of a JPEG."""
    data = path.read_bytes()
    position = 2
    while position < len(data):
        if data[position] != 0xFF:
            raise ValueError(f"{path} is not a valid JPEG")
        marker = data[position + 1]
        length = struct.unpack(">H", data[position + 2:position + 4])[0]
        if 0xC0 <= marker <= 0xCF and marker not in (0xC4, 0xC8, 0xCC):
            height, width = struct.unpack(">HH", data[position + 5:position + 9])
            return width, height
        position += 2 + length
    raise ValueError(f"{path} has no frame header")


def run(command, check=True, timeout=60):
    result = subprocess.run(command, capture_output=True, text=True, timeout=timeout, check=False)
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

    def find_window(self, pid):
        found = []
        callback_type = self.ctypes.WINFUNCTYPE(self.wintypes.BOOL, self.wintypes.HWND, self.wintypes.LPARAM)

        def callback(hwnd, _):
            owner = self.wintypes.DWORD()
            self.user32.GetWindowThreadProcessId(hwnd, self.ctypes.byref(owner))
            if owner.value == pid and self.user32.IsWindowVisible(hwnd):
                found.append(hwnd)
            return True

        self.user32.EnumWindows(callback_type(callback), 0)
        return max(found, key=lambda hwnd: self.size(hwnd)[0] * self.size(hwnd)[1], default=None)

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
        script = ("Add-Type -AssemblyName System.Windows.Forms,System.Drawing; "
                  "$b = [System.Windows.Forms.SystemInformation]::VirtualScreen; "
                  "$bitmap = New-Object System.Drawing.Bitmap $b.Width, $b.Height; "
                  "[System.Drawing.Graphics]::FromImage($bitmap).CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size); "
                  f"$bitmap.Save('{path}')")
        run(["powershell", "-NoProfile", "-Command", script], check=False)


class LinuxDriver:
    def find_window(self, pid):
        windows = run(["xdotool", "search", "--onlyvisible", "--pid", str(pid)], check=False).split()
        return max(windows, key=lambda window: self.size(window)[0] * self.size(window)[1], default=None)

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

    def find_window(self, pid):
        try:
            return pid if int(self.system_events(pid, ["count windows of p"])) > 0 else None
        except (RuntimeError, ValueError):
            return None

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

    def start(self, photo):
        self.starts += 1
        environment = dict(os.environ, FLOWERY_STATE_ROOT=str(self.state))
        log = (self.output / f"app-{self.starts}.log").open("w", encoding="utf-8")
        process = subprocess.Popen([str(self.executable), str(photo)], cwd=self.executable.parent, env=environment,
                                   stdout=log, stderr=subprocess.STDOUT)
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
        """Rotates the open photo and saves a copy; returns the copy."""
        before = set(self.photos.iterdir())
        self.driver.key(window, "r")
        time.sleep(2)
        self.driver.key(window, "s", control=True)
        deadline = time.monotonic() + 30
        while True:
            # The copy is complete once it has a frame header.
            for path in set(self.photos.iterdir()) - before:
                try:
                    width, height = jpeg_size(path)
                except (OSError, ValueError, struct.error):
                    continue
                expected = tuple(reversed(jpeg_size(source)))
                if (width, height) != expected:
                    raise RuntimeError(f"{path.name} is {width} x {height}, not rotated to {expected[0]} x {expected[1]}")
                print(f"Saved the rotated copy {path.name}, {width} x {height}", flush=True)
                return path
            if process.poll() is not None:
                raise RuntimeError(f"Photos exited with code {process.returncode} instead of saving a copy")
            if time.monotonic() > deadline:
                raise RuntimeError(f"Photos saved no copy of {source.name} within 30 s")
            time.sleep(1)

    def stop(self, process, window):
        self.driver.close(window)
        try:
            code = process.wait(timeout=30)
        except subprocess.TimeoutExpired:
            process.kill()
            raise RuntimeError("Photos did not exit within 30 s after its window was closed")
        if code:
            raise RuntimeError(f"Photos exited with code {code}")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--app", type=Path, required=True, help="the published app folder")
    parser.add_argument("--output", type=Path, required=True, help="folder for logs, screenshots and photos")
    args = parser.parse_args()
    output = args.output.resolve()
    if output.exists():
        shutil.rmtree(output)
    output.mkdir(parents=True)
    check = Check(args.app.resolve(strict=True), output)
    check.photos.mkdir()
    # A landscape sample, so the rotation changes its size.
    sample = next(path for path in sorted((args.app / "Assets" / "Samples").glob("wallpaper_*.jpg"))
                  if jpeg_size(path)[0] != jpeg_size(path)[1])
    photo = check.photos / "photo.jpg"
    shutil.copyfile(sample, photo)
    print(f"Photo: {sample.name}, {jpeg_size(photo)[0]} x {jpeg_size(photo)[1]}", flush=True)

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
    print("Photos opens, edits, saves and reopens a photo and keeps its window size across a restart", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
