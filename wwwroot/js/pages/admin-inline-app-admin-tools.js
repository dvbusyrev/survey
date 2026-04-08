function navigateAdminTab(tabName, fallbackUrl) {
    if (typeof window.handleTabClick === 'function') {
        window.handleTabClick(tabName);
        return;
    }

    if (typeof handleTabClick === 'function') {
        handleTabClick(tabName);
        return;
    }

    if (fallbackUrl) {
        window.location.assign(fallbackUrl);
    }
}

function submitFormAdd() {
    const messageElement = document.getElementById('message');
    messageElement.textContent = '';
    messageElement.className = '';

    if (!document.getElementById('username').value){
        alert("Введите никнейм пользователя!");
        return;
    }

        if (!document.getElementById('password').value){
        alert("Введите пароль!");
        return;
    }

            if (!document.getElementById('organization').value){
        alert("Выберите организацию пользователя!");
        return;
    }

                if (!document.getElementById('role').value){
        alert("Выберите роль пользователя!");
        return;
    }

    const formData = {
        username: document.getElementById('username')?.value || '',
        password: document.getElementById('password')?.value || '',
        fullName: document.getElementById('fullName')?.value || '',
        email: document.getElementById('email_input')?.value || '', // Используем value, а не innerHTML
        organizationId: document.getElementById('organization')?.value || '0',
        role: document.getElementById('role')?.value || 'user'
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
        if (data.success) {alert("Пользователь успешно добавлен!"); navigateAdminTab("get_users", "/get_users");}
    })
    .catch(error => {
        console.error("Ошибка:", error);
        messageElement.textContent = 'Ошибка соединения';
        messageElement.className = 'error-message';
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
    .then(response => {
        if (!response.ok) {
            throw new Error('Ошибка при удалении');
        }
        return response.text();
    })
    .then(result => {
        alert(result); // Показываем ответ сервера
        closeModal('deleteUserModal');
        navigateAdminTab("get_users", "/get_users");
    })
    .catch(error => {
        console.error("Ошибка:", error);
        alert("Произошла ошибка: " + error.message);
    });
}

function deleteUserFromTrigger(trigger) {
    const id = Number.parseInt(trigger?.dataset?.userId || '', 10);
    const fullName = trigger?.dataset?.userFullName || '';

    if (!Number.isFinite(id) || id <= 0) {
        alert('Не найден идентификатор пользователя');
        return;
    }

    deleteUser(id, fullName);
}

function deleteUserFromModal() {
    const id = Number.parseInt(document.getElementById('deleteUserId')?.value || '', 10);
    const fullName = document.getElementById('deleteUserName')?.textContent?.trim() || '';

    if (!Number.isFinite(id) || id <= 0) {
        alert('Не найден идентификатор пользователя');
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

// Функция загрузки организаций
async function loadOrganizations2(selectedOrgId = null) {
    const orgSelect = getSafeElement('editOrganization');
    
    try {
        orgSelect.innerHTML = '<option value="">Загрузка организаций...</option>';
        
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
        orgSelect.innerHTML = '<option value="">Ошибка загрузки</option>';
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
        const roleEl = getSafeElement('editRole');
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
        dateBeginEl.value = dateBegin?.split('T')[0] || '';
        dateEndEl.value = dateEnd?.split('T')[0] || '';
        passwordEl.value = '';

        // Загружаем организации
        await loadOrganizations2(orgId);

        // Показываем модальное окно
        if (window.showSiteModal) {
            window.showSiteModal(modal);
        } else {
            modal.style.display = 'flex';
        }

    } catch (error) {
        console.error('Ошибка при открытии формы:', error);
        alert('Ошибка: ' + error.message);
    }
}

function openEditUserModalFromTrigger(trigger) {
    const userId = Number.parseInt(trigger?.dataset?.userId || '', 10);
    const organizationId = Number.parseInt(trigger?.dataset?.userOrganizationId || '', 10);

    if (!Number.isFinite(userId) || userId <= 0) {
        alert('Не найден идентификатор пользователя');
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
        alert("Введите никнейм пользователя!");
        return;
    }

            if (!document.getElementById('editOrganization').value)
    {
        alert("Выберите организацию пользователя!");
        return;
    }

            if (!document.getElementById('editRole').value)
    {
        alert("Выберите роль пользователя!");
        return;
    }

    try {
        // Получаем элементы
        const modal = getSafeElement('editUserModal');
        const messageContainer = document.createElement('div');
        messageContainer.className = 'message';
        modal.querySelector('.modal-body').appendChild(messageContainer);

        // Получаем значения
        const elements = {
            id: getSafeElement('editUserId'),
            fullName: getSafeElement('editFullName'),
            username: getSafeElement('editUsername'),
            email: getSafeElement('editEmail'),
            password: getSafeElement('editPassword'),
            organization: getSafeElement('editOrganization'),
            role: getSafeElement('editRole'),
            dateBegin: getSafeElement('editDateBegin'),
            dateEnd: getSafeElement('editDateEnd')
        };

        // Валидация
        if (!elements.username.value || !elements.organization.value) {
            throw new Error('Заполните все обязательные поля');
        }

        // Формируем данные
        const formData = {
            username: elements.username.value,
            password: elements.password.value || 'keep_original',
            fullName: elements.fullName.value,
            email: elements.email.value,
            organizationId: elements.organization.value,
            role: elements.role.value,
            dateBegin: elements.dateBegin.value,
            dateEnd: elements.dateEnd.value
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

        alert("Пользователь успешно обновлён!");
        navigateAdminTab("get_users", "/get_users");

    } catch (error) {
        console.error('Ошибка обновления:', error);
        const messageContainer = document.querySelector('#editUserModal .message');
        if (messageContainer) {
            messageContainer.textContent = error.message;
            messageContainer.style.color = 'red';
        } else {
            alert('Ошибка: ' + error.message);
        }
    }
}

// Функция закрытия модального окна
function closeModal2() {
    const modal = document.getElementById('editUserModal');
    if (modal) {
        if (window.hideSiteModal) {
            window.hideSiteModal(modal);
        } else {
            modal.style.display = 'none';
        }
    }
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

    if (window.showSiteModal) {
        window.showSiteModal(modal);
    } else {
        modal.style.display = 'flex';
    }
}

async function createOrganization() {
    const form = document.getElementById('organizationForm');
    const messageDiv = document.getElementById('message');
    messageDiv.style.display = 'none';

    if (!document.getElementById('Name').value)
{
    alert("Введите название организации!");
    return;
}


    try {
        // 1. Собираем данные из формы
        const formData = {
            Name: form.Name.value,
            Email: form.organization_email.value,
            DateBegin: form.DateBegin.value,
            DateEnd: form.DateEnd.value
        };

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
            navigateAdminTab("get_organization", "/organizations");
            alert("Организация успешно создана!");
        }
        
        messageDiv.className = result.success ? 'alert alert-success' : 'alert alert-danger';
        messageDiv.style.display = 'block';

    } catch (error) {
        messageDiv.textContent = 'Ошибка при отправке: ' + error.message;
        messageDiv.className = 'alert alert-danger';
        messageDiv.style.display = 'block';
        console.error('Ошибка:', error);
    }
}
// СКРИПТ ДЛЯ РЕДАКТИРОВАНИЯ ОРГАНИЗАЦИЙ
        
function closeOrganizationModal(modalId) {
    const modal = document.getElementById(modalId);
    if (modal) {
        if (window.hideSiteModal) {
            window.hideSiteModal(modal);
        } else {
            modal.style.display = 'none';
        }
    }
}

// Функция открытия модального окна редактирования
function openEditOrganizationModal(id, name, email, dateBegin, dateEnd) {
    document.getElementById('editOrganizationId').value = id;
    document.getElementById('organizationName').value = name || '';
    document.getElementById('organizationEmail').value = email || '';
    document.getElementById('organizationDateBegin').value = dateBegin || '';
    document.getElementById('organizationDateEnd').value = dateEnd || '';
    if (window.showSiteModal) {
        window.showSiteModal('editOrganizationModal');
    } else {
        document.getElementById('editOrganizationModal').style.display = 'flex';
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
        trigger?.dataset?.organizationEmail || '',
        trigger?.dataset?.organizationDateBegin || '',
        trigger?.dataset?.organizationDateEnd || '');
}

// Функция обновления организации с улучшенной обработкой данных
async function updateOrganization() {


    if (!document.getElementById('organizationName').value)
{
    alert("Введите название организации!");
    return;
}

    try {
        // 1. Получаем значения из формы
        const id = document.getElementById('editOrganizationId').value;
        const name = document.getElementById('organizationName').value.trim();
        const email = document.getElementById('organizationEmail').value.trim();
        const dateBegin = document.getElementById('organizationDateBegin').value;
        const dateEnd = document.getElementById('organizationDateEnd').value;

        // 2. Подготовка данных в формате, ожидаемом сервером
        const organizationData = {
            Name: name,
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
            throw new Error("Ошибка сети. Проверьте соединение.");
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
        await response.text();
        closeOrganizationModal('editOrganizationModal');
        alert("Организация успешно отредактирована!");
        navigateAdminTab("get_organization", "/organizations");

    } catch (error) {
        console.error('Ошибка при обновлении организации:', error);
        
        // Показываем пользователю понятное сообщение об ошибке
        let errorMessage = error.message;
        if (error.message.includes("Некорректные данные организации")) {
            errorMessage = "Проверьте правильность заполнения всех полей";
        }
        
        alert(`Ошибка: ${errorMessage}`);
    } finally {
        const saveBtn = document.getElementById('saveOrganizationBtn');
        if (saveBtn) {
            saveBtn.disabled = false;
            saveBtn.textContent = 'Сохранить';
        }
    }
}
// Логика глаза пароля вынесена в ~/js/pages/admin-password-tools.js
        
