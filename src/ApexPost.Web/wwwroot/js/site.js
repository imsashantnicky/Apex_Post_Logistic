"use strict";

// Inline handlers are not allowed by the content security policy, so behaviour is attached here.
document.addEventListener("submit", (event) => {
    const message = event.target.dataset.confirm;
    if (message && !window.confirm(message)) {
        event.preventDefault();
    }
});

document.addEventListener("click", (event) => {
    if (event.target.matches("[data-print]")) {
        window.print();
    }
});
