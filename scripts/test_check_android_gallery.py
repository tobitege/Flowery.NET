"""Tests how check_android_gallery.py takes failed and empty UI dumps and dialogs of the system.

    python3 -B -m unittest discover -s scripts -p "test_*.py"
"""

from pathlib import Path
import tempfile
import unittest
from unittest import mock

import check_android_gallery

EMPTY = b"<?xml version='1.0' encoding='UTF-8' standalone='yes' ?><hierarchy rotation=\"0\" />"


def hierarchy(*nodes):
    """A uiautomator dump whose root holds nodes, each given as (class, resource-id)."""
    children = "".join(f'<node class="Avalonia.{kind}" resource-id="{name}" text="" bounds="[0,0][100,100]" />'
                       for kind, name in nodes)
    return (f'<hierarchy rotation="0"><node class="android.widget.FrameLayout" bounds="[0,0][1080,2400]">{children}'
            f'</node></hierarchy>').encode()


class FakeDevice:
    """Answers the adb calls of a dump with the next result: bytes as the dumped file, an exception as a failed dump."""

    def __init__(self, *dumps):
        self.dumps = list(dumps)
        self.taps = 0
        self.current = None

    def adb(self, *args, check=True, timeout=120, binary=False):
        if args[:3] == ("shell", "uiautomator", "dump"):
            self.current = self.dumps.pop(0)
            if isinstance(self.current, Exception):
                raise self.current
            return ""
        if args[:2] == ("exec-out", "cat"):
            return self.current
        if args[:3] == ("shell", "input", "tap"):
            self.taps += 1
            return ""
        raise AssertionError(f"unexpected adb call {args}")


class DumpTests(unittest.TestCase):
    def setUp(self):
        folder = tempfile.TemporaryDirectory()
        self.addCleanup(folder.cleanup)
        self.check = check_android_gallery.Check(Path(folder.name))
        sleep = mock.patch.object(check_android_gallery.time, "sleep")
        sleep.start()
        self.addCleanup(sleep.stop)

    def use(self, device):
        patch = mock.patch.object(check_android_gallery, "adb", device.adb)
        patch.start()
        self.addCleanup(patch.stop)
        return device

    def test_an_empty_hierarchy_is_a_failed_dump(self):
        self.use(FakeDevice(EMPTY, hierarchy(("HomePage", ""))))
        home = self.check.wait_for("1-home", lambda root: root.find(kind="HomePage"), 30)
        self.assertEqual("HomePage", home.kind)
        self.use(FakeDevice(EMPTY))
        self.assertIsNone(self.check.wait_for("1-home", lambda root: root.find(kind="HomePage"), 0))

    def test_a_not_responding_dialog_of_the_system_is_closed_with_wait(self):
        device = self.use(FakeDevice(hierarchy(("Button", "android:id/aerr_wait")), hierarchy(("HomePage", ""))))
        home = self.check.wait_for("1-home", lambda root: root.find(kind="HomePage"), 30)
        self.assertEqual("HomePage", home.kind)
        self.assertEqual(1, device.taps)

    def test_opening_the_sidebar_waits_out_failed_dumps(self):
        device = self.use(FakeDevice(RuntimeError("uiautomator failed"), EMPTY, b"<hierarchy",
                                     hierarchy(("Button", "HamburgerButton")),
                                     hierarchy(("FloweryComponentSidebar", ""))))
        self.assertTrue(check_android_gallery.open_sidebar(self.check, "2-sidebar"))
        self.assertEqual(1, device.taps)
        self.assertEqual([], self.check.failures)


if __name__ == "__main__":
    unittest.main()
