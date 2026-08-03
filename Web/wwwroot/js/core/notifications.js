(function () {
    let toastContainer = null;
    let confirmOverlay = null;
    let confirmResolver = null;
    const modalOrigins = new WeakMap();
    const modalShowTokens = new WeakMap();
    const backdropPointerDownTargets = new WeakSet();
    const BODY_SCROLL_LOCK_FLAG = 'modalScrollLock';
    const BODY_SCROLL_LOCK_INLINE_PADDING = 'modalOriginalPaddingRight';

    function getViewportScrollbarWidth() {
        const root = document.documentElement;
        if (!root) {
            return 0;
        }

        const scrollbarWidth = window.innerWidth - root.clientWidth;
        return scrollbarWidth > 0 ? scrollbarWidth : 0;
    }

    function syncBodyScrollLock(isLocked) {
        if (!document.body) {
            return;
        }

        const body = document.body;
        const alreadyLocked = body.dataset[BODY_SCROLL_LOCK_FLAG] === 'true';

        if (isLocked) {
            body.classList.add('modal-open');

            if (alreadyLocked) {
                return;
            }

            const computedPaddingRight = Number.parseFloat(window.getComputedStyle(body).paddingRight) || 0;
            const scrollbarWidth = getViewportScrollbarWidth();

            body.dataset[BODY_SCROLL_LOCK_FLAG] = 'true';
            body.dataset[BODY_SCROLL_LOCK_INLINE_PADDING] = body.style.paddingRight || '';

            if (scrollbarWidth > 0) {
                body.style.paddingRight = `${computedPaddingRight + scrollbarWidth}px`;
            }

            return;
        }

        body.classList.remove('modal-open');

        if (!alreadyLocked) {
            return;
        }

        const originalInlinePaddingRight = body.dataset[BODY_SCROLL_LOCK_INLINE_PADDING] || '';
        if (originalInlinePaddingRight) {
            body.style.paddingRight = originalInlinePaddingRight;
        } else {
            body.style.removeProperty('padding-right');
        }

        delete body.dataset[BODY_SCROLL_LOCK_FLAG];
        delete body.dataset[BODY_SCROLL_LOCK_INLINE_PADDING];
    }

    function syncBodyModalState() {
        const hasOpenModal = Boolean(
            document.querySelector('.modal.modal--visible, .modal-overlay.active, .notification-overlay.active, .site-confirm-overlay.is-open, .modal[style*="display: flex"], .modal[style*="display:flex"], .modal[style*="display: block"], .modal[style*="display:block"], #loadingOverlay[style*="display: flex"], #loadingOverlay[style*="display:flex"]')
        );

        syncBodyScrollLock(hasOpenModal);
    }

    function ensureBody(callback) {
        if (document.body) {
            callback();
            return;
        }

        document.addEventListener('DOMContentLoaded', callback, { once: true });
    }

    function ensureToastContainer() {
        if (toastContainer) {
            return toastContainer;
        }

        toastContainer = document.createElement('div');
        toastContainer.className = 'site-toast-container';
        document.body.appendChild(toastContainer);
        return toastContainer;
    }

    function ensureConfirmOverlay() {
        if (confirmOverlay) {
            return confirmOverlay;
        }

        confirmOverlay = document.createElement('div');
        confirmOverlay.className = 'site-confirm-overlay';
        const confirmDialog = document.createElement('div');
        confirmDialog.className = 'site-confirm';
        confirmDialog.setAttribute('role', 'dialog');
        confirmDialog.setAttribute('aria-modal', 'true');
        confirmDialog.setAttribute('aria-labelledby', 'siteConfirmTitle');

        const title = document.createElement('h3');
        title.id = 'siteConfirmTitle';
        title.className = 'site-confirm__title';

        const message = document.createElement('p');
        message.className = 'site-confirm__message';

        const actions = document.createElement('div');
        actions.className = 'site-confirm__actions';

        const cancelButtonNode = createUiElement('button', {
            type: 'button',
            className: 'site-confirm__button site-confirm__button--cancel',
            text: 'Отмена'
        });

        const confirmButtonNode = createUiElement('button', {
            type: 'button',
            className: 'site-confirm__button site-confirm__button--confirm',
            text: 'Подтвердить'
        });

        actions.appendChild(cancelButtonNode);
        actions.appendChild(confirmButtonNode);
        confirmDialog.appendChild(title);
        confirmDialog.appendChild(message);
        confirmDialog.appendChild(actions);
        confirmOverlay.appendChild(confirmDialog);

        const cancelButton = confirmOverlay.querySelector('.site-confirm__button--cancel');
        const confirmButton = confirmOverlay.querySelector('.site-confirm__button--confirm');

        cancelButton.addEventListener('click', () => closeConfirm(false));
        confirmButton.addEventListener('click', () => closeConfirm(true));
        confirmOverlay.addEventListener('click', (event) => {
            if (event.target === confirmOverlay) {
                closeConfirm(false);
            }
        });

        document.addEventListener('keydown', (event) => {
            if (event.key === 'Escape' && confirmOverlay?.classList.contains('is-open')) {
                closeConfirm(false);
            }
        });

        document.body.appendChild(confirmOverlay);
        return confirmOverlay;
    }

    function closeConfirm(result) {
        if (!confirmOverlay) {
            return;
        }

        confirmOverlay.classList.remove('is-open');
        syncBodyModalState();

        if (typeof confirmResolver === 'function') {
            const resolver = confirmResolver;
            confirmResolver = null;
            resolver(result);
        }
    }

    function normalizeMessage(message) {
        if (message == null) {
            return '';
        }

        if (typeof message === 'string') {
            return message;
        }

        try {
            return JSON.stringify(message);
        } catch (error) {
            return String(message);
        }
    }

    function normalizeClientErrorMessage(message, fallbackMessage = 'Произошла ошибка.') {
        const normalizedMessage = normalizeMessage(message).trim();
        if (!normalizedMessage) {
            return fallbackMessage;
        }

        const lowerMessage = normalizedMessage.toLowerCase();
        const withoutTypePrefix = normalizedMessage.replace(/^(typeerror|error):\s*/i, '').trim();

        if (/(failed to fetch|load failed|network request failed|fetch failed|networkerror|err_connection_refused|err_network|err_failed|err_internet_disconnected|connection refused|connection reset|connection aborted|econnrefused|econnreset|enotfound|econnaborted)/i.test(normalizedMessage)) {
            return 'Сервер недоступен.';
        }

        if (/(internet connection appears to be offline|network connection was lost|network is offline|offline)/i.test(normalizedMessage)) {
            return 'Нет соединения с сетью.';
        }

        if (/(timeout|timed out|time-out|etimedout)/i.test(normalizedMessage)) {
            return 'Превышено время ожидания ответа от сервера.';
        }

        if (/(aborterror|operation was aborted|request aborted|aborted)/i.test(normalizedMessage)) {
            return 'Запрос был отменён.';
        }

        if (/(unexpected end of json input|json.parse|unexpected token.*json|is not valid json)/i.test(normalizedMessage)) {
            return 'Сервер вернул некорректный ответ.';
        }

        if (/unexpected token\s*</i.test(normalizedMessage)) {
            return 'Сервер вернул страницу вместо данных.';
        }

        if (/(cannot read propert|cannot set propert|is not a function|undefined is not an object|null is not an object|script error)/i.test(normalizedMessage)) {
            return 'Произошла ошибка интерфейса.';
        }

        if (/(23505|duplicate key value|unique constraint|повторяющееся значение ключа)/i.test(normalizedMessage)) {
            return 'Такая запись уже существует.';
        }

        if (/(23502|null value in column|not-null constraint|нарушает ограничение not-null)/i.test(normalizedMessage)) {
            return 'Не заполнены обязательные поля.';
        }

        if (/(23503|foreign key constraint|violates foreign key|нарушает ограничение внешнего ключа)/i.test(normalizedMessage)) {
            return 'Нельзя изменить запись: есть связанные данные.';
        }

        if (lowerMessage === 'undefined' || lowerMessage === 'null' || lowerMessage === 'nan') {
            return fallbackMessage;
        }

        return withoutTypePrefix || fallbackMessage;
    }

    function showToast(message, type, options) {
        ensureBody(() => {
            const container = ensureToastContainer();
            const toast = document.createElement('div');
            const toastType = type || 'info';
            const title = options?.title || (
                toastType === 'success' ? 'Успешно' :
                toastType === 'error' ? 'Ошибка' :
                toastType === 'warning' ? 'Внимание' :
                'Сообщение'
            );

            toast.className = `site-toast site-toast--${toastType}`;
            const toastBody = document.createElement('div');
            toastBody.className = 'site-toast__body';

            const toastTitle = document.createElement('h4');
            toastTitle.className = 'site-toast__title';

            const toastMessage = document.createElement('p');
            toastMessage.className = 'site-toast__message';

            toastBody.appendChild(toastTitle);
            toastBody.appendChild(toastMessage);

            const closeNode = createUiElement('button', {
                type: 'button',
                className: 'site-toast__close',
                ariaLabel: 'Закрыть',
                text: '×'
            });

            toast.appendChild(toastBody);
            toast.appendChild(closeNode);

            toast.querySelector('.site-toast__title').textContent = title;
            toast.querySelector('.site-toast__message').textContent = normalizeClientErrorMessage(message);

            const closeButton = toast.querySelector('.site-toast__close');
            let closeTimer = 0;
            let removeTimer = 0;
            const removeToast = () => {
                if (!toast.isConnected || toast.classList.contains('site-toast--closing')) {
                    return;
                }

                if (closeTimer) {
                    window.clearTimeout(closeTimer);
                    closeTimer = 0;
                }

                toast.classList.add('site-toast--closing');
                toast.addEventListener('animationend', () => toast.remove(), { once: true });
                removeTimer = window.setTimeout(() => {
                    removeTimer = 0;
                    toast.remove();
                }, 260);
            };

            closeButton.addEventListener('click', (event) => {
                event.preventDefault();
                removeToast();
            });
            container.appendChild(toast);

            const requestedDuration = Number(options?.duration);
            const duration = Number.isFinite(requestedDuration) && requestedDuration > 0
                ? requestedDuration
                : 5000;
            closeTimer = window.setTimeout(removeToast, duration);
        });
    }

    function showConfirm(message, options) {
        return new Promise((resolve) => {
            ensureBody(() => {
                const overlay = ensureConfirmOverlay();
                const titleElement = overlay.querySelector('.site-confirm__title');
                const messageElement = overlay.querySelector('.site-confirm__message');
                const cancelButton = overlay.querySelector('.site-confirm__button--cancel');
                const confirmButton = overlay.querySelector('.site-confirm__button--confirm');

                titleElement.textContent = options?.title || 'Подтверждение';
                messageElement.textContent = normalizeMessage(message);
                cancelButton.textContent = options?.cancelText || 'Отмена';
                confirmButton.textContent = options?.confirmText || 'Подтвердить';

                confirmResolver = resolve;
                overlay.classList.add('is-open');
                syncBodyModalState();
                confirmButton.focus();
            });
        });
    }

    function resolveModal(target) {
        if (!target) {
            return null;
        }

        if (typeof target === 'string') {
            return document.getElementById(target);
        }

        return target;
    }

    function hoistModal(modal) {
        if (!modal || !document.body) {
            return;
        }

        if (!modalOrigins.has(modal)) {
            modalOrigins.set(modal, {
                parent: modal.parentNode,
                nextSibling: modal.nextSibling
            });
        }

        if (modal.parentNode !== document.body) {
            document.body.appendChild(modal);
        }
    }

    function restoreModal(modal) {
        if (!modal || !modalOrigins.has(modal)) {
            return;
        }

        const origin = modalOrigins.get(modal);
        if (origin?.parent && origin.parent.isConnected) {
            if (origin.nextSibling && origin.nextSibling.parentNode === origin.parent) {
                origin.parent.insertBefore(modal, origin.nextSibling);
            } else {
                origin.parent.appendChild(modal);
            }
        }
    }

    function dispatchModalEvent(modal, eventName) {
        if (!modal || typeof modal.dispatchEvent !== 'function') {
            return;
        }

        modal.dispatchEvent(new CustomEvent(eventName, {
            bubbles: true,
            detail: {
                modal
            }
        }));
    }

    function getInteractiveModalFromTarget(target) {
        return target?.closest?.('.modal.modal--visible, .modal-overlay.active, .notification-overlay.active') || null;
    }

    function isModalBackdropEvent(modal, event) {
        return Boolean(modal && event?.target === modal);
    }

    function normalizeClassList(...values) {
        return values
            .flatMap((value) => {
                if (!value) {
                    return [];
                }

                if (Array.isArray(value)) {
                    return normalizeClassList(...value).split(' ');
                }

                if (typeof value === 'object') {
                    return Object.entries(value)
                        .filter(([, isEnabled]) => Boolean(isEnabled))
                        .map(([className]) => className);
                }

                return String(value).split(/\s+/);
            })
            .map((className) => className.trim())
            .filter(Boolean)
            .join(' ');
    }

    function appendUiContent(element, content) {
        if (content === null || typeof content === 'undefined') {
            return;
        }

        if (Array.isArray(content)) {
            content.forEach((item) => appendUiContent(element, item));
            return;
        }

        if (content instanceof Node) {
            element.appendChild(content);
            return;
        }

        element.appendChild(document.createTextNode(String(content)));
    }

    function applyElementOptions(element, options = {}) {
        const {
            className,
            classes,
            text,
            html,
            children,
            attrs,
            dataset,
            events,
            id,
            type,
            name,
            value,
            placeholder,
            role,
            ariaLabel,
            disabled,
            readOnly,
            checked
        } = options;

        const normalizedClassName = normalizeClassList(className, classes);
        if (normalizedClassName) {
            element.className = normalizedClassName;
        }

        if (id) {
            element.id = id;
        }

        if (type) {
            element.type = type;
        }

        if (name) {
            element.name = name;
        }

        if (typeof value !== 'undefined') {
            element.value = value;
        }

        if (typeof placeholder !== 'undefined') {
            element.placeholder = placeholder;
        }

        if (role) {
            element.setAttribute('role', role);
        }

        if (ariaLabel) {
            element.setAttribute('aria-label', ariaLabel);
        }

        if (typeof disabled !== 'undefined') {
            element.disabled = Boolean(disabled);
        }

        if (typeof readOnly !== 'undefined') {
            element.readOnly = Boolean(readOnly);
        }

        if (typeof checked !== 'undefined') {
            element.checked = Boolean(checked);
        }

        Object.entries(attrs || {}).forEach(([attributeName, attributeValue]) => {
            if (attributeValue === null || typeof attributeValue === 'undefined' || attributeValue === false) {
                return;
            }

            element.setAttribute(attributeName, attributeValue === true ? '' : String(attributeValue));
        });

        Object.entries(dataset || {}).forEach(([key, datasetValue]) => {
            if (datasetValue === null || typeof datasetValue === 'undefined') {
                return;
            }

            element.dataset[key] = String(datasetValue);
        });

        Object.entries(events || {}).forEach(([eventName, handler]) => {
            if (typeof handler === 'function') {
                element.addEventListener(eventName, handler);
            }
        });

        if (typeof html !== 'undefined') {
            element.innerHTML = html;
        } else if (typeof text !== 'undefined') {
            element.textContent = text;
        }

        appendUiContent(element, children);

        return element;
    }

    function createUiElement(tagName, options = {}, textContent) {
        const element = document.createElement(tagName);
        if (typeof options === 'string' || Array.isArray(options)) {
            return applyElementOptions(element, {
                className: options,
                text: textContent
            });
        }

        return applyElementOptions(element, options);
    }

    function createUiButton(options = {}) {
        const variant = options.variant || 'secondary';
        return createUiElement('button', {
            ...options,
            type: options.type || 'button',
            className: normalizeClassList('app-button', variant ? `app-button--${variant}` : '', options.className)
        });
    }

    function createUiField(options = {}) {
        const tagName = options.tagName || 'div';
        return createUiElement(tagName, {
            ...options,
            className: normalizeClassList('app-field', options.className)
        });
    }

    function createUiFieldGroup(options = {}) {
        const group = createUiElement('div', {
            className: normalizeClassList('app-field-group', options.className)
        });

        if (options.label) {
            const label = createUiElement('label', {
                text: options.label,
                attrs: options.labelFor ? { for: options.labelFor } : null
            });
            group.appendChild(label);
        }

        appendUiContent(group, options.children || options.field);
        return group;
    }

    function createUiCheckboxOption(options = {}) {
        const option = createUiElement('label', {
            className: normalizeClassList('app-checkbox-option', options.className, {
                'is-selected': options.selected || options.checked,
                selected: options.selectedClass
            })
        });
        const checkbox = createUiElement('input', {
            type: 'checkbox',
            className: 'app-checkbox-input',
            checked: options.checked,
            disabled: options.disabled,
            dataset: options.dataset,
            attrs: options.attrs
        });
        const text = createUiElement('span', {
            className: 'app-checkbox-text',
            text: options.text || ''
        });

        option.appendChild(checkbox);
        option.appendChild(text);

        return { option, checkbox, text };
    }

    function createUiDropdown(options = {}) {
        const hiddenClass = options.hiddenClass || 'is-hidden';
        const root = options.root || createUiElement('div', {
            className: normalizeClassList(options.className),
            dataset: options.dataset,
            attrs: options.attrs
        });
        const trigger = options.trigger || createUiButton({
            variant: options.triggerVariant || 'secondary',
            className: options.triggerClassName,
            children: options.triggerChildren,
            text: options.triggerChildren ? undefined : options.triggerText,
            attrs: {
                'aria-expanded': 'false',
                'aria-haspopup': options.haspopup || 'dialog',
                ...(options.triggerAttrs || {})
            }
        });
        const menu = options.menu || createUiElement('div', {
            className: normalizeClassList('app-dropdown', options.menuClassName, {
                [hiddenClass]: options.hidden !== false
            }),
            role: options.menuRole,
            ariaLabel: options.menuLabel,
            dataset: options.menuDataset,
            attrs: options.menuAttrs,
            children: options.menuChildren
        });

        if (!root.contains(trigger)) {
            root.appendChild(trigger);
        }
        if (!root.contains(menu)) {
            root.appendChild(menu);
        }

        const controller = options.mount === false
            ? null
            : mountUiDropdown({
                root,
                trigger,
                menu,
                openClass: options.openClass,
                hiddenClass,
                onOpen: options.onOpen,
                onClose: options.onClose
            });

        return {
            root,
            trigger,
            menu,
            controller,
            destroy: () => controller?.destroy()
        };
    }

    function createUiMultiselect(options = {}) {
        const isInline = Boolean(options.inline);
        const selectedValues = new Set((options.selectedValues || []).map((value) => String(value)));
        const dropdown = createUiDropdown({
            ...options,
            className: normalizeClassList('app-multiselect app-checkbox-dropdown', {
                'app-multiselect--inline app-checkbox-dropdown--inline': isInline
            }, options.className),
            triggerVariant: options.triggerVariant || 'primary',
            triggerClassName: normalizeClassList(
                'app-multiselect__trigger app-checkbox-dropdown__trigger',
                options.triggerClassName
            ),
            triggerChildren: options.triggerChildren || createUiElement('span', {
                text: options.label || options.triggerText || 'Выбрать'
            }),
            menuClassName: normalizeClassList(
                'app-multiselect__menu app-checkbox-dropdown__menu',
                {
                    'app-multiselect__menu--inline app-checkbox-dropdown__menu--inline': isInline
                },
                options.menuClassName
            ),
            menuRole: options.menuRole || 'listbox',
            menuLabel: options.menuLabel
        });

        function normalizeOption(rawOption) {
            if (rawOption && typeof rawOption === 'object') {
                return {
                    value: rawOption.value ?? rawOption.id ?? rawOption.name ?? rawOption.text ?? '',
                    text: rawOption.text ?? rawOption.name ?? rawOption.label ?? rawOption.value ?? '',
                    checked: rawOption.checked,
                    selected: rawOption.selected,
                    disabled: rawOption.disabled,
                    dataset: rawOption.dataset,
                    attrs: rawOption.attrs
                };
            }

            return {
                value: rawOption,
                text: rawOption
            };
        }

        function clearOptions() {
            dropdown.menu.textContent = '';
        }

        function appendOption(rawOption) {
            const option = normalizeOption(rawOption);
            const optionValue = String(option.value ?? '');
            const isSelected = Boolean(option.selected || option.checked || selectedValues.has(optionValue));
            const checkboxOption = createUiCheckboxOption({
                text: option.text,
                checked: isSelected,
                selected: isSelected,
                disabled: option.disabled,
                dataset: option.dataset,
                attrs: option.attrs
            });

            checkboxOption.checkbox.value = optionValue;
            checkboxOption.checkbox.addEventListener('change', (event) => {
                checkboxOption.option.classList.toggle('is-selected', event.target.checked);
                options.onChange?.(option, event);
            });
            dropdown.menu.appendChild(checkboxOption.option);
            return checkboxOption;
        }

        function setOptions(nextOptions = []) {
            clearOptions();
            nextOptions.forEach(appendOption);
        }

        if (Array.isArray(options.options)) {
            setOptions(options.options);
        }

        return {
            ...dropdown,
            appendOption,
            clearOptions,
            setOptions
        };
    }

    function createUiTable(options = {}) {
        const table = createUiElement('table', {
            className: normalizeClassList('app-table', options.className),
            dataset: options.dataset,
            attrs: options.attrs
        });
        const thead = createUiElement('thead');
        const headRow = createUiElement('tr', {
            className: normalizeClassList('app-table__header-row', options.headerRowClassName)
        });
        const tbody = createUiElement('tbody');

        function appendHeaderCell(cell) {
            const cellOptions = typeof cell === 'string' ? { text: cell } : (cell || {});
            const headerCell = createUiElement('th', {
                className: cellOptions.className,
                text: cellOptions.text,
                attrs: cellOptions.attrs,
                dataset: cellOptions.dataset
            });
            appendUiContent(headerCell, cellOptions.children);
            headRow.appendChild(headerCell);
            return headerCell;
        }

        function appendCell(row, cell) {
            const cellOptions = cell instanceof Node || typeof cell !== 'object' || cell === null
                ? { children: cell }
                : cell;
            const tableCell = createUiElement('td', {
                className: cellOptions.className,
                text: cellOptions.text,
                attrs: cellOptions.attrs,
                dataset: cellOptions.dataset
            });
            if (cellOptions.rowSpan) {
                tableCell.rowSpan = cellOptions.rowSpan;
            }
            if (cellOptions.colSpan) {
                tableCell.colSpan = cellOptions.colSpan;
            }
            appendUiContent(tableCell, cellOptions.children);
            row.appendChild(tableCell);
            return tableCell;
        }

        function appendRow(cells, rowOptions = {}) {
            const row = createUiElement('tr', {
                className: rowOptions.className,
                dataset: rowOptions.dataset,
                attrs: rowOptions.attrs
            });
            (cells || []).forEach((cell) => appendCell(row, cell));
            tbody.appendChild(row);
            return row;
        }

        (options.headerCells || []).forEach(appendHeaderCell);
        thead.appendChild(headRow);
        table.appendChild(thead);
        table.appendChild(tbody);

        return {
            table,
            thead,
            headRow,
            tbody,
            appendHeaderCell,
            appendRow,
            appendCell
        };
    }

    function createUiRowTooltip(options = {}) {
        const offsetX = Number.isFinite(Number(options.offsetX)) ? Number(options.offsetX) : 12;
        const offsetY = Number.isFinite(Number(options.offsetY)) ? Number(options.offsetY) : 14;
        const className = options.className || 'app-row-tooltip';
        const defaultLabel = options.defaultLabel || 'Смотреть';
        let tooltip = null;
        let activeRow = null;
        let latestX = 0;
        let latestY = 0;
        let frameId = 0;

        function ensure(label) {
            if (!tooltip) {
                tooltip = createUiElement('div', {
                    className,
                    attrs: {
                        'aria-hidden': 'true'
                    }
                });
                document.body.appendChild(tooltip);
            }

            tooltip.textContent = label || defaultLabel;
            return tooltip;
        }

        function applyPosition() {
            frameId = 0;
            if (!activeRow || !tooltip) {
                return;
            }

            tooltip.style.transform = `translate3d(${latestX + offsetX}px, ${latestY + offsetY}px, 0)`;
        }

        function move(event) {
            if (activeRow && !activeRow.isConnected) {
                hide();
                return;
            }

            latestX = event.clientX;
            latestY = event.clientY;
            if (!frameId) {
                frameId = window.requestAnimationFrame(applyPosition);
            }
        }

        function show(row, event) {
            activeRow = row;
            ensure(row?.dataset?.hoverLabel || defaultLabel).classList.add('is-visible');
            move(event);
        }

        function hide() {
            activeRow = null;
            if (frameId) {
                window.cancelAnimationFrame(frameId);
                frameId = 0;
            }

            if (tooltip) {
                tooltip.classList.remove('is-visible');
                tooltip.style.transform = 'translate3d(-9999px, -9999px, 0)';
            }
        }

        function destroy() {
            hide();
            tooltip?.remove();
            tooltip = null;
        }

        return {
            show,
            move,
            hide,
            destroy,
            isActiveRow: (row) => activeRow === row,
            hasActiveRow: () => Boolean(activeRow),
            activeRowContains: (node) => Boolean(activeRow?.contains(node))
        };
    }

    function mountUiDropdown(options = {}) {
        const root = options.root;
        const trigger = options.trigger;
        const menu = options.menu;
        const openClass = options.openClass || 'is-open';
        const hiddenClass = options.hiddenClass || 'is-hidden';

        if (!root || !trigger || !menu) {
            return null;
        }

        let isOpen = !menu.classList.contains(hiddenClass);

        function setOpen(nextOpen) {
            isOpen = Boolean(nextOpen);
            root.classList.toggle(openClass, isOpen);
            menu.classList.toggle(hiddenClass, !isOpen);
            trigger.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
            if (isOpen) {
                options.onOpen?.();
            } else {
                options.onClose?.();
            }
        }

        function toggle() {
            setOpen(!isOpen);
        }

        function handleDocumentPointer(event) {
            if (!isOpen || root.contains(event.target) || menu.contains(event.target)) {
                return;
            }

            setOpen(false);
        }

        function handleKeydown(event) {
            if (event.key === 'Escape') {
                setOpen(false);
            }
        }

        trigger.addEventListener('click', toggle);
        document.addEventListener('pointerdown', handleDocumentPointer, true);
        document.addEventListener('keydown', handleKeydown);

        return {
            setOpen,
            open: () => setOpen(true),
            close: () => setOpen(false),
            toggle,
            destroy() {
                trigger.removeEventListener('click', toggle);
                document.removeEventListener('pointerdown', handleDocumentPointer, true);
                document.removeEventListener('keydown', handleKeydown);
            }
        };
    }

    function prepareModalFrame(modal) {
        if (!modal || modal.classList.contains('modal-overlay') || modal.classList.contains('notification-overlay')) {
            return;
        }

        modal.classList.add('modal');
        modal.setAttribute('role', modal.getAttribute('role') || 'dialog');
        modal.setAttribute('aria-modal', modal.getAttribute('aria-modal') || 'true');
        modal.style.position = 'fixed';
        modal.style.top = '0';
        modal.style.right = '0';
        modal.style.bottom = '0';
        modal.style.left = '0';
    }

    function createSiteModalFrame(options = {}) {
        const modal = document.createElement('div');
        modal.id = options.id || '';
        modal.className = ['modal', options.className || ''].filter(Boolean).join(' ');
        modal.setAttribute('aria-hidden', 'true');

        const modalContent = document.createElement('div');
        modalContent.className = ['modal-content', options.contentClassName || ''].filter(Boolean).join(' ');

        const modalHeader = document.createElement('div');
        modalHeader.className = 'modal-header';

        const title = document.createElement('h2');
        title.className = options.titleClassName || 'h2_modal';
        title.textContent = options.title || '';

        const closeButton = createUiElement('button', {
            type: 'button',
            className: 'modal-close',
            ariaLabel: 'Закрыть'
        });

        const closeIcon = document.createElement('i');
        closeIcon.className = 'fas fa-xmark';
        closeIcon.setAttribute('aria-hidden', 'true');
        closeButton.appendChild(closeIcon);

        const body = document.createElement('div');
        body.className = ['modal-body', options.bodyClassName || ''].filter(Boolean).join(' ');

        const footer = document.createElement('div');
        footer.className = 'modal-footer';

        const closeModal = () => hideSiteModal(modal);
        closeButton.addEventListener('click', (event) => {
            event.preventDefault();
            event.stopPropagation();
            (options.onClose || closeModal)();
        });

        modalHeader.appendChild(title);
        modalHeader.appendChild(closeButton);
        modalContent.appendChild(modalHeader);
        modalContent.appendChild(body);

        if (Array.isArray(options.footerButtons) && options.footerButtons.length > 0) {
            options.footerButtons.forEach((buttonOptions) => {
                const button = createUiButton({
                    ...buttonOptions,
                    variant: buttonOptions.variant || 'secondary',
                    className: buttonOptions.className,
                    text: buttonOptions.text || ''
                });
                if (typeof buttonOptions.onClick === 'function') {
                    button.addEventListener('click', buttonOptions.onClick);
                }
                footer.appendChild(button);
            });
            modalContent.appendChild(footer);
        } else if (options.footer !== false) {
            modalContent.appendChild(footer);
        }

        modal.appendChild(modalContent);

        return {
            modal,
            content: modalContent,
            header: modalHeader,
            title,
            closeButton,
            body,
            footer,
            setTitle(value) {
                title.textContent = value || '';
            },
            show() {
                return showSiteModal(modal);
            },
            hide() {
                return hideSiteModal(modal);
            }
        };
    }

    function showSiteModal(target) {
        const modal = resolveModal(target);
        if (!modal) {
            return false;
        }

        hoistModal(modal);
        prepareModalFrame(modal);

        if (modal.classList.contains('modal-overlay') || modal.classList.contains('notification-overlay')) {
            modal.classList.add('active');
            modal.setAttribute('aria-hidden', 'false');
            syncBodyModalState();
            dispatchModalEvent(modal, 'site-modal:shown');
            return true;
        } else {
            const token = Symbol('modal-show');
            modalShowTokens.set(modal, token);
            modal.classList.remove('modal--visible');
            modal.classList.add('modal--preparing');
            modal.style.display = 'flex';
            modal.style.visibility = 'hidden';
            modal.setAttribute('aria-hidden', 'false');
            syncBodyModalState();

            window.requestAnimationFrame(() => {
                window.requestAnimationFrame(() => {
                    if (modalShowTokens.get(modal) !== token) {
                        return;
                    }

                    modal.classList.remove('modal--preparing');
                    modal.classList.add('modal--visible');
                    modal.style.removeProperty('visibility');
                    dispatchModalEvent(modal, 'site-modal:shown');
                });
            });
        }

        return true;
    }

    function hideSiteModal(target) {
        const modal = resolveModal(target);
        if (!modal) {
            return false;
        }

        if (modal.classList.contains('modal-overlay') || modal.classList.contains('notification-overlay')) {
            modal.classList.remove('active');
        } else {
            modalShowTokens.delete(modal);
            modal.classList.remove('modal--preparing');
            modal.classList.remove('modal--visible');
            modal.style.display = 'none';
            modal.style.removeProperty('visibility');
        }

        modal.setAttribute('aria-hidden', 'true');
        restoreModal(modal);
        dispatchModalEvent(modal, 'site-modal:hidden');
        window.setTimeout(syncBodyModalState, 0);
        return true;
    }

    const appUi = window.AppUi || {};

    appUi.createElement = createUiElement;
    appUi.createButton = createUiButton;
    appUi.createField = createUiField;
    appUi.createFieldGroup = createUiFieldGroup;
    appUi.createCheckboxOption = createUiCheckboxOption;
    appUi.createDropdown = createUiDropdown;
    appUi.createMultiselect = createUiMultiselect;
    appUi.createTable = createUiTable;
    appUi.createRowTooltip = createUiRowTooltip;
    appUi.mountDropdown = mountUiDropdown;

    appUi.notify = function (message, type, options) {
        const normalizedMessage = normalizeClientErrorMessage(message);
        if (!normalizedMessage) {
            return false;
        }

        showToast(normalizedMessage, type, options);
        return true;
    };

    appUi.setModalVisibility = function (target, isVisible) {
        return isVisible ? showSiteModal(target) : hideSiteModal(target);
    };

    window.AppUi = appUi;

    window.siteConfirm = function (message, options) {
        return showConfirm(message, options);
    };

    window.showSiteModal = showSiteModal;
    window.hideSiteModal = hideSiteModal;
    window.createSiteModalFrame = createSiteModalFrame;
    window.syncSiteModalBodyState = syncBodyModalState;

    window.normalizeClientErrorMessage = normalizeClientErrorMessage;

    document.addEventListener('pointerdown', function (event) {
        const modal = getInteractiveModalFromTarget(event.target);
        if (isModalBackdropEvent(modal, event)) {
            backdropPointerDownTargets.add(modal);
        } else if (modal) {
            backdropPointerDownTargets.delete(modal);
        }
    }, true);

    document.addEventListener('click', function (event) {
        const closeButton = event.target.closest('.modal-close');
        if (closeButton) {
            const ownedModal = closeButton.closest('.modal, .modal-overlay, .notification-overlay');
            if (ownedModal) {
                hideSiteModal(ownedModal);
                event.preventDefault();
                event.stopPropagation();
                return;
            }
        }

        const modal = getInteractiveModalFromTarget(event.target);
        if (isModalBackdropEvent(modal, event) && backdropPointerDownTargets.has(modal)) {
            hideSiteModal(modal);
        }

        if (modal) {
            backdropPointerDownTargets.delete(modal);
        }
    });

    document.addEventListener('keydown', function (event) {
        if (event.key !== 'Escape') {
            return;
        }

        const activeModal = document.querySelector('.modal.modal--visible, .modal-overlay.active, .notification-overlay.active');
        if (activeModal) {
            hideSiteModal(activeModal);
        }
    });
})();
