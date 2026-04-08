// stage12: check answers actions extracted from check_answers.cshtml
function CSP(idSurvey, idOrganization) {
        // Логика подписания
        document.getElementById('block_btn_csp')?.classList.remove('is-hidden');
    }

    function createAnswerReport(idSurvey, idOrganization, type) {
        window.location.href = `/answers/${idSurvey}/${idOrganization}/report/${type}`;
    }
