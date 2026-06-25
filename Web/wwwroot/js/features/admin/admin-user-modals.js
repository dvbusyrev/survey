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

    if (typeof window.refreshAdminTab === 'function') {
        window.refreshAdminTab(tabName, null, resolvedOptions);
        return;
    }

    if (typeof window.handleTabClick === 'function') {
        window.handleTabClick(tabName, {
            force: true,
            scrollMode: 'restore',
            ...resolvedOptions
        });
        return;
    }

    if (typeof handleTabClick === 'function') {
        handleTabClick(tabName, {
            force: true,
            scrollMode: 'restore',
            ...resolvedOptions
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

function ensureValidDateInput(target, label, options = {}) {
    const error = window.AppDate?.getInputError?.(target, { label, required: options.required }) || '';
    if (!error) {
        return true;
    }

    showAdminToast(error);
    window.AppDate?.focusInput?.(target);
    return false;
}

function submitFormAdd() {
    if (!document.getElementById('username').value){
        showAdminToast('Введите никнейм пользователя!');
        return;
    }

        if (!document.getElementById('password').value){
        showAdminToast('Введите пароль!');
        return;
    }

            if (!document.getElementById('userOrganization').value){
        showAdminToast('Выберите организацию пользователя!');
        return;
    }

                if (!document.getElementById('userRole').value){
        showAdminToast('Выберите роль пользователя!');
        return;
    }

    const dateBegin = window.AppDate?.getInputIso('dateBegin') || '';
    const dateEnd = window.AppDate?.getInputIso('dateEnd') || '';

    if (!ensureValidDateInput('dateBegin', 'Дата начала')) {
        return;
    }

    if (!ensureValidDateInput('dateEnd', 'Дата конца')) {
        return;
    }

    if (dateBegin && dateEnd && (window.AppDate?.compare(dateEnd, dateBegin) ?? -1) < 0) {
        showAdminToast('Дата конца не может быть раньше даты начала.');
        return;
    }

    const formData = {
        username: document.getElementById('username')?.value || '',
        password: document.getElementById('password')?.value || '',
        fullName: document.getElementById('fullName')?.value || '',
        email: document.getElementById('email_input')?.value || '', // Используем value, а не innerHTML
        organizationId: document.getElementById('userOrganization')?.value || '0',
        role: document.getElementById('userRole')?.value || 'user',
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
            showAdminToast(data.message || data.error || 'Не удалось добавить пользователя');
            return;
        }

        window.AppUi?.setModalVisibility('addUserModal', false);
        if (typeof window.handleAdminMutationSuccess === 'function') {
            window.handleAdminMutationSuccess({
                message: data.message || 'Пользователь успешно добавлен',
                tabName: 'get_users',
                fallbackUrl: '/users'
            });
            return;
        }

        window.AppUi?.notify?.(data.message || 'Пользователь успешно добавлен', 'success');
        navigateAdminTab("get_users", "/users");
    })
    .catch(error => {
        console.error("Ошибка:", error);
        showAdminToast('Ошибка соединения');
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
            throw new Error(responseText || 'Ошибка при удалении');
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
        showAdminToast(`Произошла ошибка: ${error.message}`);
    });
}

function deleteUserFromTrigger(trigger) {
    const id = Number.parseInt(trigger?.dataset?.userId || '', 10);
    const fullName = trigger?.dataset?.userFullName || '';

    if (!Number.isFinite(id) || id <= 0) {
        showAdminToast('Не найден идентификатор пользователя');
        return;
    }

    deleteUser(id, fullName);
}

function deleteUserFromModal() {
    const id = Number.parseInt(document.getElementById('deleteUserId')?.value || '', 10);
    const fullName = document.getElementById('deleteUserName')?.textContent?.trim() || '';

    if (!Number.isFinite(id) || id <= 0) {
        showAdminToast('Не найден идентификатор пользователя');
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
        if (!response.ok) throw new Error('Не удалось загрузить организации');
        
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
        setSingleOption(orgSelect, 'Ошибка загрузки');
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
        showAdminToast(`Ошибка: ${error.message}`);
    }
}

function openEditUserModalFromTrigger(trigger) {
    const userId = Number.parseInt(trigger?.dataset?.userId || '', 10);
    const organizationId = Number.parseInt(trigger?.dataset?.userOrganizationId || '', 10);

    if (!Number.isFinite(userId) || userId <= 0) {
        showAdminToast('Не найден идентификатор пользователя');
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
        if (!document.getElementById('editUsername').value)
    {
        showAdminToast('Введите никнейм пользователя!');
        return;
    }

            if (!document.getElementById('editUserOrganization').value)
    {
        showAdminToast('Выберите организацию пользователя!');
        return;
    }

            if (!document.getElementById('editUserRole').value)
    {
        showAdminToast('Выберите роль пользователя!');
        return;
    }

    try {
        // Получаем элементы
        const modal = getSafeElement('editUserModal');
        let messageContainer = modal.querySelector('.message');
        if (!messageContainer) {
            messageContainer = document.createElement('div');
            messageContainer.className = 'message';
            modal.querySelector('.modal-body').appendChild(messageContainer);
        }
        messageContainer.textContent = '';
        messageContainer.style.color = '';

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

        // Валидация
        if (!elements.username.value || !elements.organization.value) {
            throw new Error('Заполните все обязательные поля');
        }

        const dateBeginIso = window.AppDate?.getInputIso(elements.dateBegin) || '';
        const dateEndIso = window.AppDate?.getInputIso(elements.dateEnd) || '';

        if (!ensureValidDateInput(elements.dateBegin, 'Дата начала')) {
            return;
        }

        if (!ensureValidDateInput(elements.dateEnd, 'Дата конца')) {
            return;
        }

        if (elements.dateBegin.value && elements.dateEnd.value && (window.AppDate?.compare(dateEndIso, dateBeginIso) ?? -1) < 0) {
            throw new Error('Дата конца не может быть раньше даты начала.');
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
            throw new Error(result.message || 'Ошибка сервера');
        }

        window.AppUi?.setModalVisibility(modal, false);
        if (typeof window.handleAdminMutationSuccess === 'function') {
            await window.handleAdminMutationSuccess({
                message: result.message || 'Пользователь успешно обновлён',
                tabName: 'get_users',
                fallbackUrl: '/users'
            });
            return;
        }

        window.AppUi?.notify?.('Пользователь успешно обновлён', 'success');
        navigateAdminTab("get_users", "/users");

    } catch (error) {
        console.error('Ошибка обновления:', error);
        const safeErrorMessage = typeof window.normalizeClientErrorMessage === 'function'
            ? window.normalizeClientErrorMessage(error.message)
            : error.message;
        showAdminToast(`Ошибка: ${safeErrorMessage}`);
    }
}

// Функция закрытия модального окна
function closeModal2() {
    window.AppUi?.setModalVisibility('editUserModal', false);
}

document.dispatchEvent(new CustomEvent('admin:user-modal-ready'));
