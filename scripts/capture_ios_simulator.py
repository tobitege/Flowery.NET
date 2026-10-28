"""Capture the unmodified Gallery in Apple's simulator (macOS only).

Called by .github/workflows/ios-simulator.yml. Uses only the Python standard
library and Xcode tools. Navigation uses the existing sidebar.state contract;
theme and language use GallerySettings. Each capture starts a fresh app process.
"""

import argparse
import json
import os
from pathlib import Path
import plistlib
import re
import subprocess
import sys
import time


def simctl(*args, timeout=120, check=True):
    result = subprocess.run(
        ["xcrun", "simctl", *map(str, args)],
        capture_output=True, text=True, timeout=timeout, check=False,
    )
    if check and result.returncode:
        raise RuntimeError(f"simctl {args[0]} failed: {result.stdout}\n{result.stderr}")
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--app", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--device", choices=["iPhone 17", "iPad (A16)"], required=True)
    parser.add_argument("--sections", required=True)
    parser.add_argument("--settle-seconds", type=int, default=10)
    args = parser.parse_args()
    if not 5 <= args.settle_seconds <= 60:
        parser.error("--settle-seconds must be between 5 and 60")

    sections = list(dict.fromkeys(item.strip() for item in args.sections.split(",")))
    sidebar = Path(__file__).resolve().parents[1] / "Flowery.NET.Gallery/GallerySidebarData.cs"
    available = set(re.findall(r'Id = "([a-z0-9-]+)"', sidebar.read_text(encoding="utf-8-sig")))
    if not sections or any(section not in available for section in sections):
        parser.error(f"Use existing sidebar IDs. Available: {', '.join(sorted(available))}")
    if len(sections) > 20:
        parser.error("Select at most 20 sections per run")
    if len(sections) * args.settle_seconds * 2 > 600:
        parser.error("Reduce sections or wait time: combined Light/Dark waits must not exceed 10 minutes")
    if sys.platform != "darwin":
        parser.error("Apple's simulator requires macOS and Xcode")

    app = args.app.resolve(strict=True)
    with (app / "Info.plist").open("rb") as stream:
        info = plistlib.load(stream)
    bundle_id = info["CFBundleIdentifier"]
    executable = info["CFBundleExecutable"]
    output = args.output.resolve()
    screenshots = output / "screenshots"
    logs = output / "logs"
    screenshots.mkdir(parents=True, exist_ok=True)
    logs.mkdir(parents=True, exist_ok=True)

    inventory = json.loads(simctl("list", "--json").stdout)
    runtimes = [runtime for runtime in inventory["runtimes"]
                if runtime.get("isAvailable") and runtime["name"].startswith("iOS 26.")]
    if not runtimes:
        raise RuntimeError("No available iOS 26 simulator runtime in this Xcode installation")
    runtime = max(runtimes, key=lambda item: tuple(map(int, item["version"].split("."))))
    device_type = next(item for item in inventory["devicetypes"] if item["name"] == args.device)
    udid = simctl("create", "Flowery Gallery CI", device_type["identifier"], runtime["identifier"]).stdout.strip()
    metadata = {
        "commit": os.environ.get("GITHUB_SHA"), "device": args.device,
        "runtime": runtime["name"], "udid": udid, "bundle_id": bundle_id,
        "settle_seconds": args.settle_seconds, "captures": [],
    }
    try:
        print(f"Booting {args.device} with {runtime['name']}", flush=True)
        simctl("boot", udid)
        simctl("bootstatus", udid, "-b", timeout=300)
        simctl("status_bar", udid, "override", "--time", "9:41", "--batteryState", "charged",
               "--batteryLevel", "100", "--wifiMode", "active", "--wifiBars", "3")
        simctl("install", udid, app)
        container = Path(simctl("get_app_container", udid, bundle_id, "data").stdout.strip()).resolve(strict=True)
        # .NET 10 Environment.iOS.cs maps LocalApplicationData to Documents.
        documents = container / "Documents"
        settings = documents / "Flowery.NET.Gallery"
        state = documents / "FloweryGallery"
        settings.mkdir(parents=True, exist_ok=True)
        state.mkdir(parents=True, exist_ok=True)
        (settings / "language.txt").write_text("en", encoding="utf-8")

        for theme in ("Light", "Dark"):
            simctl("ui", udid, "appearance", theme.lower())
            for section in sections:
                name = f"{theme.lower()}-{section}"
                (settings / "theme.txt").write_text(theme, encoding="utf-8")
                (state / "sidebar.state").write_text(f"last:{section}\n", encoding="utf-8")
                print(f"Capturing {name}", flush=True)
                launched = simctl("launch", f"--stdout={logs / (name + '.stdout.txt')}",
                                 f"--stderr={logs / (name + '.stderr.txt')}", udid, bundle_id)
                pid = int(launched.stdout.strip().rsplit(":", 1)[1])
                # A successful launch alone is insufficient: detect startup crashes
                # before recording a SpringBoard image as an application screenshot.
                for _ in range(args.settle_seconds):
                    time.sleep(1)
                    process = subprocess.run(
                        ["ps", "-p", str(pid), "-o", "comm="],
                        capture_output=True, text=True, timeout=10, check=False,
                    )
                    if process.returncode or Path(process.stdout.strip()).name != executable:
                        raise RuntimeError(f"Gallery process {pid} exited during {name}; inspect launch logs")
                image_path = screenshots / f"{name}.png"
                simctl("io", udid, "screenshot", "--type=png", image_path)
                if not image_path.is_file() or image_path.stat().st_size == 0:
                    raise RuntimeError(f"No screenshot produced for {name}")
                simctl("terminate", udid, bundle_id)
                metadata["captures"].append({"theme": theme, "section": section, "file": image_path.name})
        print(f"Saved {len(metadata['captures'])} screenshots to {screenshots}", flush=True)
    except Exception:
        # Keep the original failure if diagnostics also fail (for example during boot).
        try:
            simctl("io", udid, "screenshot", screenshots / "failure.png", check=False, timeout=20)
            # The app and what the system logged about it, such as dyld or a refused code signature.
            diagnostic = simctl("spawn", udid, "log", "show", "--last", "5m", "--style", "compact",
                                "--predicate", f'process == "{executable}" OR eventMessage CONTAINS "{bundle_id}"',
                                check=False, timeout=60)
            (logs / "simulator-failure.txt").write_text(diagnostic.stdout + diagnostic.stderr, encoding="utf-8")
            # The simulator writes the crash reports of its apps on the host.
            for report in (Path.home() / "Library/Logs/DiagnosticReports").glob(f"{executable}*"):
                (logs / report.name).write_bytes(report.read_bytes())
        except (OSError, subprocess.TimeoutExpired):
            pass
        raise
    finally:
        try:
            (output / "captures.json").write_text(json.dumps(metadata, indent=2) + "\n", encoding="utf-8")
        finally:
            # Stop only the simulator created above. The hosted runner owns its storage.
            try:
                stopped = simctl("shutdown", udid, check=False, timeout=60)
                if stopped.returncode:
                    print(f"Simulator shutdown: {stopped.stderr}", file=sys.stderr)
            except (OSError, subprocess.TimeoutExpired) as error:
                print(f"Simulator shutdown failed: {error}", file=sys.stderr)


if __name__ == "__main__":
    main()
