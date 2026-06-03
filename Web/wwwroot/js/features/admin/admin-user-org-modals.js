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

    if (typeof window.siteNotify === 'function') {
        window.siteNotify(normalizedMessage, type, {
            title: options.title,
            duration: options.duration ?? (type === 'error' ? 0 : 4500)
        });
        return;
    }

    window.alert(normalizedMessage);
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
    const messageElement = document.getElementById('message');
    messageElement.textContent = '';
    messageElement.className = '';

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
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
        },
        body: JSON.stringify(formData)
    })
    .then(response => response.json())
    .then(data => {
        messageElement.textContent = data.message;
        messageElement.className = data.success ? 'success-message' : 'error-message';
        if (data.success) {
            setModalVisibility('addUserModal', false);
            if (typeof window.handleAdminMutationSuccess === 'function') {
                window.handleAdminMutationSuccess({
                    message: data.message || 'Пользователь успешно добавлен.',
                    tabName: 'get_users',
                    fallbackUrl: '/users'
                });
                return;
            }

            window.siteNotify?.(data.message || 'Пользователь успешно добавлен.', 'success');
            navigateAdminTab("get_users", "/users");
        }
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

function setModalVisibility(target, isVisible) {
    const modal = typeof target === 'string' ? document.getElementById(target) : target;
    if (!modal) {
        return false;
    }

    if (isVisible) {
        if (window.showSiteModal) {
            window.showSiteModal(modal);
        } else {
            modal.style.display = 'flex';
        }
        return true;
    }

    if (window.hideSiteModal) {
        window.hideSiteModal(modal);
    } else {
        modal.style.display = 'none';
    }
    return true;
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
        setSingleOption(orgSelect, 'Загрузка организаций...');
        
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
        setModalVisibility(modal, true);

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
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
            },
            body: JSON.stringify(formData)
        });

        const result = await response.json();
        
        if (!response.ok) {
            throw new Error(result.message || 'Ошибка сервера');
        }

        setModalVisibility(modal, false);
        if (typeof window.handleAdminMutationSuccess === 'function') {
            await window.handleAdminMutationSuccess({
                message: result.message || 'Пользователь успешно обновлён.',
                tabName: 'get_users',
                fallbackUrl: '/users'
            });
            return;
        }

        window.siteNotify?.('Пользователь успешно обновлён.', 'success');
        navigateAdminTab("get_users", "/users");

    } catch (error) {
        console.error('Ошибка обновления:', error);
        const safeErrorMessage = typeof window.normalizeClientErrorMessage === 'function'
            ? window.normalizeClientErrorMessage(error.message)
            : error.message;
        const messageContainer = document.querySelector('#editUserModal .message');
        if (messageContainer) {
            messageContainer.textContent = safeErrorMessage;
            messageContainer.style.color = 'red';
        } else {
            showAdminToast(`Ошибка: ${safeErrorMessage}`);
        }
    }
}

// Функция закрытия модального окна
function closeModal2() {
    setModalVisibility('editUserModal', false);
}

function resetAddOrganizationForm() {
    const form = document.getElementById('organizationForm');
    const messageDiv = document.getElementById('message');

    if (form) {
        form.reset();
    }

    if (messageDiv) {
        messageDiv.textContent = '';
        messageDiv.className = 'organization-form__message';
        messageDiv.style.display = 'none';
    }
}

function openAddOrganizationModal() {
    resetAddOrganizationForm();
    const modal = document.getElementById('addOrganizationModal');
    if (!modal) {
        return;
    }

    setModalVisibility(modal, true);
}

async function createOrganization() {
    const form = document.getElementById('organizationForm');
    const messageDiv = document.getElementById('message');
    messageDiv.style.display = 'none';

    if (!document.getElementById('Name').value)
{
    showAdminToast('Введите название организации!');
    return;
}

    try {
        if (!ensureValidDateInput(form.DateBegin, 'Дата начала')) {
            return;
        }

        if (!ensureValidDateInput(form.DateEnd, 'Дата конца')) {
            return;
        }

        // 1. Собираем данные из формы
        const formData = {
            Name: form.Name.value,
            ShortName: (document.getElementById('ShortName')?.value || '').trim(),
            Email: form.organization_email.value,
            DateBegin: window.AppDate?.getInputIso(form.DateBegin) || '',
            DateEnd: window.AppDate?.getInputIso(form.DateEnd) || ''
        };

        if (formData.DateBegin && formData.DateEnd && (window.AppDate?.compare(formData.DateEnd, formData.DateBegin) ?? -1) < 0) {
            showAdminToast('Дата конца не может быть раньше даты начала.');
            window.AppDate?.focusInput?.(form.DateEnd);
            return;
        }

        // 2. Получаем CSRF-токен
        const token = document.querySelector('[name="__RequestVerificationToken"]').value;

        // 3. Отправляем на сервер
        const response = await fetch('/organizations/create', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify(formData)
        });

        // 4. Обрабатываем ответ
        const result = await response.json();
        
        // 5. Показываем результат
        messageDiv.textContent = result.success 
            ? 'Организация успешно создана!' 
            : 'Ошибка: ' + (result.error || 'Неизвестная ошибка');
        if (result.success) {
            closeOrganizationModal('addOrganizationModal');
            if (typeof window.handleAdminMutationSuccess === 'function') {
                await window.handleAdminMutationSuccess({
                    message: result.message || 'Организация успешно создана!',
                    tabName: 'get_organization',
                    fallbackUrl: '/organizations'
                });
                return;
            }

            navigateAdminTab("get_organization", "/organizations");
            showAdminToast('Организация успешно создана!', 'success');
        }
        
        messageDiv.className = result.success ? 'alert alert-success' : 'alert alert-danger';
        messageDiv.style.display = 'block';

    } catch (error) {
        const safeErrorMessage = typeof window.normalizeClientErrorMessage === 'function'
            ? window.normalizeClientErrorMessage(error.message)
            : error.message;
        messageDiv.textContent = 'Ошибка при отправке: ' + safeErrorMessage;
        messageDiv.className = 'alert alert-danger';
        messageDiv.style.display = 'block';
        console.error('Ошибка:', error);
    }
}
// СКРИПТ ДЛЯ РЕДАКТИРОВАНИЯ ОРГАНИЗАЦИЙ
        
