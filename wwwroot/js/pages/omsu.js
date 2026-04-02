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
            const response = await fetch('/add_omsu_bd', {
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
            { name: 'Name', value: name },
            { name: 'Email', value: email },
            { name: 'DateBegin', value: dateBegin },
            { name: 'DateEnd', value: dateEnd }
        ];

        try {
            const response = await fetch(`/update_omsu_bd/${id}`, {
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

            closeOmsuModal('editOmsuModal');
            if (typeof window.handleTabClick === 'function') {
                window.handleTabClick('get_omsu');
            }
        } catch (error) {
            alert(error.message || 'Ошибка обновления организации');
        }
    }

    async function delete_omsu(id) {
        if (!id) return;
        if (!window.confirm('Удалить организацию?')) return;

        try {
            const response = await fetch(`/delete_omsu/${id}`, {
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
    window.delete_omsu = delete_omsu;
    window.archive_list_omsus = archive_list_omsus;
})();
