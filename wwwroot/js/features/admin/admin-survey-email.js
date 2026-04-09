async function sendEmail() {
    try {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

        const response = await fetch('/mail/send', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token || ''
            },
            body: JSON.stringify({})
        });

        if (!response.ok) {
            const errorData = await response.json();
            throw new Error(errorData.message || 'Ошибка сервера');
        }

        const result = await response.json();
        alert(result.message || 'Письмо успешно отправлено!');
    } catch (error) {
        console.error('Ошибка:', error);
        alert('Ошибка при отправке: ' + error.message);
    }
}
