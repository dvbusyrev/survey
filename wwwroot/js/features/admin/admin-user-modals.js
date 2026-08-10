function navigateAdminTab(tabName, fallbackUrl, options = {}) {
    const resolvedOptions = options && typeof options === 'object' ? options : {};

    if (typeof window.refreshAdminUi === 'function') {
        window.refreshAdminUi({
            tabName,
            fallbackUrl,
            options: resolvedOptions
        });
        return;
    }

    if (fallbackUrl) {
        window.AppScrollState?.saveCurrentPosition?.();
        window.location.assign(fallbackUrl);
    }
}

function showAdminToast(message, type = 'error', options = {}) {
    const normalizedMessage = String(message || '').trim();
    if (!normalizedMessage) {
        return;
    }

    window.AppUi.notify(normalizedMessage, type, {
        title: options.title,
        duration: options.duration ?? (type === 'error' ? 0 : 4500)
    });
}

var userDetailsFrame = window.__userDetailsFrame || null;

function closeUserDetailsModal() {
    userDetailsFrame?.hide?.();
}

function ensureUserDetailsModal() {
    if (userDetailsFrame?.modal?.isConnected) {
        return userDetailsFrame;
    }

    if (typeof window.createSiteModalFrame !== 'function') {
        throw new Error('Модуль модальных окон не загружен.');
    }

    userDetailsFrame = window.createSiteModalFrame({
        id: 'userDetailsModal',
        className: 'user-details-modal',
        title: 'Просмотр пользователя',
        bodyClassName: 'app-details-modal__body',
        footer: false,
        onClose: closeUserDetailsModal
    });
    document.body.appendChild(userDetailsFrame.modal);
    window.__userDetailsFrame = userDetailsFrame;
    return userDetailsFrame;
}

function createUserDetailsField(label, value) {
    const field = window.AppUi.createField({
        text: String(value || '').trim() || 'Не указано'
    });
    return window.AppUi.createFieldGroup({ label, field });
}

function openUserDetailsModalFromRow(row) {
    if (!(row instanceof Element)) {
        return;
    }

    try {
        const frame = ensureUserDetailsModal();
        frame.body.replaceChildren(
            createUserDetailsField('ФИО', row.dataset.userFullName),
            createUserDetailsField('Логин', row.dataset.userName),
            createUserDetailsField('Эл. почта', row.dataset.userEmail),
            createUserDetailsField('Организация', row.dataset.userOrganizationName),
            createUserDetailsField('Роль', row.dataset.userRoleName),
            createUserDetailsField('Дата начала', row.dataset.userDateBeginDisplay),
            createUserDetailsField('Дата конца', row.dataset.userDateEndDisplay)
        );
        frame.show();
    } catch (error) {
        showAdminToast(error.message || 'Не удалось открыть данные пользователя.');
    }
}

function mountUserRowViewer(page) {
    const viewer = window.AppUi?.mountRowViewer?.({
        root: page,
        rowSelector: '.users-table tbody tr[data-role="user-row"]',
        label: 'Смотреть',
        onOpen: openUserDetailsModalFromRow
    });

    return () => viewer?.destroy?.();
}

function ensureValidDateInput(target, label, options = {}) {
    const error = window.AppDate?.getInputError?.(target, { label, required: options.required }) || '';
    if (!error) {
        window.AppValidation?.clearFieldError?.(target);
        return true;
    }

    window.AppValidation?.setFieldError?.(target, error);
    showAdminToast(error);
    window.AppDate?.focusInput?.(target);
    return false;
}

function ensureUserEndDateNotPast(dateEnd, target) {
    if (!dateEnd || (window.AppDate?.compare(dateEnd, window.AppDate.todayIso()) ?? -1) >= 0) {
        window.AppValidation?.clearFieldError?.(target);
        return true;
    }

    const message = 'Дата конца не может быть раньше сегодняшней даты.';
    window.AppValidation?.setFieldError?.(target, message);
    showAdminToast(message);
    window.AppDate?.focusInput?.(target);
    return false;
}

