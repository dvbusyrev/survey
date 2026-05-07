(() => {
    const adminInlineAppPages = window.AdminInlineAppPages || (window.AdminInlineAppPages = {});

    adminInlineAppPages.mountExtensionModal = function mountExtensionModal(host, options = {}) {
        if (!host) {
            return null;
        }

        const {
            survey,
            onClose,
            submitButton: externalSubmitButton = null,
            cancelButton: externalCancelButton = null
        } = options;
        const closeModal = typeof onClose === 'function' ? onClose : () => {};
        const hasExternalActions = Boolean(externalSubmitButton || externalCancelButton);
        let disposed = false;
        let organizations = [];
        let loading = true;
        let error = '';
        let extension = { organizationIds: [], extendedUntil: '' };
        let isOrganizationPanelOpen = false;
        const today = window.AppDate?.todayIso?.() || new Date().toISOString().split('T')[0];
        const minEndDate = (() => {
            const date = new Date();
            date.setDate(date.getDate() + 1);
            const year = date.getFullYear();
            const month = String(date.getMonth() + 1).padStart(2, '0');
            const day = String(date.getDate()).padStart(2, '0');
            return `${year}-${month}-${day}`;
        })();

        const isFormValid = () => {
            return Boolean(
                extension.organizationIds.length > 0
                && extension.extendedUntil
                && window.AppDate?.compare(extension.extendedUntil, today) > 0
            );
        };

        const handleChange = (field, value) => {
            extension = {
                ...extension,
                [field]: value
            };
            render();
        };

        const toggleOrganization = (organizationId, isSelected) => {
            const normalizedId = String(organizationId || '');
            if (!normalizedId) {
                return;
            }

            const currentIds = new Set(extension.organizationIds);
            if (isSelected) {
                currentIds.add(normalizedId);
            } else {
                currentIds.delete(normalizedId);
            }

            extension = {
                ...extension,
                organizationIds: Array.from(currentIds)
            };
            isOrganizationPanelOpen = true;
            render();
        };

        const closeOrganizationPanel = () => {
            if (!isOrganizationPanelOpen || disposed) {
                return;
            }

            isOrganizationPanelOpen = false;
            render();
        };

        const handleDocumentPointerDown = (event) => {
            if (!host.contains(event.target)) {
                closeOrganizationPanel();
            }
        };

        const handleDocumentKeyDown = (event) => {
            if (event.key === 'Escape') {
                closeOrganizationPanel();
            }
        };

        const updateCheckboxListHeight = (container) => {
            const list = container?.querySelector('.app-checkbox-list');
            if (!list) {
                return;
            }

            const listTop = list.getBoundingClientRect().top;
            const availableHeight = Math.max(160, window.innerHeight - listTop - 24);
            list.style.setProperty('--app-checkbox-list-max-height', `${availableHeight}px`);
        };

        const scheduleCheckboxListHeightUpdate = (container) => {
            window.requestAnimationFrame(() => updateCheckboxListHeight(container));
        };

        const handleSubmit = async () => {
            if (extension.organizationIds.length === 0 || !extension.extendedUntil) {
                window.siteNotify?.('Пожалуйста, заполните все поля.', 'error');
                return;
            }

            if ((window.AppDate?.compare(extension.extendedUntil, today) ?? -1) <= 0) {
                window.siteNotify?.('Дата конца должна быть в будущем.', 'error');
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
                        extensions: extension.organizationIds.map((organizationId) => ({
                            organizationId: parseInt(organizationId, 10),
                            extendedUntil: extension.extendedUntil
                        }))
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

                closeModal();
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

                window.siteNotify?.(responseData.message || 'Доступ успешно продлён.', 'success');
                window.location.reload();
            } catch (submitError) {
                console.error('Ошибка продления анкеты:', submitError);
                window.siteNotify?.(submitError.message || 'Не удалось продлить доступ.', 'error');
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

            host.replaceChildren();
            const root = template.content.firstElementChild.cloneNode(true);
            root.classList.toggle('admin-extension-modal-root--external-actions', hasExternalActions);
            const surveyName = root.querySelector('[data-role="survey-name"]');
            const errorNode = root.querySelector('[data-role="error"]');
            const rowsContainer = root.querySelector('[data-role="rows-container"]');
            const emptyState = root.querySelector('[data-role="empty-state"]');
            const submitButton = externalSubmitButton || root.querySelector('[data-role="submit"]');
            const cancelButton = externalCancelButton || root.querySelector('[data-role="cancel"]');

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
                const organizationTrigger = row.querySelector('[data-role="organization-trigger"]');
                const organizationLabel = row.querySelector('[data-role="organization-label"]');
                const organizationPanel = row.querySelector('[data-role="organization-panel"]');
                const organizationOptions = row.querySelector('[data-role="organization-options"]');
                const dateInput = row.querySelector('[data-role="date-input"]');
                const selectedOrganizationIds = new Set(extension.organizationIds);

                if (organizationTrigger) {
                    organizationTrigger.setAttribute('aria-expanded', isOrganizationPanelOpen ? 'true' : 'false');
                    organizationTrigger.addEventListener('click', (event) => {
                        event.preventDefault();
                        isOrganizationPanelOpen = !isOrganizationPanelOpen;
                        render();
                    });
                }

                if (organizationLabel) {
                    const selectedOrganizations = organizations.filter((organization) => (
                        selectedOrganizationIds.has(organization.organizationId)
                    ));
                    organizationLabel.textContent = selectedOrganizations.length === 0
                        ? 'Выберите организации'
                        : selectedOrganizations.length === 1
                            ? selectedOrganizations[0].organizationName
                            : `Выбрано: ${selectedOrganizations.length}`;
                }

                if (organizationPanel) {
                    organizationPanel.classList.toggle('is-hidden', !isOrganizationPanelOpen);
                }

                if (organizationOptions) {
                    organizations.forEach((organization) => {
                        const optionLabel = document.createElement('label');
                        const checkbox = document.createElement('input');
                        const labelText = document.createElement('span');
                        const isSelected = selectedOrganizationIds.has(organization.organizationId);

                        optionLabel.className = 'app-checkbox-option survey-period-filter__organization-option';
                        optionLabel.classList.toggle('is-selected', isSelected);
                        optionLabel.setAttribute('role', 'option');
                        optionLabel.setAttribute('aria-selected', isSelected ? 'true' : 'false');

                        checkbox.type = 'checkbox';
                        checkbox.className = 'app-checkbox-input survey-period-filter__organization-checkbox';
                        checkbox.checked = isSelected;
                        checkbox.value = organization.organizationId;
                        checkbox.addEventListener('change', (event) => {
                            toggleOrganization(organization.organizationId, event.target.checked);
                        });

                        labelText.className = 'app-checkbox-text survey-period-filter__organization-name';
                        labelText.textContent = organization.organizationName;

                        optionLabel.appendChild(checkbox);
                        optionLabel.appendChild(labelText);
                        organizationOptions.appendChild(optionLabel);
                    });
                }

                if (dateInput) {
                    dateInput.dataset.dateMin = minEndDate;
                    dateInput.min = minEndDate;
                    dateInput.value = extension.extendedUntil;
                    if (window.AppDate?.enhanceDateInputs) {
                        window.AppDate.enhanceDateInputs(row);
                    }
                    if (window.AppDate?.setInputValue) {
                        window.AppDate.setInputValue(dateInput, extension.extendedUntil);
                    } else {
                        dateInput.value = extension.extendedUntil;
                    }
                    dateInput.addEventListener('change', (event) => {
                        handleChange('extendedUntil', window.AppDate?.getInputIso(event.target) || event.target.value);
                    });
                }

                rowsContainer.appendChild(row);
            }

            if (submitButton) {
                submitButton.disabled = !isFormValid() || loading;
                submitButton.textContent = loading ? 'Обработка...' : 'Продлить доступ';
                submitButton.style.removeProperty('background-color');
                submitButton.style.cursor = isFormValid() ? 'pointer' : 'not-allowed';
                submitButton.style.opacity = isFormValid() ? '1' : '0.6';
                submitButton.onclick = handleSubmit;
            }
            if (cancelButton) {
                cancelButton.onclick = closeModal;
            }

            host.appendChild(root);
            if (isOrganizationPanelOpen) {
                scheduleCheckboxListHeightUpdate(root);
            }
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

        document.addEventListener('pointerdown', handleDocumentPointerDown, true);
        document.addEventListener('keydown', handleDocumentKeyDown);
        render();
        fetchOrganizations();

        return () => {
            disposed = true;
            document.removeEventListener('pointerdown', handleDocumentPointerDown, true);
            document.removeEventListener('keydown', handleDocumentKeyDown);
            if (externalSubmitButton) {
                externalSubmitButton.onclick = null;
                externalSubmitButton.disabled = true;
                externalSubmitButton.style.removeProperty('background-color');
                externalSubmitButton.style.removeProperty('cursor');
                externalSubmitButton.style.removeProperty('opacity');
            }
            if (externalCancelButton) {
                externalCancelButton.onclick = null;
            }
            host.replaceChildren();
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
            radar: null
        };
        const chartInstances = {
            line: null,
            bar: null,
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

            const yearGuideLinePlugin = {
                id: 'adminStatisticsYearGuideLine',
                beforeDatasetsDraw(chart, _args, options) {
                    const yScale = chart.scales.y;
                    const meta = chart.getDatasetMeta(0);
                    if (!yScale || !meta || meta.hidden) {
                        return;
                    }

                    const startY = yScale.getPixelForValue(0);
                    const color = options?.color || 'rgba(79, 70, 229, 0.25)';
                    const lineWidth = options?.lineWidth || 2;
                    chart.ctx.save();
                    chart.ctx.strokeStyle = color;
                    chart.ctx.lineWidth = lineWidth;

                    meta.data.forEach((point) => {
                        if (!point || point.skip) {
                            return;
                        }

                        chart.ctx.beginPath();
                        chart.ctx.moveTo(point.x, startY);
                        chart.ctx.lineTo(point.x, point.y);
                        chart.ctx.stroke();
                    });

                    chart.ctx.restore();
                }
            };

            const getScoreScale = () => ({
                type: 'linear',
                min: 0,
                max: 5,
                ticks: {
                    stepSize: 1
                },
                title: {
                    display: true,
                    text: 'Средняя оценка'
                }
            });

            const buildCommonOptions = (showLegend) => ({
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: Boolean(showLegend),
                        position: 'bottom',
                        labels: {
                            padding: 14,
                            boxWidth: 12,
                            font: {
                                size: 12
                            }
                        }
                    },
                    tooltip: {
                        callbacks: {
                            label(context) {
                                const value = context.parsed?.y ?? context.parsed?.x ?? context.parsed;
                                const numericValue = Number(value);
                                if (Number.isFinite(numericValue)) {
                                    return `${context.dataset.label || 'Средняя оценка'}: ${numericValue.toFixed(2)}`;
                                }

                                return context.dataset.label || '';
                            }
                        }
                    }
                },
                layout: {
                    padding: {
                        top: 10,
                        bottom: showLegend ? 20 : 10
                    }
                }
            });

            if (chartRefs.line && chartsData.lineChart) {
                const yearLabels = chartsData.lineChart.labels || [];
                const yearData = chartsData.lineChart.data || [];

                chartInstances.line = new Chart(chartRefs.line, {
                    type: 'line',
                    data: {
                        labels: yearLabels,
                        datasets: [{
                            label: chartsData.lineChart.label || 'Средняя оценка',
                            data: yearData,
                            borderColor: 'rgb(79, 70, 229)',
                            backgroundColor: 'rgb(79, 70, 229)',
                            borderWidth: 2,
                            pointRadius: 4,
                            pointHoverRadius: 6,
                            tension: 0.2
                        }]
                    },
                    options: {
                        ...buildCommonOptions(false),
                        scales: {
                            x: {
                                grid: {
                                    display: false
                                }
                            },
                            y: getScoreScale()
                        },
                        plugins: {
                            ...buildCommonOptions(false).plugins,
                            adminStatisticsYearGuideLine: {
                                color: 'rgba(79, 70, 229, 0.32)',
                                lineWidth: 2
                            }
                        }
                    },
                    plugins: [yearGuideLinePlugin]
                });
            }

            if (chartRefs.bar && chartsData.barChart) {
                chartInstances.bar = new Chart(chartRefs.bar, {
                    type: 'bar',
                    data: {
                        labels: chartsData.barChart.labels || [],
                        datasets: [{
                            label: chartsData.barChart.label || 'Средняя оценка',
                            data: chartsData.barChart.data || [],
                            backgroundColor: 'rgba(14, 165, 233, 0.72)',
                            borderColor: 'rgb(14, 165, 233)',
                            borderWidth: 1
                        }]
                    },
                    options: {
                        ...buildCommonOptions(false),
                        scales: {
                            x: {
                                grid: {
                                    display: false
                                }
                            },
                            y: getScoreScale()
                        }
                    }
                });
            }

            if (chartRefs.radar && chartsData.avgScoreByOrganizationRadar) {
                chartInstances.radar = new Chart(chartRefs.radar, {
                    type: 'bar',
                    data: {
                        labels: chartsData.avgScoreByOrganizationRadar.labels || [],
                        datasets: (chartsData.avgScoreByOrganizationRadar.datasets || []).map((dataset) => ({
                            ...dataset,
                            grouped: false,
                            borderWidth: 1,
                            barPercentage: 0.78,
                            categoryPercentage: 0.92
                        }))
                    },
                    options: {
                        ...buildCommonOptions(true),
                        scales: {
                            x: {
                                ticks: {
                                    display: false
                                },
                                grid: {
                                    display: false
                                }
                            },
                            y: getScoreScale()
                        },
                        plugins: {
                            ...buildCommonOptions(true).plugins,
                            tooltip: {
                                callbacks: {
                                    title(items) {
                                        return items[0]?.dataset?.label || '';
                                    },
                                    label(context) {
                                        const value = Number(context.parsed?.y || 0);
                                        return `Средняя оценка: ${value.toFixed(2)}`;
                                    }
                                }
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
