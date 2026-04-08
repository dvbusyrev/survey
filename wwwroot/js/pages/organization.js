(function () {
    function byId(id) {
        return document.getElementById(id);
    }

    function antiforgeryToken() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    }

    function showMessage(element, text, isSuccess) {
        if (!element) return;
        element.style.display = 'block';
        element.textContent = text || '';
        element.className = isSuccess ? 'success-message' : 'error-message';
    }

    function closeOrganizationModal(modalId) {
        const modal = byId(modalId);
        if (modal) {
            if (typeof window.hideSiteModal === 'function') {
                window.hideSiteModal(modal);
            } else {
                modal.style.display = 'none';
            }
        }
    }

    function resetAddOrganizationForm() {
        const form = byId('organizationForm');
        const message = byId('message');
        if (form) {
            form.reset();
        }

        if (message) {
            message.textContent = '';
            message.className = 'organization-form__message';
            message.style.display = 'none';
        }
    }

    function openAddOrganizationModal() {
        resetAddOrganizationForm();
        const modal = byId('addOrganizationModal');
        if (modal) {
            if (typeof window.showSiteModal === 'function') {
                window.showSiteModal(modal);
            } else {
                modal.style.display = 'flex';
            }
        }
    }

    async function submitOrganizationUpdate(id, payload) {
        const response = await fetch(`/organizations/${id}/update`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                ...(antiforgeryToken() ? { RequestVerificationToken: antiforgeryToken() } : {})
            },
            body: JSON.stringify(payload)
        });

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(errorText || 'Ошибка обновления организации');
        }
    }

    async function add_organization_bd() {
        const form = byId('organizationForm');
        if (!form) return;

        const message = byId('message');
        const payload = {
            Name: byId('Name')?.value?.trim() || '',
            Email: byId('organization_email')?.value?.trim() || '',
            DateBegin: byId('DateBegin')?.value || '',
            DateEnd: byId('DateEnd')?.value || ''
        };

        if (!payload.Name || !payload.Email || !payload.DateBegin || !payload.DateEnd) {
            showMessage(message, 'Заполните все поля.', false);
            return;
        }

        try {
            const response = await fetch('/organizations/create/save', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    ...(antiforgeryToken() ? { RequestVerificationToken: antiforgeryToken() } : {})
                },
                body: JSON.stringify(payload)
            });

            const result = await response.json();
            if (!response.ok || !result.success) {
                throw new Error(result.message || 'Ошибка добавления организации');
            }

            showMessage(message, result.message || 'Организация добавлена.', true);
            closeOrganizationModal('addOrganizationModal');
            if (typeof window.handleTabClick === 'function') {
                window.handleTabClick('get_organization');
            } else {
                window.location.assign('/organizations');
            }
        } catch (error) {
            showMessage(message, error.message || 'Ошибка добавления организации', false);
        }
    }

    function openEditOrganizationModal(id, name, email, dateBegin, dateEnd) {
        byId('editOrganizationId').value = id || '';
        byId('organizationName').value = name || '';
        byId('organizationEmail').value = email || '';
        byId('organizationDateBegin').value = dateBegin || '';
        byId('organizationDateEnd').value = dateEnd || '';

        const modal = byId('editOrganizationModal');
        if (modal) {
            if (typeof window.showSiteModal === 'function') {
                window.showSiteModal(modal);
            } else {
                modal.style.display = 'flex';
            }
        }
    }

    async function updateOrganization() {
        const id = byId('editOrganizationId')?.value;
        if (!id) {
            alert('Не найден идентификатор организации');
            return;
        }

        const name = byId('organizationName')?.value?.trim() || '';
        const email = byId('organizationEmail')?.value?.trim() || '';
        const dateBegin = byId('organizationDateBegin')?.value || '';
        const dateEnd = byId('organizationDateEnd')?.value || '';

        const payload = {
            Name: name,
            Email: email,
            DateBegin: dateBegin,
            DateEnd: dateEnd
        };

        try {
            await submitOrganizationUpdate(id, payload);
            closeOrganizationModal('editOrganizationModal');
            if (typeof window.handleTabClick === 'function') {
                window.handleTabClick('get_organization');
            } else {
                window.location.assign('/organizations');
            }
        } catch (error) {
            alert(error.message || 'Ошибка обновления организации');
        }
    }

    async function updateOrganizationPage(id) {
        const payload = {
            Name: byId('name')?.value?.trim() || '',
            Email: byId('email')?.value?.trim() || '',
            DateBegin: byId('date_begin')?.value || '',
            DateEnd: byId('date_end')?.value || ''
        };

        try {
            await submitOrganizationUpdate(id, payload);
            window.location.assign('/organizations');
        } catch (error) {
            alert(error.message || 'Ошибка обновления организации');
        }
    }

    async function delete_organization(id) {
        if (!id) return;
        if (!await window.siteConfirm('Удалить организацию?', {
            title: 'Удаление организации',
            confirmText: 'Удалить',
            cancelText: 'Отмена'
        })) return;

        try {
            const response = await fetch(`/organizations/${id}/delete`, {
                method: 'POST',
                headers: {
                    ...(antiforgeryToken() ? { RequestVerificationToken: antiforgeryToken() } : {})
                }
            });

            if (!response.ok) {
                const text = await response.text();
                throw new Error(text || 'Ошибка удаления организации');
            }

            if (typeof window.handleTabClick === 'function') {
                window.handleTabClick('get_organization');
            }
        } catch (error) {
            alert(error.message || 'Ошибка удаления организации');
        }
    }

    function archive_list_organizations() {
        if (typeof window.handleTabClick === 'function') {
            window.handleTabClick('archive_list_organizations');
        }
    }

    window.closeOrganizationModal = closeOrganizationModal;
    window.openAddOrganizationModal = openAddOrganizationModal;
    window.add_organization_bd = add_organization_bd;
    window.openEditOrganizationModal = openEditOrganizationModal;
    window.updateOrganization = updateOrganization;
    window.updateOrganizationPage = updateOrganizationPage;
    window.delete_organization = delete_organization;
    window.archive_list_organizations = archive_list_organizations;
})();
