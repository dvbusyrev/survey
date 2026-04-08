// stage12: check answers actions extracted from check_answers.cshtml
function CSP(idSurvey, idOrganization) {
        // Логика подписания
        document.getElementById('block_btn_csp')?.classList.remove('is-hidden');
    }

    function create_otchet_for_me(idSurvey, idOrganization, type) {
        window.location.href = `/Answer/create_otchet_for_me?idSurvey=${idSurvey}&idOrganization=${idOrganization}&type=${type}`;
    }
