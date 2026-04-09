const settingsFilePath = '/mail/settings';

function loadEmailSettings() {
    fetch(settingsFilePath)
        .then(response => {
            if (!response.ok) {
                throw new Error('Ошибка загрузки настроек');
            }

            return response.json();
        })
        .then(settings => {
            document.getElementById('email-to').value = settings.to || settings.To || '';
            document.getElementById('email-subject').value = settings.subject || settings.Subject || '';
            document.getElementById('email-content').value = settings.content || settings.Content || '';
            alert('Настройки успешно загружены!');
        })
        .catch(error => {
            console.error('Ошибка:', error);
            alert('Ошибка при загрузке настроек');
        });
}

function saveEmailSettings() {
    const settingsData = {
        to: document.getElementById('email-to').value,
        subject: document.getElementById('email-subject').value,
        content: document.getElementById('email-content').value
    };

    fetch('/mail/settings', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(settingsData)
    })
        .then(response => {
            if (!response.ok) {
                throw new Error('Ошибка сохранения');
            }

            return response.json();
        })
        .then(() => {
            alert('Настройки успешно сохранены!');
        })
        .catch(error => {
            console.error('Ошибка:', error);
            alert('Ошибка при сохранении настроек');
        });
}
