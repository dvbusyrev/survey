function requireUserId(userId) {
    if (!userId) {
        throw new Error('ID пользователя не указан.');
    }
}

export function createAdminUserActions({
    fetchPage,
    getActiveTab,
    getModalData,
    getRequestVerificationToken,
    notify,
    openModalWhenReady,
    setActiveTab
}) {
    async function removeCurrentUser() {
        const userId = getModalData()?.id_user;
        const response = await fetch(`/users/${userId}/delete`, {
            method: 'POST',
            headers: {
                RequestVerificationToken: getRequestVerificationToken()
            }
        });
        const message = await response.text();
        if (!response.ok) {
            throw new Error(message || 'Ошибка при удалении пользователя.');
        }

        await fetchPage('/users');
        notify(message, 'success');
        setActiveTab('get_users');
        return message;
    }

    return {
        async add() {
            const modalIsReady = getActiveTab() === 'get_users' && document.getElementById('addUserModal');
            if (!modalIsReady) {
                await fetchPage('/users');
            }
            setActiveTab('get_users');
            openModalWhenReady('addUserModal', window.openAddUserModal);
        },

        async edit(userId) {
            requireUserId(userId);
            await fetchPage(`/users/${userId}/edit`);
            setActiveTab('update_user');
        },

        removeCurrentUser
    };
}
