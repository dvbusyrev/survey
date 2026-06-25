function requireSurveyId(surveyId) {
    if (!surveyId) {
        throw new Error('ID анкеты не указан.');
    }
}

export function createAdminSurveyActions({
    fetchPage,
    getActiveTab,
    getModalData,
    getRequestVerificationToken,
    notify,
    openModalWhenReady,
    setActiveTab
}) {
    async function removeCurrentSurvey() {
        const surveyId = getModalData()?.id_survey;
        const response = await fetch(`/survey/${surveyId}/delete`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                RequestVerificationToken: getRequestVerificationToken()
            },
            body: JSON.stringify({ surveyId })
        });
        const result = await response.json();
        if (!response.ok) {
            throw new Error(result.message || 'Ошибка при удалении анкеты.');
        }

        await fetchPage('/survey');
        notify(result.message, 'success');
        setActiveTab('get_surveys');
        return result;
    }

    return {
        async add() {
            const editorIsReady = getActiveTab() === 'get_surveys'
                && document.getElementById('surveyEditorModal')
                && !document.getElementById('surveyId');
            if (!editorIsReady) {
                await fetchPage('/survey');
            }
            setActiveTab('get_surveys');
            openModalWhenReady('surveyEditorModal', window.openAddSurveyModal);
        },

        async copy(surveyId) {
            requireSurveyId(surveyId);
            await fetchPage('/survey');
            setActiveTab('get_surveys');
            openModalWhenReady(
                'surveyEditorModal',
                () => window.openCopySurveyModalById?.(surveyId, { skipListRefresh: true })
            );
        },

        async edit(surveyId, { archived = false } = {}) {
            requireSurveyId(surveyId);
            const endpoint = archived
                ? `/survey/archive/${surveyId}/edit`
                : `/survey/${surveyId}/edit`;
            await fetchPage(endpoint);
            setActiveTab(archived ? 'archived_surveys' : 'get_surveys');
            openModalWhenReady('surveyEditorModal', window.openEditSurveyModal);
        },

        removeCurrentSurvey
    };
}
