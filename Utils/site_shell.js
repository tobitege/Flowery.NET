// --- Theme Logic ---
const themeToggle = document.querySelector('.theme-toggle');
const sunIcon = document.querySelector('.sun-icon');
const moonIcon = document.querySelector('.moon-icon');
const iframe = document.querySelector('iframe');

// Check local storage or default to dark
const currentTheme = localStorage.getItem('theme') || 'dark';
applyTheme(currentTheme);

function applyTheme(theme) {
    document.documentElement.setAttribute('data-theme', theme);

    // Update icons
    if (theme === 'dark') {
        sunIcon.style.display = 'block';
        moonIcon.style.display = 'none';
    } else {
        sunIcon.style.display = 'none';
        moonIcon.style.display = 'block';
    }

    // Sync iframe (Direct access + PostMessage fallback for local files)
    try {
        // 1. Try direct access (works for same origin)
        if (iframe.contentDocument && iframe.contentDocument.documentElement) {
            iframe.contentDocument.documentElement.setAttribute('data-theme', theme);
        }
    } catch(e) {
        // console.log('Direct access restricted');
    }

    // 2. PostMessage (works for cross-origin/local files)
    try {
        if (iframe.contentWindow) {
            iframe.contentWindow.postMessage({ type: 'setTheme', theme: theme }, '*');
        }
    } catch(e) {}
}

themeToggle.addEventListener('click', () => {
    const current = document.documentElement.getAttribute('data-theme');
    const newTheme = current === 'dark' ? 'light' : 'dark';
    localStorage.setItem('theme', newTheme);
    applyTheme(newTheme);
});

// When iframe loads, ensure it gets the theme
iframe.addEventListener('load', () => {
    const theme = localStorage.getItem('theme') || 'dark';
    applyTheme(theme);

    // Try to sync sidebar (fallback for when postMessage doesn't work)
    try {
        const iframePath = iframe.contentWindow.location.pathname;
        syncSidebarWithIframe(iframePath);
    } catch (e) {
        // Cross-origin access error - rely on postMessage instead
    }
});

function syncSidebarWithIframe(iframePath) {
    // Extract the filename (e.g., "DaisyButton.html" or "MigrationExample.html")
    const filename = iframePath ? iframePath.split('/').pop() : null;

    if (!filename || filename === 'index.html') return;

    // Find matching sidebar link
    const sidebarLinks = document.querySelectorAll('.sidebar a');
    sidebarLinks.forEach(link => {
        const linkHref = link.getAttribute('href');
        if (!linkHref) return;

        // Check if this link matches the iframe content
        const linkFilename = linkHref.split('/').pop();
        if (linkFilename === filename) {
            // Update active state
            sidebarLinks.forEach(l => l.classList.remove('active'));
            link.classList.add('active');

            // Update URL hash without triggering navigation
            const pageName = filename.replace('.html', '');
            if (window.location.hash !== '#' + pageName) {
                history.replaceState(null, '', '#' + pageName);
            }
        }
    });
}

// Listen for page load messages from iframe content
window.addEventListener('message', (event) => {
    if (event.data && event.data.type === 'pageLoaded') {
        syncSidebarWithIframe(event.data.path);
    }
});

// --- Sidebar Logic ---
const sidebar = document.querySelector('.sidebar');
const overlay = document.querySelector('.overlay');
const toggleBtn = document.querySelector('.menu-toggle');
const links = document.querySelectorAll('.sidebar a');

function toggleMenu() {
    sidebar.classList.toggle('open');
    overlay.classList.toggle('open');
}

function closeMenu() {
    sidebar.classList.remove('open');
    overlay.classList.remove('open');
}

toggleBtn.addEventListener('click', toggleMenu);
overlay.addEventListener('click', closeMenu);

// Handle active state & mobile close
links.forEach(link => {
    link.addEventListener('click', (e) => {
        // Don't mess with external links
        if (link.target === '_blank') return;

        links.forEach(l => l.classList.remove('active'));
        link.classList.add('active');

        // Close menu on mobile when link is clicked
        if (window.innerWidth <= 768) {
            closeMenu();
        }
    });
});

// --- Hash Navigation ---
// Handle URL hash to load specific pages (e.g., #MigrationExample)
function handleHashNavigation() {
    const hash = window.location.hash.slice(1); // Remove '#'
    if (hash) {
        // Try to find a matching page
        const possiblePaths = [
            `${hash}.html`,
            `controls/${hash}.html`,
            `controls/Daisy${hash}.html`,
            `categories/${hash}.html`
        ];

        // Only navigate to a generated page that is present in the sidebar.
        const matchingLink = Array.from(links)
            .find(link => possiblePaths.includes(link.getAttribute('href')));
        matchingLink?.click();
    }
}

// Handle initial hash on page load
handleHashNavigation();

// Handle hash changes (back/forward navigation)
window.addEventListener('hashchange', handleHashNavigation);
