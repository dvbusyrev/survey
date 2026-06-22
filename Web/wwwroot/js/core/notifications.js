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

        const cancelButtonNode = document.createElement('button');
        cancelButtonNode.type = 'button';
        cancelButtonNode.className = 'site-confirm__button site-confirm__button--cancel';
        cancelButtonNode.textContent = 'Отмена';

        const confirmButtonNode = document.createElement('button');
        confirmButtonNode.type = 'button';
        confirmButtonNode.className = 'site-confirm__button site-confirm__button--confirm';
        confirmButtonNode.textContent = 'Подтвердить';

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

            const closeNode = document.createElement('button');
            closeNode.type = 'button';
            closeNode.className = 'site-toast__close';
            closeNode.setAttribute('aria-label', 'Закрыть');
            closeNode.textContent = '×';

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

        const closeButton = document.createElement('button');
        closeButton.type = 'button';
        closeButton.className = 'modal-close';
        closeButton.setAttribute('aria-label', 'Закрыть');

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
                const button = document.createElement('button');
                button.type = buttonOptions.type || 'button';
                button.className = buttonOptions.className || 'modal_btn modal_btn-secondary';
                button.textContent = buttonOptions.text || '';
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

    window.siteNotify = function (message, type, options) {
        showToast(normalizeClientErrorMessage(message), type, options);
    };

    window.siteConfirm = function (message, options) {
        return showConfirm(message, options);
    };

    window.showSiteModal = showSiteModal;
    window.hideSiteModal = hideSiteModal;
    window.createSiteModalFrame = createSiteModalFrame;
    window.syncSiteModalBodyState = syncBodyModalState;

    const nativeShowNotification = window.showNotification;
    window.showNotification = function (message, isSuccess) {
        showToast(normalizeClientErrorMessage(message), isSuccess ? 'success' : 'error');
    };

    window.alert = function (message) {
        const normalizedMessage = normalizeClientErrorMessage(message);
        const hasErrorTone = /ошиб|не удалось|некоррект|проверьте|не найден|не заполн|не может/i.test(normalizedMessage);
        const hasSuccessTone = /успешно|сохранен|создан|обновлен|добавлен|загружен|удален|отправлен/i.test(normalizedMessage);
        const toastType = hasErrorTone ? 'error' : hasSuccessTone ? 'success' : 'info';
        const title = toastType === 'error'
            ? 'Ошибка'
            : toastType === 'success'
                ? 'Успешно'
                : 'Сообщение';

        showToast(normalizedMessage, toastType, { title });
    };

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
