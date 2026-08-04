(() => {
    function getAntiforgeryToken() {
        return document
            .querySelector('input[name="__RequestVerificationToken"], [name="__RequestVerificationToken"]')
            ?.value || '';
    }

    window.AppHttp = {
        ...(window.AppHttp || {}),
        getAntiforgeryToken
    };
})();
