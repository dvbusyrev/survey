// stage12: check answers actions extracted from check_answers.cshtml
function CSP(idSurvey, idOrganization) {
        // Логика подписания
        document.getElementById('block_btn_csp')?.classList.remove('is-hidden');
    }

    function create_answer_report(idSurvey, idOrganization, type) {
        window.location.href = `/Answer/create_answer_report?idSurvey=${idSurvey}&idOrganization=${idOrganization}&type=${type}`;
    }
