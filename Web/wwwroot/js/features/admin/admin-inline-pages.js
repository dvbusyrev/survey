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

        const handleSubmit = async () => {
            if (extension.organizationIds.length === 0 || !extension.extendedUntil) {
                window.AppUi?.notify?.('Пожалуйста, заполните все поля.', 'error');
                return;
            }

            if ((window.AppDate?.compare(extension.extendedUntil, today) ?? -1) <= 0) {
                window.AppUi?.notify?.('Дата конца должна быть в будущем.', 'error');
                return;
            }

            try {
                const response = await fetch('/survey-extensions', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': window.AppHttp?.getAntiforgeryToken() || ''
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
                        message: responseData.message || 'Доступ успешно продлён',
                        tabName: typeof window.resolveCurrentAdminTab === 'function'
                            ? window.resolveCurrentAdminTab()
                            : 'get_surveys',
                        fallbackUrl: window.location.pathname
                    });
                    return;
                }

                window.AppUi?.notify?.(responseData.message || 'Доступ успешно продлён', 'success');
                window.location.reload();
            } catch (submitError) {
                console.error('Ошибка продления анкеты:', submitError);
                window.AppUi?.notify?.(submitError.message || 'Не удалось продлить доступ.', 'error');
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
                errorNode.textContent = '';
                errorNode.style.display = 'none';
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

                        optionLabel.className = 'app-checkbox-option';
                        optionLabel.classList.toggle('is-selected', isSelected);
                        optionLabel.setAttribute('role', 'option');
                        optionLabel.setAttribute('aria-selected', isSelected ? 'true' : 'false');

                        checkbox.type = 'checkbox';
                        checkbox.className = 'app-checkbox-input';
                        checkbox.checked = isSelected;
                        checkbox.value = organization.organizationId;
                        checkbox.addEventListener('change', (event) => {
                            toggleOrganization(organization.organizationId, event.target.checked);
                        });

                        labelText.className = 'app-checkbox-text';
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
                submitButton.style.opacity = isFormValid() ? '1' : '0.6';
                submitButton.onclick = handleSubmit;
            }
            if (cancelButton) {
                cancelButton.onclick = closeModal;
            }

            host.appendChild(root);
            if (isOrganizationPanelOpen) {
                window.AppCheckboxDropdown?.scheduleListHeightUpdate(root);
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
                window.AppUi.notify(error, 'error', { title: 'Ошибка' });
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

            const getThemeCssValue = (name, fallback) => {
                const value = window.getComputedStyle(document.documentElement).getPropertyValue(name).trim();
                return value || fallback;
            };

            const getChartTextColor = () => getThemeCssValue(
                '--text-main',
                getThemeCssValue('--app-theme-font-color', '#343D4B')
            );
            const getChartSecondaryTextColor = () => getThemeCssValue('--text-secondary', getChartTextColor());
            const getChartGridColor = () => getThemeCssValue('--border', 'rgba(52, 61, 75, 0.12)');

            const getScoreScale = () => ({
                type: 'linear',
                min: 0,
                max: 5,
                ticks: {
                    stepSize: 1,
                    color: getChartTextColor()
                },
                title: {
                    display: true,
                    text: 'Средняя оценка',
                    color: getChartTextColor()
                },
                grid: {
                    color: getChartGridColor()
                }
            });

            const buildCommonOptions = (showLegend) => {
                const textColor = getChartTextColor();

                return {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: {
                            display: Boolean(showLegend),
                            position: 'bottom',
                            labels: {
                                padding: 14,
                                boxWidth: 12,
                                color: textColor,
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
                };
            };

            if (chartRefs.line && chartsData.lineChart) {
                const chartTextColor = getChartTextColor();
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
                                ticks: {
                                    color: chartTextColor
                                },
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
                const chartTextColor = getChartTextColor();
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
                                ticks: {
                                    color: chartTextColor
                                },
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
                const chartSecondaryTextColor = getChartSecondaryTextColor();
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
                                    display: false,
                                    color: chartSecondaryTextColor
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
                return;
            }

            if (error) {
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
                window.AppUi.notify(error, 'error', { title: 'Ошибка' });
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


})();
