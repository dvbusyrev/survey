(function () {
    window.createSurveyOrganizationController = function createSurveyOrganizationController({ safeGetElement, getElementByRole, showError }) {
        const state = window.SurveyAdminFormState;

        function getDropdown() {
            return getElementByRole('organization-dropdown');
        }

        function getMenu() {
            return getElementByRole('organization-dropdown-menu') || document.getElementById('organizationDropdownMenu');
        }

        function getItemName(item) {
            return String(item?.dataset?.name || item?.querySelector('label')?.textContent || item?.textContent || '').trim();
        }

        function syncItem(item, isSelected) {
            if (!item) return;
            item.dataset.selected = isSelected ? 'true' : 'false';
            item.classList.toggle('selected', isSelected);
            const checkbox = item.querySelector('input[type="checkbox"]');
            if (checkbox) checkbox.checked = isSelected;
        }

        function syncList() {
            const selectedIds = new Set(state.getSelected().map((organization) => organization.id));
            document.querySelectorAll('#organizationList [data-role="organization-option"]').forEach((item) => {
                const id = Number.parseInt(item.dataset.id || '', 10);
                syncItem(item, Number.isFinite(id) && selectedIds.has(id));
            });
        }

        function setVisible(isVisible) {
            const menu = getMenu();
            if (!menu) return false;
            menu.classList.toggle('is-hidden', !isVisible);
            getDropdown()?.classList.toggle('is-open', isVisible);
            window.surveyEditModalOpen = isVisible;
            if (isVisible) window.AppCheckboxDropdown?.scheduleListHeightUpdate(menu);
            return true;
        }

        function close() {
            setVisible(false);
        }

        function render() {
            const list = safeGetElement('organizationList');
            if (!list) return;
            list.replaceChildren();
            list.classList.remove('u-hidden');
            list.style.display = '';
            const selectedIds = new Set(state.getSelected().map((organization) => organization.id));
            state.getAvailable().forEach((organization) => {
                const item = document.createElement('div');
                item.className = `app-checkbox-option ${selectedIds.has(organization.id) ? 'selected' : ''}`;
                item.dataset.role = 'organization-option';
                item.dataset.id = String(organization.id);
                item.dataset.name = organization.name;
                item.dataset.selected = selectedIds.has(organization.id) ? 'true' : 'false';
                const checkbox = document.createElement('input');
                checkbox.type = 'checkbox';
                checkbox.className = 'app-checkbox-input';
                checkbox.id = `org-${organization.id}`;
                checkbox.checked = selectedIds.has(organization.id);
                checkbox.addEventListener('change', () => toggle(organization.id, organization.name));
                const label = document.createElement('label');
                label.className = 'app-checkbox-text';
                label.htmlFor = checkbox.id;
                label.textContent = organization.name;
                item.append(checkbox, label);
                list.appendChild(item);
            });
            syncList();
            window.AppCheckboxDropdown?.scheduleListHeightUpdate(getMenu());
        }

        function load() {
            const loading = safeGetElement('loadingOrgs');
            const list = safeGetElement('organizationList');
            if (!loading || !list) return;
            loading.classList.remove('u-hidden');
            loading.style.display = '';
            list.classList.add('u-hidden');
            list.style.display = 'none';
            fetch('/organizations/data', { headers: { Accept: 'application/json' } })
                .then((response) => {
                    if (!response.ok) {
                        throw new Error(window.getResponseErrorMessage
                            ? window.getResponseErrorMessage(response, 'Ошибка загрузки организаций')
                            : `Ошибка загрузки организаций: ${response.status}`);
                    }
                    return response.json();
                })
                .then((items) => {
                    if (!Array.isArray(items)) throw new Error('Получены некорректные данные организаций.');
                    state.setAvailable(items);
                    render();
                })
                .catch((error) => {
                    console.error('Ошибка загрузки организаций:', error);
                    showError('Ошибка', `Не удалось загрузить организации: ${error.message}`);
                })
                .finally(() => {
                    loading.style.display = 'none';
                    loading.classList.add('u-hidden');
                    list.classList.remove('u-hidden');
                    list.style.display = '';
                    window.AppCheckboxDropdown?.scheduleListHeightUpdate(getMenu());
                });
        }

        function open() {
            const menu = getMenu();
            if (!menu) return;
            setVisible(true);
            if (state.getAvailable().length > 0) {
                render();
                return;
            }
            if (document.querySelector('#organizationList [data-role="organization-option"]')) {
                syncList();
                return;
            }
            load();
        }

        function toggleDropdown() {
            if (getMenu()?.classList.contains('is-hidden')) open();
            else close();
        }

        function updateDisplay() {
            const container = getElementByRole('selected-organizations-container');
            const list = getElementByRole('selected-organizations-list');
            const idsInput = document.getElementById('selectedOrganizationIds');
            if (!container || !list) return;
            container.classList.remove('u-hidden');
            container.style.display = '';
            list.replaceChildren();
            const selected = state.getSelected();
            if (selected.length === 0) {
                const empty = document.createElement('p');
                empty.className = 'survey-editor-page__empty-selection';
                empty.textContent = 'Организации не выбраны';
                list.appendChild(empty);
                if (idsInput) idsInput.value = '';
                return;
            }
            selected.forEach((organization) => {
                const item = document.createElement('div');
                item.className = 'survey-editor-page__selected-organization-item';
                item.textContent = organization.name;
                list.appendChild(item);
            });
            if (idsInput) idsInput.value = selected.map((organization) => organization.id).join(',');
        }

        function toggle(id, name) {
            const selected = state.getSelected();
            const index = selected.findIndex((organization) => organization.id === id);
            if (index < 0) selected.push({ id, name });
            else selected.splice(index, 1);
            state.setSelected(selected);
            syncList();
            updateDisplay();
        }

        function remove(id) {
            state.setSelected(state.getSelected().filter((organization) => organization.id !== id));
            updateDisplay();
            syncList();
        }

        function bindDismissal() {
            if (document.documentElement.dataset.surveyOrganizationDropdownBound === 'true') return;
            document.documentElement.dataset.surveyOrganizationDropdownBound = 'true';
            document.addEventListener('click', (event) => {
                const dropdown = getDropdown();
                const menu = getMenu();
                if (dropdown && menu && !menu.classList.contains('is-hidden') && !dropdown.contains(event.target)) close();
            });
            document.addEventListener('keydown', (event) => {
                if (event.key === 'Escape') close();
            });
        }

        return { open, close, toggleDropdown, load, toggle, save: () => { close(); updateDisplay(); }, updateDisplay, remove, getSelected: state.getSelected, setSelected: state.setSelected, syncList, getItemName, resetAvailable: state.resetAvailable, bindDismissal };
    };
})();
