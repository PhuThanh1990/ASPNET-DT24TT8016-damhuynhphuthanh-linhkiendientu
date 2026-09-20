/*
 * ADDR-02 — selectbox có ô tìm kiếm cho Tỉnh/Thành phố và Phường/Xã.
 *
 * Nguyên tắc: đây là lớp nâng cấp (progressive enhancement). Thẻ <select> thật vẫn nằm
 * nguyên trong DOM và vẫn là thứ được submit; script chỉ dựng thêm một combobox lên trên và
 * chỉ ẩn <select> SAU KHI dựng xong. Tắt JavaScript hay script lỗi thì form vẫn dùng được
 * bằng selectbox mặc định của trình duyệt.
 *
 * Không dùng thư viện ngoài (Select2/Tom Select...) để khỏi thêm dependency cho đồ án.
 */
(function () {
    'use strict';

    /** Bỏ dấu tiếng Việt để gõ "ba dinh" vẫn ra "Phường Ba Đình". */
    function normalize(text) {
        return (text || '')
            .toLowerCase()
            .normalize('NFD')
            .replace(/[̀-ͯ]/g, '')
            .replace(/đ/g, 'd');
    }

    function createCombo(select) {
        var searchPlaceholder = select.dataset.searchPlaceholder || 'Tìm...';

        var wrapper = document.createElement('div');
        wrapper.className = 'addr-combo';

        var button = document.createElement('button');
        button.type = 'button';
        button.className = 'form-select addr-combo__button';
        button.setAttribute('aria-haspopup', 'listbox');
        button.setAttribute('aria-expanded', 'false');

        var label = select.labels && select.labels[0];
        if (label) {
            if (!label.id) {
                label.id = select.id + '-label';
            }
            button.setAttribute('aria-labelledby', label.id);
        }

        var panel = document.createElement('div');
        panel.className = 'addr-combo__panel';
        panel.hidden = true;

        var search = document.createElement('input');
        search.type = 'search';
        search.className = 'form-control form-control-sm addr-combo__search';
        search.placeholder = searchPlaceholder;
        search.setAttribute('aria-label', searchPlaceholder);

        var list = document.createElement('ul');
        list.className = 'addr-combo__list';
        list.setAttribute('role', 'listbox');
        list.id = select.id + '-listbox';
        button.setAttribute('aria-controls', list.id);

        var empty = document.createElement('p');
        empty.className = 'addr-combo__empty';
        empty.textContent = 'Không tìm thấy kết quả phù hợp.';
        empty.hidden = true;

        panel.appendChild(search);
        panel.appendChild(list);
        panel.appendChild(empty);

        select.parentNode.insertBefore(wrapper, select);
        wrapper.appendChild(button);
        wrapper.appendChild(panel);
        wrapper.appendChild(select);

        // Chỉ giấu select thật khi combobox đã dựng xong.
        select.classList.add('addr-combo__native');
        select.tabIndex = -1;
        select.setAttribute('aria-hidden', 'true');

        var activeIndex = -1;

        function options() {
            return Array.prototype.slice.call(list.children);
        }

        function visibleOptions() {
            return options().filter(function (item) { return !item.hidden; });
        }

        function syncButton() {
            var option = select.options[select.selectedIndex];
            var text = option ? option.textContent.trim() : '';
            button.textContent = text;
            button.classList.toggle('is-placeholder', !select.value);
            button.disabled = select.disabled;
        }

        function buildList() {
            list.innerHTML = '';
            Array.prototype.forEach.call(select.options, function (option) {
                var item = document.createElement('li');
                item.className = 'addr-combo__option';
                item.setAttribute('role', 'option');
                item.dataset.value = option.value;
                item.dataset.search = normalize(option.textContent);
                item.textContent = option.textContent.trim();
                item.setAttribute('aria-selected', option.value === select.value ? 'true' : 'false');
                if (!option.value) {
                    item.classList.add('is-placeholder');
                }
                list.appendChild(item);
            });
            // Ô tìm kiếm chỉ có ý nghĩa khi danh sách đủ dài.
            search.hidden = select.options.length <= 8;
            syncButton();
        }

        function filter() {
            var query = normalize(search.value.trim());
            var shown = 0;
            options().forEach(function (item) {
                var match = !query || item.dataset.search.indexOf(query) !== -1;
                item.hidden = !match;
                if (match) { shown++; }
            });
            empty.hidden = shown > 0;
            setActive(-1);
        }

        function setActive(index) {
            options().forEach(function (item) { item.classList.remove('is-active'); });
            activeIndex = index;
            var items = visibleOptions();
            if (index >= 0 && index < items.length) {
                items[index].classList.add('is-active');
                items[index].scrollIntoView({ block: 'nearest' });
                if (items[index].id === '') {
                    items[index].id = list.id + '-opt-' + index;
                }
                button.setAttribute('aria-activedescendant', items[index].id);
            } else {
                button.removeAttribute('aria-activedescendant');
            }
        }

        function open() {
            if (select.disabled) { return; }
            panel.hidden = false;
            button.setAttribute('aria-expanded', 'true');
            search.value = '';
            filter();
            if (!search.hidden) { search.focus(); }
        }

        function close() {
            panel.hidden = true;
            button.setAttribute('aria-expanded', 'false');
            setActive(-1);
        }

        function choose(value) {
            select.value = value;
            // Dispatch để nhánh phụ thuộc (phường/xã) chạy đúng một đường với thay đổi
            // trên select gốc, không cần hàm riêng.
            select.dispatchEvent(new Event('change', { bubbles: true }));
            syncButton();
            close();
            button.focus();
        }

        button.addEventListener('click', function () {
            if (panel.hidden) { open(); } else { close(); }
        });

        button.addEventListener('keydown', function (event) {
            if (event.key === 'ArrowDown' || event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                open();
                setActive(0);
            }
        });

        search.addEventListener('input', filter);

        search.addEventListener('keydown', function (event) {
            var items = visibleOptions();
            if (event.key === 'ArrowDown') {
                event.preventDefault();
                setActive(Math.min(activeIndex + 1, items.length - 1));
            } else if (event.key === 'ArrowUp') {
                event.preventDefault();
                setActive(Math.max(activeIndex - 1, 0));
            } else if (event.key === 'Enter') {
                event.preventDefault();
                if (activeIndex >= 0 && items[activeIndex]) {
                    choose(items[activeIndex].dataset.value);
                }
            } else if (event.key === 'Escape') {
                close();
                button.focus();
            }
        });

        list.addEventListener('click', function (event) {
            var item = event.target.closest('.addr-combo__option');
            if (item) { choose(item.dataset.value); }
        });

        document.addEventListener('click', function (event) {
            if (!wrapper.contains(event.target)) { close(); }
        });

        buildList();

        return { rebuild: buildList, sync: syncButton, close: close };
    }

    /** Nạp lại danh sách phường/xã mỗi khi đổi tỉnh/thành phố. */
    function wireDependency(select, combo) {
        var sourceName = select.dataset.dependsOn;
        var url = select.dataset.wardsUrl;
        if (!sourceName || !url) { return; }

        var form = select.form;
        var source = form && form.querySelector('[name="' + sourceName + '"]');
        if (!source) { return; }

        var emptyText = select.dataset.emptyOption || '— Chọn —';
        var placeholderText = select.dataset.placeholderOption || emptyText;
        var errorHolder = document.createElement('div');
        errorHolder.className = 'addr-combo__error';
        errorHolder.hidden = true;
        select.parentNode.appendChild(errorHolder);

        function reset(text) {
            select.innerHTML = '';
            var option = document.createElement('option');
            option.value = '';
            option.textContent = text;
            select.appendChild(option);
            select.value = '';
            combo.rebuild();
        }

        source.addEventListener('change', function () {
            var provinceCode = source.value;
            errorHolder.hidden = true;
            combo.close();

            // Đổi tỉnh thì phường/xã cũ chắc chắn không còn đúng -> xoá ngay, không chờ fetch.
            if (!provinceCode) {
                select.disabled = false;
                reset(placeholderText);
                return;
            }

            select.disabled = true;
            reset('Đang tải...');

            fetch(url + '?provinceCode=' + encodeURIComponent(provinceCode), {
                headers: { 'Accept': 'application/json' }
            })
                .then(function (response) {
                    if (!response.ok) { throw new Error('HTTP ' + response.status); }
                    return response.json();
                })
                .then(function (wards) {
                    select.innerHTML = '';
                    var placeholder = document.createElement('option');
                    placeholder.value = '';
                    placeholder.textContent = emptyText;
                    select.appendChild(placeholder);

                    wards.forEach(function (ward) {
                        var option = document.createElement('option');
                        option.value = ward.code;
                        option.textContent = ward.name;
                        select.appendChild(option);
                    });

                    select.disabled = false;
                    select.value = '';
                    combo.rebuild();
                })
                .catch(function () {
                    // Không nuốt lỗi: cho người dùng biết và cho bấm thử lại bằng cách chọn
                    // lại tỉnh, thay vì để một select rỗng không hiểu vì sao.
                    select.disabled = false;
                    reset(emptyText);
                    errorHolder.hidden = false;
                    errorHolder.textContent =
                        'Không tải được danh sách phường/xã. Vui lòng chọn lại tỉnh/thành phố.';
                });
        });
    }

    function init() {
        document.querySelectorAll('select[data-address-select]').forEach(function (select) {
            if (select.dataset.addrReady === '1') { return; }
            try {
                var combo = createCombo(select);
                wireDependency(select, combo);
                select.dataset.addrReady = '1';
            } catch (error) {
                // Dựng hỏng thì để nguyên select mặc định, form vẫn dùng được.
                select.classList.remove('addr-combo__native');
                select.removeAttribute('aria-hidden');
                select.tabIndex = 0;
                if (window.console) { console.error('address-select:', error); }
            }
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
