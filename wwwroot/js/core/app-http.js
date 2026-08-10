(() => {
    function getAntiforgeryToken() {
        return document
            .querySelector('input[name="__RequestVerificationToken"], [name="__RequestVerificationToken"]')
            ?.value || '';
    }

    async function readResponseMessage(response, fallbackMessage = 'Не удалось выполнить запрос.') {
        const responseText = await response.text();
        if (!responseText) {
            return fallbackMessage;
        }

        try {
            const payload = JSON.parse(responseText);
            if (Array.isArray(payload?.errors) && payload.errors.length > 0) {
                return payload.errors.filter(Boolean).join(' ');
            }

            return payload?.message || payload?.error || fallbackMessage;
        } catch (error) {
            return responseText;
        }
    }

    window.AppHttp = {
        ...(window.AppHttp || {}),
        getAntiforgeryToken,
        readResponseMessage
    };
})();
