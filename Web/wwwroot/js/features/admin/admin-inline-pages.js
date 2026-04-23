(() => {
    const adminInlineAppPages = window.AdminInlineAppPages || (window.AdminInlineAppPages = {});

    adminInlineAppPages.mountExtensionModal = function mountExtensionModal(host, { survey, onClose }) {
        if (!host) {
            return null;
        }

        let disposed = false;
        let organizations = [];
        let loading = true;
        let error = '';
        let extension = { organizationId: '', extendedUntil: '' };
        const today = new Date().toISOString().split('T')[0];

        const isFormValid = () => {
            return Boolean(
                extension.organizationId
                && extension.extendedUntil
                && extension.extendedUntil > today
            );
        };

        const handleChange = (field, value) => {
            extension = {
                ...extension,
                [field]: value
            };
            render();
        };

        const handleSubmit = async () => {
            if (!extension.organizationId || !extension.extendedUntil) {
                alert('Пожалуйста, заполните все поля.');
                return;
            }

            if (extension.extendedUntil <= today) {
                alert('Дата конца должна быть в будущем.');
                return;
            }

            try {
                const response = await fetch('/survey-extensions', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
                    },
                    body: JSON.stringify({
                        surveyId: survey?.id_survey,
                        extensions: [{
                            organizationId: parseInt(extension.organizationId, 10),
                            extendedUntil: extension.extendedUntil
                        }]
                    })
                });

                const responseText = await response.text();
                let responseData = null;

                try {
                    responseData = JSON.parse(responseText);
                } catch (parseError) {
                    console.error('Не удалось разобрать ответ сервера:', parseError);
                }

                if (!response.ok || !responseData?.success) {
                    const validationErrors = Array.isArray(responseData?.errors)
                        ? responseData.errors.filter(Boolean).join('\n')
                        : '';
                    throw new Error(
                        validationErrors
                        || responseData?.error
                        || responseData?.message
                        || responseText
                        || (window.getResponseErrorMessage
                            ? window.getResponseErrorMessage(response, 'Ошибка продления')
                            : `Ошибка продления: ${response.status}`)
                    );
                }

                onClose();
                if (typeof window.handleAdminMutationSuccess === 'function') {
                    await window.handleAdminMutationSuccess({
                        message: responseData.message || 'Доступ успешно продлён.',
                        tabName: typeof window.resolveCurrentAdminTab === 'function'
                            ? window.resolveCurrentAdminTab()
                            : 'get_surveys',
                        fallbackUrl: window.location.pathname
                    });
                    return;
                }

                alert(responseData.message || 'Доступ успешно продлён.');
                window.location.reload();
            } catch (submitError) {
                console.error('Ошибка продления анкеты:', submitError);
                alert(`Ошибка: ${submitError.message || 'Не удалось продлить доступ.'}`);
            }
        };

        const render = () => {
            if (disposed) {
                return;
            }
            const template = document.getElementById('admin-extension-modal-template');
            const rowTemplate = document.getElementById('admin-extension-modal-row-template');
            if (!host || !template?.content?.firstElementChild || !rowTemplate?.content?.firstElementChild) {
                return;
            }

            host.innerHTML = '';
            const root = template.content.firstElementChild.cloneNode(true);
            const surveyName = root.querySelector('[data-role="survey-name"]');
            const errorNode = root.querySelector('[data-role="error"]');
            const rowsContainer = root.querySelector('[data-role="rows-container"]');
            const emptyState = root.querySelector('[data-role="empty-state"]');
            const submitButton = root.querySelector('[data-role="submit"]');
            const cancelButton = root.querySelector('[data-role="cancel"]');

            if (surveyName) {
                surveyName.textContent = `Анкета: "${survey?.name_survey || ''}"`;
            }
            if (errorNode) {
                errorNode.textContent = error || '';
                errorNode.style.display = error ? 'block' : 'none';
            }

            const showRows = !loading && organizations.length > 0;
            if (rowsContainer) {
                rowsContainer.style.display = showRows ? '' : 'none';
            }
            if (emptyState) {
                emptyState.style.display = !loading && !error && organizations.length === 0 ? '' : 'none';
            }

            if (showRows && rowsContainer) {
                const row = rowTemplate.content.firstElementChild.cloneNode(true);
                const orgSelect = row.querySelector('[data-role="org-select"]');
                const dateInput = row.querySelector('[data-role="date-input"]');

                if (orgSelect) {
                    const defaultOption = document.createElement('option');
                    defaultOption.value = '';
                    defaultOption.textContent = '-- Выберите организацию --';
                    orgSelect.appendChild(defaultOption);

                    organizations.forEach((organization) => {
                        const option = document.createElement('option');
                        option.value = organization.organizationId;
                        option.textContent = organization.organizationName;
                        if (extension.organizationId === organization.organizationId) {
                            option.selected = true;
                        }
                        orgSelect.appendChild(option);
                    });

                    orgSelect.addEventListener('change', (event) => {
                        handleChange('organizationId', event.target.value);
                    });
                }

                if (dateInput) {
                    dateInput.value = extension.extendedUntil;
                    dateInput.min = today;
                    dateInput.addEventListener('change', (event) => {
                        handleChange('extendedUntil', event.target.value);
                    });
                }

                rowsContainer.appendChild(row);
            }

            if (submitButton) {
                submitButton.disabled = !isFormValid() || loading;
                submitButton.textContent = loading ? 'Обработка...' : 'Продлить доступ';
                submitButton.style.backgroundColor = isFormValid() ? '#4caf50' : '#9e9e9e';
                submitButton.style.cursor = isFormValid() ? 'pointer' : 'not-allowed';
                submitButton.style.opacity = isFormValid() ? '1' : '0.6';
                submitButton.addEventListener('click', handleSubmit);
            }
            if (cancelButton) {
                cancelButton.addEventListener('click', onClose);
            }

            host.appendChild(root);
        };

        const fetchOrganizations = async () => {
            try {
                loading = true;
                render();
                const response = await fetch('/organizations/data');
                if (!response.ok) {
                    throw new Error(
                        window.getResponseErrorMessage
                            ? window.getResponseErrorMessage(response, 'Не удалось загрузить организации')
                            : `Не удалось загрузить организации: ${response.status}`
                    );
                }

                const data = await response.json();
                organizations = Array.isArray(data)
                    ? data
                        .filter((org) => org && (org.id_organization !== undefined || org.id !== undefined))
                        .map((org) => ({
                            organizationId: String(org.id_organization ?? org.id),
                            organizationName: String(org.organization_name ?? org.name ?? '')
                        }))
                        .filter((org) => org.organizationName)
                    : [];
                error = '';
            } catch (fetchError) {
                console.error('Ошибка загрузки организаций:', fetchError);
                error = fetchError.message || 'Не удалось загрузить список организаций';
            } finally {
                loading = false;
                render();
            }
        };

        render();
        fetchOrganizations();

        return () => {
            disposed = true;
            host.innerHTML = '';
        };
    };

    adminInlineAppPages.mountStatisticsPage = function mountStatisticsPage(host) {
        if (!host) {
            return null;
        }

        let disposed = false;
        let chartsData = null;
        let loading = true;
        let error = '';
        const chartRefs = {
            line: null,
            bar: null,
            pie: null,
            radar: null
        };
        const chartInstances = {
            line: null,
            bar: null,
            pie: null,
            radar: null
        };

        const destroyCharts = () => {
            Object.values(chartInstances).forEach((chart) => {
                if (chart) {
                    chart.destroy();
                }
            });
            chartInstances.line = null;
            chartInstances.bar = null;
            chartInstances.pie = null;
            chartInstances.radar = null;
        };

        const renderCharts = () => {
            if (loading || error || !chartsData) {
                return;
            }

            if (typeof Chart === 'undefined') {
                error = 'Chart.js не загружен.';
                render();
                return;
            }

            destroyCharts();

            const shouldShowLegend = ({ labels = [], datasets = [] } = {}) => {
                if (datasets.length > 1) {
                    return true;
                }

                if (datasets.length === 1) {
                    if ((datasets[0]?.label || '').trim()) {
                        return false;
                    }

                    return labels.length > 1;
                }

                return labels.length > 1;
            };

            const commonOptions = {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: {
                            padding: 20,
                            boxWidth: 12,
                            font: {
                                size: 12
                            }
                        }
                    }
                },
                layout: {
                    padding: {
                        top: 10,
                        bottom: 30
                    }
                }
            };

            if (chartRefs.line && chartsData.lineChart) {
                chartInstances.line = new Chart(chartRefs.line, {
                    type: 'line',
                    data: {
                        labels: chartsData.lineChart.labels,
                        datasets: [{
                            label: chartsData.lineChart.label,
                            data: chartsData.lineChart.data,
                            borderColor: 'rgb(75, 192, 192)',
                            backgroundColor: 'rgba(75, 192, 192, 0.1)',
                            tension: 0.1,
                            borderWidth: 2,
                            pointRadius: 4
                        }]
                    },
                    options: {
                        ...commonOptions,
                        plugins: {
                            ...commonOptions.plugins,
                            legend: {
                                ...commonOptions.plugins.legend,
                                display: shouldShowLegend({
                                    labels: chartsData.lineChart.labels,
                                    datasets: [{ label: chartsData.lineChart.label }]
                                })
                            }
                        },
                        scales: {
                            y: {
                                beginAtZero: true
                            }
                        }
                    }
                });
            }

            if (chartRefs.bar && chartsData.barChart) {
                chartInstances.bar = new Chart(chartRefs.bar, {
                    type: 'bar',
                    data: {
                        labels: chartsData.barChart.labels,
                        datasets: [{
                            label: chartsData.barChart.label,
                            data: chartsData.barChart.data,
                            backgroundColor: 'rgba(54, 162, 235, 0.7)',
                            borderColor: 'rgba(54, 162, 235, 1)',
                            borderWidth: 1
                        }]
                    },
                    options: {
                        ...commonOptions,
                        plugins: {
                            ...commonOptions.plugins,
                            legend: {
                                ...commonOptions.plugins.legend,
                                display: shouldShowLegend({
                                    labels: chartsData.barChart.labels,
                                    datasets: [{ label: chartsData.barChart.label }]
                                })
                            }
                        },
                        scales: {
                            y: {
                                beginAtZero: true
                            }
                        }
                    }
                });
            }

            if (chartRefs.pie && chartsData.pieChart) {
                chartInstances.pie = new Chart(chartRefs.pie, {
                    type: 'pie',
                    data: {
                        labels: chartsData.pieChart.labels,
                        datasets: [{
                            data: chartsData.pieChart.data,
                            backgroundColor: [
                                'rgba(255, 99, 132, 0.7)',
                                'rgba(54, 162, 235, 0.7)',
                                'rgba(255, 206, 86, 0.7)',
                                'rgba(75, 192, 192, 0.7)',
                                'rgba(153, 102, 255, 0.7)'
                            ],
                            borderWidth: 1
                        }]
                    },
                    options: {
                        ...commonOptions,
                        plugins: {
                            legend: {
                                ...commonOptions.plugins.legend,
                                display: shouldShowLegend({
                                    labels: chartsData.pieChart.labels,
                                    datasets: [{ label: '' }]
                                }),
                                align: 'center'
                            }
                        }
                    }
                });
            }

            if (chartRefs.radar && chartsData.avgScoreByOrganizationRadar) {
                chartInstances.radar = new Chart(chartRefs.radar, {
                    type: 'radar',
                    data: chartsData.avgScoreByOrganizationRadar,
                    options: {
                        ...commonOptions,
                        plugins: {
                            ...commonOptions.plugins,
                            legend: {
                                ...commonOptions.plugins.legend,
                                display: shouldShowLegend(chartsData.avgScoreByOrganizationRadar)
                            },
                            title: {
                                display: true,
                                text: 'Средний балл организаций по годам'
                            }
                        },
                        scales: {
                            r: {
                                beginAtZero: true,
                                min: 0,
                                max: 5
                            }
                        }
                    }
                });
            }
        };

        const render = () => {
            if (disposed) {
                return;
            }
            host.innerHTML = '';
            if (loading) {
                const loadingNode = document.createElement('div');
                loadingNode.className = 'loading';
                loadingNode.textContent = 'Загрузка данных...';
                host.appendChild(loadingNode);
                return;
            }

            if (error) {
                const errorNode = document.createElement('div');
                errorNode.className = 'error';
                errorNode.textContent = `Ошибка: ${error}`;
                host.appendChild(errorNode);
                return;
            }

            const template = document.getElementById('admin-statistics-template');
            if (!template?.content?.firstElementChild) {
                return;
            }

            const root = template.content.firstElementChild.cloneNode(true);
            chartRefs.line = root.querySelector('[data-role="line-chart"]');
            chartRefs.bar = root.querySelector('[data-role="bar-chart"]');
            chartRefs.pie = root.querySelector('[data-role="pie-chart"]');
            chartRefs.radar = root.querySelector('[data-role="radar-chart"]');
            host.appendChild(root);
            renderCharts();
        };

        const loadData = async () => {
            try {
                await fetch('/statistics');
                const response = await fetch('/statistics/data');
                if (!response.ok) {
                    throw new Error(
                        window.getResponseErrorMessage
                            ? window.getResponseErrorMessage(response, 'Ошибка загрузки статистики')
                            : 'Ошибка загрузки статистики'
                    );
                }
                chartsData = await response.json();
            } catch (loadError) {
                console.error('Ошибка загрузки статистики:', loadError);
                error = loadError.message || 'Не удалось загрузить данные статистики.';
            } finally {
                loading = false;
                render();
            }
        };

        render();
        loadData();

        return () => {
            disposed = true;
            destroyCharts();
            host.innerHTML = '';
        };
    };

    function getEmailField(id) {
        return document.getElementById(id);
    }

    function getEmailTrimmedValue(id) {
        return (getEmailField(id)?.value || '').trim();
    }

    function splitEmailRecipients(value) {
        return String(value || '')
            .split(/[;,\r\n]+/)
            .map((item) => item.trim())
            .filter(Boolean);
    }

    function isValidEmailAddress(email) {
        const value = String(email || '').trim();
        if (!value) {
            return false;
        }

        return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
    }

    function setEmailInvalidState(id, isInvalid) {
        const element = getEmailField(id);
        if (!element) {
            return;
        }

        element.classList.toggle('invalid', Boolean(isInvalid));
        element.setAttribute('aria-invalid', isInvalid ? 'true' : 'false');
    }

    function clearEmailInvalidStates() {
        [
            'email-to',
            'email-subject',
            'email-content',
            'email-smtp-host',
            'email-smtp-port',
            'email-smtp-user-name',
            'email-smtp-password',
            'email-from-address',
            'email-from-display-name'
        ].forEach((id) => setEmailInvalidState(id, false));
    }

    function collectEmailSettingsPayload() {
        const smtpPortValue = Number.parseInt(getEmailField('email-smtp-port')?.value || '', 10);

        return {
            to: getEmailTrimmedValue('email-to'),
            subject: getEmailTrimmedValue('email-subject'),
            content: (getEmailField('email-content')?.value || '').trim(),
            smtpHost: getEmailTrimmedValue('email-smtp-host'),
            smtpPort: Number.isFinite(smtpPortValue) ? smtpPortValue : 0,
            smtpEnableSsl: (getEmailField('email-smtp-enable-ssl')?.value || 'true') === 'true',
            smtpUserName: getEmailTrimmedValue('email-smtp-user-name'),
            smtpPassword: getEmailField('email-smtp-password')?.value || '',
            fromAddress: getEmailTrimmedValue('email-from-address'),
            fromDisplayName: getEmailTrimmedValue('email-from-display-name')
        };
    }

    function validateEmailSettingsPayload(settings) {
        clearEmailInvalidStates();

        const errors = [];
        const recipients = splitEmailRecipients(settings.to);

        if (recipients.length === 0) {
            errors.push('Поле «Кому» должно содержать хотя бы один email.');
            setEmailInvalidState('email-to', true);
        } else {
            const invalidRecipients = recipients.filter((email) => !isValidEmailAddress(email));
            if (invalidRecipients.length > 0) {
                errors.push(`Поле «Кому» содержит некорректные email: ${invalidRecipients.join(', ')}.`);
                setEmailInvalidState('email-to', true);
            }
        }

        if (!settings.subject) {
            errors.push('Поле «Тема» обязательно.');
            setEmailInvalidState('email-subject', true);
        }

        if (!settings.content) {
            errors.push('Поле «Содержание» обязательно.');
            setEmailInvalidState('email-content', true);
        }

        if (!settings.smtpHost) {
            errors.push('Поле «SMTP сервер» обязательно.');
            setEmailInvalidState('email-smtp-host', true);
        }

        if (!Number.isInteger(settings.smtpPort) || settings.smtpPort < 1 || settings.smtpPort > 65535) {
            errors.push('Поле «Порт SMTP» должно быть числом от 1 до 65535.');
            setEmailInvalidState('email-smtp-port', true);
        }

        if (!isValidEmailAddress(settings.fromAddress)) {
            errors.push('Поле «Email отправителя» заполнено некорректно.');
            setEmailInvalidState('email-from-address', true);
        }

        const hasUserName = Boolean(settings.smtpUserName);
        const hasPassword = Boolean(settings.smtpPassword);
        if (hasUserName !== hasPassword) {
            errors.push('Логин SMTP и пароль SMTP должны быть заполнены вместе.');
            setEmailInvalidState('email-smtp-user-name', true);
            setEmailInvalidState('email-smtp-password', true);
        }

        return errors;
    }

    async function extractEmailApiErrors(response) {
        const fallbackMessage = typeof window.getResponseErrorMessage === 'function'
            ? window.getResponseErrorMessage(response, 'Ошибка')
            : 'Не удалось выполнить запрос.';

        const responseText = await response.text();
        if (!responseText) {
            return [fallbackMessage];
        }

        try {
            const payload = JSON.parse(responseText);
            if (Array.isArray(payload?.errors) && payload.errors.length > 0) {
                return payload.errors.filter(Boolean);
            }

            if (payload?.error) {
                return [payload.error];
            }

            if (payload?.message) {
                return [payload.message];
            }
        } catch (error) {
            return [responseText];
        }

        return [fallbackMessage];
    }

    function showEmailToast(message, type, title, options = {}) {
        const normalizedMessage = String(message || '').trim();
        if (!normalizedMessage) {
            return;
        }

        if (typeof window.siteNotify === 'function') {
            window.siteNotify(normalizedMessage, type, {
                title,
                duration: options.duration ?? (type === 'error' ? 0 : 4500)
            });
            return;
        }

        window.alert(normalizedMessage);
    }

    function showEmailValidationErrors(errors) {
        const normalizedErrors = (Array.isArray(errors) ? errors : [errors])
            .map((item) => String(item || '').trim())
            .filter(Boolean);

        if (normalizedErrors.length === 0) {
            return;
        }

        showEmailToast(normalizedErrors.join(' • '), 'error', 'Проверьте поля', { duration: 0 });
    }

    function setEmailButtonsBusy(isBusy, options = {}) {
        const activeButtonId = options.activeButtonId || '';
        const busyLabel = options.busyLabel || '';

        document
            .querySelectorAll('.email-settings-page__actions button')
            .forEach((button) => {
                button.disabled = isBusy;
                if (!button.dataset.defaultLabel) {
                    button.dataset.defaultLabel = button.textContent || '';
                }

                if (isBusy) {
                    button.textContent = activeButtonId && button.id === activeButtonId
                        ? busyLabel || button.dataset.defaultLabel || button.textContent
                        : button.dataset.defaultLabel || button.textContent;
                    return;
                }

                button.textContent = button.dataset.defaultLabel || button.textContent;
            });
    }

    async function submitEmailSettings(url, options) {
        const settings = collectEmailSettingsPayload();
        const validationErrors = validateEmailSettingsPayload(settings);
        if (validationErrors.length > 0) {
            showEmailValidationErrors(validationErrors);
            return false;
        }

        setEmailButtonsBusy(true, {
            activeButtonId: options.busyButtonId,
            busyLabel: options.busyLabel
        });

        try {
            const response = await fetch(url, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: JSON.stringify(settings)
            });

            if (!response.ok) {
                throw new Error((await extractEmailApiErrors(response)).join(' '));
            }

            const payload = await response.json();
            clearEmailInvalidStates();
            showEmailToast(
                payload?.message || options.successMessage,
                'success',
                options.successTitle
            );
            return true;
        } catch (error) {
            showEmailToast(
                error.message || options.errorMessage || 'Не удалось выполнить операцию.',
                'error',
                options.errorTitle,
                { duration: 0 }
            );
            return false;
        } finally {
            setEmailButtonsBusy(false);
        }
    }

    window.saveEmailSettings = function saveEmailSettings() {
        return submitEmailSettings('/mail/settings', {
            busyButtonId: 'email-save-button',
            busyLabel: 'Сохранение...',
            successTitle: 'Настройки сохранены',
            successMessage: 'Настройки электронной почты сохранены.',
            errorTitle: 'Сохранение не выполнено',
            errorMessage: 'Не удалось сохранить настройки.'
        });
    };

    window.sendEmailMessage = function sendEmailMessage() {
        return submitEmailSettings('/mail/send', {
            busyButtonId: 'email-send-button',
            busyLabel: 'Отправка...',
            successTitle: 'Письмо отправлено',
            successMessage: 'Письмо отправлено.',
            errorTitle: 'Письмо не отправлено',
            errorMessage: 'Не удалось отправить письмо.'
        });
    };

    function bindEmailAction(buttonId, action) {
        const button = document.getElementById(buttonId);
        if (!button || button.dataset.emailActionBound === 'true') {
            return;
        }

        button.dataset.emailActionBound = 'true';
        button.addEventListener('click', (event) => {
            event.preventDefault();
            event.stopPropagation();
            action();
        });
    }

    window.initEmailSettingsPage = function initEmailSettingsPage() {
        bindEmailAction('email-save-button', window.saveEmailSettings);
        bindEmailAction('email-send-button', window.sendEmailMessage);
    };
})();
