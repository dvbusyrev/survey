(function () {
    let organizationDetailsFrame = window.__organizationDetailsFrame || null;
    let organizationDeletePending = false;

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
            window.AppValidation?.clearFieldError?.(target);
            return true;
        }

        window.AppValidation?.setFieldError?.(target, error);
        showOrganizationToast(error);
        window.AppDate?.focusInput?.(target);
        return false;
    }

    function ensureOrganizationPeriodValid(startTarget, endTarget) {
        const error = window.AppDate?.getPeriodError?.(startTarget, endTarget);
        if (!error) {
            return true;
        }

        window.AppValidation?.setFieldError?.(error.target, error.message);
        showOrganizationToast(error.message);
        window.AppDate?.focusInput?.(error.target);
        return false;
    }

    function closeOrganizationModal(modalId) {
        const modal = byId(modalId);
        if (modal) {
            window.AppUi?.setModalVisibility(modal, false);
        }
    }

    function closeOrganizationDetailsModal() {
        organizationDetailsFrame?.hide?.();
    }

    function ensureOrganizationDetailsModal() {
        if (organizationDetailsFrame?.modal?.isConnected) {
            return organizationDetailsFrame;
        }

        if (typeof window.createSiteModalFrame !== 'function') {
            throw new Error('Модуль модальных окон не загружен.');
        }

        organizationDetailsFrame = window.createSiteModalFrame({
            id: 'organizationDetailsModal',
            className: 'organization-details-modal',
            title: 'Просмотр организации',
            bodyClassName: 'app-details-modal__body',
            footer: false,
            onClose: closeOrganizationDetailsModal
        });
        document.body.appendChild(organizationDetailsFrame.modal);
        window.__organizationDetailsFrame = organizationDetailsFrame;
        return organizationDetailsFrame;
    }

    function createOrganizationDetailsField(label, value) {
        const field = window.AppUi.createField({
            text: String(value || '').trim() || 'Не указано'
        });
        return window.AppUi.createFieldGroup({ label, field });
    }

    function openOrganizationDetailsModalFromRow(row) {
        if (!(row instanceof Element)) {
            return;
        }

        try {
            const frame = ensureOrganizationDetailsModal();
            frame.body.replaceChildren(
                createOrganizationDetailsField('Название организации', row.dataset.organizationName),
                createOrganizationDetailsField('Краткое название', row.dataset.organizationShortName),
                createOrganizationDetailsField('Эл. почта', row.dataset.organizationEmail),
                createOrganizationDetailsField('Дата начала', row.dataset.organizationDateBeginDisplay),
                createOrganizationDetailsField('Дата конца', row.dataset.organizationDateEndDisplay)
            );
            frame.show();
        } catch (error) {
            showOrganizationToast(error.message || 'Не удалось открыть данные организации.');
        }
    }

    function mountOrganizationRowViewer(page) {
        const viewer = window.AppUi?.mountRowViewer?.({
            root: page,
            rowSelector: '.organization-table tbody tr[data-role="organization-row"]',
            label: 'Смотреть',
            onOpen: openOrganizationDetailsModalFromRow
        });

        return () => viewer?.destroy?.();
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
            window.AppValidation?.clearAll?.(form);
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
            const errorText = window.AppHttp?.readResponseMessage
                ? await window.AppHttp.readResponseMessage(response, 'Не удалось обновить организацию.')
                : await response.text();
            throw new Error(errorText || 'Не удалось обновить организацию.');
        }

        return response.text();
    }

    async function createOrganization() {
        const form = byId('organizationForm');
        if (!form) return;

        const validation = window.AppValidation?.validateRequiredFields?.(form);
        if (validation && !validation.valid) {
            window.AppValidation?.notifyErrors?.(validation.errors);
            window.AppValidation?.focusFirstInvalid?.(validation);
            return;
        }

        if (!ensureOrganizationPeriodValid('DateBegin', 'DateEnd')) {
            return;
        }

        if (!ensureValidDateInput('DateBegin', 'Дата начала', { required: true })) {
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
                throw new Error(result.message || 'Не удалось создать организацию.');
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
            showOrganizationToast(error.message || 'Не удалось создать организацию.');
        }
    }

    function openEditOrganizationModal(id, name, shortName, email, dateBegin, dateEnd) {
        window.AppValidation?.clearAll?.(byId('editOrganizationModal'));
        byId('editOrganizationId').value = id || '';
        byId('organizationName').value = name || '';
        byId('organizationShortName').value = shortName || '';
        byId('organizationEmail').value = email || '';
        window.AppDate?.setInputValue('organizationDateBegin', dateBegin || '');
        window.AppDate?.setInputValue('organizationDateEnd', dateEnd || '');
        window.AppDate?.bindPeriodBounds?.('organizationDateBegin', 'organizationDateEnd');

        const modal = byId('editOrganizationModal');
        if (modal) {
            window.AppUi?.setModalVisibility(modal, true);
        }
    }

    function openEditOrganizationModalFromTrigger(trigger) {
        const organizationId = Number.parseInt(trigger?.dataset?.organizationId || '', 10);
        if (!Number.isFinite(organizationId) || organizationId <= 0) {
            window.AppUi?.notify?.('Не удалось определить организацию.', 'error');
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
            window.AppUi?.notify?.('Не удалось определить организацию.', 'error');
            return;
        }

        const name = byId('organizationName')?.value?.trim() || '';
        const shortName = byId('organizationShortName')?.value?.trim() || '';
        const email = byId('organizationEmail')?.value?.trim() || '';

        const validation = window.AppValidation?.validateRequiredFields?.(byId('editOrganizationModal'));
        if (validation && !validation.valid) {
            window.AppValidation?.notifyErrors?.(validation.errors);
            window.AppValidation?.focusFirstInvalid?.(validation);
            return;
        }

        if (!ensureOrganizationPeriodValid('organizationDateBegin', 'organizationDateEnd')) {
            return;
        }

        if (!ensureValidDateInput('organizationDateBegin', 'Дата начала', { required: true })) {
            return;
        }

        if (!ensureValidDateInput('organizationDateEnd', 'Дата конца')) {
            return;
        }

        const dateBegin = window.AppDate?.getInputIso('organizationDateBegin') || '';
        const dateEnd = window.AppDate?.getInputIso('organizationDateEnd') || '';

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
            window.AppUi?.notify?.(error.message || 'Не удалось обновить организацию.', 'error');
        }
    }

    async function updateOrganizationPage(id) {
        const validation = window.AppValidation?.validateRequiredFields?.(
            document.querySelector('[data-page="organization-update"]')
        );
        if (validation && !validation.valid) {
            window.AppValidation?.notifyErrors?.(validation.errors);
            window.AppValidation?.focusFirstInvalid?.(validation);
            return;
        }

        if (!ensureOrganizationPeriodValid('date_begin', 'date_end')) {
            return;
        }

        if (!ensureValidDateInput('date_begin', 'Дата начала', { required: true })) {
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
            window.AppUi?.notify?.(error.message || 'Не удалось обновить организацию.', 'error');
        }
    }

    async function deleteOrganization(id) {
        if (!id || organizationDeletePending) return;

        organizationDeletePending = true;
        try {
            if (!await window.siteConfirm('Удалить организацию?', {
                title: 'Удаление организации',
                confirmText: 'Удалить',
                cancelText: 'Отмена'
            })) return;

            const response = await fetch(`/organizations/${id}/delete`, {
                method: 'POST',
                headers: {
                    ...(antiforgeryToken() ? { RequestVerificationToken: antiforgeryToken() } : {})
                }
            });

            const responseText = window.AppHttp?.readResponseMessage
                ? await window.AppHttp.readResponseMessage(response, 'Не удалось удалить организацию.')
                : await response.text();
            if (!response.ok) {
                throw new Error(responseText || 'Не удалось удалить организацию.');
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
            window.AppUi?.notify?.(error.message || 'Не удалось удалить организацию.', 'error');
        } finally {
            organizationDeletePending = false;
        }
    }

    window.closeOrganizationModal = closeOrganizationModal;
    window.openAddOrganizationModal = openAddOrganizationModal;
    window.createOrganization = createOrganization;
    window.openEditOrganizationModalFromTrigger = openEditOrganizationModalFromTrigger;
    window.updateOrganization = updateOrganization;
    window.updateOrganizationPage = updateOrganizationPage;
    window.deleteOrganization = deleteOrganization;

    if (window.AppPageLifecycle?.register) {
        window.AppPageLifecycle.register(
            'organization-row-viewer',
            '.app-page[data-page="organization-list"], .app-page[data-page="organization-archive"]',
            mountOrganizationRowViewer
        );
    } else {
        document.querySelectorAll('.app-page[data-page="organization-list"], .app-page[data-page="organization-archive"]')
            .forEach(mountOrganizationRowViewer);
    }

    window.AppDate?.bindPeriodBounds?.('DateBegin', 'DateEnd');
    window.AppDate?.bindPeriodBounds?.('organizationDateBegin', 'organizationDateEnd');
    window.AppDate?.bindPeriodBounds?.('date_begin', 'date_end');
})();
