"""Installs the Android Gallery on a running emulator and checks that it works.

Called by the android-gallery job of .github/workflows/ci.yml inside reactivecircus/android-emulator-runner, after
`dotnet publish` of Flowery.NET.Gallery.Android for android-x64. Installs the APK, starts the Gallery and checks that
the home page shows, that the Button page opens from the sidebar, that the Cupcake theme applies, and that the app
neither stops nor logs a crash. Screenshots, the UI dumps and logcat go to --output.

Avalonia gives Android's accessibility tree its controls with their class names, texts and x:Name (as resource-id),
so `uiautomator dump` finds them; the theme check also compares the pixels of screenshots.

    python3 scripts/check_android_gallery.py --apk bin/android/com.CompanyName.Flowery.NET.Gallery-Signed.apk \\
        --output artifacts/android
"""

import argparse
from pathlib import Path
import re
import subprocess
import sys
import time
import xml.etree.ElementTree as ElementTree

import ci_png

PACKAGE = "com.CompanyName.Flowery.NET.Gallery"
# Errors of the Java runtime and unhandled .NET exceptions. The debug lines of AndroidRuntime come from every am and
# uiautomator call and do not count.
CRASH = re.compile(r" E AndroidRuntime|FATAL EXCEPTION|Unhandled (managed )?exception", re.IGNORECASE)
# The processes of the Gallery, as ActivityManager starts them and as a crash names them. Other apps of the emulator,
# such as Google services, crash now and then; their crashes do not count.
GALLERY_PROCESS = re.compile(r"Start proc (\d+):" + re.escape(PACKAGE) + r"/|Process: " + re.escape(PACKAGE) + r", PID: (\d+)")


def gallery_crashes(log):
    """Returns the crash lines of the Gallery's processes in a logcat in threadtime format."""
    pids = {pid for match in GALLERY_PROCESS.finditer(log) for pid in match.groups() if pid}
    crashes = []
    for line in log.splitlines():
        # date, time, PID, TID, then level, tag and message
        fields = line.split(maxsplit=4)
        if len(fields) == 5 and fields[2] in pids and CRASH.search(line):
            crashes.append(line)
    return crashes


def adb(*args, check=True, timeout=120, binary=False):
    # adb takes the device from ANDROID_SERIAL when more than one is connected.
    result = subprocess.run(["adb", *args], capture_output=True, timeout=timeout, check=False)
    if check and result.returncode:
        raise RuntimeError(f"adb {' '.join(args)} failed: {result.stdout.decode(errors='replace')}"
                           f"{result.stderr.decode(errors='replace')}")
    return result.stdout if binary else result.stdout.decode(errors="replace")


class Node:
    def __init__(self, element):
        self.kind = element.get("class", "").rsplit(".", 1)[-1]
        self.text = element.get("text", "")
        self.name = element.get("resource-id", "")
        x1, y1, x2, y2 = map(int, re.findall(r"-?\d+", element.get("bounds", "[0,0][0,0]")))
        self.bounds = (x1, y1, x2, y2)
        self.children = [Node(child) for child in element.findall("node")]

    @property
    def center(self):
        x1, y1, x2, y2 = self.bounds
        return (x1 + x2) // 2, (y1 + y2) // 2

    @property
    def visible(self):
        x1, y1, x2, y2 = self.bounds
        return x2 > x1 and y2 > y1

    def all(self):
        yield self
        for child in self.children:
            yield from child.all()

    def find(self, kind=None, text=None, name=None):
        for node in self.all():
            if node.visible and (kind is None or node.kind == kind) and (text is None or node.text == text) \
                    and (name is None or node.name == name):
                return node
        return None


