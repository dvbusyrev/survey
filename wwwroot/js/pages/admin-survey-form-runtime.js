(function () {
    var selectedOrganization = [];
    var allOrganizations = [];
    var criteriaConfirmed = false;

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

    function normalizeOrganization(rawOrganization) {
        return {
            id: Number(rawOrganization.organization_id ?? rawOrganization.id ?? 0),
            name: String(rawOrganization.organization_name ?? rawOrganization.name ?? '').trim()
        };
    }

    function openOrganizationModal() {
        const modal = safeGetElement('organizationModal');
        if (!modal) {
            return;
        }

        if (window.showSiteModal) {
            window.showSiteModal(modal);
        } else {
            modal.classList.add('active');
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

    function renderOrganizationsList() {
        const organizationList = safeGetElement('organizationList');
        if (!organizationList) {
            return;
        }

        organizationList.innerHTML = '';

        allOrganizations.forEach((organization) => {
            const isSelected = selectedOrganization.some((item) => item.id === organization.id);
            const organizationItem = document.createElement('div');
            organizationItem.className = `organization-item ${isSelected ? 'selected' : ''}`;

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
    }

    function loadOrganizations() {
        const loadingElement = safeGetElement('loadingOrgs');
        const organizationList = safeGetElement('organizationList');

        if (!loadingElement || !organizationList) {
            return;
        }

        loadingElement.style.display = 'block';
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
                organizationList.style.display = 'block';
            });
    }

    function toggleOrganizationSelection(id, name) {
        const index = selectedOrganization.findIndex((organization) => organization.id === id);

        if (index === -1) {
            selectedOrganization.push({ id, name });
            return;
        }

        selectedOrganization.splice(index, 1);
    }

    function saveSelectedOrganization() {
        closeModal('organizationModal');
        updateSelectedOrganizationDisplay();
    }

    function updateSelectedOrganizationDisplay() {
        const container = safeGetElement('selectedOrganizationContainer');
        const list = safeGetElement('selectedOrganizationList');

        if (!container || !list) {
            return;
        }

        if (selectedOrganization.length === 0) {
            container.style.display = 'none';
            list.innerHTML = '';
            return;
        }

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
    }

    function removeSelectedOrganization(id) {
        selectedOrganization = selectedOrganization.filter((organization) => organization.id !== id);
        updateSelectedOrganizationDisplay();

        const organizationModal = document.getElementById('organizationModal');
        if (organizationModal && organizationModal.classList.contains('active')) {
            renderOrganizationsList();
        }
    }

    function addRowCriteriy() {
        const container = safeGetElement('cont_criteries');
        if (!container) {
            return;
        }

        const count = container.querySelectorAll('.criteriy').length + 1;
        const wrapper = document.createElement('div');
        wrapper.className = 'form-group';
        wrapper.innerHTML = `
            <label for="criteriy${count}">Критерий оценки ${count}:</label>
            <input
                type="text"
                class="form-control criteriy"
                id="criteriy${count}"
                placeholder="Введите критерий оценки"
                required
            />
        `;

        container.appendChild(wrapper);
    }

    function confirmCriteries() {
        const criteriaInputs = document.querySelectorAll('.criteriy');
        let allValid = true;

        criteriaInputs.forEach((input) => {
            if (!input.value.trim()) {
                input.classList.add('invalid');
                allValid = false;
                return;
            }

            input.classList.remove('invalid');
        });

        if (!allValid) {
            showError('Ошибка', 'Заполните все критерии оценки.');
            return;
        }

        criteriaInputs.forEach((input) => {
            input.readOnly = true;
        });

        safeGetElement('two_step')?.classList.add('confirmed-criteria');
        if (safeGetElement('add_survey_btn')) {
            safeGetElement('add_survey_btn').style.display = 'inline-block';
        }
        if (safeGetElement('add_crit')) {
            safeGetElement('add_crit').style.display = 'none';
        }
        if (safeGetElement('conf_btn')) {
            safeGetElement('conf_btn').style.display = 'none';
        }

        criteriaConfirmed = true;
        showSuccess('Успех', 'Критерии успешно подтверждены.');
    }

    function showSuccess(title, message) {
        const notification = safeGetElement('notification');
        if (!notification) {
            window.siteNotify?.(message, 'success');
            return;
        }

        const titleElement = document.getElementById('notificationTitle');
        const messageElement = document.getElementById('notificationMessage');
        if (titleElement) {
            titleElement.textContent = title;
            titleElement.className = 'notification-title notification-success';
        }
        if (messageElement) {
            messageElement.textContent = message;
        }

        if (window.showSiteModal) {
            window.showSiteModal(notification);
        } else {
            notification.classList.add('active');
        }

        window.setTimeout(function () {
            if (window.hideSiteModal) {
                window.hideSiteModal(notification);
            } else {
                notification.classList.remove('active');
            }
        }, 3000);
    }

    function showError(title, message) {
        const notification = safeGetElement('notification');
        if (!notification) {
            window.siteNotify?.(message, 'error');
            return;
        }

        const titleElement = document.getElementById('notificationTitle');
        const messageElement = document.getElementById('notificationMessage');
        if (titleElement) {
            titleElement.textContent = title;
            titleElement.className = 'notification-title notification-error';
        }
        if (messageElement) {
            messageElement.textContent = message;
        }

        if (window.showSiteModal) {
            window.showSiteModal(notification);
        } else {
            notification.classList.add('active');
        }

        window.setTimeout(function () {
            if (window.hideSiteModal) {
                window.hideSiteModal(notification);
            } else {
                notification.classList.remove('active');
            }
        }, 3000);
    }

    function hideNotification() {
        const notification = safeGetElement('notification');
        if (!notification) {
            return;
        }

        if (window.hideSiteModal) {
            window.hideSiteModal(notification);
        } else {
            notification.classList.remove('active');
        }
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
            showError('Ошибка', 'Дата окончания должна быть позже даты начала.');
            isValid = false;
        } else {
            safeGetElement('endDate')?.classList.remove('invalid');
        }

        if (selectedOrganization.length === 0) {
            showError('Ошибка', 'Выберите хотя бы одну организацию.');
            isValid = false;
        }

        if (!criteriaConfirmed) {
            showError('Ошибка', 'Подтвердите критерии оценки.');
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
            if (window.showSiteModal) {
                window.showSiteModal(loadingOverlay);
            } else {
                loadingOverlay.style.display = 'flex';
            }
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

                if (window.hideSiteModal) {
                    window.hideSiteModal(loadingOverlay);
                } else {
                    loadingOverlay.style.display = 'none';
                }
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

        window.surveyEditSelectedOrganization = [];

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
                window.surveyEditSelectedOrganization.push({
                    id: parsedId,
                    name: resolvedName
                });
            }
        });

        if (window.surveyEditSelectedOrganization.length === 0) {
            document.querySelectorAll('#organizationList .organization-item[data-selected="true"]').forEach(function (item) {
                var parsedId = parseInt(item.dataset.id, 10);
                if (!Number.isNaN(parsedId)) {
                    window.surveyEditSelectedOrganization.push({
                        id: parsedId,
                        name: item.dataset.name || ''
                    });
                }
            });
        }

        document.querySelectorAll('#organizationList .organization-item').forEach(function (item) {
            item.classList.toggle('selected', item.dataset.selected === 'true');
        });

        if (typeof window.surveyEditUpdateSelectedOrganizationDisplay === 'function') {
            window.surveyEditUpdateSelectedOrganizationDisplay();
        }
    }

    function surveyEditOpenOrganizationModal() {
        document.querySelectorAll('#organizationList .organization-item').forEach(function (item) {
            var parsedId = parseInt(item.dataset.id, 10);
            var isSelected = window.surveyEditSelectedOrganization.some(function (organization) {
                return organization.id === parsedId;
            });
            item.dataset.selected = isSelected ? 'true' : 'false';
            item.classList.toggle('selected', isSelected);
        });

        if (window.showSiteModal) {
            window.showSiteModal('organizationModal');
        } else {
            var modal = document.getElementById('organizationModal');
            if (modal) {
                modal.style.display = 'flex';
            }
        }

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
    window.addRowCriteriy = addRowCriteriy;
    window.confirmCriteries = confirmCriteries;
    window.showSuccess = showSuccess;
    window.showError = showError;
    window.hideNotification = hideNotification;
    window.addSurvey = addSurvey;
    window.validateForm = validateForm;

    window.surveyEditInit = surveyEditInit;
    window.surveyEditOpenOrganizationModal = surveyEditOpenOrganizationModal;
    window.surveyEditCloseModal = surveyEditCloseModal;

    window.safeGetElement = safeGetElement;
    window.safeGetValue = safeGetValue;
})();
