(function () {
    function byId(id) {
        return document.getElementById(id);
    }

    function tokenValue() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    }

    function closeUserModal(modalId) {
        const modal = byId(modalId);
        if (modal) {
            modal.style.display = 'none';
        }
    }

    function openAddUserModal() {
        const modal = byId('addUserModal');
        if (modal) {
            modal.style.display = 'block';
        }
    }

    function promptDeleteUser(id, fullName) {
        const idField = byId('deleteUserId');
        const nameField = byId('deleteUserName');
        const modal = byId('deleteUserModal');

        if (idField) idField.value = id || '';
        if (nameField) nameField.textContent = fullName || '';
        if (modal) modal.style.display = 'block';
    }

    async function submitFormAdd() {
        const messageElement = byId('message');
        const formData = {
            username: byId('username')?.value || '',
            password: byId('password')?.value || '',
            fullName: byId('fullName')?.value || '',
            email: byId('email_input')?.value || '',
            organizationId: byId('organization')?.value || '0',
            role: byId('role_bd')?.value || 'user'
        };

        try {
            const response = await fetch('/add_user_bd', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    ...(tokenValue() ? { RequestVerificationToken: tokenValue() } : {})
                },
                body: JSON.stringify(formData)
            });

            const data = await response.json();
            if (messageElement) {
                messageElement.textContent = data.message || '';
                messageElement.className = data.success ? 'success-message' : 'error-message';
            }

            if (data.success) {
                closeUserModal('addUserModal');
                if (typeof window.handleTabClick === 'function') {
                    window.handleTabClick('get_users');
                } else {
                    window.location.reload();
                }
            }
        } catch (error) {
            if (messageElement) {
                messageElement.textContent = 'Ошибка соединения';
                messageElement.className = 'error-message';
            }
        }
    }

    async function openEditUserModal(id, fullName, username, email, orgId, role, dateBegin, dateEnd) {
        byId('editUserId').value = id || '';
        byId('editFullName').value = fullName || '';
        byId('editUsername').value = username || '';
        byId('editEmail').value = email || '';
        byId('editOrganization').value = orgId || '';
        byId('editRole').value = role || 'user';
        byId('editDateBegin').value = (dateBegin || '').split('T')[0];
        byId('editDateEnd').value = (dateEnd || '').split('T')[0];
        byId('editPassword').value = '';

        const modal = byId('editUserModal');
        if (modal) {
            modal.style.display = 'block';
        }
    }

    async function updateUser() {
        const id = byId('editUserId')?.value;
        if (!id) {
            alert('Не найден идентификатор пользователя');
            return;
        }

        const formData = {
            username: byId('editUsername')?.value || '',
            password: byId('editPassword')?.value || 'keep_original',
            fullName: byId('editFullName')?.value || '',
            email: byId('editEmail')?.value || '',
            organizationId: byId('editOrganization')?.value || '0',
            role: byId('editRole')?.value || 'user',
            dateBegin: byId('editDateBegin')?.value || '',
            dateEnd: byId('editDateEnd')?.value || ''
        };

        try {
            const response = await fetch(`/update_user_bd/${id}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    ...(tokenValue() ? { RequestVerificationToken: tokenValue() } : {})
                },
                body: JSON.stringify(formData)
            });

            const result = await response.json();
            if (!response.ok || !result.success) {
                throw new Error(result.message || 'Ошибка обновления');
            }

            closeUserModal('editUserModal');
            if (typeof window.handleTabClick === 'function') {
                window.handleTabClick('get_users');
            } else {
                window.location.reload();
            }
        } catch (error) {
            alert(error.message || 'Ошибка обновления');
        }
    }

    async function deleteUser() {
        const id = byId('deleteUserId')?.value;
        if (!id) {
            alert('Не найден идентификатор пользователя');
            return;
        }

        try {
            const response = await fetch(`/delete_user/${id}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    ...(tokenValue() ? { RequestVerificationToken: tokenValue() } : {})
                }
            });

            const raw = await response.text();
            let message = raw || 'Ошибка удаления';
            let success = response.ok;

            try {
                const parsed = JSON.parse(raw);
                success = response.ok && (parsed.success ?? true);
                message = parsed.message || message;
            } catch (_) {
                // Сервер может вернуть обычный текст — это текущая легаси-логика.
            }

            if (!success) {
                throw new Error(message);
            }

            closeUserModal('deleteUserModal');
            if (typeof window.handleTabClick === 'function') {
                window.handleTabClick('get_users');
            } else {
                window.location.reload();
            }
        } catch (error) {
            alert(error.message || 'Ошибка удаления');
        }
    }

    function initUsersPage(root) {
        const container = root || document.querySelector('.users-page[data-page="users"]');
        if (!container || container.dataset.usersInit === 'true') {
            return;
        }

        container.dataset.usersInit = 'true';

        if (container.dataset.openAdd === 'true') {
            setTimeout(openAddUserModal, 0);
        }
    }

    const observer = new MutationObserver(function () {
        initUsersPage();
    });

    if (document.body) {
        observer.observe(document.body, { childList: true, subtree: true });
        initUsersPage();
    }

    window.UsersPage = { init: initUsersPage };
    window.closeUserModal = closeUserModal;
    window.openAddUserModal = openAddUserModal;
    window.openEditUserModal = openEditUserModal;
    window.promptDeleteUser = promptDeleteUser;
    window.submitFormAdd = submitFormAdd;
    window.updateUser = updateUser;
    window.deleteUser = deleteUser;
})();
