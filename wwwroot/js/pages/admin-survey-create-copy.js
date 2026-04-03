(function () {
    'use strict';

    function getElement(id) {
        return document.getElementById(id);
    }

    function getToken() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    }

    function getSafeElement(id) {
        if (typeof window.safeGetElement === 'function') {
            return window.safeGetElement(id);
        }
        return getElement(id);
    }

    function showErrorMessage(title, message) {
        if (typeof window.showError === 'function') {
            window.showError(title, message);
            return;
        }
        alert((title ? title + ': ' : '') + message);
    }

    function showSuccessMessage(title, message) {
        if (typeof window.showSuccess === 'function') {
            window.showSuccess(title, message);
            return;
        }
        alert((title ? title + ': ' : '') + message);
    }

    window.validateForm = function validateForm() {
        let isValid = true;

        ['surveyTitle', 'surveyDescription', 'startDate', 'endDate'].forEach(id => {
            const el = getSafeElement(id);
            if (!el || !String(el.value || '').trim()) {
                el?.classList.add('invalid');
                isValid = false;
            } else {
                el.classList.remove('invalid');
            }
        });

        const startDate = new Date(getSafeElement('startDate')?.value || '');
        const endDate = new Date(getSafeElement('endDate')?.value || '');
        if (String(startDate) !== 'Invalid Date' && String(endDate) !== 'Invalid Date' && endDate <= startDate) {
            getSafeElement('endDate')?.classList.add('invalid');
            showErrorMessage('Ошибка', 'Дата окончания должна быть позже даты начала');
            isValid = false;
        } else {
            getSafeElement('endDate')?.classList.remove('invalid');
        }

        const selectedOmsu = Array.isArray(window.selectedOmsu) ? window.selectedOmsu : [];
        if (selectedOmsu.length === 0) {
            showErrorMessage('Ошибка', 'Выберите хотя бы одну организацию');
            isValid = false;
        }

        if (!window.criteriaConfirmed) {
            showErrorMessage('Ошибка', 'Подтвердите критерии оценки');
            isValid = false;
        }

        return isValid;
    };

    window.addSurvey = function addSurvey() {
        if (!window.validateForm()) {
            return;
        }

        const selectedOmsu = Array.isArray(window.selectedOmsu) ? window.selectedOmsu : [];
        const surveyData = {
            Title: getSafeElement('surveyTitle')?.value.trim() || '',
            Description: getSafeElement('surveyDescription')?.value.trim() || '',
            StartDate: getSafeElement('startDate')?.value || '',
            EndDate: getSafeElement('endDate')?.value || '',
            Organizations: selectedOmsu.map(org => org.id),
            Criteria: Array.from(document.querySelectorAll('.criteriy')).map(input => input.value.trim())
        };

        const loadingOverlay = getSafeElement('loadingOverlay');
        if (loadingOverlay) loadingOverlay.style.display = 'flex';

        fetch('/add_survey_bd', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getToken()
            },
            body: JSON.stringify(surveyData)
        })
            .then(response => {
                if (!response.ok) {
                    return response.json().then(err => {
                        throw new Error(err.message || 'Ошибка сервера');
                    });
                }
                return response.json();
            })
            .then(data => {
                if (data.success) {
                    showSuccessMessage('Успех', 'Анкета успешно создана! Пожалуйста, перезагрузите страницу!');
                    setTimeout(() => window.location.reload(), 2000);
                } else {
                    throw new Error(data.message || 'Ошибка при создании анкеты');
                }
            })
            .catch(error => {
                showErrorMessage('Ошибка', error.message);
                console.error('Error:', error);
            })
            .finally(() => {
                if (loadingOverlay) loadingOverlay.style.display = 'none';
            });
    };

    function handleResponse(response) {
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        return response.json();
    }

    function showNotification(message, isSuccess) {
        const notification = getElement('notification');
        const messageElement = getElement('notificationMessage');
        if (!notification || !messageElement) {
            alert(message);
            return;
        }

        messageElement.textContent = message;
        messageElement.className = isSuccess
            ? 'notification-message notification-success'
            : 'notification-message notification-error';

        notification.style.display = 'flex';
    }

    window.hideNotification2 = function hideNotification2() {
        const notification = getElement('notification');
        const messageElement = getElement('notificationMessage');
        if (notification) notification.style.display = 'none';
        if (messageElement && messageElement.className.includes('notification-success')) {
            window.location.reload();
        }
    };

    window.copySurvey = function copySurvey(id) {
        const startDate = getElement('startDate')?.value || '';
        const endDate = getElement('endDate')?.value || '';
        const token = getToken();

        if (!startDate || !endDate) {
            showNotification('Пожалуйста, заполните все обязательные поля', false);
            return;
        }

        if (new Date(endDate) <= new Date(startDate)) {
            showNotification('Дата окончания должна быть позже даты начала', false);
            return;
        }

        const loadingOverlay = getElement('loadingOverlay');
        if (loadingOverlay) loadingOverlay.style.display = 'flex';

        fetch('/Survey/copy_survey_bd/' + id, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify({ StartDate: startDate, EndDate: endDate })
        })
            .then(handleResponse)
            .then(data => {
                if (loadingOverlay) loadingOverlay.style.display = 'none';
                if (data.success) {
                    alert('Анкета успешно скопирована!');
                    window.location.reload();
                } else {
                    throw new Error(data.message || 'Ошибка при копировании анкеты');
                }
            })
            .catch(error => {
                if (loadingOverlay) loadingOverlay.style.display = 'none';
                showNotification(error.message, false);
                console.error('Error:', error);
            });
    };
})();
