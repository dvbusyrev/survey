(function () {
    window.surveyEditSelectedOrganization = window.surveyEditSelectedOrganization || [];
    window.surveyEditModalOpen = false;

    function resolveModal(target) {
        return typeof target === 'string' ? document.getElementById(target) : target;
    }

    function safeGetElement(id) {
        const element = document.getElementById(id);
        if (!element) console.error('Элемент не найден:', id);
        return element;
    }

    function safeGetValue(id) {
        return safeGetElement(id)?.value.trim() || '';
    }

    function getElementByRole(role) {
        return document.querySelector(`[data-role="${role}"]`);
    }

    function closeModal(modalId) {
        const modal = resolveModal(modalId);
        if (modal) window.AppUi.setModalVisibility(modal, false);
    }

    function setModalVisible(target, isVisible) {
        const modal = resolveModal(target);
        return modal ? window.AppUi.setModalVisibility(modal, isVisible) : false;
    }

    function showTimedNotification(type, title, message) {
        window.AppUi.notify(message, type, { title, duration: type === 'error' ? 0 : 3000 });
    }

    function showSuccess(title, message) {
        showTimedNotification('success', title, message);
    }

    function showError(title, message) {
        showTimedNotification(
            'error',
            title,
            window.normalizeClientErrorMessage?.(message) || message
        );
    }

    const organizations = window.createSurveyOrganizationController({
        safeGetElement,
        getElementByRole,
        showError
    });
    const criteria = window.createSurveyCriteriaController({ getElementByRole, showError });
    organizations.bindDismissal();

    function setSubmitButtonLabel(label) {
        const submitButton = getElementByRole('survey-submit');
        if (submitButton) submitButton.textContent = label;
    }

    function resetSurveyCreateForm() {
        setSubmitButtonLabel('Сохранить');
        organizations.setSelected([]);
        organizations.resetAvailable();
        ['surveyTitle', 'surveyDescription', 'startDate', 'endDate'].forEach((id) => {
            const field = safeGetElement(id);
            if (field) {
                field.value = '';
                field.classList.remove('invalid');
            }
        });
        getElementByRole('criteria-step')?.classList.remove('confirmed-criteria');
        const organizationField = getElementByRole('selected-organizations-container');
        organizationField?.setAttribute('aria-invalid', 'false');
        organizationField?.classList.remove('invalid');
        criteria.replace(['']);
        organizations.updateDisplay();
        organizations.close();
        setModalVisible('loadingOverlay', false);
    }

    function normalizeCopyTemplate(rawTemplate) {
        return {
            title: String(rawTemplate?.title || '').trim(),
            description: String(rawTemplate?.description || '').trim(),
            startDate: String(rawTemplate?.startDate || '').trim(),
            endDate: String(rawTemplate?.endDate || '').trim(),
            organizations: window.SurveyAdminFormState.cloneOrganizations(rawTemplate?.organizations),
            criteria: Array.isArray(rawTemplate?.criteria)
                ? rawTemplate.criteria.map((item) => String(item || '').trim()).filter(Boolean)
                : []
        };
    }

    function prefillSurveyCreateForm(rawTemplate) {
        const template = normalizeCopyTemplate(rawTemplate);
        resetSurveyCreateForm();
        setSubmitButtonLabel('Копировать');
        const title = safeGetElement('surveyTitle');
        const description = safeGetElement('surveyDescription');
        if (title) title.value = template.title;
        if (description) description.value = template.description;
        window.AppDate?.setInputValue?.('startDate', template.startDate);
        window.AppDate?.setInputValue?.('endDate', template.endDate);
        criteria.replace(template.criteria);
        organizations.setSelected(template.organizations);
        organizations.updateDisplay();
        organizations.syncList();
    }

    function validateForm() {
        let isValid = true;
        ['surveyTitle', 'startDate', 'endDate'].forEach((id) => {
            const field = safeGetElement(id);
            if (!field?.value.trim()) {
                field?.classList.add('invalid');
                isValid = false;
            } else {
                field.classList.remove('invalid');
            }
        });
        safeGetElement('surveyDescription')?.classList.remove('invalid');
        const startDate = window.AppDate?.getInputIso('startDate') || '';
        const endDate = window.AppDate?.getInputIso('endDate') || '';
        if ((safeGetValue('startDate') && !startDate) || (safeGetValue('endDate') && !endDate)) {
            showError('Ошибка', 'Используйте формат даты ДД.ММ.ГГГГ.');
            isValid = false;
        } else if (endDate && window.AppDate?.compare(endDate, window.AppDate.todayIso()) < 0) {
            safeGetElement('endDate')?.classList.add('invalid');
            showError('Ошибка', 'Дата конца не может быть раньше сегодняшней даты.');
            isValid = false;
        } else if (startDate && endDate && window.AppDate?.compare(endDate, startDate) <= 0) {
            safeGetElement('endDate')?.classList.add('invalid');
            showError('Ошибка', 'Дата конца должна быть позже даты начала.');
            isValid = false;
        }
        if (organizations.getSelected().length === 0) {
            const organizationField = getElementByRole('selected-organizations-container');
            organizationField?.setAttribute('aria-invalid', 'true');
            organizationField?.classList.add('invalid');
            showError('Ошибка', 'Выберите хотя бы одну организацию.');
            isValid = false;
        } else {
            const organizationField = getElementByRole('selected-organizations-container');
            organizationField?.setAttribute('aria-invalid', 'false');
            organizationField?.classList.remove('invalid');
        }
        return criteria.validate() && isValid;
    }

    function addSurvey() {
        if (!validateForm()) return;
        const loading = safeGetElement('loadingOverlay');
        if (loading) setModalVisible(loading, true);
        fetch('/survey/create', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                RequestVerificationToken: window.AppHttp?.getAntiforgeryToken() || ''
            },
            body: JSON.stringify({
                Title: safeGetValue('surveyTitle'),
                Description: safeGetValue('surveyDescription'),
                StartDate: window.AppDate?.getInputIso('startDate') || '',
                EndDate: window.AppDate?.getInputIso('endDate') || '',
                Organizations: organizations.getSelected().map((organization) => organization.id),
                Criteria: criteria.values()
            })
        })
            .then((response) => response.ok
                ? response.json()
                : response.json().then((error) => Promise.reject(new Error(error.message || 'Ошибка сервера.'))))
            .then((result) => {
                if (!result.success) throw new Error(result.message || 'Не удалось создать анкету.');
                if (typeof window.handleSurveyCreateSuccess === 'function') {
                    window.handleSurveyCreateSuccess(result);
                } else if (typeof window.handleAdminMutationSuccess === 'function') {
                    window.handleAdminMutationSuccess({ message: result.message || 'Анкета успешно создана', tabName: 'get_surveys', fallbackUrl: '/survey' });
                } else {
                    showSuccess('Успех', 'Анкета успешно создана');
                    window.setTimeout(() => window.location.reload(), 2000);
                }
            })
            .catch((error) => {
                console.error('Ошибка создания анкеты:', error);
                showError('Ошибка', error.message);
            })
            .finally(() => {
                if (loading) setModalVisible(loading, false);
            });
    }

    function surveyEditInit() {
        const selectedIds = (document.getElementById('selectedOrganizationIds')?.value || '').split(',');
        const selectedNames = window.selectedOrganizationNames || window.__adminBootstrap?.selectedOrganizationNames || [];
        const selected = selectedIds.reduce((items, rawId, index) => {
            const id = Number.parseInt(rawId, 10);
            if (!Number.isFinite(id)) return items;
            const node = document.querySelector(`#organizationList [data-role="organization-option"][data-id="${id}"]`);
            const name = selectedNames[index] || organizations.getItemName(node);
            if (name) items.push({ id, name });
            return items;
        }, []);
        if (selected.length === 0) {
            document.querySelectorAll('#organizationList [data-role="organization-option"][data-selected="true"]').forEach((node) => {
                const id = Number.parseInt(node.dataset.id || '', 10);
                const name = organizations.getItemName(node);
                if (Number.isFinite(id) && name) selected.push({ id, name });
            });
        }
        organizations.setSelected(selected);
        organizations.syncList();
        organizations.updateDisplay();
    }

    function surveyEditCloseModal(modalId) {
        if (modalId === 'organizationModal') {
            organizations.close();
            return;
        }
        closeModal(modalId);
        window.surveyEditModalOpen = false;
    }

    Object.assign(window, {
        openOrganizationModal: organizations.open,
        closeOrganizationDropdown: organizations.close,
        closeModal,
        loadOrganizations: organizations.load,
        toggleOrganizationSelection: organizations.toggle,
        saveSelectedOrganization: organizations.save,
        updateSelectedOrganizationDisplay: organizations.updateDisplay,
        removeSelectedOrganization: organizations.remove,
        getSelectedOrganizations: organizations.getSelected,
        setSelectedOrganizations: organizations.setSelected,
        syncOrganizationListSelectionFromState: organizations.syncList,
        appendSurveyCriteriaField: criteria.append,
        removeSurveyCriterion: criteria.remove,
        validateSurveyCriteriaFields: criteria.validate,
        addRowCriteriy: () => criteria.append(''),
        showSuccess,
        showError,
        addSurvey,
        validateForm,
        resetSurveyCreateForm,
        prefillSurveyCreateForm,
        surveyEditInit,
        surveyEditOpenOrganizationModal: () => {
            organizations.syncList();
            organizations.open();
        },
        surveyEditCloseModal,
        surveyEditCloseOrganizationDropdown: organizations.close,
        safeGetElement,
        safeGetValue
    });
})();