class Check:
    def __init__(self, output):
        self.output = output
        self.failures = []
        self.dumps = 0

    def dump(self, name):
        self.dumps += 1
        adb("shell", "uiautomator", "dump", "/sdcard/flowery-ui.xml", timeout=60)
        data = adb("exec-out", "cat", "/sdcard/flowery-ui.xml", binary=True)
        (self.output / f"{name}.xml").write_bytes(data)
        return Node(ElementTree.fromstring(data).find("node"))

    def shot(self, name):
        data = adb("exec-out", "screencap", "-p", binary=True)
        (self.output / f"{name}.png").write_bytes(data)
        return ci_png.read(data)

    def wait_for(self, name, find, seconds):
        """Dumps the UI until find returns a node; returns that node or None."""
        deadline = time.monotonic() + seconds
        while True:
            try:
                node = find(self.dump(name))
            except (RuntimeError, ElementTree.ParseError, subprocess.TimeoutExpired):
                # uiautomator fails while the window changes; the next dump succeeds.
                node = None
            if node is not None or time.monotonic() > deadline:
                return node
            time.sleep(1)

    def fail(self, message):
        print(f"::error::{message}", flush=True)
        self.failures.append(message)

    def tap(self, node):
        adb("shell", "input", "tap", *map(str, node.center))


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--apk", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True, help="folder for screenshots, UI dumps and logcat")
    args = parser.parse_args()
    apk = args.apk.resolve(strict=True)
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)

    print(adb("shell", "getprop", "ro.build.version.release").strip(), "/",
          adb("shell", "getprop", "ro.product.cpu.abi").strip(), "/", adb("shell", "wm", "size").strip(), flush=True)
    print(adb("install", "-r", str(apk), timeout=300).strip(), flush=True)
    adb("logcat", "-c")
    check = Check(output)
    with (output / "logcat.txt").open("wb") as logcat_file:
        logcat = subprocess.Popen(["adb", "logcat", "-v", "threadtime"], stdout=logcat_file, stderr=subprocess.STDOUT)
        try:
            run(check)
        finally:
            logcat.terminate()
            logcat.wait(timeout=30)

    crashes = gallery_crashes((output / "logcat.txt").read_text(encoding="utf-8", errors="replace"))
    if crashes:
        check.fail("logcat reports a crash:\n" + "\n".join(crashes[:20]))
    if check.failures:
        print(f"{len(check.failures)} check(s) failed; screenshots, UI dumps and logcat are in {output}", flush=True)
        return 1
    print("The Android Gallery starts, opens a page and applies a theme without a crash", flush=True)
    return 0


def run(check):
    activity = adb("shell", "cmd", "package", "resolve-activity", "--brief", PACKAGE).strip().splitlines()[-1]
    print(adb("shell", "am", "start", "-W", "-n", activity, timeout=180).strip(), flush=True)

    home = check.wait_for("1-home", lambda root: root.find(kind="HomePage"), 90)
    check.shot("1-home")
    if home is None:
        check.fail("The home page did not show")
        return
    print("The home page shows", flush=True)

    if not open_sidebar(check, "2-sidebar"):
        return
    root = check.dump("2-sidebar")
    item = root.find(kind="Button", text="Button")
    if item is None:
        check.fail("The sidebar has no Button item")
        return
    check.tap(item)
    # The title bar names the category, Actions; the page starts with the section of the item.
    title = check.wait_for("3-button-page", lambda root: root.find(kind="TextBlock", text="Button (DaisyButton)"), 20)
    before = check.shot("3-button-page")
    if title is None:
        check.fail("The Button page did not open from the sidebar")
        return
    print("The Button page opened from the sidebar", flush=True)

    if not open_sidebar(check, "4-sidebar"):
        return
    dropdown = check.dump("4-sidebar").find(kind="DaisyThemeDropdown")
    if dropdown is None:
        check.fail("The sidebar has no theme dropdown")
        return
    check.tap(dropdown)
    # The open list scrolls to the selected theme, Dark; Cupcake is two entries above it.
    cupcake = check.wait_for("5-themes", lambda root: root.find(kind="TextBlock", text="Cupcake"), 20)
    if cupcake is None:
        check.fail("The theme list does not show Cupcake")
        return
    check.tap(cupcake)
    selected = check.wait_for("6-cupcake", lambda root: theme_name(root) == "Cupcake" or None, 20)
    # Close the sidebar, so the page alone is compared.
    adb("shell", "input", "keyevent", "KEYCODE_BACK")
    time.sleep(2)
    after = check.shot("6-cupcake")
    share = after.changed_share(before, (0, after.height // 3, after.width, after.height // 2))
    if selected is None or share < 0.5:
        check.fail(f"The Cupcake theme did not apply: the dropdown shows {theme_name(check.dump('6-cupcake'))}, "
                   f"{share:.0%} of the page changed")
        return
    print(f"The Cupcake theme applied: {share:.0%} of the page changed", flush=True)

    if not adb("shell", "pidof", PACKAGE, check=False).strip():
        check.fail("The Gallery is no longer running")


def open_sidebar(check, name):
    """Taps the menu button of the header or the title bar and waits for the sidebar."""
    root = check.dump(name)
    button = root.find(name="HamburgerButton") or root.find(name="CategoryMenuButton")
    if button is None:
        check.fail("The Gallery shows no menu button")
        return False
    check.tap(button)
    sidebar = check.wait_for(name, lambda root: root.find(kind="FloweryComponentSidebar"), 15)
    if sidebar is None:
        check.fail("The sidebar did not open")
        return False
    return True


def theme_name(root):
    dropdown = root.find(kind="DaisyThemeDropdown")
    text = dropdown and next((node.text for node in dropdown.all() if node.kind == "TextBlock" and node.text), None)
    return text


if __name__ == "__main__":
    sys.exit(main())
