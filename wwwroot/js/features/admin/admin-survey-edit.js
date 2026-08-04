function surveyEditGetOrganizationItems() {
    const organizationList = document.getElementById('organizationList');
    return organizationList ? organizationList.querySelectorAll('[data-role="organization-option"]') : [];
}

function surveyEditNotify(message, type = 'error', options = {}) {
    const normalizedMessage = String(message || '').trim();
    if (!normalizedMessage) {
        return;
    }

    window.AppUi.notify(normalizedMessage, type, {
        title: options.title,
        duration: options.duration ?? (type === 'error' ? 0 : 4500)
    });
}

function surveyEditGetElementByRole(role) {
    return document.querySelector(`[data-role="${role}"]`);
}

function surveyEditCreateIconButton(iconClass, label) {
    const button = window.AppUi.createElement('button', {
        type: 'button',
        ariaLabel: label
    });

    const icon = window.AppUi.createElement('i', {
        className: iconClass,
        attrs: { 'aria-hidden': 'true' }
    });
    button.appendChild(icon);

    return button;
}

function surveyEditToggleOrganizationSelection(element) {
    const orgId = parseInt(element.dataset.id, 10);
    const orgName = element.dataset.name || element.querySelector('label')?.textContent?.trim() || '';
    if (!Number.isFinite(orgId) || !orgName) {
        return;
    }

    if (typeof window.toggleOrganizationSelection === 'function') {
        window.toggleOrganizationSelection(orgId, orgName);
        return;
    }

    const checkbox = element.querySelector('input[type="checkbox"]');
    const nextSelected = element.dataset.selected !== 'true';
    element.dataset.selected = nextSelected ? 'true' : 'false';
    element.classList.toggle('selected', nextSelected);
    if (checkbox) {
        checkbox.checked = nextSelected;
    }
}

