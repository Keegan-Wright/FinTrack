// Shows a password field's text for a few seconds, the eye icon on the sign in pages' password fields
// (MudStaticTextField's AdornmentClickFunction).
const ftPasswordTimeouts = new WeakMap();

function ftTogglePassword(input) {
    if (!input) {
        return;
    }

    clearTimeout(ftPasswordTimeouts.get(input));
    if (input.type === "password") {
        input.type = "text";
        ftPasswordTimeouts.set(input, setTimeout(() => {
            input.type = "password";
            ftPasswordTimeouts.delete(input);
        }, 5000));
    } else {
        input.type = "password";
        ftPasswordTimeouts.delete(input);
    }
}

// Supports the sticky page header (PageHeader.razor, PageHeader.razor.css):
// - marks the page as scrolled so the header can tighten, see .ft-scrolled. The gap between the two thresholds stops
//   it flickering when the header's change in height moves the page.
// - measures where the header ends as --ft-header-bottom, so tables can fill the space under it, see .ft-grid.
(() => {
    const root = document.documentElement;
    let queued = false;
    let observed = null;

    const measure = () => {
        const header = document.querySelector(".ft-page-header");
        if (!header) {
            root.style.removeProperty("--ft-header-bottom");
            return;
        }

        const appBar = parseFloat(getComputedStyle(root).getPropertyValue("--mud-appbar-height")) || 64;
        const margin = parseFloat(getComputedStyle(header).marginBottom) || 0;
        root.style.setProperty("--ft-header-bottom", `${appBar + header.offsetHeight + margin}px`);
    };

    const resizeObserver = new ResizeObserver(measure);

    const update = () => {
        queued = false;
        const y = window.scrollY;
        if (y > 48) {
            root.classList.add("ft-scrolled");
        } else if (y < 8) {
            root.classList.remove("ft-scrolled");
        }

        // Blazor swaps the header out on navigation, follow the current one
        const header = document.querySelector(".ft-page-header");
        if (header !== observed) {
            if (observed) {
                resizeObserver.unobserve(observed);
            }
            observed = header;
            if (header) {
                resizeObserver.observe(header);
            }
            measure();
        }
    };

    const queue = () => {
        if (!queued) {
            queued = true;
            requestAnimationFrame(update);
        }
    };

    window.addEventListener("scroll", queue, { passive: true });
    new MutationObserver(queue).observe(document.body, { childList: true, subtree: true });
    update();
})();
