function requireOrganizationId(organizationId) {
    if (!organizationId) {
        throw new Error('ID организации не указан.');
    }
}

export function createAdminOrganizationActions({
    fetchPage,
    getActiveTab,
    getModalData,
    getRequestVerificationToken,
    openModalWhenReady,
    setActiveTab
}) {
    async function removeCurrentOrganization() {
        const modalData = getModalData();
        const organizationId = modalData?.id_organization ?? modalData?.organizationId;
        const response = await fetch(`/organizations/${organizationId}/delete`, {
            method: 'POST',
            cache: 'no-store',
            headers: {
                'X-Admin-Inline-Request': 'true',
                RequestVerificationToken: getRequestVerificationToken()
            }
        });
        if (!response.ok) {
            throw new Error((await response.text()) || 'Ошибка при удалении организации.');
        }

        await fetchPage('/organizations');
        setActiveTab('get_organization');
    }

    return {
        async add() {
            const modalIsReady = getActiveTab() === 'get_organization'
                && document.getElementById('addOrganizationModal');
            if (!modalIsReady) {
                await fetchPage('/organizations');
            }
            setActiveTab('get_organization');
            openModalWhenReady('addOrganizationModal', window.openAddOrganizationModal);
        },

        async edit(organizationId) {
            requireOrganizationId(organizationId);
            await fetchPage(`/organizations/${organizationId}/edit`);
            setActiveTab('update_organization');
        },

        removeCurrentOrganization
    };
}
