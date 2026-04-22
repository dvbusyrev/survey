(function () {
    var selectedOrganization = [];
    var allOrganizations = [];

    window.surveyEditSelectedOrganization = window.surveyEditSelectedOrganization || [];
    window.surveyEditModalOpen = false;

    function resolveModal(target) {
        if (!target) {
            return null;
        }

        if (typeof target === 'string') {
            return document.getElementById(target);
        }

        return target;
    }

    function safeGetElement(id) {
        const element = document.getElementById(id);
        if (!element) {
            console.error('Элемент не найден:', id);
        }
        return element;
    }

    function safeGetValue(id) {
        const element = safeGetElement(id);
        return element ? element.value.trim() : '';
    }

    function getElementByRole(role) {
        return document.querySelector(`[data-role="${role}"]`);
    }

    function normalizeOrganization(rawOrganization) {
        return {
            id: Number(rawOrganization.id_organization ?? rawOrganization.id ?? 0),
            name: String(rawOrganization.organization_name ?? rawOrganization.name ?? '').trim()
        };
    }

    function cloneOrganizations(items) {
        if (!Array.isArray(items)) {
            return [];
        }

        return items
            .map((item) => normalizeOrganization(item || {}))
            .filter((item) => item.id > 0 && item.name);
    }

    function setSelectedOrganizations(items) {
        selectedOrganization = cloneOrganizations(items);
        window.surveyEditSelectedOrganization = cloneOrganizations(selectedOrganization);
    }

    function getSelectedOrganizations() {
        return cloneOrganizations(selectedOrganization);
    }

    function getOrganizationItemName(item) {
        const datasetName = String(item?.dataset?.name || '').trim();
        if (datasetName) {
            return datasetName;
        }

        const labelText = String(item?.querySelector('label')?.textContent || '').trim();
        if (labelText) {
            return labelText;
        }

        return String(item?.textContent || '').trim();
    }

    function syncOrganizationItemSelection(item, isSelected) {
        if (!item) {
            return;
        }

        item.dataset.selected = isSelected ? 'true' : 'false';
        item.classList.toggle('selected', isSelected);

        const checkbox = item.querySelector('input[type="checkbox"]');
        if (checkbox) {
            checkbox.checked = isSelected;
        }
    }

    function syncOrganizationListSelectionFromState() {
        const selectedIds = new Set(selectedOrganization.map((organization) => organization.id));
        document.querySelectorAll('#organizationList .organization-item').forEach((item) => {
            const id = Number.parseInt(item.dataset.id || '', 10);
            syncOrganizationItemSelection(item, Number.isFinite(id) && selectedIds.has(id));
        });
    }

    function openOrganizationModal() {
        const modal = safeGetElement('organizationModal');
        if (!modal) {
            return;
        }

        setModalVisible(modal, true);
        if (allOrganizations.length > 0) {
            renderOrganizationsList();
            return;
        }

        loadOrganizations();
    }

    function closeModal(modalId) {
        const modal = resolveModal(modalId);
        if (!modal) {
            return;
        }

        if (window.hideSiteModal) {
            window.hideSiteModal(modal);
        } else {
            modal.classList.remove('active');
            modal.style.display = 'none';
        }
    }

    function setModalVisible(target, isVisible) {
        const modal = resolveModal(target);
        if (!modal) {
            return false;
        }

        if (isVisible) {
            if (window.showSiteModal) {
                window.showSiteModal(modal);
            } else {
                modal.classList.add('active');
                modal.style.display = 'flex';
            }
            return true;
        }

        if (window.hideSiteModal) {
            window.hideSiteModal(modal);
        } else {
            modal.classList.remove('active');
            modal.style.display = 'none';
        }
        return true;
    }

    function showTimedNotification(type, title, message) {
        const notification = safeGetElement('notification');
        if (!notification) {
            window.siteNotify?.(message, type);
            return;
        }

        const titleElement = document.getElementById('notificationTitle');
        const messageElement = document.getElementById('notificationMessage');
        if (titleElement) {
            titleElement.textContent = title;
            titleElement.className = `notification-title notification-${type}`;
        }
        if (messageElement) {
            messageElement.textContent = message;
        }

        setModalVisible(notification, true);
        window.setTimeout(function () {
            setModalVisible(notification, false);
        }, 3000);
    }

    function renderOrganizationsList() {
        const organizationList = safeGetElement('organizationList');
        if (!organizationList) {
            return;
        }

        organizationList.innerHTML = '';
        organizationList.classList.remove('u-hidden');
        organizationList.style.display = '';

        allOrganizations.forEach((organization) => {
            const isSelected = selectedOrganization.some((item) => item.id === organization.id);
            const organizationItem = document.createElement('div');
            organizationItem.className = `organization-item ${isSelected ? 'selected' : ''}`;
            organizationItem.dataset.id = String(organization.id);
            organizationItem.dataset.name = organization.name;
            organizationItem.dataset.selected = isSelected ? 'true' : 'false';

            const checkbox = document.createElement('input');
            checkbox.type = 'checkbox';
            checkbox.id = `org-${organization.id}`;
            checkbox.checked = isSelected;
            checkbox.addEventListener('change', function () {
                toggleOrganizationSelection(organization.id, organization.name);
            });

            const label = document.createElement('label');
            label.htmlFor = checkbox.id;
            label.textContent = organization.name;

            organizationItem.appendChild(checkbox);
            organizationItem.appendChild(label);
            organizationList.appendChild(organizationItem);
        });

        syncOrganizationListSelectionFromState();
    }

    function loadOrganizations() {
        const loadingElement = safeGetElement('loadingOrgs');
        const organizationList = safeGetElement('organizationList');

        if (!loadingElement || !organizationList) {
            return;
        }

        loadingElement.classList.remove('u-hidden');
        loadingElement.style.display = 'block';
        organizationList.classList.add('u-hidden');
        organizationList.style.display = 'none';

        fetch('/organizations/data', {
            headers: {
                Accept: 'application/json'
            }
        })
            .then((response) => {
                if (!response.ok) {
                    throw new Error(
                        window.getResponseErrorMessage
                            ? window.getResponseErrorMessage(response, 'Ошибка загрузки организаций')
                            : `Ошибка загрузки организаций: ${response.status}`
                    );
                }

                return response.json();
            })
            .then((data) => {
                if (!Array.isArray(data)) {
                    throw new Error('Получены некорректные данные организаций.');
                }

                allOrganizations = data
                    .map(normalizeOrganization)
                    .filter((organization) => organization.id > 0 && organization.name);

                renderOrganizationsList();
            })
            .catch((error) => {
                console.error('Ошибка загрузки организаций:', error);
                showError('Ошибка', `Не удалось загрузить организации: ${error.message}`);
            })
            .finally(() => {
                loadingElement.style.display = 'none';
                loadingElement.classList.add('u-hidden');
                organizationList.classList.remove('u-hidden');
                organizationList.style.display = 'block';
            });
    }

    function toggleOrganizationSelection(id, name) {
        const nextSelection = getSelectedOrganizations();
        const index = nextSelection.findIndex((organization) => organization.id === id);

        if (index === -1) {
            nextSelection.push({ id, name });
        } else {
            nextSelection.splice(index, 1);
        }

        setSelectedOrganizations(nextSelection);
        syncOrganizationListSelectionFromState();
    }

    function saveSelectedOrganization() {
        closeModal('organizationModal');
        updateSelectedOrganizationDisplay();
    }

    function updateSelectedOrganizationDisplay() {
        const container = getElementByRole('selected-organizations-container');
        const list = getElementByRole('selected-organizations-list');
        const idsInput = document.getElementById('selectedOrganizationIds');

        if (!container || !list) {
            return;
        }

        if (selectedOrganization.length === 0) {
            container.classList.add('u-hidden');
            container.style.display = 'none';
            list.innerHTML = '';
            if (idsInput) {
                idsInput.value = '';
            }
            return;
        }

        container.classList.remove('u-hidden');
        container.style.display = 'block';
        list.innerHTML = '';

        selectedOrganization.forEach((organization) => {
            const item = document.createElement('div');
            item.className = 'selected-organization-item';
            item.appendChild(document.createTextNode(organization.name));

            const button = document.createElement('button');
            button.type = 'button';
            button.setAttribute('aria-label', 'Убрать организацию');
            button.addEventListener('click', function () {
                removeSelectedOrganization(organization.id);
            });

            const icon = document.createElement('i');
            icon.className = 'fas fa-xmark';
            icon.setAttribute('aria-hidden', 'true');
            button.appendChild(icon);

            item.appendChild(button);
            list.appendChild(item);
        });

        if (idsInput) {
            idsInput.value = selectedOrganization.map((organization) => organization.id).join(',');
        }
    }

    function removeSelectedOrganization(id) {
        setSelectedOrganizations(
            selectedOrganization.filter((organization) => organization.id !== id)
        );
        updateSelectedOrganizationDisplay();
        syncOrganizationListSelectionFromState();
    }

    function getCriteriaList() {
        return getElementByRole('criteria-list');
    }

    function getCriteriaItems(container) {
        const criteriaList = container || getCriteriaList();
        return criteriaList
            ? Array.from(criteriaList.querySelectorAll('.survey-editor-page__criteria-item'))
            : [];
    }

    function refreshCriteriaFields(container) {
        getCriteriaItems(container).forEach((item, index) => {
            const criterionNumber = index + 1;
            const label = item.querySelector('label');
            const input = item.querySelector('.criteriy');
            const removeButton = item.querySelector('.survey-editor-page__criteria-remove');

            if (input) {
                input.id = `criterion${criterionNumber}`;
            }

            if (label) {
                label.setAttribute('for', `criterion${criterionNumber}`);
                label.textContent = `Критерий №${criterionNumber}:`;
            }

            if (removeButton) {
                removeButton.setAttribute('aria-label', `Удалить критерий ${criterionNumber}`);
            }
        });
    }

    function createCriteriaField(value) {
        const wrapper = document.createElement('div');
        wrapper.className = 'form-group survey-editor-page__criteria-item';

        const label = document.createElement('label');

        const control = document.createElement('div');
        control.className = 'survey-editor-page__criteria-control';

        const input = document.createElement('input');
        input.type = 'text';
        input.className = 'form-control criteriy';
        input.placeholder = 'Введите критерий оценки';
        input.required = true;
        input.value = value || '';

        const removeButton = document.createElement('button');
        removeButton.type = 'button';
        removeButton.className = 'survey-editor-page__criteria-remove';
        removeButton.dataset.clickCall = 'removeSurveyCriterion';
        removeButton.dataset.clickPassElement = 'true';

        const icon = document.createElement('i');
        icon.className = 'fas fa-trash';
        icon.setAttribute('aria-hidden', 'true');
        removeButton.appendChild(icon);

        const error = document.createElement('div');
        error.className = 'error-message';

        control.appendChild(input);
        control.appendChild(removeButton);
        wrapper.appendChild(label);
        wrapper.appendChild(control);
        wrapper.appendChild(error);

        return wrapper;
    }

    function appendSurveyCriteriaField(value) {
        const container = getCriteriaList();
        if (!container) {
            return null;
        }

        const field = createCriteriaField(value);
        container.appendChild(field);
        refreshCriteriaFields(container);
        return field;
    }

    function removeSurveyCriterion(trigger) {
        const criterionItem = trigger?.closest('.survey-editor-page__criteria-item');
        if (!criterionItem) {
            return;
        }

        const container = criterionItem.parentElement;
        criterionItem.remove();
        refreshCriteriaFields(container);
    }

    function validateSurveyCriteriaFields() {
        const criteriaItems = getCriteriaItems();
        if (criteriaItems.length === 0) {
            showError('Ошибка', 'Добавьте хотя бы один критерий оценки.');
            return false;
        }

        let hasErrors = false;
        let hasFilledCriteria = false;

        criteriaItems.forEach((item) => {
            const input = item.querySelector('.criteriy');
            const error = item.querySelector('.error-message');
            const value = input?.value.trim() || '';

            if (value) {
                hasFilledCriteria = true;
                input?.classList.remove('invalid');
                if (error) {
                    error.textContent = '';
                    error.style.display = 'none';
                }
                return;
            }

            hasErrors = true;
            input?.classList.add('invalid');
            if (error) {
                error.textContent = 'Заполните критерий или удалите это поле.';
                error.style.display = 'block';
            }
        });

        if (!hasFilledCriteria) {
            showError('Ошибка', 'Добавьте хотя бы один критерий оценки.');
            return false;
        }

        if (hasErrors) {
            showError('Ошибка', 'Заполните все критерии оценки или удалите пустые поля.');
            return false;
        }

        return true;
    }

    function addRowCriteriy() {
        appendSurveyCriteriaField('');
    }

    function showSuccess(title, message) {
        showTimedNotification('success', title, message);
    }

    function showError(title, message) {
        showTimedNotification('error', title, message);
    }

    function hideNotification() {
        setModalVisible('notification', false);
    }

    function resetSurveyCreateForm() {
        setSelectedOrganizations([]);
        allOrganizations = [];

        const title = safeGetElement('surveyTitle');
        const description = safeGetElement('surveyDescription');
        const startDate = safeGetElement('startDate');
        const endDate = safeGetElement('endDate');

        if (title) {
            title.value = '';
            title.classList.remove('invalid');
        }

        if (description) {
            description.value = '';
            description.classList.remove('invalid');
        }

        if (startDate) {
            startDate.value = '';
            startDate.classList.remove('invalid');
        }

        if (endDate) {
            endDate.value = '';
            endDate.classList.remove('invalid');
        }

        const criteriaStep = getElementByRole('criteria-step');
        const criteriaList = getElementByRole('criteria-list');

        if (criteriaStep) {
            criteriaStep.classList.remove('confirmed-criteria');
        }

        if (criteriaList) {
            criteriaList.innerHTML = '';
            appendSurveyCriteriaField('');
        }

        updateSelectedOrganizationDisplay();
        closeModal('organizationModal');
        setModalVisible('notification', false);
        setModalVisible('loadingOverlay', false);
    }

    function validateForm() {
        let isValid = true;

        ['surveyTitle', 'surveyDescription', 'startDate', 'endDate'].forEach((id) => {
            const element = safeGetElement(id);
            if (!element) {
                isValid = false;
                return;
            }

            if (!element.value.trim()) {
                element.classList.add('invalid');
                isValid = false;
                return;
            }

            element.classList.remove('invalid');
        });

        const startDate = new Date(safeGetValue('startDate'));
        const endDate = new Date(safeGetValue('endDate'));
        if (endDate <= startDate) {
            safeGetElement('endDate')?.classList.add('invalid');
            showError('Ошибка', 'Дата конца должна быть позже даты начала.');
            isValid = false;
        } else {
            safeGetElement('endDate')?.classList.remove('invalid');
        }

        if (selectedOrganization.length === 0) {
            showError('Ошибка', 'Выберите хотя бы одну организацию.');
            isValid = false;
        }

        if (!validateSurveyCriteriaFields()) {
            isValid = false;
        }

        return isValid;
    }

    function addSurvey() {
        if (!validateForm()) {
            return;
        }

        const loadingOverlay = safeGetElement('loadingOverlay');
        if (loadingOverlay) {
            setModalVisible(loadingOverlay, true);
        }

        fetch('/surveys/create', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
            },
            body: JSON.stringify({
                Title: safeGetValue('surveyTitle'),
                Description: safeGetValue('surveyDescription'),
                StartDate: safeGetElement('startDate')?.value,
                EndDate: safeGetElement('endDate')?.value,
                Organizations: selectedOrganization.map((organization) => organization.id),
                Criteria: Array.from(document.querySelectorAll('.criteriy')).map((input) => input.value.trim())
            })
        })
            .then((response) => {
                if (!response.ok) {
                    return response.json().then((errorData) => {
                        throw new Error(errorData.message || 'Ошибка сервера.');
                    });
                }

                return response.json();
            })
            .then((data) => {
                if (!data.success) {
                    throw new Error(data.message || 'Не удалось создать анкету.');
                }

                if (typeof window.handleSurveyCreateSuccess === 'function') {
                    window.handleSurveyCreateSuccess(data);
                    return;
                }

                if (typeof window.handleAdminMutationSuccess === 'function') {
                    window.handleAdminMutationSuccess({
                        message: data.message || 'Анкета успешно создана.',
                        tabName: 'get_surveys',
                        fallbackUrl: '/surveys'
                    });
                    return;
                }

                showSuccess('Успех', 'Анкета успешно создана. Пожалуйста, перезагрузите страницу.');
                window.setTimeout(function () {
                    window.location.reload();
                }, 2000);
            })
            .catch((error) => {
                console.error('Ошибка создания анкеты:', error);
                showError('Ошибка', error.message);
            })
            .finally(() => {
                if (!loadingOverlay) {
                    return;
                }

                setModalVisible(loadingOverlay, false);
            });
    }

    function surveyEditInit() {
        var selectedIdsInput = document.getElementById('selectedOrganizationIds');
        var selectedIds = selectedIdsInput && selectedIdsInput.value
            ? selectedIdsInput.value.split(',')
            : [];
        var selectedNames = window.selectedOrganizationNames
            || window.__adminBootstrap?.selectedOrganizationNames
            || [];
        var nextSelection = [];

        selectedIds.forEach(function (rawId, index) {
            if (!rawId) {
                return;
            }

            var parsedId = parseInt(rawId, 10);
            if (Number.isNaN(parsedId)) {
                return;
            }

            var resolvedName = selectedNames[index];
            if (!resolvedName) {
                var organizationElement = document.querySelector(
                    '#organizationList .organization-item[data-id="' + parsedId + '"]'
                );
                resolvedName = organizationElement ? organizationElement.dataset.name : '';
            }

            if (resolvedName) {
                nextSelection.push({
                    id: parsedId,
                    name: resolvedName
                });
            }
        });

        if (nextSelection.length === 0) {
            document.querySelectorAll('#organizationList .organization-item[data-selected="true"]').forEach(function (item) {
                var parsedId = parseInt(item.dataset.id, 10);
                if (!Number.isNaN(parsedId)) {
                    nextSelection.push({
                        id: parsedId,
                        name: getOrganizationItemName(item)
                    });
                }
            });
        }

        setSelectedOrganizations(nextSelection);
        syncOrganizationListSelectionFromState();

        updateSelectedOrganizationDisplay();
    }

    function surveyEditOpenOrganizationModal() {
        syncOrganizationListSelectionFromState();

        setModalVisible('organizationModal', true);

        window.surveyEditModalOpen = true;
    }

    function surveyEditCloseModal(modalId) {
        closeModal(modalId);
        window.surveyEditModalOpen = false;
    }

    window.openOrganizationModal = openOrganizationModal;
    window.closeModal = closeModal;
    window.loadOrganizations = loadOrganizations;
    window.toggleOrganizationSelection = toggleOrganizationSelection;
    window.saveSelectedOrganization = saveSelectedOrganization;
    window.updateSelectedOrganizationDisplay = updateSelectedOrganizationDisplay;
    window.removeSelectedOrganization = removeSelectedOrganization;
    window.getSelectedOrganizations = getSelectedOrganizations;
    window.setSelectedOrganizations = setSelectedOrganizations;
    window.syncOrganizationListSelectionFromState = syncOrganizationListSelectionFromState;
    window.appendSurveyCriteriaField = appendSurveyCriteriaField;
    window.removeSurveyCriterion = removeSurveyCriterion;
    window.validateSurveyCriteriaFields = validateSurveyCriteriaFields;
    window.addRowCriteriy = addRowCriteriy;
    window.showSuccess = showSuccess;
    window.showError = showError;
    window.hideNotification = hideNotification;
    window.addSurvey = addSurvey;
    window.validateForm = validateForm;
    window.resetSurveyCreateForm = resetSurveyCreateForm;

    window.surveyEditInit = surveyEditInit;
    window.surveyEditOpenOrganizationModal = surveyEditOpenOrganizationModal;
    window.surveyEditCloseModal = surveyEditCloseModal;

    window.safeGetElement = safeGetElement;
    window.safeGetValue = safeGetValue;
})();
