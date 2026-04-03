(function () {
    'use strict';

    function token() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    }

    function byId(id) {
        const element = document.getElementById(id);
        if (!element) {
            throw new Error(`Элемент ${id} не найден`);
        }
        return element;
    }

    function safeDateValue(value) {
        return (value || '').split('T')[0];
    }

    function ensureMessage(modalSelector) {
        const modal = document.querySelector(modalSelector);
        if (!modal) return null;
        let message = modal.querySelector('.message');
        if (!message) {
            message = document.createElement('div');
            message.className = 'message';
            const body = modal.querySelector('.modal-body') || modal;
            body.appendChild(message);
        }
        return message;
    }

    async function loadOrganizationsForEdit(selectedOrgId) {
        const select = byId('editOrganization');
        select.innerHTML = '<option value="">Загрузка организаций...</option>';

        const response = await fetch('/get_omsu/data');
        if (!response.ok) {
            throw new Error('Не удалось загрузить организации');
        }

        const organizations = await response.json();
        select.innerHTML = '';
        organizations.forEach(org => {
            const option = document.createElement('option');
            option.value = org.id;
            option.textContent = org.name;
            if (selectedOrgId != null && String(org.id) === String(selectedOrgId)) {
                option.selected = true;
            }
            select.appendChild(option);
        });
    }

    window.submitFormAdd = async function submitFormAdd() {
        const messageElement = document.getElementById('message');
        if (messageElement) {
            messageElement.textContent = '';
            messageElement.className = '';
        }

        const username = document.getElementById('username')?.value || '';
        const password = document.getElementById('password')?.value || '';
        const fullName = document.getElementById('fullName')?.value || '';
        const email = document.getElementById('email_input')?.value || '';
        const organizationId = document.getElementById('organization')?.value || '0';
        const role = document.getElementById('role_bd')?.value || 'user';

        if (!username) return alert('Введите никнейм пользователя!');
        if (!password) return alert('Введите пароль!');
        if (password.length < 12) return alert('Пароль должен содержать не меньше 12 символов!');
        if (!organizationId) return alert('Выберите организацию пользователя!');
        if (!role) return alert('Выберите роль пользователя!');

        const formData = { username, password, fullName, email, organizationId, role };

        try {
            const response = await fetch('/add_user_bd', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token()
                },
                body: JSON.stringify(formData)
            });

            const data = await response.json();
            if (messageElement) {
                messageElement.textContent = data.message || '';
                messageElement.className = data.success ? 'success-message' : 'error-message';
            }

            if (data.success) {
                alert('Пользователь успешно добавлен!');
                if (typeof window.handleTabClick === 'function') {
                    window.handleTabClick('get_users');
                }
            }
        } catch (error) {
            console.error('Ошибка:', error);
            if (messageElement) {
                messageElement.textContent = 'Ошибка соединения';
                messageElement.className = 'error-message';
            }
        }
    };

    window.deleteUser = async function deleteUser(id) {
        if (!confirm('Вы уверены, что хотите удалить пользователя?')) {
            return;
        }

        try {
            const response = await fetch(`/delete_user/${id}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' }
            });

            if (!response.ok) {
                throw new Error('Ошибка при удалении');
            }

            const result = await response.text();
            alert(result);
            if (typeof window.closeModal === 'function') {
                window.closeModal('deleteUserModal');
            }
            if (typeof window.handleTabClick === 'function') {
                window.handleTabClick('get_users');
            }
        } catch (error) {
            console.error('Ошибка:', error);
            alert('Произошла ошибка: ' + error.message);
        }
    };

    window.openEditUserModal = async function openEditUserModal(id, fullName, username, email, orgId, role, dateBegin, dateEnd) {
        try {
            byId('editUserId').value = id;
            byId('editFullName').value = fullName || '';
            byId('editUsername').value = username || '';
            byId('editRole').value = role || 'user';
            byId('editDateBegin').value = safeDateValue(dateBegin);
            byId('editDateEnd').value = safeDateValue(dateEnd);
            byId('editPassword').value = '';

            await loadOrganizationsForEdit(orgId);
            byId('editUserModal').style.display = 'block';
        } catch (error) {
            console.error('Ошибка при открытии формы:', error);
            alert('Ошибка: ' + error.message);
        }
    };

    window.updateUser = async function updateUser() {
        const username = document.getElementById('editUsername')?.value || '';
        const organization = document.getElementById('editOrganization')?.value || '';
        const role = document.getElementById('editRole')?.value || '';
        const dateBegin = document.getElementById('editDateBegin')?.value || '';
        const dateEnd = document.getElementById('editDateEnd')?.value || '';

        if (!username) return alert('Введите никнейм пользователя!');
        if (!organization) return alert('Выберите организацию пользователя!');
        if (!role) return alert('Выберите роль пользователя!');
        if (!dateBegin) return alert('Введите дату начала!');
        if (dateBegin && dateEnd && new Date(dateBegin) >= new Date(dateEnd)) {
            return alert('Дата начала должна быть раньше даты окончания!');
        }

        try {
            const payload = {
                username,
                password: document.getElementById('editPassword')?.value || 'keep_original',
                fullName: document.getElementById('editFullName')?.value || '',
                organizationId: organization,
                role,
                dateBegin,
                dateEnd
            };

            const response = await fetch(`/update_user_bd/${byId('editUserId').value}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token()
                },
                body: JSON.stringify(payload)
            });

            const result = await response.json();
            if (!response.ok) {
                throw new Error(result.message || 'Ошибка сервера');
            }

            alert('Пользователь успешно обновлён!');
            if (typeof window.handleTabClick === 'function') {
                window.handleTabClick('get_users');
            }
        } catch (error) {
            console.error('Ошибка обновления:', error);
            const message = ensureMessage('#editUserModal');
            if (message) {
                message.textContent = error.message;
                message.style.color = 'red';
            } else {
                alert('Ошибка: ' + error.message);
            }
        }
    };

    window.closeModal2 = function closeModal2() {
        const modal = document.getElementById('editUserModal');
        if (modal) {
            modal.style.display = 'none';
        }
    };

    window.delete_omsu = function delete_omsu(id) {
        if (!confirm('Вы уверены, что хотите удалить эту организацию?')) {
            return;
        }

        const xhr = new XMLHttpRequest();
        xhr.onreadystatechange = function () {
            if (xhr.readyState === 4) {
                if (xhr.status === 200) {
                    alert('Организация успешно удалена!');
                    if (typeof window.handleTabClick === 'function') {
                        window.handleTabClick('get_omsu');
                    }
                } else {
                    console.error('Ошибка при удалении организации: ' + xhr.status);
                }
            }
        };
        xhr.onerror = function () {
            console.error('Проблемы с интернетом');
        };
        xhr.open('POST', `/delete_omsu/${id}`, true);
        xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
        xhr.send();
    };

    window.get_omsu_name = function get_omsu_name() {
        const xhr = new XMLHttpRequest();
        xhr.onreadystatechange = function () {
            if (xhr.readyState === 4 && xhr.status === 200) {
                const data = JSON.parse(xhr.responseText);
                const select = document.getElementById('organization');
                if (!select) return;
                data.forEach(function (org) {
                    const option = document.createElement('option');
                    option.value = org;
                    option.text = org;
                    select.appendChild(option);
                });
            } else if (xhr.readyState === 4) {
                console.error('Ошибка при загрузке названий организаций: ' + xhr.status);
            }
        };
        xhr.onerror = function () {
            console.error('Ошибка при загрузке названий организаций');
        };
        xhr.open('GET', '/get_omsu/data', true);
        xhr.send();
    };

    window.add_omsu_bd = async function add_omsu_bd() {
        const form = document.getElementById('organizationForm');
        const messageDiv = document.getElementById('message');
        if (messageDiv) {
            messageDiv.style.display = 'none';
        }

        if (!document.getElementById('Name')?.value) return alert('Введите название организации!');
        if (!document.getElementById('omsu_email')?.value) return alert('Введите почту организации!');
        if (!document.getElementById('DateBegin')?.value) return alert('Введите дату начала!');
        if (!document.getElementById('DateEnd')?.value) return alert('Введите дату окончания!');

        const startDate = new Date(document.getElementById('DateBegin').value);
        const endDate = new Date(document.getElementById('DateEnd').value);
        if (startDate >= endDate) return alert('Дата начала должна быть раньше даты окончания!');

        try {
            const tokenValue = form?.querySelector('input[name="__RequestVerificationToken"]')?.value || token();
            const formData = [
                document.getElementById('Name').value,
                document.getElementById('omsu_email').value,
                document.getElementById('DateBegin').value,
                document.getElementById('DateEnd').value
            ];

            const response = await fetch('/add_omsu_bd', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': tokenValue
                },
                body: JSON.stringify(formData)
            });

            const result = await response.json();
            if (messageDiv) {
                messageDiv.textContent = result.success
                    ? 'Организация успешно создана!'
                    : 'Ошибка: ' + (result.error || 'Неизвестная ошибка');
                messageDiv.className = result.success ? 'alert alert-success' : 'alert alert-danger';
                messageDiv.style.display = 'block';
            }

            if (result.success) {
                alert('Организация успешно создана!');
                if (typeof window.handleTabClick === 'function') {
                    window.handleTabClick('get_omsu');
                }
            }
        } catch (error) {
            if (messageDiv) {
                messageDiv.textContent = 'Ошибка при отправке: ' + error.message;
                messageDiv.className = 'alert alert-danger';
                messageDiv.style.display = 'block';
            }
            console.error('Ошибка:', error);
        }
    };

    window.openEditOmsuModal = function openEditOmsuModal(id, name, email, dateBegin, dateEnd) {
        byId('editOmsuId').value = id;
        byId('omsuName').value = name || '';
        byId('omsuEmail').value = email || '';
        byId('omsuDateBegin').value = dateBegin || '';
        byId('omsuDateEnd').value = dateEnd || '';
        byId('editOmsuModal').style.display = 'block';
    };

    window.updateOmsu = async function updateOmsu() {
        if (!document.getElementById('omsuName')?.value) return alert('Введите название организации!');
        if (!document.getElementById('omsuDateBegin')?.value) return alert('Выберите дату начала!');

        const startDate = new Date(document.getElementById('omsuDateBegin').value);
        const endDate = new Date(document.getElementById('omsuDateEnd').value);
        if (startDate >= endDate) return alert('Дата начала должна быть раньше даты окончания!');

        const saveBtn = document.getElementById('saveOmsuBtn');
        try {
            const id = byId('editOmsuId').value;
            const omsuData = [
                byId('omsuName').value.trim(),
                (document.getElementById('omsuEmail')?.value || '').trim(),
                document.getElementById('omsuDateBegin')?.value || '',
                document.getElementById('omsuDateEnd')?.value || ''
            ];

            if (saveBtn) {
                saveBtn.disabled = true;
                saveBtn.textContent = 'Сохранение...';
            }

            const response = await fetch(`/update_omsu_bd/${id}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(omsuData)
            });

            if (!response.ok) {
                let errorText;
                try {
                    errorText = await response.text();
                } catch {
                    errorText = `Ошибка сервера: ${response.status}`;
                }
                throw new Error(errorText);
            }

            await response.text();
            alert('Организация успешно отредактирована!');
            if (typeof window.handleTabClick === 'function') {
                window.handleTabClick('get_omsu');
            }
        } catch (error) {
            console.error('Ошибка при обновлении организации:', error);
            alert('Ошибка: ' + error.message);
            if (saveBtn) {
                saveBtn.disabled = false;
                saveBtn.textContent = 'Сохранить';
            }
        }
    };

    window.archive_list_omsus = function archive_list_omsus() {
        if (typeof window.handleTabClick === 'function') {
            window.handleTabClick('archive_list_omsus');
        }
    };

    window.archive_list_users = function archive_list_users() {
        if (typeof window.handleTabClick === 'function') {
            window.handleTabClick('archive_list_users');
        }
    };
})();