function closeOrganizationModal(modalId) {
    setModalVisibility(modalId, false);
}

// Функция открытия модального окна редактирования
function openEditOrganizationModal(id, name, shortName, email, dateBegin, dateEnd) {
    document.getElementById('editOrganizationId').value = id;
    document.getElementById('organizationName').value = name || '';
    document.getElementById('organizationShortName').value = shortName || '';
    document.getElementById('organizationEmail').value = email || '';
    window.AppDate?.setInputValue('organizationDateBegin', dateBegin || '');
    window.AppDate?.setInputValue('organizationDateEnd', dateEnd || '');
    setModalVisibility('editOrganizationModal', true);
}

function openEditOrganizationModalFromTrigger(trigger) {
    const organizationId = Number.parseInt(trigger?.dataset?.organizationId || '', 10);

    if (!Number.isFinite(organizationId) || organizationId <= 0) {
        showAdminToast('Не найден идентификатор организации');
        return;
    }

    openEditOrganizationModal(
        organizationId,
        trigger?.dataset?.organizationName || '',
        trigger?.dataset?.organizationShortName || '',
        trigger?.dataset?.organizationEmail || '',
        trigger?.dataset?.organizationDateBegin || '',
        trigger?.dataset?.organizationDateEnd || '');
}

// Функция обновления организации с улучшенной обработкой данных
async function updateOrganization() {
    if (!document.getElementById('organizationName').value)
{
    showAdminToast('Введите название организации!');
    return;
}

    try {
        if (!ensureValidDateInput('organizationDateBegin', 'Дата начала')) {
            return;
        }

        if (!ensureValidDateInput('organizationDateEnd', 'Дата конца')) {
            return;
        }

        // 1. Получаем значения из формы
        const id = document.getElementById('editOrganizationId').value;
        const name = document.getElementById('organizationName').value.trim();
        const shortName = document.getElementById('organizationShortName').value.trim();
        const email = document.getElementById('organizationEmail').value.trim();
        const dateBegin = window.AppDate?.getInputIso('organizationDateBegin') || '';
        const dateEnd = window.AppDate?.getInputIso('organizationDateEnd') || '';

        if (dateBegin && dateEnd && (window.AppDate?.compare(dateEnd, dateBegin) ?? -1) < 0) {
            showAdminToast('Дата конца не может быть раньше даты начала.');
            window.AppDate?.focusInput?.('organizationDateEnd');
            return;
        }

        // 2. Подготовка данных в формате, ожидаемом сервером
        const organizationData = {
            Name: name,
            ShortName: shortName,
            Email: email || "",
            DateBegin: dateBegin || "",
            DateEnd: dateEnd || ""
        };

        // 3. Блокируем кнопку на время отправки
        const saveBtn = document.getElementById('saveOrganizationBtn');
        saveBtn.disabled = true;
        saveBtn.textContent = 'Сохранение...';

        // 4. Отправка данных с обработкой возможных ошибок сети
        let response;
        try {
            response = await fetch(`/organizations/${id}/update`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify(organizationData)
            });
        } catch (networkError) {
            throw new Error("Ошибка сети.");
        }

        // 5. Проверка ответа сервера
        if (!response.ok) {
            let errorText;
            try {
                errorText = await response.text();
            } catch (parseError) {
                errorText = `Ошибка сервера: ${response.status}`;
            }
            throw new Error(errorText);
        }

        // 6. Успешное завершение
        const successMessage = await response.text();
        closeOrganizationModal('editOrganizationModal');
        if (typeof window.handleAdminMutationSuccess === 'function') {
            await window.handleAdminMutationSuccess({
                message: successMessage || 'Организация успешно отредактирована!',
                tabName: 'get_organization',
                fallbackUrl: '/organizations'
            });
            return;
        }

        showAdminToast('Организация успешно отредактирована!', 'success');
        navigateAdminTab("get_organization", "/organizations");

    } catch (error) {
        console.error('Ошибка при обновлении организации:', error);
        
        // Показываем пользователю понятное сообщение об ошибке
        let errorMessage = error.message;
        if (error.message.includes("Некорректные данные организации")) {
            errorMessage = "Проверьте правильность заполнения всех полей";
        }
        
        showAdminToast(`Ошибка: ${errorMessage}`);
    } finally {
        const saveBtn = document.getElementById('saveOrganizationBtn');
        if (saveBtn) {
            saveBtn.disabled = false;
            saveBtn.textContent = 'Сохранить';
        }
    }
}
// Логика глаза пароля вынесена в ~/js/pages/admin-password-tools.js
        
