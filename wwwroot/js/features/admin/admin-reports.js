function downloadReport(url, defaultFileName) {
    const loader = document.createElement('div');
    loader.style.position = 'fixed';
    loader.style.top = '0';
    loader.style.left = '0';
    loader.style.width = '100%';
    loader.style.height = '3px';
    loader.style.backgroundColor = '#007bff';
    loader.style.zIndex = '9999';
    document.body.appendChild(loader);

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
            const link = document.createElement('a');
            link.href = objectUrl;
            link.download = sanitizeFileName(fileName);
            document.body.appendChild(link);
            link.click();

            setTimeout(() => {
                document.body.removeChild(link);
                window.URL.revokeObjectURL(objectUrl);
                document.body.removeChild(loader);
            }, 100);
        })
        .catch(error => {
            console.error('Ошибка при скачивании файла:', error);
            document.body.removeChild(loader);
            alert('Произошла ошибка при скачивании отчета. Пожалуйста, попробуйте позже.');
        });
}

function sanitizeFileName(name) {
    return name
        .replace(/[/\\?%*:|"<>]/g, '_')
        .replace(/\s+/g, ' ')
        .trim()
        .substring(0, 255);
}

function createMonthlyReport(id) {
    downloadReport(`/reports/monthly/${id}`, 'Отчет.docx');
}

function createMonthlySummaryReport() {
    downloadReport('/reports/monthly', 'Отчет_по_всем_анкетам.docx');
}

function createQuarterlyReport(quarter, year) {
    const xhr = new XMLHttpRequest();
    xhr.responseType = 'blob';

    xhr.onreadystatechange = function () {
        if (xhr.readyState !== 4) {
            return;
        }

        if (xhr.status === 200) {
            const contentDisposition = xhr.getResponseHeader('Content-Disposition');
            let fileName = `quarterly_report_q${quarter}.xlsx`;

            if (contentDisposition) {
                const fileNameMatch = contentDisposition.match(/filename="?([^"]+)"?/);
                if (fileNameMatch && fileNameMatch[1]) {
                    fileName = fileNameMatch[1];
                }
            }

            const blob = new Blob([xhr.response], {
                type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
            });
            const link = document.createElement('a');
            link.href = window.URL.createObjectURL(blob);
            link.download = fileName;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            return;
        }

        console.error('Ошибочка: ' + xhr.status);
    };

    xhr.onerror = function () {
        console.error('Проблемы с интернетом');
    };

    const yearSegment = year ? `/${year}` : '';
    xhr.open('GET', `/reports/quarterly/${quarter}${yearSegment}`, true);
    xhr.send();
}

function populateYears(quarter, select) {
    if (select.options.length > 1) {
        return;
    }

    const currentYear = new Date().getFullYear();
    for (let offset = 1; offset <= 4; offset += 1) {
        const year = currentYear - offset;
        const option = document.createElement('option');
        option.value = year;
        option.textContent = year;
        select.appendChild(option);
    }
}

function onYearChange(quarter, select) {
    const year = select.value;
    if (!year) {
        return;
    }

    createQuarterlyReport(quarter, year);
    select.selectedIndex = 0;
}
