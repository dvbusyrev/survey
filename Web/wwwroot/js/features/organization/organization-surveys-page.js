(function () {
    function getPageRoot() {
        return document.querySelector('.organization-surveys-page');
    }

    function getPageMessageNode() {
        return document.getElementById('organizationSurveyPageMessage');
    }

    function setPageMessage(text, isSuccess) {
        const messageNode = getPageMessageNode();
        if (!messageNode) {
            if (text && typeof window.siteNotify === 'function') {
                window.siteNotify(text, isSuccess ? 'success' : 'error');
            }
            return;
        }

        messageNode.textContent = text || '';
        messageNode.classList.toggle('organization-surveys-page__message--visible', Boolean(text));
        messageNode.classList.toggle('organization-surveys-page__message--success', Boolean(text) && isSuccess);
        messageNode.classList.toggle('organization-surveys-page__message--error', Boolean(text) && !isSuccess);
    }

    function clearPageMessage() {
        setPageMessage('', false);
    }

    function getRequestVerificationToken() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    }

    function getOrganizationGroup(element) {
        return element?.closest('[data-organization-group]') || null;
    }

    function getSelectAllOrganizationsCheckbox() {
        return document.querySelector('[data-select-all-organizations]');
    }

    function getOrganizationCheckboxes(pageRoot) {
        return Array.from(pageRoot?.querySelectorAll('[data-organization-checkbox]') || []);
    }

    function getSurveyCheckboxes(group) {
        return Array.from(group?.querySelectorAll('[data-survey-checkbox]') || []);
    }

    function updateSelectAllOrganizationsCheckboxState() {
        const pageRoot = getPageRoot();
        const selectAllCheckbox = getSelectAllOrganizationsCheckbox();
        if (!pageRoot || !selectAllCheckbox) {
            return;
        }

        const organizationCheckboxes = getOrganizationCheckboxes(pageRoot)
            .filter((checkbox) => !checkbox.disabled);
        const checkedCount = organizationCheckboxes.filter((checkbox) => checkbox.checked).length;

        selectAllCheckbox.disabled = organizationCheckboxes.length === 0;
        selectAllCheckbox.indeterminate = checkedCount > 0 && checkedCount < organizationCheckboxes.length;
        selectAllCheckbox.checked = organizationCheckboxes.length > 0 && checkedCount === organizationCheckboxes.length;
    }

    function updateOrganizationCheckboxState(group) {
        const organizationCheckbox = group?.querySelector('[data-organization-checkbox]');
        if (!organizationCheckbox) {
            return;
        }

        const surveyCheckboxes = getSurveyCheckboxes(group);
        const checkedCount = surveyCheckboxes.filter((checkbox) => checkbox.checked).length;

        organizationCheckbox.indeterminate = checkedCount > 0 && checkedCount < surveyCheckboxes.length;
        organizationCheckbox.checked = surveyCheckboxes.length > 0 && checkedCount === surveyCheckboxes.length;
    }

    function updateAllOrganizationCheckboxes() {
        const pageRoot = getPageRoot();
        if (!pageRoot) {
            return;
        }

        pageRoot.querySelectorAll('[data-organization-group]').forEach((group) => {
            updateOrganizationCheckboxState(group);
        });

        updateSelectAllOrganizationsCheckboxState();
    }

    function toggleOrganizationSurveys(group, isChecked) {
        getSurveyCheckboxes(group).forEach((checkbox) => {
            checkbox.checked = isChecked;
        });

        updateOrganizationCheckboxState(group);
    }

    function toggleAllOrganizations(isChecked) {
        const pageRoot = getPageRoot();
        if (!pageRoot) {
            return;
        }

        getOrganizationCheckboxes(pageRoot)
            .filter((checkbox) => !checkbox.disabled)
            .forEach((checkbox) => {
                checkbox.checked = isChecked;
                const group = getOrganizationGroup(checkbox);
                if (group) {
                    toggleOrganizationSurveys(group, isChecked);
                }
            });

        updateAllOrganizationCheckboxes();
    }

    function getSelectedSurveyRows() {
        const pageRoot = getPageRoot();
        if (!pageRoot) {
            return [];
        }

        return Array.from(pageRoot.querySelectorAll('[data-survey-row]')).filter((row) => {
            return row.querySelector('[data-survey-checkbox]')?.checked;
        });
    }

    function clearSelections() {
        getSelectedSurveyRows().forEach((row) => {
            const checkbox = row.querySelector('[data-survey-checkbox]');
            if (checkbox) {
                checkbox.checked = false;
            }
        });

        updateAllOrganizationCheckboxes();
    }

    function collectAssignments() {
        return getSelectedSurveyRows().map((row) => ({
            organizationId: Number(row.dataset.organizationId || 0),
            surveyId: Number(row.dataset.surveyId || 0)
        })).filter((item) => Number.isInteger(item.organizationId) && item.organizationId > 0
            && Number.isInteger(item.surveyId) && item.surveyId > 0);
    }

    function buildErrorMessage(result, fallbackMessage) {
        const errors = Array.isArray(result?.errors) ? result.errors.filter(Boolean) : [];
        if (errors.length > 0) {
            return errors.join(' ');
        }

        return result?.message || result?.error || fallbackMessage;
    }

    async function readJsonResponse(response) {
        const responseText = await response.text();
        if (!responseText) {
            return null;
        }

        try {
            return JSON.parse(responseText);
        } catch (error) {
            return {
                success: response.ok,
                message: responseText
            };
        }
    }

    function applyUpdatedAssignments(updatedAssignments) {
        (updatedAssignments || []).forEach((item) => {
            const selector = `[data-survey-row][data-organization-id="${item.organizationId}"][data-survey-id="${item.surveyId}"]`;
            const row = document.querySelector(selector);
            if (!row) {
                return;
            }

            const dateNode = row.querySelector('[data-role="end-date"]');
            const remainingNode = row.querySelector('[data-role="remaining"]');

            if (dateNode) {
                dateNode.textContent = `Дата конца: ${item.effectiveEndDateDisplay || ''}`;
            }

            if (remainingNode) {
                remainingNode.textContent = item.remainingText || '';
            }

            if (item.effectiveEndDateIso) {
                row.dataset.effectiveEndDate = item.effectiveEndDateIso;
            }

            row.classList.toggle('organization-surveys-page__survey-row--expired', Boolean(item.isExpired));
        });
    }

    async function saveOrganizationSurveyEndDates() {
        const pageRoot = getPageRoot();
        if (!pageRoot) {
            return false;
        }

        const dateInput = document.getElementById('organizationSurveyDateEnd');
        const saveButton = document.getElementById('organizationSurveySaveButton');
        const assignments = collectAssignments();
        const dateEnd = window.AppDate?.getInputIso(dateInput) || '';

        if (!dateInput?.value || !dateEnd) {
            setPageMessage('Укажите новую дату конца.', false);
            return false;
        }

        if (assignments.length === 0) {
            setPageMessage('Выберите хотя бы одну анкету организации.', false);
            return false;
        }

        clearPageMessage();

        if (saveButton) {
            saveButton.disabled = true;
        }

        try {
            const response = await fetch('/organizations/surveys/end-date', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    ...(getRequestVerificationToken()
                        ? { RequestVerificationToken: getRequestVerificationToken() }
                        : {})
                },
                body: JSON.stringify({
                    dateEnd,
                    assignments
                })
            });

            const result = await readJsonResponse(response);
            if (!response.ok || !result?.success) {
                throw new Error(buildErrorMessage(result, 'Не удалось обновить дату конца анкет.'));
            }

            applyUpdatedAssignments(result.updatedAssignments);
            clearSelections();
            setPageMessage(result.message || 'Дата конца для выбранных анкет обновлена.', true);
            return true;
        } catch (error) {
            console.error('Ошибка при обновлении даты конца анкет:', error);
            setPageMessage(error.message || 'Не удалось обновить дату конца анкет.', false);
            return false;
        } finally {
            if (saveButton) {
                saveButton.disabled = false;
            }
        }
    }

    if (!window.__organizationSurveyPageHandlersBound) {
        window.__organizationSurveyPageHandlersBound = true;

        document.addEventListener('change', (event) => {
            const selectAllOrganizationsCheckbox = event.target.closest('[data-select-all-organizations]');
            if (selectAllOrganizationsCheckbox) {
                toggleAllOrganizations(selectAllOrganizationsCheckbox.checked);
                clearPageMessage();
                return;
            }

            const organizationCheckbox = event.target.closest('[data-organization-checkbox]');
            if (organizationCheckbox) {
                const group = getOrganizationGroup(organizationCheckbox);
                if (group) {
                    toggleOrganizationSurveys(group, organizationCheckbox.checked);
                }
                updateSelectAllOrganizationsCheckboxState();
                clearPageMessage();
                return;
            }

            const surveyCheckbox = event.target.closest('[data-survey-checkbox]');
            if (surveyCheckbox) {
                const group = getOrganizationGroup(surveyCheckbox);
                if (group) {
                    updateOrganizationCheckboxState(group);
                }
                updateSelectAllOrganizationsCheckboxState();
                clearPageMessage();
                return;
            }

            if (event.target.id === 'organizationSurveyDateEnd') {
                clearPageMessage();
            }
        });
    }

    updateAllOrganizationCheckboxes();

    window.saveOrganizationSurveyEndDates = saveOrganizationSurveyEndDates;
})();
