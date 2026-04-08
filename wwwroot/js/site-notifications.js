(function () {
    let toastContainer = null;
    let confirmOverlay = null;
    let confirmResolver = null;
    const modalOrigins = new WeakMap();

    function syncBodyModalState() {
        const hasOpenModal = Boolean(
            document.querySelector('.modal.modal--visible, .modal-overlay.active, .notification-overlay.active, .modal[style*="display: flex"], .modal[style*="display:flex"], .modal[style*="display: block"], .modal[style*="display:block"], #loadingOverlay[style*="display: flex"], #loadingOverlay[style*="display:flex"]')
        );

        if (document.body) {
            document.body.classList.toggle('modal-open', hasOpenModal);
        }
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
        confirmOverlay.innerHTML = `
            <div class="site-confirm" role="dialog" aria-modal="true" aria-labelledby="siteConfirmTitle">
                <h3 id="siteConfirmTitle" class="site-confirm__title"></h3>
                <p class="site-confirm__message"></p>
                <div class="site-confirm__actions">
                    <button type="button" class="site-confirm__button site-confirm__button--cancel">Отмена</button>
                    <button type="button" class="site-confirm__button site-confirm__button--confirm">Подтвердить</button>
                </div>
            </div>
        `;

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
            toast.innerHTML = `
                <div class="site-toast__body">
                    <h4 class="site-toast__title"></h4>
                    <p class="site-toast__message"></p>
                </div>
                <button type="button" class="site-toast__close" aria-label="Закрыть">×</button>
            `;

            toast.querySelector('.site-toast__title').textContent = title;
            toast.querySelector('.site-toast__message').textContent = normalizeMessage(message);

            const closeButton = toast.querySelector('.site-toast__close');
            const removeToast = () => {
                toast.remove();
            };

            closeButton.addEventListener('click', removeToast);
            container.appendChild(toast);

            const duration = options?.duration ?? 4500;
            if (duration > 0) {
                window.setTimeout(removeToast, duration);
            }
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

    function showSiteModal(target) {
        const modal = resolveModal(target);
        if (!modal) {
            return false;
        }

        hoistModal(modal);

        if (modal.classList.contains('modal-overlay') || modal.classList.contains('notification-overlay')) {
            modal.classList.add('active');
        } else {
            modal.classList.add('modal--visible');
            modal.style.display = 'flex';
            modal.style.position = 'fixed';
            modal.style.top = '0';
            modal.style.right = '0';
            modal.style.bottom = '0';
            modal.style.left = '0';
        }

        modal.setAttribute('aria-hidden', 'false');
        syncBodyModalState();
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
            modal.classList.remove('modal--visible');
            modal.style.display = 'none';
        }

        modal.setAttribute('aria-hidden', 'true');
        restoreModal(modal);
        window.setTimeout(syncBodyModalState, 0);
        return true;
    }

    window.siteNotify = function (message, type, options) {
        showToast(message, type, options);
    };

    window.siteConfirm = function (message, options) {
        return showConfirm(message, options);
    };

    window.showSiteModal = showSiteModal;
    window.hideSiteModal = hideSiteModal;

    const nativeShowNotification = window.showNotification;
    window.showNotification = function (message, isSuccess) {
        if (typeof nativeShowNotification === 'function' && document.getElementById('notification')) {
            nativeShowNotification(message, isSuccess);
            return;
        }

        showToast(message, isSuccess ? 'success' : 'error');
    };

    window.alert = function (message) {
        showToast(message, 'error', { title: 'Сообщение' });
    };

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

        const modal = event.target.closest('.modal.modal--visible, .modal-overlay.active, .notification-overlay.active');
        if (modal && event.target === modal) {
            hideSiteModal(modal);
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
