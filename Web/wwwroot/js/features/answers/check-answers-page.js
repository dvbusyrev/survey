(function () {
    if (typeof window.createAnswerReport !== 'function') {
        window.createAnswerReport = function createAnswerReport(idSurvey, idOrganization, type) {
            window.AppScrollState?.prepareNavigation({ carry: true });
            window.location.assign(`/answers/${idSurvey}/${idOrganization}/report/${type}`);
        };
    }
})();
