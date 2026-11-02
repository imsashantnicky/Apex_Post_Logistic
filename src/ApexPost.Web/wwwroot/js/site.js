"use strict";

// Inline handlers are not allowed by the content security policy, so behaviour is attached here.
// Every page works without this file; it only adds the drawer, confirmations and the double-submit guard.
document.documentElement.classList.add("js");

const app = document.querySelector(".app");
const menuToggle = document.querySelector("[data-menu-toggle]");

function setMenu(open) {
    if (!app || !menuToggle) {
        return;
    }
    app.classList.toggle("nav-open", open);
    menuToggle.setAttribute("aria-expanded", String(open));
}

document.addEventListener("submit", (event) => {
    const form = event.target;
    const message = form.dataset.confirm;
    if (message && !window.confirm(message)) {
        event.preventDefault();
        return;
    }
    if (form.method.toLowerCase() !== "post") {
        return;
    }
    if (form.dataset.submitting) {
        event.preventDefault();
        return;
    }
    form.dataset.submitting = "true";
    event.submitter?.classList.add("is-busy");
    event.submitter?.setAttribute("aria-disabled", "true");
});

// A page restored from the back-forward cache must accept a new submission.
window.addEventListener("pageshow", () => {
    document.querySelectorAll("form[data-submitting]").forEach((form) => {
        delete form.dataset.submitting;
        form.querySelectorAll(".is-busy").forEach((button) => {
            button.classList.remove("is-busy");
            button.removeAttribute("aria-disabled");
        });
    });
});

document.addEventListener("click", (event) => {
    if (event.target.closest("[data-print]")) {
        window.print();
    } else if (event.target.closest("[data-menu-toggle]")) {
        setMenu(!app.classList.contains("nav-open"));
    } else if (event.target.closest("[data-menu-close]")) {
        setMenu(false);
    }
});

document.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && app?.classList.contains("nav-open")) {
        setMenu(false);
        menuToggle.focus();
    }
});
