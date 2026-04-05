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

    function closeOmsuModal(modalId) {
        const modal = byId(modalId);
        if (modal) {
            modal.style.display = 'none';
        }
    }

    async function submitOmsuUpdate(id, payload) {
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

    async function add_omsu_bd() {
        const form = byId('organizationForm');
        if (!form) return;

        const message = byId('message');
        const payload = {
            Name: byId('Name')?.value?.trim() || '',
            Email: byId('omsu_email')?.value?.trim() || '',
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
            if (typeof window.handleTabClick === 'function') {
                window.handleTabClick('get_omsu');
            } else {
                window.location.assign('/organizations');
            }
        } catch (error) {
            showMessage(message, error.message || 'Ошибка добавления организации', false);
        }
    }

    function openEditOmsuModal(id, name, email, dateBegin, dateEnd) {
        byId('editOmsuId').value = id || '';
        byId('omsuName').value = name || '';
        byId('omsuEmail').value = email || '';
        byId('omsuDateBegin').value = dateBegin || '';
        byId('omsuDateEnd').value = dateEnd || '';

        const modal = byId('editOmsuModal');
        if (modal) {
            modal.style.display = 'block';
        }
    }

    async function updateOmsu() {
        const id = byId('editOmsuId')?.value;
        if (!id) {
            alert('Не найден идентификатор организации');
            return;
        }

        const name = byId('omsuName')?.value?.trim() || '';
        const email = byId('omsuEmail')?.value?.trim() || '';
        const dateBegin = byId('omsuDateBegin')?.value || '';
        const dateEnd = byId('omsuDateEnd')?.value || '';

        if (!name || !dateBegin || !dateEnd) {
            alert('Заполните обязательные поля');
            return;
        }

        const startDate = new Date(dateBegin);
        const endDate = new Date(dateEnd);
        if (endDate < startDate) {
            alert('Дата окончания не может быть раньше даты начала');
            return;
        }

        const payload = [
            name,
            email,
            dateBegin,
            dateEnd
        ];

        try {
            await submitOmsuUpdate(id, payload);
            closeOmsuModal('editOmsuModal');
            if (typeof window.handleTabClick === 'function') {
                window.handleTabClick('get_omsu');
            } else {
                window.location.assign('/organizations');
            }
        } catch (error) {
            alert(error.message || 'Ошибка обновления организации');
        }
    }

    async function updateOmsuPage(id) {
        const payload = [
            byId('name')?.value?.trim() || '',
            byId('email')?.value?.trim() || '',
            byId('date_begin')?.value || '',
            byId('date_end')?.value || ''
        ];

        if (!payload[0] || !payload[2] || !payload[3]) {
            alert('Заполните обязательные поля');
            return;
        }

        if (new Date(payload[3]) < new Date(payload[2])) {
            alert('Дата окончания не может быть раньше даты начала');
            return;
        }

        try {
            await submitOmsuUpdate(id, payload);
            window.location.assign('/organizations');
        } catch (error) {
            alert(error.message || 'Ошибка обновления организации');
        }
    }

    async function delete_omsu(id) {
        if (!id) return;
        if (!window.confirm('Удалить организацию?')) return;

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
                window.handleTabClick('get_omsu');
            }
        } catch (error) {
            alert(error.message || 'Ошибка удаления организации');
        }
    }

    function archive_list_omsus() {
        if (typeof window.handleTabClick === 'function') {
            window.handleTabClick('archive_list_omsus');
        }
    }

    window.closeOmsuModal = closeOmsuModal;
    window.add_omsu_bd = add_omsu_bd;
    window.openEditOmsuModal = openEditOmsuModal;
    window.updateOmsu = updateOmsu;
    window.updateOmsuPage = updateOmsuPage;
    window.delete_omsu = delete_omsu;
    window.archive_list_omsus = archive_list_omsus;
})();