function submitFormAdd() {
    const modal = document.getElementById('addUserModal');
    const validation = window.AppValidation?.validateRequiredFields?.(modal);
    if (validation && !validation.valid) {
        window.AppValidation?.notifyErrors?.(validation.errors);
        window.AppValidation?.focusFirstInvalid?.(validation);
        return;
    }

    const dateBegin = window.AppDate?.getInputIso('dateBegin') || '';
    const dateEnd = window.AppDate?.getInputIso('dateEnd') || '';

    if (!ensureValidDateInput('dateBegin', 'Дата начала', { required: true })) {
        return;
    }

    if (!ensureValidDateInput('dateEnd', 'Дата конца')) {
        return;
    }

    if (!ensureUserEndDateNotPast(dateEnd, 'dateEnd')) {
        return;
    }

    if (dateBegin && dateEnd && (window.AppDate?.compare(dateEnd, dateBegin) ?? -1) < 0) {
        const message = 'Дата конца не может быть раньше даты начала.';
        window.AppValidation?.setFieldError?.('dateEnd', message);
        showAdminToast(message);
        return;
    }

    const formData = {
        username: document.getElementById('username')?.value || '',
        password: document.getElementById('password')?.value || '',
        fullName: document.getElementById('fullName')?.value || '',
        email: document.getElementById('email_input')?.value || '', // Используем value, а не innerHTML
        organizationId: document.getElementById('userOrganization')?.value || '0',
        role: document.getElementById('userRole')?.value || '',
        dateBegin,
        dateEnd
    };

    fetch('/users/create', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': window.AppHttp?.getAntiforgeryToken() || ''
        },
        body: JSON.stringify(formData)
    })
    .then(response => response.json())
    .then(data => {
        if (!data.success) {
            showAdminToast(data.message || data.error || 'Не удалось создать пользователя.');
            return;
        }

        window.AppUi?.setModalVisibility('addUserModal', false);
        if (typeof window.handleAdminMutationSuccess === 'function') {
            window.handleAdminMutationSuccess({
                message: data.message || 'Пользователь успешно добавлен.',
                tabName: 'get_users',
                fallbackUrl: '/users'
            });
            return;
        }

        window.AppUi?.notify?.(data.message || 'Пользователь успешно добавлен.', 'success');
        navigateAdminTab("get_users", "/users");
    })
    .catch(error => {
        console.error("Ошибка:", error);
        showAdminToast('Сервер недоступен.');
    });
}

async function deleteUser(id, fullName) {
    const confirmed = await window.siteConfirm(`Вы уверены, что хотите удалить пользователя ${fullName || ''}?`, {
        title: "Удаление пользователя",
        confirmText: "Удалить",
        cancelText: "Отмена"
    });

    if (!confirmed) {
        return;
    }

    fetch(`/users/${id}/delete`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        }
    })
    .then(async response => {
        const responseText = await response.text();
        if (!response.ok) {
            let responseMessage = responseText;
            try {
                const payload = JSON.parse(responseText);
                responseMessage = payload?.message || payload?.error || responseMessage;
            } catch (parseError) {
                responseMessage = responseText;
            }
            throw new Error(responseMessage || 'Не удалось удалить пользователя.');
        }
        return responseText;
    })
    .then(result => {
        closeModal('deleteUserModal');
        if (typeof window.handleAdminMutationSuccess === 'function') {
            window.handleAdminMutationSuccess({
                message: result,
                tabName: 'get_users',
                fallbackUrl: '/users'
            });
            return;
        }

        showAdminToast(result, 'success');
        navigateAdminTab("get_users", "/users");
    })
    .catch(error => {
        console.error("Ошибка:", error);
        showAdminToast(error.message || 'Не удалось удалить пользователя.');
    });
}

function deleteUserFromTrigger(trigger) {
    const id = Number.parseInt(trigger?.dataset?.userId || '', 10);
    const fullName = trigger?.dataset?.userFullName || '';

    if (!Number.isFinite(id) || id <= 0) {
        showAdminToast('Не удалось определить пользователя.');
        return;
    }

    deleteUser(id, fullName);
}

