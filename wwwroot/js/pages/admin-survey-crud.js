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

    window.addSurvey = function addSurvey() {
        if (!window.validateForm()) {
            return;
        }

        const surveyData = {
            Title: getSafeElement('surveyTitle')?.value.trim() || '',
            Description: getSafeElement('surveyDescription')?.value.trim() || '',
            StartDate: getSafeElement('startDate')?.value || '',
            EndDate: getSafeElement('endDate')?.value || '',
            Organizations: Array.isArray(window.selectedOmsu) ? window.selectedOmsu.map(org => org.id) : [],
            Criteria: Array.from(document.querySelectorAll('.criteriy')).map(input => input.value.trim())
        };

        const loadingOverlay = getSafeElement('loadingOverlay');
        if (loadingOverlay) {
            loadingOverlay.style.display = 'flex';
        }

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
                if (loadingOverlay) {
                    loadingOverlay.style.display = 'none';
                }
            });
    };

    window.validateForm = function validateForm() {
        let isValid = true;

        ['surveyTitle', 'surveyDescription', 'startDate', 'endDate'].forEach(id => {
            const el = getSafeElement(id);
            if (!el || !el.value.trim()) {
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

        if (!Array.isArray(window.selectedOmsu) || window.selectedOmsu.length === 0) {
            showErrorMessage('Ошибка', 'Выберите хотя бы одну организацию');
            isValid = false;
        }

        if (!window.criteriaConfirmed) {
            showErrorMessage('Ошибка', 'Подтвердите критерии оценки');
            isValid = false;
        }

        return isValid;
    };

    window.surveyEditSelectedOmsu = window.surveyEditSelectedOmsu || [];
    window.surveyEditModalOpen = window.surveyEditModalOpen || false;
    window.surveyEditAllOrganizations = window.surveyEditAllOrganizations || [];

    window.surveyEditInit = function surveyEditInit() {
        window.surveyEditSelectedOmsu = [];

        const selectedIdsInput = getElement('selectedOmsuIds');
        if (selectedIdsInput && selectedIdsInput.value) {
            const ids = selectedIdsInput.value.split(',');
            const names = window.selectedOmsuNames || [];

            for (let i = 0; i < ids.length; i++) {
                if (ids[i] && names[i]) {
                    window.surveyEditSelectedOmsu.push({
                        id: parseInt(ids[i], 10),
                        name: names[i]
                    });
                }
            }
        }

        document.querySelectorAll('.organization-item').forEach(item => {
            if (item.dataset.selected === 'true') {
                item.classList.add('selected');
            }
        });
    };

    window.surveyEditOpenOmsuModal = function surveyEditOpenOmsuModal() {
        const modal = getElement('omsuModal');
        if (modal) {
            modal.style.display = 'block';
            window.surveyEditModalOpen = true;
        }
    };

    window.surveyEditCloseModal = function surveyEditCloseModal(modalId) {
        const modal = getElement(modalId);
        if (modal) {
            modal.style.display = 'none';
        }
        window.surveyEditModalOpen = false;
    };

    window.surveyEditToggleOmsuSelection = function surveyEditToggleOmsuSelection(element) {
        if (!element) return;

        const orgId = parseInt(element.dataset.id, 10);
        const orgName = element.dataset.name;

        if (element.dataset.selected === 'true') {
            element.dataset.selected = 'false';
            element.classList.remove('selected');
            window.surveyEditSelectedOmsu = window.surveyEditSelectedOmsu.filter(org => org.id !== orgId);
            return;
        }

        if (!window.surveyEditSelectedOmsu.some(org => org.id === orgId)) {
            window.surveyEditSelectedOmsu.push({ id: orgId, name: orgName });
        }
        element.dataset.selected = 'true';
        element.classList.add('selected');
    };

    window.surveyEditSaveSelectedOmsu = function surveyEditSaveSelectedOmsu() {
        window.surveyEditCloseModal('omsuModal');
        window.surveyEditUpdateSelectedOmsuDisplay();
    };

    window.surveyEditUpdateSelectedOmsuDisplay = function surveyEditUpdateSelectedOmsuDisplay() {
        const container = getElement('selectedOmsuContainer');
        const list = getElement('selectedOmsuList');
        const idsInput = getElement('selectedOmsuIds');

        const selectedElements = document.querySelectorAll('.organization-item.selected');
        window.surveyEditSelectedOmsu = [];

        selectedElements.forEach(el => {
            const id = parseInt(el.dataset.id, 10);
            const name = el.dataset.name;
            window.surveyEditSelectedOmsu.push({ id, name });
        });

        if (!container || !list) return;

        if (window.surveyEditSelectedOmsu.length === 0) {
            container.style.display = 'none';
            if (idsInput) idsInput.value = '';
            list.innerHTML = '';
            return;
        }

        container.style.display = 'block';
        list.innerHTML = '';

        window.surveyEditSelectedOmsu.forEach(org => {
            const item = document.createElement('span');
            item.className = 'selected-omsu-item';
            const escapedName = String(org.name).replace(/'/g, "\\'");
            item.innerHTML = org.name + ' <button type="button" onclick="surveyEditRemoveOmsu(this, \'' + escapedName + '\')">×</button>';
            list.appendChild(item);
        });

        if (idsInput) {
            idsInput.value = window.surveyEditSelectedOmsu.map(org => org.id).join(',');
        }
    };

    window.surveyEditRemoveOmsu = function surveyEditRemoveOmsu(button, name) {
        window.surveyEditSelectedOmsu = window.surveyEditSelectedOmsu.filter(org => org.name !== name);
        window.surveyEditUpdateSelectedOmsuDisplay();

        if (window.surveyEditModalOpen) {
            document.querySelectorAll('.organization-item').forEach(item => {
                if (item.dataset.name === name) {
                    item.dataset.selected = 'false';
                    item.classList.remove('selected');
                }
            });
        }
    };

    window.surveyEditAddCriteria = function surveyEditAddCriteria() {
        const container = getElement('cont_criteries');
        if (!container) return;

        const div = document.createElement('div');
        div.className = 'form-group cont_criteries';
        div.innerHTML = '<label>Критерий оценки</label><input type="text" class="form-control criteriy" required /><div class="error-message">Это поле обязательно для заполнения.</div>';
        container.appendChild(div);
    };

    window.surveyEditConfirmCriteria = function surveyEditConfirmCriteria() {
        const criteriaInputs = document.querySelectorAll('.criteriy');
        const hasValidCriteria = Array.from(criteriaInputs).some(input => input.value.trim() !== '');

        if (!hasValidCriteria) {
            alert('Пожалуйста, добавьте и заполните хотя бы один критерий оценки');
            return;
        }

        let allCriteriaValid = true;
        criteriaInputs.forEach(input => {
            if (input.value.trim() === '') {
                input.classList.add('invalid');
                allCriteriaValid = false;
            } else {
                input.classList.remove('invalid');
            }
        });

        if (!allCriteriaValid) {
            alert('Пожалуйста, заполните все добавленные критерии оценки');
            return;
        }

        const container = getElement('two_step');
        container?.querySelectorAll('.criteriy').forEach(input => {
            input.readOnly = true;
        });

        container?.classList.add('confirmed');
        getElement('add_survey_btn')?.style.setProperty('display', 'inline-block');
        getElement('send_email')?.style.setProperty('display', 'inline-block');
        getElement('add_crit')?.style.setProperty('display', 'none');
        getElement('conf_btn')?.style.setProperty('display', 'none');
        alert('Критерии подтверждены. Теперь вы можете обновить анкету.');
    };

    window.surveyEditUpdate = async function surveyEditUpdate() {
        const surveyTitle = getElement('surveyTitle');
        const surveyDescription = getElement('surveyDescription');
        const startDate = getElement('startDate');
        const endDate = getElement('endDate');
        const token = getToken();
        const surveyId = getElement('surveyId')?.value;

        try {
            if (!surveyTitle?.value.trim() || !startDate?.value || !endDate?.value) {
                alert('Пожалуйста, заполните все обязательные поля');
                return;
            }

            if (new Date(endDate.value) <= new Date(startDate.value)) {
                alert('Дата окончания должна быть позже даты начала');
                return;
            }

            if (!token || !surveyId) {
                alert('Ошибка безопасности. Пожалуйста, обновите страницу.');
                return;
            }

            if (!window.surveyEditSelectedOmsu.length) {
                alert('Пожалуйста, выберите хотя бы одну организацию!');
                return;
            }

            const formData = {
                Title: surveyTitle.value.trim(),
                Description: surveyDescription?.value.trim() || '',
                StartDate: new Date(startDate.value).toISOString(),
                EndDate: new Date(endDate.value).toISOString(),
                Organizations: window.surveyEditSelectedOmsu.map(org => org.id),
                Criteria: Array.from(document.querySelectorAll('.criteriy'))
                    .map(input => input.value.trim())
                    .filter(text => text !== '')
            };

            const response = await fetch(`/update_survey_bd/${surveyId}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token,
                    'Accept': 'application/json'
                },
                body: JSON.stringify(formData)
            });

            if (!response.ok) {
                let errorMessage = 'Ошибка сервера';
                try {
                    const errorData = await response.json();
                    errorMessage = errorData.message || errorData.error || errorMessage;
                } catch (e) {
                    console.error('Ошибка при чтении ответа:', e);
                }
                throw new Error(errorMessage);
            }

            const result = await response.json();
            if (result.success) {
                alert(result.message || 'Анкета успешно обновлена!');
                window.location.reload();
                return;
            }

            throw new Error(result.message || 'Неизвестная ошибка');
        } catch (error) {
            console.error('Ошибка при обновлении анкеты:', error);

            let userMessage = error.message;
            if (error.message.includes('jsonb') && error.message.includes('text')) {
                userMessage = 'Ошибка формата данных. Пожалуйста, обновите страницу и попробуйте снова.';
            } else if (error.message.includes('date')) {
                userMessage = 'Ошибка в датах. Проверьте правильность введенных дат.';
            } else if (error.message.includes('validation')) {
                userMessage = 'Ошибка валидации данных: ' + error.message;
            }

            alert(`Ошибка: ${userMessage}`);
            if (confirm('Показать подробности ошибки? (для разработчиков)')) {
                alert(`Техническая информация:\n${error.stack || error.message}`);
            }
        }
    };

    window.surveyEditValidateForm = function surveyEditValidateForm() {
        let isValid = true;

        const requiredFields = [
            { element: getElement('surveyTitle'), errorId: 'titleError' },
            { element: getElement('startDate'), errorId: 'startDateError' },
            { element: getElement('endDate'), errorId: 'endDateError' }
        ];

        requiredFields.forEach(field => {
            const errorElement = getElement(field.errorId);
            if (!field.element?.value.trim()) {
                field.element?.classList.add('invalid');
                if (errorElement) errorElement.style.display = 'block';
                isValid = false;
            } else {
                field.element.classList.remove('invalid');
                if (errorElement) errorElement.style.display = 'none';
            }
        });

        const startDate = getElement('startDate');
        const endDate = getElement('endDate');
        const endDateError = getElement('endDateError');

        if (startDate?.value && endDate?.value && new Date(endDate.value) <= new Date(startDate.value)) {
            endDate.classList.add('invalid');
            if (endDateError) {
                endDateError.textContent = 'Дата окончания должна быть позже даты начала';
                endDateError.style.display = 'block';
            }
            isValid = false;
        }

        const omsuError = getElement('omsuError');
        if (!window.surveyEditSelectedOmsu.length) {
            if (omsuError) omsuError.style.display = 'block';
            isValid = false;
        } else if (omsuError) {
            omsuError.style.display = 'none';
        }

        return isValid;
    };

    window.handleResponse = function handleResponse(response) {
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        return response.json();
    };

    window.handleError = function handleError(error) {
        console.error('Ошибка:', error);
        alert('Произошла ошибка: ' + (error.message || 'Попробуйте снова или обратитесь в поддержку'));
    };

    window.getValueSafe = function getValueSafe(elementId) {
        return getElement(elementId)?.value || '';
    };

    window.showNotification = function showNotification(message, isSuccess) {
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
    };

    window.hideNotification2 = function hideNotification2() {
        const notification = getElement('notification');
        const messageElement = getElement('notificationMessage');

        if (notification) {
            notification.style.display = 'none';
        }

        if (messageElement && messageElement.className.includes('notification-success')) {
            window.location.reload();
        }
    };

    window.copySurvey = function copySurvey(id) {
        const startDate = getElement('startDate')?.value;
        const endDate = getElement('endDate')?.value;
        const token = getToken();

        if (!startDate || !endDate) {
            window.showNotification('Пожалуйста, заполните все обязательные поля', false);
            return;
        }

        if (new Date(endDate) <= new Date(startDate)) {
            window.showNotification('Дата окончания должна быть позже даты начала', false);
            return;
        }

        getElement('loadingOverlay')?.style.setProperty('display', 'flex');

        fetch('/Survey/copy_survey_bd/' + id, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify({
                StartDate: startDate,
                EndDate: endDate
            })
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
                getElement('loadingOverlay')?.style.setProperty('display', 'none');

                if (data.success) {
                    alert('Анкета успешно скопирована!');
                    window.location.reload();
                    return;
                }

                throw new Error(data.message || 'Ошибка при копировании анкеты');
            })
            .catch(error => {
                getElement('loadingOverlay')?.style.setProperty('display', 'none');
                window.showNotification(error.message, false);
                console.error('Error:', error);
            });
    };

    window.restoreSurveys = function restoreSurveys() {
        if (typeof window.handleTabClick === 'function') {
            window.handleTabClick('get_surveys');
        }
    };

    window.copy_archive_survey = function copy_archive_survey(name_survey, description, date_begin, date_end, file_questions) {
        const data = {
            name_survey,
            description,
            date_open: date_begin,
            date_close: date_end,
            questions: file_questions
        };

        fetch('/copy_archive_survey', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(data)
        })
            .then(response => {
                if (!response.ok) {
                    throw new Error('Ошибка при добавлении анкеты');
                }
                return response.json();
            })
            .then(() => {
                alert('Анкета успешно добавлена!');
                window.location.reload();
            })
            .catch(err => {
                alert(err.message);
            });
    };
})();
