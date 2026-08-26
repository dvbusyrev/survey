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

    function getSurveyEditorContext() {
        const modal = document.getElementById('surveyEditorModal');
        const entityKind = modal?.dataset?.entityKind || 'survey';
        const isPlannedTemplate = entityKind === 'planned-template';
        const isTemplate = entityKind === 'template' || isPlannedTemplate;
        return {
            isTemplate,
            isPlannedTemplate,
            createUrl: modal?.dataset?.createUrl || '/survey/create',
            listTab: isPlannedTemplate ? 'planned_survey_templates' : isTemplate ? 'survey_templates' : 'get_surveys',
            listUrl: isPlannedTemplate ? '/survey-templates/planned' : isTemplate ? '/survey-templates' : '/survey',
            entityName: isPlannedTemplate ? 'плановый шаблон' : isTemplate ? 'шаблон' : 'анкету'
        };
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
    let originalEditModal = null;
    let originalEditOrganizations = [];
    let originalEditCriteria = [];
    let templateDropdownController = null;
    let autoCreationDropdownController = null;
    let templateOptions = null;
    let selectedTemplateId = null;

    function getTemplatePickerElement(role) {
        return document.querySelector(`[data-role="${role}"]`);
    }

    function closeTemplatePicker() {
        templateDropdownController?.controller?.close?.();
    }

    function resetTemplatePicker() {
        closeTemplatePicker();
        selectedTemplateId = null;
        const ancestorField = document.getElementById('plannedTemplateAncestorId');
        if (ancestorField) {
            ancestorField.value = '';
            ancestorField.dataset.dateEnd = '';
        }
        const label = getTemplatePickerElement('survey-template-dropdown-label');
        if (label) label.textContent = 'Не выбран';
        ensureTemplatePickerController();
    }

    function setParentTemplateSelection(templateId, templateName, templateDateEnd = '') {
        const id = Number.parseInt(String(templateId || ''), 10);
        selectedTemplateId = Number.isFinite(id) && id > 0 ? id : null;
        const ancestorField = document.getElementById('plannedTemplateAncestorId');
        const label = getTemplatePickerElement('survey-template-dropdown-label');
        if (ancestorField) {
            ancestorField.value = selectedTemplateId ? String(selectedTemplateId) : '';
            ancestorField.dataset.dateEnd = selectedTemplateId ? String(templateDateEnd || '').trim() : '';
        }
        if (label) label.textContent = selectedTemplateId ? String(templateName || '').trim() : 'Не выбран';
        configureEditorDateBounds();
    }

    function getAutoCreationPickerElement(role) {
        return document.querySelector(`[data-role="${role}"]`);
    }

    function setSurveyAutoCreationEnabled(value) {
        const normalizedValue = String(value) === 'true';
        const field = document.getElementById('surveyAutoCreationEnabled');
        const label = getAutoCreationPickerElement('survey-auto-creation-enabled-label');
        if (field) field.value = normalizedValue ? 'true' : 'false';
        if (label) label.textContent = normalizedValue ? 'Да' : 'Нет';

        getAutoCreationPickerElement('survey-auto-creation-enabled-menu')
            ?.querySelectorAll('[role="option"]')
            .forEach((option) => {
                const isSelected = option.dataset.value === (normalizedValue ? 'true' : 'false');
                option.classList.toggle('selected', isSelected);
                option.setAttribute('aria-selected', isSelected ? 'true' : 'false');
            });
    }

    function selectSurveyAutoCreationEnabled(element) {
        setSurveyAutoCreationEnabled(element?.dataset?.value);
        autoCreationDropdownController?.controller?.close?.();
    }

    function ensureAutoCreationPickerController() {
        const root = getAutoCreationPickerElement('survey-auto-creation-enabled-dropdown');
        const trigger = getAutoCreationPickerElement('survey-auto-creation-enabled-trigger');
        const menu = getAutoCreationPickerElement('survey-auto-creation-enabled-menu');
        if (!root || !trigger || !menu || typeof window.AppUi?.createMultiselect !== 'function') {
            return null;
        }
        if (autoCreationDropdownController?.root === root) {
            return autoCreationDropdownController;
        }

        autoCreationDropdownController?.destroy?.();
        autoCreationDropdownController = window.AppUi.createMultiselect({
            root,
            trigger,
            menu,
            openClass: 'is-open',
            hiddenClass: 'is-hidden'
        });
        return autoCreationDropdownController;
    }

    async function parseTemplateResponse(response, fallbackMessage) {
        const responseText = await response.text();
        let payload = null;
        try {
            payload = responseText ? JSON.parse(responseText) : null;
        } catch (error) {
            payload = null;
        }
        if (!response.ok) {
            throw new Error(payload?.message || fallbackMessage);
        }
        return payload;
    }

    async function selectSurveyTemplate(templateId, templateName) {
        const id = Number.parseInt(String(templateId || ''), 10);
        if (!Number.isFinite(id) || id <= 0) return;

        try {
            const response = await fetch(`/survey-templates/${id}/copy-template`, {
                headers: { Accept: 'application/json' }
            });
            const template = await parseTemplateResponse(response, 'Не удалось загрузить выбранный шаблон.');
            const context = getSurveyEditorContext();
            prefillSurveyCreateForm(template, {
                preserveDates: true,
                fillEmptyOnly: context.isPlannedTemplate,
                submitLabel: 'Сохранить',
                ancestorId: context.isPlannedTemplate ? id : null,
                ancestorName: context.isPlannedTemplate ? templateName : '',
                ancestorDateEnd: context.isPlannedTemplate ? template.endDate : ''
            });
            closeTemplatePicker();
        } catch (error) {
            console.error('Ошибка загрузки шаблона анкеты:', error);
            showError('Ошибка', error.message || 'Не удалось загрузить выбранный шаблон.');
        }
    }

    function renderTemplateOptions() {
        const list = getTemplatePickerElement('survey-template-options');
        if (!list) return;
        list.replaceChildren();

        if (getSurveyEditorContext().isPlannedTemplate) {
            const emptyOption = window.AppUi.createElement('button', {
                type: 'button',
                className: 'app-checkbox-option survey-editor-page__template-option',
                text: 'Не выбран',
                attrs: { role: 'option', 'aria-selected': selectedTemplateId ? 'false' : 'true' }
            });
            emptyOption.classList.toggle('selected', !selectedTemplateId);
            emptyOption.addEventListener('click', () => {
                setParentTemplateSelection(null, '');
                closeTemplatePicker();
            });
            list.appendChild(emptyOption);
        }

        if (!Array.isArray(templateOptions) || templateOptions.length === 0) {
            list.appendChild(window.AppUi.createElement('p', {
                className: 'app-checkbox-empty',
                text: 'Шаблоны не найдены.'
            }));
            return;
        }

        templateOptions.forEach((template) => {
            const id = Number.parseInt(String(template?.id || ''), 10);
            const name = String(template?.name || '').trim();
            if (!Number.isFinite(id) || id <= 0 || !name) return;

            const option = window.AppUi.createElement('button', {
                type: 'button',
                className: 'app-checkbox-option survey-editor-page__template-option',
                text: name,
                dataset: {
                    role: 'survey-template-option',
                    templateId: String(id)
                },
                attrs: {
                    role: 'option',
                    'aria-selected': id === selectedTemplateId ? 'true' : 'false'
                }
            });
            option.classList.toggle('selected', id === selectedTemplateId);
            option.addEventListener('click', () => selectSurveyTemplate(id, name));
            list.appendChild(option);
        });
        window.AppCheckboxDropdown?.scheduleListHeightUpdate(
            getTemplatePickerElement('survey-template-dropdown-menu')
        );
    }

    async function loadTemplateOptions() {
        const loading = getTemplatePickerElement('survey-template-loading');
        const list = getTemplatePickerElement('survey-template-options');
        loading?.classList.remove('u-hidden');
        list?.classList.add('u-hidden');
        try {
            const response = await fetch('/survey-templates/options', {
                headers: { Accept: 'application/json' }
            });
            const payload = await parseTemplateResponse(response, 'Не удалось загрузить список шаблонов.');
            if (!Array.isArray(payload)) {
                throw new Error('Получены некорректные данные шаблонов.');
            }
            templateOptions = payload;
            renderTemplateOptions();
        } catch (error) {
            console.error('Ошибка загрузки списка шаблонов:', error);
            showError('Ошибка', error.message || 'Не удалось загрузить список шаблонов.');
        } finally {
            loading?.classList.add('u-hidden');
            list?.classList.remove('u-hidden');
        }
    }

    function ensureTemplatePickerController() {
        const root = getTemplatePickerElement('survey-template-dropdown');
        const trigger = getTemplatePickerElement('survey-template-dropdown-trigger');
        const menu = getTemplatePickerElement('survey-template-dropdown-menu');
        if (!root || !trigger || !menu || typeof window.AppUi?.createMultiselect !== 'function') {
            return null;
        }
        if (getSurveyEditorContext().isPlannedTemplate && selectedTemplateId === null) {
            const initialAncestorId = Number.parseInt(document.getElementById('plannedTemplateAncestorId')?.value || '', 10);
            selectedTemplateId = Number.isFinite(initialAncestorId) && initialAncestorId > 0 ? initialAncestorId : null;
        }
        if (templateDropdownController?.root === root) {
            return templateDropdownController;
        }

        templateDropdownController?.destroy?.();
        templateDropdownController = window.AppUi.createMultiselect({
            root,
            trigger,
            menu,
            openClass: 'is-open',
            hiddenClass: 'is-hidden',
            onOpen: () => {
                if (Array.isArray(templateOptions)) {
                    renderTemplateOptions();
                    return;
                }
                loadTemplateOptions();
            }
        });
        return templateDropdownController;
    }

    function getEditSelectedOrganizationNames() {
        const configuredNames = window.selectedOrganizationNames || window.__adminBootstrap?.selectedOrganizationNames;
        if (Array.isArray(configuredNames)) {
            return configuredNames;
        }

        const template = document.getElementById('survey-edit-selected-organization-names');
        const serializedNames = template?.content?.textContent || template?.textContent || '';
        if (!serializedNames.trim()) {
            return [];
        }

        try {
            const parsedNames = JSON.parse(serializedNames);
            return Array.isArray(parsedNames) ? parsedNames : [];
        } catch (error) {
            console.error('Не удалось прочитать исходные организации анкеты:', error);
            return [];
        }
    }

    function captureOriginalEditState(modal, selected) {
        if (!modal || modal === originalEditModal) {
            return;
        }

        originalEditModal = modal;
        originalEditOrganizations = window.SurveyAdminFormState.cloneOrganizations(selected);
        originalEditCriteria = Array.from(modal.querySelectorAll('.criteriy'))
            .map((input) => input.value.trim());
    }

    function restoreSurveyEditProtectedFields() {
        const modal = document.getElementById('surveyEditorModal');
        if (!modal || modal !== originalEditModal) {
            return;
        }

        const criteriaInputs = Array.from(modal.querySelectorAll('.criteriy'));
        if (criteriaInputs.length === originalEditCriteria.length) {
            criteriaInputs.forEach((input, index) => {
                input.value = originalEditCriteria[index] || '';
                input.dataset.originalValue = originalEditCriteria[index] || '';
                window.SurveyAdminValidation?.clearFieldError(input);
            });
        } else {
            criteria.replace(originalEditCriteria);
            Array.from(modal.querySelectorAll('.criteriy')).forEach((input, index) => {
                input.dataset.originalValue = originalEditCriteria[index] || '';
                window.SurveyAdminValidation?.clearFieldError(input);
            });
        }

        organizations.setSelected(originalEditOrganizations);
        organizations.syncList();
        organizations.updateDisplay();
        window.SurveyAdminValidation?.clearFieldError(getElementByRole('selected-organizations-container'));
    }

    function setSubmitButtonLabel(label) {
        const submitButton = getElementByRole('survey-submit');
        if (submitButton) submitButton.textContent = label;
    }

    function formatLocalIso(date) {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        return `${year}-${month}-${day}`;
    }

    function addIsoDays(value, days) {
        const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(String(value || ''));
        if (!match) return '';
        const date = new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
        date.setDate(date.getDate() + days);
        return formatLocalIso(date);
    }

    function configureEditorDateBounds() {
        const startDate = safeGetElement('startDate');
        const endDate = safeGetElement('endDate');
        if (!startDate || !endDate) return;

        if (!getSurveyEditorContext().isPlannedTemplate) {
            window.AppDate?.bindPeriodBounds?.(startDate, endDate);
            return;
        }

        const tomorrow = new Date();
        tomorrow.setHours(0, 0, 0, 0);
        tomorrow.setDate(tomorrow.getDate() + 1);
        const tomorrowIso = formatLocalIso(tomorrow);
        const ancestorDateEnd = window.AppDate?.toIso?.(
            document.getElementById('plannedTemplateAncestorId')?.dataset?.dateEnd || ''
        ) || '';
        const firstDateAfterAncestor = addIsoDays(ancestorDateEnd, 1);
        startDate.min = firstDateAfterAncestor > tomorrowIso ? firstDateAfterAncestor : tomorrowIso;
        startDate.removeAttribute('max');
        endDate.removeAttribute('max');

        const updateEndMinimum = () => {
            const startIso = window.AppDate?.getInputIso?.(startDate) || window.AppDate?.toIso?.(startDate.value) || '';
            const minimumEnd = addIsoDays(startIso, 1);
            if (minimumEnd) endDate.min = minimumEnd;
            else endDate.removeAttribute('min');
        };
        updateEndMinimum();
        if (startDate.dataset.plannedBoundsBound !== 'true') {
            startDate.dataset.plannedBoundsBound = 'true';
            startDate.addEventListener('change', updateEndMinimum);
            startDate.addEventListener('input', updateEndMinimum);
        }
    }

    function resetSurveyCreateForm() {
        setSubmitButtonLabel('Сохранить');
        resetTemplatePicker();
        window.SurveyAdminValidation?.clearAll(document.getElementById('surveyEditorModal'));
        organizations.setSelected([]);
        organizations.resetAvailable();
        ['surveyTitle', 'surveyDescription', 'startDate', 'endDate'].forEach((id) => {
            const field = safeGetElement(id);
            if (field) {
                field.value = '';
                field.classList.remove('invalid');
            }
        });
        const autoCreationField = safeGetElement('surveyAutoCreationEnabled');
        if (autoCreationField) {
            setSurveyAutoCreationEnabled(false);
        }
        getElementByRole('criteria-step')?.classList.remove('confirmed-criteria');
        const organizationField = getElementByRole('selected-organizations-container');
        organizationField?.setAttribute('aria-invalid', 'false');
        organizationField?.classList.remove('invalid');
        criteria.replace(['']);
        organizations.updateDisplay();
        organizations.close();
        setModalVisible('loadingOverlay', false);
        configureEditorDateBounds();
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
                : [],
            isAutoCreationEnabled: rawTemplate?.isAutoCreationEnabled === true
        };
    }

    function prefillSurveyCreateForm(rawTemplate, options = {}) {
        const template = normalizeCopyTemplate(rawTemplate);
        const fillEmptyOnly = options.fillEmptyOnly === true;
        const preservedStartDate = options.preserveDates
            ? window.AppDate?.getInputIso?.('startDate') || safeGetValue('startDate')
            : '';
        const preservedEndDate = options.preserveDates
            ? window.AppDate?.getInputIso?.('endDate') || safeGetValue('endDate')
            : '';
        if (!fillEmptyOnly) {
            resetSurveyCreateForm();
        }
        setSubmitButtonLabel(options.submitLabel || 'Копировать');
        const title = safeGetElement('surveyTitle');
        const description = safeGetElement('surveyDescription');
        if (title && (!fillEmptyOnly || !title.value.trim())) title.value = template.title;
        if (description && (!fillEmptyOnly || !description.value.trim())) {
            description.value = template.description;
        }
        if (!fillEmptyOnly) {
            window.AppDate?.setInputValue?.(
                'startDate',
                options.preserveDates ? preservedStartDate : template.startDate
            );
            window.AppDate?.setInputValue?.(
                'endDate',
                options.preserveDates ? preservedEndDate : template.endDate
            );
        }
        configureEditorDateBounds();
        if (!fillEmptyOnly || !criteria.values().some((value) => value.trim())) {
            criteria.replace(template.criteria);
        }
        if (!fillEmptyOnly || organizations.getSelected().length === 0) {
            organizations.setSelected(template.organizations);
            organizations.updateDisplay();
            organizations.syncList();
        }
        if (!fillEmptyOnly) {
            setSurveyAutoCreationEnabled(template.isAutoCreationEnabled);
        }
        if (options.ancestorId) {
            setParentTemplateSelection(
                options.ancestorId,
                options.ancestorName,
                options.ancestorDateEnd
            );
        }
    }

    function validateForm() {
        const context = getSurveyEditorContext();
        let isValid = true;
        const errors = [];
        const requiredFields = [
            { id: 'surveyTitle', message: context.isTemplate ? 'Введите название шаблона.' : 'Введите название анкеты.' },
            { id: 'startDate', message: 'Укажите дату начала.' }
        ];
        if (!context.isTemplate) {
            requiredFields.push({ id: 'endDate', message: 'Укажите дату конца.' });
        }
        requiredFields.forEach(({ id, message }) => {
            const field = safeGetElement(id);
            if (field?.value.trim()) {
                window.SurveyAdminValidation?.clearFieldError(field);
                return;
            }
            window.SurveyAdminValidation?.setFieldError(field, message);
            errors.push(message);
            isValid = false;
        });
        safeGetElement('surveyDescription')?.classList.remove('invalid');
        const startDate = window.AppDate?.toIso(safeGetValue('startDate')) || '';
        const endDate = window.AppDate?.toIso(safeGetValue('endDate')) || '';
        if (safeGetValue('startDate') && !startDate) {
            const message = 'Введите дату начала в формате ДД.ММ.ГГГГ.';
            window.SurveyAdminValidation?.setFieldError(safeGetElement('startDate'), message);
            errors.push(message);
            isValid = false;
        }
        if (safeGetValue('endDate') && !endDate) {
            const message = 'Введите дату конца в формате ДД.ММ.ГГГГ.';
            window.SurveyAdminValidation?.setFieldError(safeGetElement('endDate'), message);
            errors.push(message);
            isValid = false;
        }
        if (context.isPlannedTemplate) {
            const today = new Date();
            today.setHours(0, 0, 0, 0);
            if (startDate && startDate <= formatLocalIso(today)) {
                const message = 'Дата начала планового шаблона должна быть позже сегодняшней даты.';
                window.SurveyAdminValidation?.setFieldError(safeGetElement('startDate'), message);
                errors.push(message);
                isValid = false;
            }
            const ancestorDateEnd = window.AppDate?.toIso?.(
                document.getElementById('plannedTemplateAncestorId')?.dataset?.dateEnd || ''
            ) || '';
            if (startDate && ancestorDateEnd && startDate <= ancestorDateEnd) {
                const message = 'Дата начала планового шаблона должна быть позже даты окончания шаблона-родителя.';
                window.SurveyAdminValidation?.setFieldError(safeGetElement('startDate'), message);
                errors.push(message);
                isValid = false;
            }
            if (startDate && endDate && endDate <= startDate) {
                const message = 'Дата конца должна быть позже даты начала.';
                window.SurveyAdminValidation?.setFieldError(safeGetElement('endDate'), message);
                errors.push(message);
                isValid = false;
            }
        } else {
            const periodError = window.AppDate?.getPeriodError?.('startDate', 'endDate');
            if (periodError) {
                window.SurveyAdminValidation?.setFieldError(periodError.target, periodError.message);
                errors.push(periodError.message);
                isValid = false;
            }
        }
        const organizationField = getElementByRole('selected-organizations-container');
        if (organizations.getSelected().length === 0) {
            const message = 'Выберите хотя бы одну организацию.';
            window.SurveyAdminValidation?.setFieldError(organizationField, message);
            errors.push(message);
            isValid = false;
        } else {
            window.SurveyAdminValidation?.clearFieldError(organizationField);
        }
        const criteriaValid = criteria.validate();
        if (!criteriaValid) {
            errors.push(criteria.getValidationMessage() || 'Заполните критерии оценки.');
        }
        if (errors.length > 0) {
            const uniqueErrors = [...new Set(errors)];
            showError('Проверьте поля', uniqueErrors.join(' • '));
        }
        return criteriaValid && isValid;
    }

    function addSurvey() {
        if (!validateForm()) return;
        const context = getSurveyEditorContext();
        const loading = safeGetElement('loadingOverlay');
        if (loading) setModalVisible(loading, true);
        fetch(context.createUrl, {
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
                Criteria: criteria.values(),
                IsAutoCreationEnabled: context.isTemplate
                    && safeGetElement('surveyAutoCreationEnabled')?.value === 'true',
                AncestorId: context.isPlannedTemplate
                    ? Number.parseInt(safeGetElement('plannedTemplateAncestorId')?.value || '', 10) || null
                    : null
            })
        })
            .then((response) => response.ok
                ? response.json()
                : response.json().then((error) => Promise.reject(new Error(
                    error.message || `Не удалось создать ${context.entityName}.`
                ))))
            .then((result) => {
                if (!result.success) throw new Error(result.message || `Не удалось создать ${context.entityName}.`);
                if (typeof window.handleSurveyCreateSuccess === 'function') {
                    window.handleSurveyCreateSuccess(result);
                } else if (typeof window.handleAdminMutationSuccess === 'function') {
                    window.handleAdminMutationSuccess({
                        message: result.message || (context.isPlannedTemplate
                            ? 'Плановый шаблон успешно создан.'
                            : context.isTemplate ? 'Шаблон успешно создан.' : 'Анкета успешно создана.'),
                        tabName: context.listTab,
                        fallbackUrl: context.listUrl
                    });
                } else {
                    showSuccess('Успех', context.isPlannedTemplate
                        ? 'Плановый шаблон успешно создан.'
                        : context.isTemplate ? 'Шаблон успешно создан.' : 'Анкета успешно создана.');
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
        const modal = document.getElementById('surveyEditorModal');
        const selectedIds = (document.getElementById('selectedOrganizationIds')?.value || '').split(',');
        const selectedNames = getEditSelectedOrganizationNames();
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
        captureOriginalEditState(modal, selected);
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
        selectSurveyAutoCreationEnabled,
        restoreSurveyEditProtectedFields,
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

    configureEditorDateBounds();
    ensureTemplatePickerController();
    ensureAutoCreationPickerController();
})();
