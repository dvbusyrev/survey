(() => {
    const surveyExtensionModal = window.AdminSurveyExtensionModal || (window.AdminSurveyExtensionModal = {});

    function createPageScope() {
        const externalScope = window.AppPageLifecycle?.createScope?.();
        if (externalScope) {
            return externalScope;
        }

        const disposers = [];
        return {
            listen(target, type, handler, options) {
                target?.addEventListener?.(type, handler, options);
                disposers.push(() => target?.removeEventListener?.(type, handler, options));
            },
            dispose() {
                while (disposers.length > 0) {
                    disposers.pop()?.();
                }
            }
        };
    }

    surveyExtensionModal.mount = function mountExtensionModal(host, options = {}) {
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
        let organizationDropdownController = null;
        let renderScope = null;
        let organizationsRequest = null;
        const today = window.AppDate?.todayIso?.() || new Date().toISOString().split('T')[0];

        const addOneDay = (isoDate) => {
            const date = window.AppDate?.parseDate?.(isoDate);
            if (!date) {
                return '';
            }

            date.setDate(date.getDate() + 1);
            return window.AppDate?.toIso?.(date) || '';
        };

        const getMinimumEndDate = () => {
            const selectedIds = new Set(extension.organizationIds);
            const currentEndDates = [survey?.date_end]
                .concat(
                    organizations
                        .filter((organization) => selectedIds.has(organization.organizationId))
                        .flatMap((organization) => [organization.dateEnd, organization.surveyDateEnd])
                )
                .filter(Boolean);
            const latestCurrentEndDate = currentEndDates.reduce((latest, candidate) => (
                (window.AppDate?.compare?.(candidate, latest) ?? -1) > 0 ? candidate : latest
            ), '');
            const firstDateAfterCurrentEnd = addOneDay(latestCurrentEndDate);

            return [today, firstDateAfterCurrentEnd]
                .filter(Boolean)
                .reduce((latest, candidate) => (
                    (window.AppDate?.compare?.(candidate, latest) ?? -1) > 0 ? candidate : latest
                ), today);
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

        const handleSubmit = async () => {
            const organizationField = host.querySelector('.admin-extension-selected-organizations');
            const dateInput = host.querySelector('[data-role="date-input"]');
            const errors = [];

            if (extension.organizationIds.length === 0) {
                const message = 'Выберите хотя бы одну организацию.';
                window.AppValidation?.setFieldError?.(organizationField, message);
                errors.push(message);
            } else {
                window.AppValidation?.clearFieldError?.(organizationField);
            }

            if (!extension.extendedUntil) {
                const message = 'Укажите дату конца.';
                window.AppValidation?.setFieldError?.(dateInput, message);
                errors.push(message);
            } else {
                window.AppValidation?.clearFieldError?.(dateInput);
            }

            if (errors.length > 0) {
                window.AppValidation?.notifyErrors?.(errors);
                return;
            }

            const minimumEndDate = getMinimumEndDate();
            if ((window.AppDate?.compare(extension.extendedUntil, minimumEndDate) ?? -1) < 0) {
                const message = 'Новая дата конца должна быть позже текущей даты конца назначения.';
                window.AppValidation?.setFieldError?.(dateInput, message);
                window.AppValidation?.notifyErrors?.([message]);
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
                            ? window.getResponseErrorMessage(response, 'Не удалось продлить доступ')
                            : `Не удалось продлить доступ. Сервер вернул ошибку (${response.status}).`)
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

                window.AppUi?.notify?.(responseData.message || 'Доступ успешно продлён.', 'success');
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
            renderScope?.dispose?.();
            renderScope = createPageScope();
            organizationDropdownController?.destroy?.();
            organizationDropdownController = null;
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
                surveyName.textContent = survey?.name_survey || '';
            }
            if (errorNode) {
                errorNode.textContent = error;
                errorNode.classList.toggle('is-hidden', !error);
            }

            if (rowsContainer) {
                rowsContainer.classList.remove('is-hidden');
            }
            if (emptyState) {
                emptyState.classList.toggle('is-hidden', loading || Boolean(error) || organizations.length > 0);
            }

            if (rowsContainer) {
                const row = rowTemplate.content.firstElementChild.cloneNode(true);
                const organizationDropdown = row.querySelector('[data-role="organization-dropdown"]');
                const organizationTrigger = row.querySelector('[data-role="organization-trigger"]');
                const organizationSelection = row.querySelector('[data-role="organization-selection"]');
                const organizationClearButton = row.querySelector('[data-role="organization-selection-clear"]');
                const organizationField = row.querySelector('.admin-extension-selected-organizations');
                const organizationPanel = row.querySelector('[data-role="organization-panel"]');
                const organizationOptions = row.querySelector('[data-role="organization-options"]');
                const dateInput = row.querySelector('[data-role="date-input"]');
                const selectedOrganizationIds = new Set(extension.organizationIds);
                const minimumEndDate = getMinimumEndDate();
                const fieldsDisabled = loading || Boolean(error) || organizations.length === 0;

                if (organizationField) {
                    organizationField.dataset.value = extension.organizationIds.join(',');
                }

                if (organizationSelection) {
                    const selectedOrganizations = organizations.filter((organization) => (
                        selectedOrganizationIds.has(organization.organizationId)
                    ));
                    organizationSelection.replaceChildren();

                    if (selectedOrganizations.length === 0) {
                        organizationSelection.appendChild(window.AppUi.createElement('p', {
                            className: 'app-field-placeholder survey-editor-page__empty-selection',
                            text: 'Организации не выбраны'
                        }));
                    } else {
                        selectedOrganizations.forEach((organization) => {
                            organizationSelection.appendChild(window.AppUi.createElement('span', {
                                className: 'app-chip',
                                text: organization.organizationName
                            }));
                        });
                    }
                }

                if (organizationClearButton) {
                    organizationClearButton.classList.toggle('is-hidden', extension.organizationIds.length === 0);
                    renderScope.listen(organizationClearButton, 'click', () => {
                        handleChange('organizationIds', []);
                    });
                }

                if (organizationTrigger) {
                    organizationTrigger.disabled = fieldsDisabled;
                    organizationTrigger.textContent = loading
                        ? 'Загрузка организаций...'
                        : organizations.length > 0
                            ? 'Выбрать организации'
                            : 'Организации недоступны';
                }

                if (organizationOptions) {
                    organizations.forEach((organization) => {
                        const isSelected = selectedOrganizationIds.has(organization.organizationId);
                        const checkboxOption = window.AppUi.createCheckboxOption({
                            text: organization.organizationName,
                            checked: isSelected,
                            selected: isSelected
                        });
                        const optionLabel = checkboxOption.option;
                        const checkbox = checkboxOption.checkbox;

                        optionLabel.setAttribute('role', 'option');
                        optionLabel.setAttribute('aria-selected', isSelected ? 'true' : 'false');

                        checkbox.value = organization.organizationId;
                        renderScope.listen(checkbox, 'change', (event) => {
                            toggleOrganization(organization.organizationId, event.target.checked);
                        });

                        organizationOptions.appendChild(optionLabel);
                    });
                }

                if (organizationDropdown && organizationTrigger && organizationPanel && typeof window.AppUi?.createMultiselect === 'function') {
                    organizationDropdownController = window.AppUi.createMultiselect({
                        root: organizationDropdown,
                        trigger: organizationTrigger,
                        menu: organizationPanel,
                        openClass: 'is-open',
                        hiddenClass: 'is-hidden',
                        onOpen: () => {
                            isOrganizationPanelOpen = true;
                            window.AppCheckboxDropdown?.scheduleListHeightUpdate(organizationPanel);
                        },
                        onClose: () => {
                            isOrganizationPanelOpen = false;
                        }
                    });

                    if (isOrganizationPanelOpen) {
                        organizationDropdownController.controller?.open();
                    } else {
                        organizationDropdownController.controller?.close();
                    }
                }

                if (dateInput) {
                    dateInput.disabled = fieldsDisabled;
                    dateInput.dataset.dateMin = minimumEndDate;
                    dateInput.min = minimumEndDate;
                    dateInput.value = extension.extendedUntil;
                    if (window.AppDate?.enhanceDateInputs) {
                        window.AppDate.enhanceDateInputs(row);
                    }
                    if (window.AppDate?.setInputValue) {
                        window.AppDate.setInputValue(dateInput, extension.extendedUntil);
                    } else {
                        dateInput.value = extension.extendedUntil;
                    }
                    renderScope.listen(dateInput, 'change', (event) => {
                        handleChange('extendedUntil', window.AppDate?.getInputIso(event.target) || event.target.value);
                    });
                }

                rowsContainer.appendChild(row);
            }

            if (submitButton) {
                submitButton.disabled = loading || Boolean(error) || organizations.length === 0;
                submitButton.textContent = loading ? 'Обработка...' : 'Продлить доступ';
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
            organizationsRequest?.abort?.();
            const request = new AbortController();
            organizationsRequest = request;

            try {
                loading = true;
                render();
                const response = await fetch(`/survey/${survey?.id_survey}/assigned-organizations`, {
                    signal: request.signal
                });
                if (!response.ok) {
                    throw new Error(
                        window.getResponseErrorMessage
                            ? window.getResponseErrorMessage(response, 'Не удалось загрузить назначенные организации')
                            : `Не удалось загрузить назначенные организации. Сервер вернул ошибку (${response.status}).`
                    );
                }

                const data = await response.json();
                organizations = Array.isArray(data)
                    ? data
                        .filter((org) => org && (org.id_organization !== undefined || org.id !== undefined))
                        .map((org) => ({
                            organizationId: String(org.id_organization ?? org.id),
                            organizationName: String(org.organization_name ?? org.name ?? ''),
                            dateEnd: String(org.date_end ?? org.dateEnd ?? ''),
                            surveyDateEnd: String(org.survey_date_end ?? org.surveyDateEnd ?? '')
                        }))
                        .filter((org) => org.organizationName)
                    : [];
                error = '';
            } catch (fetchError) {
                if (disposed || request.signal.aborted) {
                    return;
                }
                console.error('Ошибка загрузки организаций:', fetchError);
                error = fetchError.message || 'Не удалось загрузить назначенные организации.';
                window.AppUi.notify(error, 'error', { title: 'Ошибка' });
            } finally {
                if (disposed || request.signal.aborted) {
                    return;
                }
                loading = false;
                render();
            }
        };

        render();
        fetchOrganizations();

        return () => {
            disposed = true;
            organizationsRequest?.abort?.();
            renderScope?.dispose?.();
            organizationDropdownController?.destroy?.();
            organizationDropdownController = null;
            if (externalSubmitButton) {
                externalSubmitButton.onclick = null;
                externalSubmitButton.disabled = true;
            }
            if (externalCancelButton) {
                externalCancelButton.onclick = null;
            }
            host.replaceChildren();
        };
    };

})();
