(function () {
    function handleResponse(response) {
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
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

        notification.style.display = 'flex';
    }

    window.handleResponse = handleResponse;
    window.handleError = handleError;
    window.getValueSafe = getValueSafe;
    window.showNotification = showNotification;
})();
