(function () {
    function getHttpStatusMessage(status, statusText) {
        switch (Number(status)) {
            case 0:
                return 'Сервер недоступен или соединение прервано.';
            case 400:
                return 'Некорректный запрос.';
            case 401:
                return 'Требуется авторизация.';
            case 403:
                return 'Доступ запрещён.';
            case 404:
                return 'Страница не найдена.';
            case 409:
                return 'Конфликт данных.';
            case 422:
                return 'Данные не прошли проверку.';
            case 500:
                return 'Произошла внутренняя ошибка сервера.';
            case 502:
            case 503:
            case 504:
                return 'Сервер временно недоступен.';
            default:
                return statusText && String(statusText).trim()
                    ? String(statusText).trim()
                    : `Ошибка сервера (${status})`;
        }
    }

    function getResponseErrorMessage(response, prefix) {
        const resolvedPrefix = prefix || 'Ошибка';
        return `${resolvedPrefix}: ${getHttpStatusMessage(response?.status, response?.statusText)}`;
    }

    function handleResponse(response) {
        if (!response.ok) {
            throw new Error(getResponseErrorMessage(response, 'Ошибка запроса'));
        }
        return response.json();
    }

    function handleError(error) {
        console.error('Ошибка:', error);
        window.siteNotify(
            'Произошла ошибка: ' + (error.message || 'Попробуйте снова или обратитесь в поддержку'),
            'error'
        );
    }

    function getValueSafe(elementId) {
        const element = document.getElementById(elementId);
        return element ? element.value : '';
    }

    function showNotification(message, isSuccess) {
        const notification = document.getElementById('notification');
        const messageElement = document.getElementById('notificationMessage');
        if (!notification || !messageElement) {
            window.siteNotify(message, isSuccess ? 'success' : 'error');
            return;
        }

        messageElement.textContent = message;
        messageElement.className = isSuccess
            ? 'notification-message notification-success'
            : 'notification-message notification-error';

        if (window.showSiteModal) {
            window.showSiteModal(notification);
        } else {
            notification.style.display = 'flex';
            notification.classList.add('active');
        }
    }

    window.handleResponse = handleResponse;
    window.handleError = handleError;
    window.getValueSafe = getValueSafe;
    window.showNotification = showNotification;
    window.getHttpStatusMessage = getHttpStatusMessage;
    window.getResponseErrorMessage = getResponseErrorMessage;
})();
