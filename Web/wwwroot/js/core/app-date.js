(function () {
    if (window.AppDate) {
        return;
    }

    const DISPLAY_PLACEHOLDER = 'ДД/ММ/ГГГГ';

    function pad(value) {
        return String(value).padStart(2, '0');
    }

    function normalizeInput(value) {
        return String(value || '').trim();
    }

    function buildDate(year, month, day) {
        const parsedYear = Number.parseInt(year, 10);
        const parsedMonth = Number.parseInt(month, 10);
        const parsedDay = Number.parseInt(day, 10);

        if (!Number.isInteger(parsedYear) || !Number.isInteger(parsedMonth) || !Number.isInteger(parsedDay)) {
            return null;
        }

        const date = new Date(parsedYear, parsedMonth - 1, parsedDay);
        if (Number.isNaN(date.getTime())) {
            return null;
        }

        if (date.getFullYear() !== parsedYear
            || date.getMonth() !== parsedMonth - 1
            || date.getDate() !== parsedDay) {
            return null;
        }

        return date;
    }

    function parseDate(value) {
        const normalized = normalizeInput(value);
        if (!normalized) {
            return null;
        }

        let match = normalized.match(/^(\d{4})-(\d{2})-(\d{2})$/);
        if (match) {
            return buildDate(match[1], match[2], match[3]);
        }

        match = normalized.match(/^(\d{2})[./-](\d{2})[./-](\d{4})$/);
        if (match) {
            return buildDate(match[3], match[2], match[1]);
        }

        return null;
    }

    function toIso(value) {
        const date = parseDate(value);
        if (!date) {
            return '';
        }

        return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
    }

    function toDisplay(value) {
        const date = parseDate(value);
        if (!date) {
            return normalizeInput(value);
        }

        return `${pad(date.getDate())}/${pad(date.getMonth() + 1)}/${date.getFullYear()}`;
    }

    function compare(left, right) {
        const leftIso = toIso(left);
        const rightIso = toIso(right);

        if (!leftIso || !rightIso) {
            return null;
        }

        if (leftIso === rightIso) {
            return 0;
        }

        return leftIso > rightIso ? 1 : -1;
    }

    function resolveInput(target) {
        if (!target) {
            return null;
        }

        if (typeof target === 'string') {
            return document.getElementById(target);
        }

        return target;
    }

    function setInputValue(target, value) {
        const input = resolveInput(target);
        if (!input) {
            return false;
        }

        input.value = toDisplay(value);
        return true;
    }

    function getInputIso(target) {
        const input = resolveInput(target);
        if (!input) {
            return '';
        }

        return toIso(input.value);
    }

    function isInputValid(target) {
        const input = resolveInput(target);
        if (!input) {
            return false;
        }

        const normalized = normalizeInput(input.value);
        return !normalized || Boolean(toIso(normalized));
    }

    function enhanceInput(input) {
        if (!input || input.dataset.dateEnhanced === 'true') {
            return;
        }

        input.dataset.dateEnhanced = 'true';

        if (input.type === 'date') {
            input.type = 'text';
        }

        input.inputMode = 'numeric';
        input.placeholder = DISPLAY_PLACEHOLDER;
        input.autocomplete = 'off';
        input.dataset.dateFormat = 'dd/mm/yyyy';

        if (input.value) {
            input.value = toDisplay(input.value);
        }

        input.addEventListener('blur', function () {
            if (!input.value) {
                input.classList.remove('invalid');
                return;
            }

            const normalizedIso = toIso(input.value);
            if (!normalizedIso) {
                input.classList.add('invalid');
                return;
            }

            input.value = toDisplay(normalizedIso);
            input.classList.remove('invalid');
        });
    }

    function enhanceDateInputs(root) {
        const scope = root && typeof root.querySelectorAll === 'function' ? root : document;
        scope.querySelectorAll('input[type="date"], input[data-date-format="dd/mm/yyyy"]').forEach(enhanceInput);
    }

    function observeDateInputs() {
        if (!document.body || typeof MutationObserver === 'undefined') {
            return;
        }

        const observer = new MutationObserver((mutations) => {
            mutations.forEach((mutation) => {
                mutation.addedNodes.forEach((node) => {
                    if (!(node instanceof Element)) {
                        return;
                    }

                    if (node.matches?.('input[type="date"], input[data-date-format="dd/mm/yyyy"]')) {
                        enhanceInput(node);
                    }

                    enhanceDateInputs(node);
                });
            });
        });

        observer.observe(document.body, {
            childList: true,
            subtree: true
        });
    }

    function todayIso() {
        return toIso(new Date());
    }

    window.AppDate = {
        compare,
        enhanceDateInputs,
        getInputIso,
        isInputValid,
        parseDate,
        setInputValue,
        toDisplay,
        toIso,
        todayIso
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () {
            enhanceDateInputs(document);
            observeDateInputs();
        });
        return;
    }

    enhanceDateInputs(document);
    observeDateInputs();
})();