function deleteUserFromModal() {
    const id = Number.parseInt(document.getElementById('deleteUserId')?.value || '', 10);
    const fullName = document.getElementById('deleteUserName')?.textContent?.trim() || '';

    if (!Number.isFinite(id) || id <= 0) {
        showAdminToast('Не удалось определить пользователя.');
        return;
    }

    deleteUser(id, fullName);
}


// Глобальное открытие addUserModal вынесено в ~/js/pages/admin-password-tools.js


// Вспомогательная функция для безопасного получения элементов
function getSafeElement(id) {
    const element = document.getElementById(id);
    if (!element) {
        console.error(`Element with ID ${id} not found`);
        throw new Error(`Элемент ${id} не найден`);
    }
    return element;
}

function setSingleOption(selectElement, text) {
    if (!selectElement) {
        return;
    }

    selectElement.innerHTML = '';
    const option = document.createElement('option');
    option.value = '';
    option.textContent = text;
    selectElement.appendChild(option);
}

// Функция загрузки организаций
async function loadOrganizations2(selectedOrgId = null) {
    const orgSelect = getSafeElement('editUserOrganization');

    try {
        setSingleOption(orgSelect, '');

        const response = await fetch('/organizations/data');
        if (!response.ok) throw new Error('Не удалось загрузить организации.');

        const organizations = await response.json();
        orgSelect.innerHTML = '';

        organizations.forEach(org => {
            const option = document.createElement('option');
            option.value = org.id;
            option.textContent = org.name;
            if (selectedOrgId && org.id == selectedOrgId) {
                option.selected = true;
            }
            orgSelect.appendChild(option);
        });

    } catch (error) {
        console.error('Ошибка загрузки организаций:', error);
        setSingleOption(orgSelect, 'Организации недоступны');
    }
}

// Функция открытия модального окна
async function openEditUserModal(id, fullName, username, email, orgId, role, dateBegin, dateEnd) {
    try {
        // Получаем элементы
        const userId = getSafeElement('editUserId');
        const fullNameEl = getSafeElement('editFullName');
        const usernameEl = getSafeElement('editUsername');
        const emailEl = getSafeElement('editEmail');
        const roleEl = getSafeElement('editUserRole');
        const dateBeginEl = getSafeElement('editDateBegin');
        const dateEndEl = getSafeElement('editDateEnd');
        const passwordEl = getSafeElement('editPassword');
        const modal = getSafeElement('editUserModal');
        window.AppValidation?.clearAll?.(modal);

        // Заполняем поля
        userId.value = id;
        fullNameEl.value = fullName || '';
        usernameEl.value = username || '';
        emailEl.value = email || '';
        roleEl.value = role || 'user';
        window.AppDate?.setInputValue(dateBeginEl, dateBegin || '');
        window.AppDate?.setInputValue(dateEndEl, dateEnd || '');
        passwordEl.value = '';

        // Загружаем организации
        await loadOrganizations2(orgId);

        // Показываем модальное окно
        window.AppUi?.setModalVisibility(modal, true);

    } catch (error) {
        console.error('Ошибка при открытии формы:', error);
        showAdminToast(error.message || 'Не удалось открыть редактирование пользователя.');
    }
}

function openEditUserModalFromTrigger(trigger) {
    const userId = Number.parseInt(trigger?.dataset?.userId || '', 10);
    const organizationId = Number.parseInt(trigger?.dataset?.userOrganizationId || '', 10);

    if (!Number.isFinite(userId) || userId <= 0) {
        showAdminToast('Не удалось определить пользователя.');
        return;
    }

    openEditUserModal(
        userId,
        trigger?.dataset?.userFullName || '',
        trigger?.dataset?.userName || '',
        trigger?.dataset?.userEmail || '',
        Number.isFinite(organizationId) ? organizationId : null,
        trigger?.dataset?.userRole || '',
        trigger?.dataset?.userDateBegin || '',
        trigger?.dataset?.userDateEnd || '');
}

