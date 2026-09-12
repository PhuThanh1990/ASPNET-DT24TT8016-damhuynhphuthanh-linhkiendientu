// Storefront behaviour. Kept tiny and framework-free: Bootstrap/Tabler already handle
// offcanvas, dropdown, collapse and modal through their data-bs-* attributes, so this file
// only adds the few behaviours markup alone cannot express.
(function () {
    'use strict';

    // Any <select data-auto-submit> applies immediately on change (the sort dropdown on the
    // product list). Markup keeps a <noscript> submit button, so the form still works with
    // JavaScript disabled.
    document.querySelectorAll('select[data-auto-submit]').forEach(function (select) {
        select.addEventListener('change', function () {
            if (select.form) {
                select.form.submit();
            }
        });
    });

    // Submit buttons marked data-submit-once disable themselves after the form starts
    // submitting, so an impatient double click cannot post twice. Purely cosmetic: the
    // server-side duplicate handling is untouched, and the button is only disabled AFTER
    // the browser has begun the submit so the value still reaches the server.
    document.querySelectorAll('form[data-submit-once]').forEach(function (form) {
        form.addEventListener('submit', function () {
            var button = form.querySelector('[type="submit"]');
            if (!button || button.disabled) {
                return;
            }

            // Let the submit proceed first, then lock the button.
            window.setTimeout(function () {
                button.disabled = true;
                button.setAttribute('aria-busy', 'true');
                if (button.dataset.busyText) {
                    button.textContent = button.dataset.busyText;
                }
            }, 0);
        });
    });

    // Quantity steppers on the product detail page: [-] [input] [+]. The cart page uses
    // real submit buttons instead, because there the change has to reach the server.
    document.querySelectorAll('[data-qty-stepper]').forEach(function (stepper) {
        var input = stepper.querySelector('input[type="number"]');
        if (!input) {
            return;
        }

        stepper.querySelectorAll('[data-qty-step]').forEach(function (button) {
            button.addEventListener('click', function () {
                var step = parseInt(button.dataset.qtyStep, 10) || 0;
                var min = parseInt(input.min, 10);
                var max = parseInt(input.max, 10);
                var next = (parseInt(input.value, 10) || 0) + step;

                if (!isNaN(min)) { next = Math.max(min, next); }
                if (!isNaN(max)) { next = Math.min(max, next); }

                input.value = next;
            });
        });
    });
})();