function surveyEditSaveSelectedOrganization() {
    if (typeof window.surveyEditCloseOrganizationDropdown === 'function') {
        window.surveyEditCloseOrganizationDropdown();
    } else {
        surveyEditCloseModal('organizationModal');
    }

    if (typeof window.updateSelectedOrganizationDisplay === 'function') {
        window.updateSelectedOrganizationDisplay();
    }
}

 function surveyEditUpdateSelectedOrganizationDisplay() {
    if (typeof window.updateSelectedOrganizationDisplay === 'function') {
        window.updateSelectedOrganizationDisplay();
    }
}


            function surveyEditRemoveOrganization(orgId) {
                if (typeof window.removeSelectedOrganization === 'function') {
                    window.removeSelectedOrganization(orgId);
                    return;
                }
            }

            function surveyEditAddCriteria() {
                if (typeof window.appendSurveyCriteriaField === 'function') {
                    window.appendSurveyCriteriaField('');
                }
            }

    async function surveyEditUpdate() {
        const surveyTitle = document.getElementById('surveyTitle');
        const surveyDescription = document.getElementById('surveyDescription');
        const startDate = document.getElementById('startDate');
        const endDate = document.getElementById('endDate');
        const token = window.AppHttp?.getAntiforgeryToken() || '';
        const surveyId = document.getElementById('surveyId')?.value;
        try {
            if (typeof window.surveyEditValidateForm === 'function' && !window.surveyEditValidateForm()) {
                return;
            }

            if (!token || !surveyId) {
                surveyEditNotify('Ошибка безопасности.');
                return;
            }

            const formData = {
                Title: surveyTitle.value.trim(),
                Description: surveyDescription?.value.trim() || '',
                StartDate: window.AppDate?.getInputIso(startDate) || '',
                EndDate: window.AppDate?.getInputIso(endDate) || '',
                Organizations: (typeof window.getSelectedOrganizations === 'function'
                    ? window.getSelectedOrganizations()
                    : surveyEditSelectedOrganization
                ).map(org => org.id),
                Criteria: Array.from(document.querySelectorAll('.criteriy'))
                    .map(input => input.value.trim())
                    .filter(text => text !== '')
            };

            const response = await fetch(`/survey/${surveyId}/update`, {
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
                if (typeof window.handleSurveyUpdateSuccess === 'function') {
                    window.handleSurveyUpdateSuccess(result);
                    return;
                }

                if (typeof window.handleAdminMutationSuccess === 'function') {
                    await window.handleAdminMutationSuccess({
                        message: result.message || 'Анкета успешно обновлена!',
                        tabName: 'get_surveys',
                        fallbackUrl: '/survey'
                    });
                    return;
                }

                surveyEditNotify(result.message || 'Анкета успешно обновлена!', 'success');
                window.location.reload();
            } else {
                throw new Error(result.message || 'Неизвестная ошибка');
            }

        } catch (error) {
            console.error('Ошибка при обновлении анкеты:', error);

            let userMessage = error.message;

            if (error.message.includes('jsonb') && error.message.includes('text')) {
                userMessage = 'Ошибка формата данных.';
            } else if (error.message.includes('date')) {
                userMessage = 'Ошибка в датах. Проверьте правильность введенных дат.';
            } else if (error.message.includes('validation')) {
                userMessage = 'Ошибка валидации данных: ' + error.message;
            }

            surveyEditNotify(userMessage);

            const showDetails = await window.siteConfirm('Показать технические подробности ошибки?', {
                title: 'Подробности ошибки',
                confirmText: 'Показать',
                cancelText: 'Закрыть'
            });

            if (showDetails) {
                console.error('Техническая информация:', error.stack || error.message);
                window.AppUi?.notify?.('Подробности ошибки выведены в консоль браузера.', 'info');
            }
        }
    }

    function surveyEditValidateForm() {
        let isValid = true;
        const errors = [];

        const requiredFields = [
            { element: document.getElementById('surveyTitle'), message: 'Введите название анкеты' },
            { element: document.getElementById('startDate'), message: 'Введите дату начала' },
            { element: document.getElementById('endDate'), message: 'Введите дату конца' }
        ];

        requiredFields.forEach(field => {
            if (!field.element.value.trim()) {
                field.element.classList.add('invalid');
                errors.push(field.message);
                isValid = false;
            } else {
                field.element.classList.remove('invalid');
            }
        });

        const startDate = document.getElementById('startDate');
        const endDate = document.getElementById('endDate');

        const startDateIso = window.AppDate?.getInputIso(startDate) || '';
        const endDateIso = window.AppDate?.getInputIso(endDate) || '';

        if ((startDate.value && !startDateIso) || (endDate.value && !endDateIso)) {
            errors.push('Используйте формат даты ДД.ММ.ГГГГ');
            isValid = false;
        } else if (startDateIso && endDateIso && window.AppDate?.compare(endDateIso, startDateIso) <= 0) {
            endDate.classList.add('invalid');
            errors.push('Дата конца должна быть позже даты начала');
            isValid = false;
        }

        const selectedOrganizations = typeof window.getSelectedOrganizations === 'function'
            ? window.getSelectedOrganizations()
            : surveyEditSelectedOrganization;
        if (selectedOrganizations.length === 0) {
            errors.push('Выберите организацию');
            isValid = false;
        }

        if (typeof window.validateSurveyCriteriaFields === 'function' && !window.validateSurveyCriteriaFields()) {
            isValid = false;
        }

        if (errors.length > 0) {
            surveyEditNotify([...new Set(errors)].join(' • '));
        }

        return isValid;
    }
    // Общие helper-функции вынесены в ~/js/pages/admin-common-helpers.js

        // СКРИПТЫ ДЛЯ ВКЛАДКИ КОПИРОВАНИЯ АНКЕТЫ

            function copySurvey(id) {
                const startDate = window.AppDate?.getInputIso('startDate') || '';
                const endDate = window.AppDate?.getInputIso('endDate') || '';
                const token = window.AppHttp?.getAntiforgeryToken() || '';

                if (!startDate || !endDate) {
                    window.AppUi?.notify?.('Пожалуйста, заполните все обязательные поля', 'error');
                    return;
                }

                if ((window.AppDate?.compare(endDate, startDate) ?? -1) <= 0) {
                    window.AppUi?.notify?.('Дата конца должна быть позже даты начала', 'error');
                    return;
                }

                document.getElementById('loadingOverlay').style.display = 'flex';

                fetch('/survey/' + id + '/copy', {
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
                    document.getElementById('loadingOverlay').style.display = 'none';

                    if (data.success) {
                        if (typeof window.handleAdminMutationSuccess === 'function') {
                            return window.handleAdminMutationSuccess({
                                message: data.message || 'Анкета успешно скопирована!',
                                tabName: 'get_surveys',
                                fallbackUrl: '/survey'
                            });
                        }

                        surveyEditNotify('Анкета успешно скопирована!', 'success');
                        window.location.reload();
                    } else {
                        throw new Error(data.message || 'Ошибка при копировании анкеты');
                    }
                })
                .catch(error => {
                    document.getElementById('loadingOverlay').style.display = 'none';
                    window.AppUi?.notify?.(error.message, 'error');
                    console.error('Error:', error);
                });
            }

window.surveyEditToggleOrganizationSelection = surveyEditToggleOrganizationSelection;
window.surveyEditSaveSelectedOrganization = surveyEditSaveSelectedOrganization;
window.surveyEditUpdateSelectedOrganizationDisplay = surveyEditUpdateSelectedOrganizationDisplay;
window.surveyEditRemoveOrganization = surveyEditRemoveOrganization;
window.surveyEditAddCriteria = surveyEditAddCriteria;
window.surveyEditUpdate = surveyEditUpdate;
window.surveyEditValidateForm = surveyEditValidateForm;
window.copySurvey = copySurvey;