// Функция обновления пользователя
async function updateUser() {
    const requiredValidation = window.AppValidation?.validateRequiredFields?.(
        document.getElementById('editUserModal')
    );
    if (requiredValidation && !requiredValidation.valid) {
        window.AppValidation?.notifyErrors?.(requiredValidation.errors);
        window.AppValidation?.focusFirstInvalid?.(requiredValidation);
        return;
    }

    try {
        // Получаем элементы
        const modal = getSafeElement('editUserModal');

        // Получаем значения
        const elements = {
            id: getSafeElement('editUserId'),
            fullName: getSafeElement('editFullName'),
            username: getSafeElement('editUsername'),
            email: getSafeElement('editEmail'),
            password: getSafeElement('editPassword'),
            organization: getSafeElement('editUserOrganization'),
            role: getSafeElement('editUserRole'),
            dateBegin: getSafeElement('editDateBegin'),
            dateEnd: getSafeElement('editDateEnd')
        };

        const dateBeginIso = window.AppDate?.getInputIso(elements.dateBegin) || '';
        const dateEndIso = window.AppDate?.getInputIso(elements.dateEnd) || '';

        if (!ensureValidDateInput(elements.dateBegin, 'Дата начала', { required: true })) {
            return;
        }

        if (!ensureValidDateInput(elements.dateEnd, 'Дата конца')) {
            return;
        }

        if (!ensureUserEndDateNotPast(dateEndIso, elements.dateEnd)) {
            return;
        }

        if (elements.dateBegin.value && elements.dateEnd.value && (window.AppDate?.compare(dateEndIso, dateBeginIso) ?? -1) < 0) {
            const message = 'Дата конца не может быть раньше даты начала.';
            window.AppValidation?.setFieldError?.(elements.dateEnd, message);
            showAdminToast(message);
            return;
        }

        // Формируем данные
        const formData = {
            username: elements.username.value,
            password: elements.password.value || 'keep_original',
            fullName: elements.fullName.value,
            email: elements.email.value,
            organizationId: elements.organization.value,
            role: elements.role.value,
            dateBegin: dateBeginIso,
            dateEnd: dateEndIso
        };

        // Отправляем запрос
        const response = await fetch(`/users/${elements.id.value}/update`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': window.AppHttp?.getAntiforgeryToken() || ''
            },
            body: JSON.stringify(formData)
        });

        const result = await response.json();

        if (!response.ok) {
            throw new Error(result.message || 'Не удалось обновить пользователя.');
        }

        window.AppUi?.setModalVisibility(modal, false);
        if (typeof window.handleAdminMutationSuccess === 'function') {
            await window.handleAdminMutationSuccess({
                message: result.message || 'Пользователь успешно обновлён.',
                tabName: 'get_users',
                fallbackUrl: '/users'
            });
            return;
        }

        window.AppUi?.notify?.('Пользователь успешно обновлён.', 'success');
        navigateAdminTab("get_users", "/users");

    } catch (error) {
        console.error('Ошибка обновления:', error);
        const safeErrorMessage = typeof window.normalizeClientErrorMessage === 'function'
            ? window.normalizeClientErrorMessage(error.message)
            : error.message;
        showAdminToast(safeErrorMessage);
    }
}

// Функция закрытия модального окна
function closeModal2() {
    window.AppUi?.setModalVisibility('editUserModal', false);
}

if (window.AppPageLifecycle?.register) {
    window.AppPageLifecycle.register(
        'admin-user-row-viewer',
        '.app-page[data-page="users-list"], .app-page[data-page="users-archive"]',
        mountUserRowViewer
    );
} else {
    document.querySelectorAll('.app-page[data-page="users-list"], .app-page[data-page="users-archive"]')
        .forEach(mountUserRowViewer);
}

document.dispatchEvent(new CustomEvent('admin:user-modal-ready'));
