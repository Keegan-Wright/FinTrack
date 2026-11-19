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

// Saves a file the app has fetched, e.g. an export (FinanceTracker.Client.Extensions.FileDownloadExtensions). The
// stream arrives from .NET as a DotNetStreamReference, a link to it is clicked, then let go of.
async function ftDownloadFile(fileName, contentType, streamReference) {
    const buffer = await streamReference.arrayBuffer();
    const url = URL.createObjectURL(new Blob([buffer], {type: contentType}));
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
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

    window.addEventListener("scroll", queue, {passive: true});
    new MutationObserver(queue).observe(document.body, {childList: true, subtree: true});
    update();
})();

// Whether the browser can make and use passkeys from the server's JSON options (WebAuthn level 3), for hiding the
// passkey buttons where it can't.
function ftPasskeysSupported() {
    return typeof PublicKeyCredential !== "undefined"
        && typeof PublicKeyCredential.parseCreationOptionsFromJSON === "function"
        && typeof PublicKeyCredential.parseRequestOptionsFromJSON === "function";
}

// Makes a passkey from the server's creation options, for Your profile's Passkeys section (PasskeysSection.razor).
// The browser or phone asks for a fingerprint, face or PIN. Gives back the new credential as JSON for the server to
// check and keep, or null when the person cancels.
async function ftCreatePasskey(optionsJson) {
    const publicKey = PublicKeyCredential.parseCreationOptionsFromJSON(JSON.parse(optionsJson));
    try {
        const credential = await navigator.credentials.create({publicKey});
        return JSON.stringify(credential);
    } catch (error) {
        if (error.name === "NotAllowedError" || error.name === "AbortError") {
            return null;
        }
        throw error;
    }
}

// The sign in page's passkey sign in (Login.razor). Its button asks the browser for a passkey, and while the page is
// open the browser also offers saved passkeys in the username field. Either way the passkey is put in the element's
// form and posted. A custom element, so it starts again when Blazor's enhanced navigation brings the page back. It
// hides itself where the browser can't use passkeys, rather than starting hidden, as enhanced navigation puts the
// server's attributes back.
customElements.define("ft-passkey-sign-in", class extends HTMLElement {
    connectedCallback() {
        if (!ftPasskeysSupported()) {
            this.hidden = true;
            return;
        }

        this.querySelector("button")?.addEventListener("click", () => this.signIn("optional"));
        this.offerInUsernameField();
    }

    disconnectedCallback() {
        this.pending?.abort();
    }

    // Lets the browser offer passkeys in the username field, where it supports that
    async offerInUsernameField() {
        if (await PublicKeyCredential.isConditionalMediationAvailable?.()) {
            this.signIn("conditional");
        }
    }

    // Asks the server for options, the browser for a passkey, and posts it. Only one request at a time, so the
    // button's replaces the username field's
    async signIn(mediation) {
        this.pending?.abort();
        const pending = new AbortController();
        this.pending = pending;

        const form = this.closest("form");
        const token = form.querySelector("input[name='__RequestVerificationToken']")?.value ?? "";
        try {
            const response = await fetch(this.getAttribute("options-url"), {
                method: "POST",
                headers: {"RequestVerificationToken": token},
                credentials: "same-origin",
                signal: pending.signal
            });
            if (!response.ok) {
                return;
            }

            const publicKey = PublicKeyCredential.parseRequestOptionsFromJSON(await response.json());
            const credential = await navigator.credentials.get({publicKey, mediation, signal: pending.signal});
            form.querySelector("input[name='PasskeyInput.CredentialJson']").value = JSON.stringify(credential);
            form.requestSubmit();
        } catch (error) {
            // Offering them in the username field is a nicety some browsers and devices can't do. Cancelled, or
            // replaced by another request, leaves the page as it was
            const quiet = mediation === "conditional" || error.name === "NotAllowedError" || error.name === "AbortError";
            if (!quiet) {
                throw error;
            }
        }
    }
});
