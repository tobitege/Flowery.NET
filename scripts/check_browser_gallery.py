"""Starts the published Browser Gallery in headless Chromium and checks that it works.

Called by the browser-gallery job of .github/workflows/ci.yml after `dotnet publish` of
Flowery.NET.Gallery.Browser. Serves the published wwwroot on 127.0.0.1, opens it with Playwright and checks that the
home page renders, that a page opens through the sidebar, that a theme change and a size change apply, and that the
browser logs no errors. Screenshots and the console log go to --output.

The Gallery draws into a canvas, so the checks click at fixed positions of a 1280 x 800 viewport and compare pixels.

    pip install playwright==1.64.0 && python -m playwright install --with-deps chromium-headless-shell
    python scripts/check_browser_gallery.py --site bin/browser-publish/wwwroot --output artifacts/browser
"""

import argparse
import functools
import http.server
from pathlib import Path
import sys
import threading
import time

from playwright.sync_api import sync_playwright

import ci_png

VIEWPORT = {"width": 1280, "height": 800}
# The purple banner of the home page; other pages have none.
BANNER = (747, 267)
# The sidebar: the Button item under Actions, the theme dropdown and the size dropdown of the Home section.
BUTTON_ITEM = (40, 669)
THEME_DROPDOWN = (100, 165)
SIZE_DROPDOWN = (100, 309)
# An empty spot of the header; a click there closes an open dropdown.
HEADER = (900, 40)
# The page right of the sidebar, below the header.
CONTENT = (230, 120, 1040, 670)
SIDEBAR = (0, 60, 200, 740)


class QuietHandler(http.server.SimpleHTTPRequestHandler):
    extensions_map = {**http.server.SimpleHTTPRequestHandler.extensions_map,
                      ".wasm": "application/wasm", ".js": "text/javascript", ".mjs": "text/javascript",
                      ".json": "application/json", ".dat": "application/octet-stream"}

    def log_message(self, *args):
        pass


class Check:
    def __init__(self, page, output):
        self.page = page
        self.output = output
        self.failures = []

    def shot(self, name):
        data = self.page.screenshot(path=str(self.output / f"{name}.png"))
        return ci_png.read(data)

    def fail(self, message):
        print(f"::error::{message}", flush=True)
        self.failures.append(message)

    def wait_for(self, name, condition, seconds):
        """Takes screenshots until condition holds for one of them; returns the last screenshot and the result."""
        deadline = time.monotonic() + seconds
        while True:
            image = self.shot(name)
            if condition(image):
                return image, True
            if time.monotonic() > deadline:
                return image, False
            time.sleep(1)


def is_banner(image):
    red, green, blue = image.pixel(*BANNER)
    return blue > 150 and red > 90 and green < 140


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--site", type=Path, required=True, help="the published wwwroot folder")
    parser.add_argument("--output", type=Path, required=True, help="folder for screenshots and the console log")
    args = parser.parse_args()
    site = args.site.resolve(strict=True)
    if not (site / "index.html").is_file() or not (site / "_framework").is_dir():
        parser.error(f"{site} is not a published wwwroot")
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)

    server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), functools.partial(QuietHandler, directory=str(site)))
    threading.Thread(target=server.serve_forever, daemon=True).start()
    url = f"http://127.0.0.1:{server.server_address[1]}/"
    log = []
    errors = []

    def on_console(message):
        log.append(f"{message.type}: {message.text}")
        if message.type == "error":
            errors.append(message.text)

    def on_page_error(error):
        log.append(f"pageerror: {error}")
        errors.append(str(error))

    def on_response(response):
        if response.status >= 400:
            log.append(f"http {response.status}: {response.url}")
            errors.append(f"HTTP {response.status} for {response.url}")

    try:
        with sync_playwright() as playwright:
            browser = playwright.chromium.launch()
            page = browser.new_page(viewport=VIEWPORT, locale="en-US")
            page.on("console", on_console)
            page.on("pageerror", on_page_error)
            page.on("response", on_response)
            page.on("requestfailed", lambda request: errors.append(f"Request failed: {request.url}"))
            check = Check(page, output)
            run(check, page, url)
            browser.close()
    finally:
        server.shutdown()
        (output / "console.log").write_text("\n".join(log) + "\n", encoding="utf-8")

    for error in errors:
        check.fail(f"Browser error: {error}")
    if check.failures:
        print(f"{len(check.failures)} check(s) failed; screenshots and console.log are in {output}", flush=True)
        return 1
    print("The Browser Gallery renders, opens a page, applies a theme and a size, and logs no errors", flush=True)
    return 0


def run(check, page, url):
    started = time.monotonic()
    page.goto(url)
    # main.js closes the splash when Avalonia attaches its canvas; drawing the home page takes a moment more.
    page.wait_for_selector(".avalonia-splash.splash-close", state="attached", timeout=120_000)
    home, shown = check.wait_for("1-home", is_banner, 60)
    if not shown:
        check.fail("The home page did not render its banner")
        return
    print(f"Home page after {time.monotonic() - started:.0f} s", flush=True)

    page.mouse.click(*BUTTON_ITEM)
    button, opened = check.wait_for("2-button-page", lambda image: not is_banner(image), 15)
    if not opened:
        check.fail("The Button page did not open from the sidebar")
        return
    print("The Button page opened from the sidebar", flush=True)

    # The dropdown lists the themes by name; typing a name selects it. Cupcake is light, the default Dark is dark,
    # so nearly every pixel of the page changes.
    page.mouse.click(*THEME_DROPDOWN)
    time.sleep(1)
    page.keyboard.type("Cupcake", delay=50)
    page.keyboard.press("Enter")
    page.mouse.click(*HEADER)
    time.sleep(1)
    themed, applied = check.wait_for("3-theme-cupcake",
                                     lambda image: image.changed_share(button, CONTENT) > 0.5, 15)
    if not applied:
        check.fail(f"The Cupcake theme did not apply: {themed.changed_share(button, CONTENT):.0%} of the page changed")
        return
    print("The Cupcake theme applied", flush=True)

    # Medium is selected; two steps down is Extra Large, which makes every sidebar item taller and moves the items
    # below. The dropdown's own text alone changes about 2% of the sidebar.
    page.mouse.click(*SIZE_DROPDOWN)
    time.sleep(1)
    page.keyboard.press("ArrowDown")
    page.keyboard.press("ArrowDown")
    page.keyboard.press("Enter")
    page.mouse.click(*HEADER)
    time.sleep(1)
    sized, applied = check.wait_for("4-size-extra-large",
                                    lambda image: image.changed_share(themed, SIDEBAR) > 0.15, 15)
    if not applied:
        check.fail(f"The Extra Large size did not apply: {sized.changed_share(themed, SIDEBAR):.0%} of the sidebar changed")
        return
    print("The Extra Large size applied", flush=True)


if __name__ == "__main__":
    sys.exit(main())
