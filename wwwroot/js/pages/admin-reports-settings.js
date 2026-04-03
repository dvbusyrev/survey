(function () {
    function showTopLoader() {
        const loader = document.createElement('div');
        loader.style.position = 'fixed';
        loader.style.top = '0';
        loader.style.left = '0';
        loader.style.width = '100%';
        loader.style.height = '3px';
        loader.style.backgroundColor = '#007bff';
        loader.style.zIndex = '9999';
        document.body.appendChild(loader);
        return loader;
    }

    function sanitizeFileName(name) {
        return String(name || '')
            .replace(/[\/\\?%*:|"<>]/g, '_')
            .replace(/\s+/g, ' ')
            .trim()
            .substring(0, 255);
    }

    function downloadReport(url, defaultFileName) {
        const loader = showTopLoader();

        fetch(url)
            .then(response => {
                if (!response.ok) {
                    throw new Error(`HTTP error! status: ${response.status}`);
                }

                let fileName = defaultFileName;
                const contentDisposition = response.headers.get('Content-Disposition');

                if (contentDisposition) {
                    const utf8FilenameMatch = contentDisposition.match(/filename\*=UTF-8''(.+)/);
                    if (utf8FilenameMatch) {
                        fileName = decodeURIComponent(utf8FilenameMatch[1]);
                    } else {
                        const regularMatch = contentDisposition.match(/filename="(.+)"/);
                        if (regularMatch) {
                            fileName = regularMatch[1];
                        }
                    }
                }

                return response.blob().then(blob => ({ blob, fileName }));
            })
            .then(({ blob, fileName }) => {
                const objectUrl = window.URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = objectUrl;
                a.download = sanitizeFileName(fileName);
                document.body.appendChild(a);
                a.click();

                setTimeout(() => {
                    document.body.removeChild(a);
                    window.URL.revokeObjectURL(objectUrl);
                    if (loader.parentNode) {
                        document.body.removeChild(loader);
                    }
                }, 100);
            })
            .catch(error => {
                console.error('Ошибка при скачивании файла:', error);
                if (loader.parentNode) {
                    document.body.removeChild(loader);
                }
                alert('Произошла ошибка при скачивании отчета. Пожалуйста, попробуйте позже.');
            });
    }

    function create_otchet_month(id) {
        downloadReport(`/create_otchet_month/${id}`, 'Отчет.docx');
    }

    function create_otchetAll_month() {
        downloadReport('/create_otchetAll_month', 'Отчет_по_всем_анкетам.docx');
    }

    function create_otchetAll_kvartal(kvartal, year) {
        const xhr = new XMLHttpRequest();
        xhr.responseType = 'blob';

        xhr.onreadystatechange = function () {
            if (xhr.readyState !== 4) {
                return;
            }

            if (xhr.status === 200) {
                const contentDisposition = xhr.getResponseHeader('Content-Disposition');
                let fileName = `Otchet_za_${kvartal}_kvartal.xlsx`;

                if (contentDisposition) {
                    const fileNameMatch = contentDisposition.match(/filename="?([^"]+)"?/);
                    if (fileNameMatch && fileNameMatch[1]) {
                        fileName = fileNameMatch[1];
                    }
                }

                const blob = new Blob([
                    xhr.response
                ], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
                const link = document.createElement('a');
                link.href = window.URL.createObjectURL(blob);
                link.download = fileName;
                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);
            } else {
                console.error('Ошибочка: ' + xhr.status);
            }
        };

        xhr.onerror = function () {
            console.error('Проблемы с интернетом');
        };

        xhr.open('GET', `/create_otchet_kvartal/${kvartal}/${year}`, true);
        xhr.send();
    }

    function submitExtension(id) {
        const rows = document.querySelectorAll('.form-row');
        const data = [];

        if (rows.length === 0) {
            alert('Пожалуйста, добавьте хотя бы одну организацию для продления.');
            return;
        }

        let isValid = true;
        rows.forEach(row => {
            const organizationSelect = row.querySelector('select.form-control');
            const endDateInput = row.querySelector('input.form-control[type="date"]');

            if (!organizationSelect || !endDateInput) {
                return;
            }

            const organization = organizationSelect.value;
            const endDate = endDateInput.value;

            if (!organization || !endDate) {
                isValid = false;
                return;
            }

            const today = new Date().toISOString().split('T')[0];
            if (endDate <= today) {
                alert('Дата окончания должна быть в будущем!');
                isValid = false;
                return;
            }

            data.push({
                id_omsu: parseInt(organization, 10),
                new_end_date: endDate,
                id_survey: id
            });
        });

        if (!isValid) {
            alert('Пожалуйста, заполните все поля перед применением.');
            return;
        }

        const xhr = new XMLHttpRequest();
        xhr.open('POST', '/prodlenie_omsus', true);
        xhr.setRequestHeader('Content-Type', 'application/json;charset=UTF-8');
        xhr.onreadystatechange = function () {
            if (xhr.readyState !== 4) {
                return;
            }

            if (xhr.status === 200) {
                alert('Доступ к анкете успешно продлён!');
                if (typeof window.closeModal === 'function') {
                    window.closeModal();
                }
                window.location.reload();
            } else {
                console.error('Ошибка при отправке данных:', xhr.status, xhr.responseText);
                try {
                    const response = JSON.parse(xhr.responseText);
                    alert('Ошибка: ' + (response.message || xhr.statusText));
                } catch (error) {
                    alert('Ошибка при отправке данных: ' + xhr.status);
                }
            }
        };

        try {
            xhr.send(JSON.stringify(data));
        } catch (error) {
            console.error('Ошибка при отправке запроса:', error);
            alert('Произошла ошибка при отправке запроса');
        }
    }

    const settingsFilePath = '/email_settings.txt';

    function parseSettingsFile(text) {
        const lines = text.split('\n');
        const settings = { to: '', subject: '', content: '' };

        lines.forEach(line => {
            if (line.startsWith('Кому:')) {
                settings.to = line.replace('Кому:', '').trim();
            } else if (line.startsWith('Тема:')) {
                settings.subject = line.replace('Тема:', '').trim();
            } else if (line.startsWith('Содержание:')) {
                settings.content = line.replace('Содержание:', '').trim();
            } else if (settings.content) {
                settings.content += '\n' + line.trim();
            }
        });

        return settings;
    }

    function loadEmailSettings() {
        fetch(settingsFilePath)
            .then(response => {
                if (!response.ok) {
                    if (response.status === 404) {
                        return Promise.resolve('Кому:\nТема:\nСодержание:');
                    }
                    throw new Error('Ошибка загрузки файла');
                }
                return response.text();
            })
            .then(text => {
                const settings = parseSettingsFile(text);
                const to = document.getElementById('email-to');
                const subject = document.getElementById('email-subject');
                const content = document.getElementById('email-content');
                if (to) to.value = settings.to || '';
                if (subject) subject.value = settings.subject || '';
                if (content) content.value = settings.content || '';
                alert('Настройки успешно загружены!');
            })
            .catch(error => {
                console.error('Ошибка:', error);
                alert('Ошибка при загрузке настроек');
            });
    }

    function saveEmailSettings() {
        const emailTo = document.getElementById('email-to')?.value || '';
        const emailSubject = document.getElementById('email-subject')?.value || '';
        const emailContent = document.getElementById('email-content')?.value || '';

        const settingsData = {
            to: emailTo,
            subject: emailSubject,
            content: emailContent
        };

        fetch('/save_email_settings', {
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

    function populateYears(kvartal, select) {
        if (!select || select.options.length > 1) {
            return;
        }

        const currentYear = new Date().getFullYear();
        for (let i = 1; i <= 4; i += 1) {
            const year = currentYear - i;
            const option = document.createElement('option');
            option.value = year;
            option.textContent = year;
            select.appendChild(option);
        }
    }

    function onYearChange(kvartal, select) {
        const year = select?.value;
        if (year) {
            create_otchetAll_kvartal(kvartal, year);
            select.selectedIndex = 0;
        }
    }

    window.sanitizeFileName = sanitizeFileName;
    window.downloadReport = downloadReport;
    window.create_otchet_month = create_otchet_month;
    window.create_otchetAll_month = create_otchetAll_month;
    window.create_otchetAll_kvartal = create_otchetAll_kvartal;
    window.submitExtension = submitExtension;
    window.loadEmailSettings = loadEmailSettings;
    window.saveEmailSettings = saveEmailSettings;
    window.parseSettingsFile = parseSettingsFile;
    window.populateYears = populateYears;
    window.onYearChange = onYearChange;
})();
