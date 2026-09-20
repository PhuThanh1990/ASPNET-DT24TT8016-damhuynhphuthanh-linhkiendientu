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

    // ADDR-03 — sổ địa chỉ ở trang thanh toán.
    // Chỉ là lớp tiện dụng: form vẫn gửi SelectedAddressId / UseNewAddress như bình thường,
    // nên tắt JavaScript thì khách vẫn chọn được radio rồi bấm "Đặt hàng".
    (function () {
        var picker = document.querySelector('[data-address-picker]');
        if (!picker) {
            return;
        }

        var useNewInput = document.getElementById('useNewAddress');
        var newForm = document.getElementById('newAddressForm');
        var current = document.getElementById('addressCurrent');
        var currentBody = document.getElementById('addressCurrentBody');
        var pickerPanel = document.getElementById('addressPicker');

        function collapse(show) {
            if (!pickerPanel || typeof bootstrap === 'undefined' || !bootstrap.Collapse) {
                // Không có Bootstrap JS thì đổi class trực tiếp, vẫn dùng được.
                if (pickerPanel) { pickerPanel.classList.toggle('show', show); }
                return;
            }
            bootstrap.Collapse.getOrCreateInstance(pickerPanel, { toggle: false })[show ? 'show' : 'hide']();
        }

        function useSaved() {
            if (useNewInput) { useNewInput.value = 'false'; }
            if (newForm) { newForm.classList.add('d-none'); }
            if (current) { current.classList.remove('d-none'); }
        }

        // Chọn một địa chỉ trong sổ: cập nhật khối xem trước rồi thu gọn danh sách lại.
        picker.querySelectorAll('[data-address-option]').forEach(function (radio) {
            radio.addEventListener('change', function () {
                if (!radio.checked) {
                    return;
                }

                useSaved();

                var label = picker.querySelector('label[for="' + radio.id + '"]');
                if (label && currentBody) {
                    currentBody.innerHTML = label.innerHTML;
                }

                collapse(false);
            });
        });

        // "Thêm địa chỉ mới": bỏ chọn trong sổ và mở form nhập tay.
        var addButton = document.querySelector('[data-address-new]');
        if (addButton) {
            addButton.addEventListener('click', function () {
                picker.querySelectorAll('[data-address-option]').forEach(function (radio) {
                    radio.checked = false;
                });

                if (useNewInput) { useNewInput.value = 'true'; }
                if (newForm) { newForm.classList.remove('d-none'); }
                if (current) { current.classList.add('d-none'); }

                collapse(false);
            });
        }

        // "Dùng địa chỉ đã lưu": quay lại sổ, chọn lại mục đang được đánh dấu (hoặc mục đầu).
        var cancelButton = document.querySelector('[data-address-cancel-new]');
        if (cancelButton) {
            cancelButton.addEventListener('click', function () {
                var options = picker.querySelectorAll('[data-address-option]');
                if (options.length === 0) {
                    return;
                }

                var chosen = picker.querySelector('[data-address-option]:checked') || options[0];
                chosen.checked = true;
                chosen.dispatchEvent(new Event('change', { bubbles: true }));

                collapse(true);
            });
        }
    })();

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
