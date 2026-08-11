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
                surveyEditNotify('Не удалось подтвердить безопасность запроса.');
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
                Criteria: Array.from(document.querySelectorAll('#surveyEditorModal .criteriy'))
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
                let errorMessage = 'Не удалось обновить анкету.';
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
                        message: result.message || 'Анкета успешно обновлена.',
                        tabName: 'get_surveys',
                        fallbackUrl: '/survey'
                    });
                    return;
                }

                surveyEditNotify(result.message || 'Анкета успешно обновлена.', 'success');
                window.location.reload();
            } else {
                throw new Error(result.message || 'Не удалось обновить анкету.');
            }

        } catch (error) {
            console.error('Ошибка при обновлении анкеты:', error);

            let userMessage = window.normalizeClientErrorMessage?.(
                error.message,
                'Не удалось обновить анкету.'
            ) || 'Не удалось обновить анкету.';

            if (error.message.includes('jsonb') && error.message.includes('text')) {
                userMessage = 'Ошибка формата данных.';
            } else if (error.message.includes('date')) {
                userMessage = 'Проверьте правильность введённых дат.';
            } else if (error.message.includes('validation')) {
                userMessage = 'Проверьте данные анкеты.';
            }

            surveyEditNotify(userMessage);
        }
    }

    function surveyEditValidateForm() {
        let isValid = true;
        const errors = [];

        const requiredFields = [
            { element: document.getElementById('surveyTitle'), message: 'Введите название анкеты.' },
            { element: document.getElementById('startDate'), message: 'Укажите дату начала.' },
            { element: document.getElementById('endDate'), message: 'Укажите дату конца.' }
        ];

        requiredFields.forEach(field => {
            if (!field.element.value.trim()) {
                window.SurveyAdminValidation?.setFieldError(field.element, field.message);
                errors.push(field.message);
                isValid = false;
            } else {
                window.SurveyAdminValidation?.clearFieldError(field.element);
            }
        });

        const startDate = document.getElementById('startDate');
        const endDate = document.getElementById('endDate');

        const startDateIso = window.AppDate?.toIso(startDate.value) || '';
        const endDateIso = window.AppDate?.toIso(endDate.value) || '';

        if (startDate.value && !startDateIso) {
            const message = 'Введите дату начала в формате ДД.ММ.ГГГГ.';
            window.SurveyAdminValidation?.setFieldError(startDate, message);
            errors.push(message);
            isValid = false;
        }
        if (endDate.value && !endDateIso) {
            const message = 'Введите дату конца в формате ДД.ММ.ГГГГ.';
            window.SurveyAdminValidation?.setFieldError(endDate, message);
            errors.push(message);
            isValid = false;
        }

        const periodError = window.AppDate?.getPeriodError?.(startDate, endDate);
        if (periodError) {
            window.SurveyAdminValidation?.setFieldError(periodError.target, periodError.message);
            errors.push(periodError.message);
            isValid = false;
        }

        const selectedOrganizations = typeof window.getSelectedOrganizations === 'function'
            ? window.getSelectedOrganizations()
            : surveyEditSelectedOrganization;
        if (selectedOrganizations.length === 0) {
            const organizationField = document.querySelector('[data-role="selected-organizations-container"]');
            window.SurveyAdminValidation?.setFieldError(organizationField, 'Выберите хотя бы одну организацию.');
            errors.push('Выберите хотя бы одну организацию.');
            isValid = false;
        } else {
            const organizationField = document.querySelector('[data-role="selected-organizations-container"]');
            window.SurveyAdminValidation?.clearFieldError(organizationField);
        }

        if (typeof window.validateSurveyCriteriaFields === 'function' && !window.validateSurveyCriteriaFields()) {
            errors.push('Заполните критерии оценки.');
            isValid = false;
        }

        if (errors.length > 0) {
            const uniqueErrors = [...new Set(errors)];
            surveyEditNotify(uniqueErrors.join(' • '));
        }

        return isValid;
    }
    // Общие helper-функции вынесены в ~/js/pages/admin-common-helpers.js

        // СКРИПТЫ ДЛЯ ВКЛАДКИ КОПИРОВАНИЯ АНКЕТЫ

            function copySurvey(id) {
                const startDateInput = document.getElementById('startDate');
                const endDateInput = document.getElementById('endDate');
                const startDate = window.AppDate?.toIso(startDateInput?.value) || '';
                const endDate = window.AppDate?.toIso(endDateInput?.value) || '';
                const token = window.AppHttp?.getAntiforgeryToken() || '';

                if (!startDate || !endDate) {
                    const errors = [];
                    if (!startDate) {
                        const message = startDateInput?.value
                            ? 'Укажите корректную дату начала.'
                            : 'Укажите дату начала.';
                        window.AppValidation?.setFieldError?.(startDateInput, message);
                        errors.push(message);
                    }
                    if (!endDate) {
                        const message = endDateInput?.value
                            ? 'Укажите корректную дату конца.'
                            : 'Укажите дату конца.';
                        window.AppValidation?.setFieldError?.(endDateInput, message);
                        errors.push(message);
                    }
                    window.AppValidation?.notifyErrors?.(errors);
                    return;
                }

                const periodError = window.AppDate?.getPeriodError?.(startDateInput, endDateInput);
                if (periodError) {
                    window.AppValidation?.setFieldError?.(periodError.target, periodError.message);
                    window.AppValidation?.notifyErrors?.([periodError.message]);
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
                            throw new Error(err.message || 'Не удалось скопировать анкету.');
                        });
                    }
                    return response.json();
                })
                .then(data => {
                    document.getElementById('loadingOverlay').style.display = 'none';

                    if (data.success) {
                        if (typeof window.handleAdminMutationSuccess === 'function') {
                            return window.handleAdminMutationSuccess({
                                message: data.message || 'Анкета успешно скопирована.',
                                tabName: 'get_surveys',
                                fallbackUrl: '/survey'
                            });
                        }

                        surveyEditNotify('Анкета успешно скопирована.', 'success');
                        window.location.reload();
                    } else {
                        throw new Error(data.message || 'Не удалось скопировать анкету.');
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
window.AppDate?.bindPeriodBounds?.('startDate', 'endDate');
