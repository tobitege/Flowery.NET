import { dotnet } from './_framework/dotnet.js'

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

const host = document.getElementById("out");
const splash = document.querySelector(".avalonia-splash");

const hideSplashIfAppAttached = () => {
    if (!host || !splash) return;
    for (const child of host.children) {
        if (child !== splash) {
            splash.classList.add("splash-close");
            return;
        }
    }
};

if (host) {
    new MutationObserver(hideSplashIfAppAttached).observe(host, { childList: true });
}

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

const config = dotnetRuntime.getConfig();

dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href])
    .catch(err => {
        const msg = (err && (err.stack || err.message)) ? (err.stack || err.message) : String(err);
        const pre = document.createElement("pre");
        pre.style.cssText = "white-space:pre-wrap;padding:16px;margin:0;font:12px/1.4 ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono', 'Courier New', monospace;color:#b91c1c;background:#fff;";
        pre.textContent = msg;
        document.body.appendChild(pre);
        splash?.classList.add("splash-close");
    });
