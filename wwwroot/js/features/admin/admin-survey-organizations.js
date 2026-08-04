(function () {
    window.createSurveyOrganizationController = function createSurveyOrganizationController({ safeGetElement, getElementByRole, showError }) {
        const state = window.SurveyAdminFormState;
        let dropdownController = null;

        function getDropdown() {
            return getElementByRole('organization-dropdown');
        }

        function getTrigger() {
            return getElementByRole('organization-dropdown-trigger') || getDropdown()?.querySelector('button');
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

        function ensureDropdownController() {
            const dropdown = getDropdown();
            const trigger = getTrigger();
            const menu = getMenu();
            if (!dropdown || !trigger || !menu || typeof window.AppUi?.createMultiselect !== 'function') {
                return null;
            }

            if (dropdownController?.root === dropdown
                && dropdownController?.trigger === trigger
                && dropdownController?.menu === menu) {
                return dropdownController;
            }

            dropdownController?.destroy?.();
            trigger.removeAttribute('data-click-call');
            dropdownController = window.AppUi.createMultiselect({
                root: dropdown,
                trigger,
                menu,
                openClass: 'is-open',
                hiddenClass: 'is-hidden',
                onOpen: () => {
                    window.surveyEditModalOpen = true;
                    window.AppCheckboxDropdown?.scheduleListHeightUpdate(menu);
                    if (state.getAvailable().length > 0) {
                        render();
                        return;
                    }
                    if (document.querySelector('#organizationList [data-role="organization-option"]')) {
                        syncList();
                        return;
                    }
                    load();
                },
                onClose: () => {
                    window.surveyEditModalOpen = false;
                }
            });

            return dropdownController;
        }

        function syncList() {
            const selectedIds = new Set(state.getSelected().map((organization) => organization.id));
            document.querySelectorAll('#organizationList [data-role="organization-option"]').forEach((item) => {
                const id = Number.parseInt(item.dataset.id || '', 10);
                syncItem(item, Number.isFinite(id) && selectedIds.has(id));
            });
        }

        function setVisible(isVisible) {
            const controller = ensureDropdownController();
            if (controller?.controller) {
                if (isVisible) {
                    controller.controller.open();
                } else {
                    controller.controller.close();
                }
                return true;
            }

            return false;
        }

        function close() {
            setVisible(false);
        }

        function render() {
            const list = safeGetElement('organizationList');
            if (!list) return;
            list.replaceChildren();
            list.classList.remove('u-hidden');
            const selectedIds = new Set(state.getSelected().map((organization) => organization.id));
            state.getAvailable().forEach((organization) => {
                const isSelected = selectedIds.has(organization.id);
                const checkboxOption = window.AppUi.createCheckboxOption({
                    text: organization.name,
                    checked: isSelected,
                    selected: isSelected,
                    selectedClass: true
                });
                const item = checkboxOption.option;
                const checkbox = checkboxOption.checkbox;
                const label = checkboxOption.text;

                item.dataset.role = 'organization-option';
                item.dataset.id = String(organization.id);
                item.dataset.name = organization.name;
                item.dataset.selected = isSelected ? 'true' : 'false';
                checkbox.id = `org-${organization.id}`;
                checkbox.addEventListener('change', () => toggle(organization.id, organization.name));
                label.htmlFor = checkbox.id;
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
            list.classList.add('u-hidden');
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
                    loading.classList.add('u-hidden');
                    list.classList.remove('u-hidden');
                    window.AppCheckboxDropdown?.scheduleListHeightUpdate(getMenu());
                });
        }

        function open() {
            setVisible(true);
        }

        function updateDisplay() {
            const container = getElementByRole('selected-organizations-container');
            const list = getElementByRole('selected-organizations-list');
            const idsInput = document.getElementById('selectedOrganizationIds');
            if (!container || !list) return;
            container.classList.remove('u-hidden');
            list.replaceChildren();
            const selected = state.getSelected();
            if (selected.length === 0) {
                const empty = window.AppUi.createElement('p', {
                    className: 'survey-editor-page__empty-selection',
                    text: 'Организации не выбраны'
                });
                list.appendChild(empty);
                if (idsInput) idsInput.value = '';
                return;
            }
            selected.forEach((organization) => {
                const item = window.AppUi.createElement('div', {
                    className: 'app-chip survey-editor-page__selected-organization-item',
                    text: organization.name
                });
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
            ensureDropdownController();
        }

        return { open, close, load, toggle, save: () => { close(); updateDisplay(); }, updateDisplay, remove, getSelected: state.getSelected, setSelected: state.setSelected, syncList, getItemName, resetAvailable: state.resetAvailable, bindDismissal };
    };
})();
