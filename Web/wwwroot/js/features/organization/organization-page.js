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

    function refreshOrganizationList() {
        if (typeof window.refreshAdminUi === 'function') {
            window.refreshAdminUi({
                tabName: 'get_organization',
                fallbackUrl: '/organizations',
                options: {
                    force: true,
                    historyMode: 'replace',
                    scrollMode: 'carry'
                }
            });
            return;
        }

        if (typeof window.refreshAdminTab === 'function') {
            window.refreshAdminTab('get_organization');
            return;
        }

        if (typeof window.handleTabClick === 'function') {
            window.handleTabClick('get_organization', {
                force: true,
                scrollMode: 'restore'
            });
            return;
        }

        window.AppScrollState?.saveCurrentPosition?.();
        window.location.assign('/organizations');
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

        return response.text();
    }

    async function createOrganization() {
        const form = byId('organizationForm');
        if (!form) return;

        const message = byId('message');
        const payload = {
            Name: byId('Name')?.value?.trim() || '',
            ShortName: byId('ShortName')?.value?.trim() || '',
            Email: byId('organization_email')?.value?.trim() || '',
            DateBegin: byId('DateBegin')?.value || '',
            DateEnd: byId('DateEnd')?.value || ''
        };

        if (!payload.Name || !payload.Email || !payload.DateBegin) {
            showMessage(message, 'Заполните все поля.', false);
            return;
        }

        try {
            const response = await fetch('/organizations/create', {
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

            closeOrganizationModal('addOrganizationModal');
            if (typeof window.handleAdminMutationSuccess === 'function') {
                await window.handleAdminMutationSuccess({
                    message: result.message || 'Организация добавлена.',
                    tabName: 'get_organization',
                    fallbackUrl: '/organizations'
                });
                return;
            }

            showMessage(message, result.message || 'Организация добавлена.', true);
            refreshOrganizationList();
        } catch (error) {
            showMessage(message, error.message || 'Ошибка добавления организации', false);
        }
    }

    function openEditOrganizationModal(id, name, shortName, email, dateBegin, dateEnd) {
        byId('editOrganizationId').value = id || '';
        byId('organizationName').value = name || '';
        byId('organizationShortName').value = shortName || '';
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

    function openEditOrganizationModalFromTrigger(trigger) {
        const organizationId = Number.parseInt(trigger?.dataset?.organizationId || '', 10);
        if (!Number.isFinite(organizationId) || organizationId <= 0) {
            alert('Не найден идентификатор организации');
            return;
        }

        openEditOrganizationModal(
            organizationId,
            trigger?.dataset?.organizationName || '',
            trigger?.dataset?.organizationShortName || '',
            trigger?.dataset?.organizationEmail || '',
            trigger?.dataset?.organizationDateBegin || '',
            trigger?.dataset?.organizationDateEnd || ''
        );
    }

    async function updateOrganization() {
        const id = byId('editOrganizationId')?.value;
        if (!id) {
            alert('Не найден идентификатор организации');
            return;
        }

        const name = byId('organizationName')?.value?.trim() || '';
        const shortName = byId('organizationShortName')?.value?.trim() || '';
        const email = byId('organizationEmail')?.value?.trim() || '';
        const dateBegin = byId('organizationDateBegin')?.value || '';
        const dateEnd = byId('organizationDateEnd')?.value || '';

        const payload = {
            Name: name,
            ShortName: shortName,
            Email: email,
            DateBegin: dateBegin,
            DateEnd: dateEnd
        };

        try {
            const successMessage = await submitOrganizationUpdate(id, payload);
            closeOrganizationModal('editOrganizationModal');
            if (typeof window.handleAdminMutationSuccess === 'function') {
                await window.handleAdminMutationSuccess({
                    message: successMessage || 'Организация успешно обновлена.',
                    tabName: 'get_organization',
                    fallbackUrl: '/organizations'
                });
                return;
            }

            refreshOrganizationList();
        } catch (error) {
            alert(error.message || 'Ошибка обновления организации');
        }
    }

    async function updateOrganizationPage(id) {
        const payload = {
            Name: byId('name')?.value?.trim() || '',
            ShortName: byId('short_name')?.value?.trim() || '',
            Email: byId('email')?.value?.trim() || '',
            DateBegin: byId('date_begin')?.value || '',
            DateEnd: byId('date_end')?.value || ''
        };

        try {
            const successMessage = await submitOrganizationUpdate(id, payload);
            if (typeof window.handleAdminMutationSuccess === 'function') {
                await window.handleAdminMutationSuccess({
                    message: successMessage || 'Организация успешно обновлена.',
                    tabName: 'get_organization',
                    fallbackUrl: '/organizations'
                });
                return;
            }

            refreshOrganizationList();
        } catch (error) {
            alert(error.message || 'Ошибка обновления организации');
        }
    }

    async function deleteOrganization(id) {
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

            const responseText = await response.text();
            if (!response.ok) {
                throw new Error(responseText || 'Ошибка удаления организации');
            }

            if (typeof window.handleAdminMutationSuccess === 'function') {
                await window.handleAdminMutationSuccess({
                    message: responseText || 'Организация успешно удалена.',
                    tabName: 'get_organization',
                    fallbackUrl: '/organizations'
                });
                return;
            }

            refreshOrganizationList();
        } catch (error) {
            alert(error.message || 'Ошибка удаления организации');
        }
    }

    window.closeOrganizationModal = closeOrganizationModal;
    window.openAddOrganizationModal = openAddOrganizationModal;
    window.createOrganization = createOrganization;
    window.openEditOrganizationModalFromTrigger = openEditOrganizationModalFromTrigger;
    window.updateOrganization = updateOrganization;
    window.updateOrganizationPage = updateOrganizationPage;
    window.deleteOrganization = deleteOrganization;
})();
