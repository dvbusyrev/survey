(function () {
    function byId(id) {
        return document.getElementById(id);
    }

    function antiforgeryToken() {
        return window.AppHttp?.getAntiforgeryToken() || '';
    }

    function showOrganizationToast(text, isSuccess = false) {
        const message = text || '';
        if (message) {
            window.AppUi?.notify?.(message, isSuccess ? 'success' : 'error');
        }
    }

    function ensureValidDateInput(target, label, options = {}) {
        const error = window.AppDate?.getInputError?.(target, { label, required: options.required }) || '';
        if (!error) {
            return true;
        }

        showOrganizationToast(error);
        window.AppDate?.focusInput?.(target);
        return false;
    }

    function closeOrganizationModal(modalId) {
        const modal = byId(modalId);
        if (modal) {
            window.AppUi?.setModalVisibility(modal, false);
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

        window.AppScrollState?.saveCurrentPosition?.();
        window.location.assign('/organizations');
    }

    function resetAddOrganizationForm() {
        const form = byId('organizationForm');
        if (form) {
            form.reset();
        }
    }

    function openAddOrganizationModal() {
        resetAddOrganizationForm();
        const modal = byId('addOrganizationModal');
        if (modal) {
            window.AppUi?.setModalVisibility(modal, true);
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

        if (!ensureValidDateInput('DateBegin', 'Дата начала')) {
            return;
        }

        if (!ensureValidDateInput('DateEnd', 'Дата конца')) {
            return;
        }

        const payload = {
            Name: byId('Name')?.value?.trim() || '',
            ShortName: byId('ShortName')?.value?.trim() || '',
            Email: byId('organization_email')?.value?.trim() || '',
            DateBegin: window.AppDate?.getInputIso('DateBegin') || '',
            DateEnd: window.AppDate?.getInputIso('DateEnd') || ''
        };

        if (!payload.Name) {
            showOrganizationToast('Введите название организации!');
            return;
        }

        if (payload.DateBegin && payload.DateEnd && (window.AppDate?.compare(payload.DateEnd, payload.DateBegin) ?? -1) < 0) {
            showOrganizationToast('Дата конца не может быть раньше даты начала.');
            window.AppDate?.focusInput?.('DateEnd');
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

            showOrganizationToast(result.message || 'Организация добавлена.', true);
            refreshOrganizationList();
        } catch (error) {
            showOrganizationToast(error.message || 'Ошибка добавления организации');
        }
    }

    function openEditOrganizationModal(id, name, shortName, email, dateBegin, dateEnd) {
        byId('editOrganizationId').value = id || '';
        byId('organizationName').value = name || '';
        byId('organizationShortName').value = shortName || '';
        byId('organizationEmail').value = email || '';
        window.AppDate?.setInputValue('organizationDateBegin', dateBegin || '');
        window.AppDate?.setInputValue('organizationDateEnd', dateEnd || '');

        const modal = byId('editOrganizationModal');
        if (modal) {
            window.AppUi?.setModalVisibility(modal, true);
        }
    }

    function openEditOrganizationModalFromTrigger(trigger) {
        const organizationId = Number.parseInt(trigger?.dataset?.organizationId || '', 10);
        if (!Number.isFinite(organizationId) || organizationId <= 0) {
            window.AppUi?.notify?.('Не найден идентификатор организации', 'error');
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
            window.AppUi?.notify?.('Не найден идентификатор организации', 'error');
            return;
        }

        const name = byId('organizationName')?.value?.trim() || '';
        const shortName = byId('organizationShortName')?.value?.trim() || '';
        const email = byId('organizationEmail')?.value?.trim() || '';

        if (!name) {
            window.AppUi?.notify?.('Введите название организации!', 'error');
            return;
        }

        if (!ensureValidDateInput('organizationDateBegin', 'Дата начала')) {
            return;
        }

        if (!ensureValidDateInput('organizationDateEnd', 'Дата конца')) {
            return;
        }

        const dateBegin = window.AppDate?.getInputIso('organizationDateBegin') || '';
        const dateEnd = window.AppDate?.getInputIso('organizationDateEnd') || '';

        if (dateBegin && dateEnd && (window.AppDate?.compare(dateEnd, dateBegin) ?? -1) < 0) {
            window.AppUi?.notify?.('Дата конца не может быть раньше даты начала.', 'error');
            window.AppDate?.focusInput?.('organizationDateEnd');
            return;
        }

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
                    message: successMessage || 'Организация успешно обновлена',
                    tabName: 'get_organization',
                    fallbackUrl: '/organizations'
                });
                return;
            }

            refreshOrganizationList();
        } catch (error) {
            window.AppUi?.notify?.(error.message || 'Ошибка обновления организации', 'error');
        }
    }

    async function updateOrganizationPage(id) {
        if (!ensureValidDateInput('date_begin', 'Дата начала')) {
            return;
        }

        if (!ensureValidDateInput('date_end', 'Дата конца')) {
            return;
        }

        const payload = {
            Name: byId('name')?.value?.trim() || '',
            ShortName: byId('short_name')?.value?.trim() || '',
            Email: byId('email')?.value?.trim() || '',
            DateBegin: window.AppDate?.getInputIso('date_begin') || '',
            DateEnd: window.AppDate?.getInputIso('date_end') || ''
        };

        if (!payload.Name) {
            window.AppUi?.notify?.('Введите название организации!', 'error');
            return;
        }

        if (payload.DateBegin && payload.DateEnd && (window.AppDate?.compare(payload.DateEnd, payload.DateBegin) ?? -1) < 0) {
            window.AppUi?.notify?.('Дата конца не может быть раньше даты начала.', 'error');
            window.AppDate?.focusInput?.('date_end');
            return;
        }

        try {
            const successMessage = await submitOrganizationUpdate(id, payload);
            if (typeof window.handleAdminMutationSuccess === 'function') {
                await window.handleAdminMutationSuccess({
                    message: successMessage || 'Организация успешно обновлена',
                    tabName: 'get_organization',
                    fallbackUrl: '/organizations'
                });
                return;
            }

            refreshOrganizationList();
        } catch (error) {
            window.AppUi?.notify?.(error.message || 'Ошибка обновления организации', 'error');
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
                    message: responseText || 'Организация успешно удалена',
                    tabName: 'get_organization',
                    fallbackUrl: '/organizations'
                });
                return;
            }

            refreshOrganizationList();
        } catch (error) {
            window.AppUi?.notify?.(error.message || 'Ошибка удаления организации', 'error');
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
