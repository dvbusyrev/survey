(function () {
    let selectedOrganizations = [];
    let availableOrganizations = [];

    function normalizeOrganization(rawOrganization) {
        return {
            id: Number(rawOrganization?.id_organization ?? rawOrganization?.id ?? 0),
            name: String(rawOrganization?.organization_name ?? rawOrganization?.name ?? '').trim()
        };
    }

    function cloneOrganizations(items) {
        return Array.isArray(items)
            ? items.map((item) => normalizeOrganization(item)).filter((item) => item.id > 0 && item.name)
            : [];
    }

    window.SurveyAdminFormState = {
        getSelected: () => cloneOrganizations(selectedOrganizations),
        setSelected: (items) => {
            selectedOrganizations = cloneOrganizations(items);
            window.surveyEditSelectedOrganization = cloneOrganizations(selectedOrganizations);
        },
        getAvailable: () => cloneOrganizations(availableOrganizations),
        setAvailable: (items) => {
            availableOrganizations = cloneOrganizations(items);
        },
        resetAvailable: () => {
            availableOrganizations = [];
        },
        normalizeOrganization,
        cloneOrganizations
    };
})();
