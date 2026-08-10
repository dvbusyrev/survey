function createReportLoader() {
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

function removeReportLoader(loader) {
    if (loader && loader.parentNode) {
        loader.parentNode.removeChild(loader);
    }
}

function sanitizeFileName(name) {
    return name
        .replace(/[/\\?%*:|"<>]/g, '_')
        .replace(/\s+/g, ' ')
        .trim()
        .substring(0, 255);
}

function parseContentDispositionFileName(headerValue, defaultFileName) {
    if (!headerValue) {
        return defaultFileName;
    }

    const utf8FilenameMatch = headerValue.match(/filename\*=UTF-8''(.+)/i);
    if (utf8FilenameMatch) {
        return decodeURIComponent(utf8FilenameMatch[1]);
    }

    const regularFilenameMatch = headerValue.match(/filename="?([^"]+)"?/i);
    if (regularFilenameMatch) {
        return regularFilenameMatch[1];
    }

    return defaultFileName;
}

async function extractReportErrorMessage(response, fallbackMessage) {
    const responseText = await response.text();
    if (!responseText) {
        return fallbackMessage;
    }

    try {
        const payload = JSON.parse(responseText);
        return payload?.message || payload?.error || responseText;
    } catch (error) {
        return responseText;
    }
}

function normalizeReportFailureReason(message) {
    const normalized = String(message || '').trim();
    if (!normalized) {
        return 'Причина: не удалось получить данные для формирования отчёта.';
    }

    return /^Причина:/i.test(normalized)
        ? normalized
        : `Причина: ${normalized}`;
}

function showReportFailure(reportTitle, message) {
    const safeTitle = reportTitle && String(reportTitle).trim()
        ? `${String(reportTitle).trim()} не сформирован`
        : 'Отчёт не сформирован';
    const safeMessage = normalizeReportFailureReason(message);

    window.AppUi.notify(safeMessage, 'error', { title: safeTitle, duration: 0 });
}

function resolvePositiveInteger(value, fallbackElementId) {
    const candidate = Number(value);
    if (Number.isInteger(candidate) && candidate > 0) {
        return candidate;
    }

    const field = fallbackElementId ? document.getElementById(fallbackElementId) : null;
    const fieldValue = Number(field?.value || 0);
    return Number.isInteger(fieldValue) && fieldValue > 0 ? fieldValue : 0;
}

async function downloadReport(url, defaultFileName, options = {}) {
    const { reportTitle = 'Отчёт' } = options;
    const loader = createReportLoader();

    try {
        const response = await fetch(url);
        if (!response.ok) {
            throw new Error(await extractReportErrorMessage(response, 'Не удалось сформировать отчёт.'));
        }

        const fileName = parseContentDispositionFileName(
            response.headers.get('Content-Disposition'),
            defaultFileName
        );
        const blob = await response.blob();

        if (!blob || blob.size === 0) {
            throw new Error('Сервер вернул пустой файл отчёта.');
        }

        const objectUrl = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = objectUrl;
        link.download = sanitizeFileName(fileName);
        document.body.appendChild(link);
        link.click();

        window.setTimeout(() => {
            if (link.parentNode) {
                link.parentNode.removeChild(link);
            }
            window.URL.revokeObjectURL(objectUrl);
        }, 100);

        return true;
    } catch (error) {
        console.error('Ошибка при скачивании файла:', error);
        showReportFailure(reportTitle, error.message || 'Не удалось скачать отчёт.');
        return false;
    } finally {
        removeReportLoader(loader);
    }
}

function createMonthlyReport(id) {
    return downloadReport(`/reports/monthly/${id}`, 'Отчет.docx');
}

function createMonthlySummaryReport(month, year, options = {}) {
    const resolvedMonth = resolvePositiveInteger(month, 'reportMonth');
    const resolvedYear = resolvePositiveInteger(year, 'reportMonthYear');

    if (!resolvedMonth || !resolvedYear) {
        showReportFailure('Месячный отчёт', 'Выберите месяц и год для формирования отчёта.');
        return false;
    }

    return downloadReport(
        `/reports/monthly?month=${resolvedMonth}&year=${resolvedYear}`,
        `Отчет_за_${resolvedMonth}_${resolvedYear}.docx`,
        {
            ...options,
            reportTitle: 'Месячный отчёт'
        }
    );
}

function createQuarterlyReport(quarter, year, options = {}) {
    const resolvedQuarter = resolvePositiveInteger(quarter, 'reportQuarter');
    const resolvedYear = resolvePositiveInteger(year, 'reportQuarterYear');

    if (!resolvedQuarter || !resolvedYear) {
        showReportFailure('Квартальный отчёт', 'Выберите квартал и год для формирования отчёта.');
        return false;
    }

    return downloadReport(
        `/reports/quarterly/${resolvedQuarter}/${resolvedYear}`,
        `Отчет_за_${resolvedQuarter}_квартал_${resolvedYear}.xlsx`,
        {
            ...options,
            reportTitle: 'Квартальный отчёт'
        }
    );
}

function submitMonthlyReport() {
    return createMonthlySummaryReport();
}

function submitQuarterlyReport() {
    return createQuarterlyReport();
}

function populateYears() {
    return null;
}

function onYearChange() {
    return null;
}

window.createMonthlyReport = createMonthlyReport;
window.createMonthlySummaryReport = createMonthlySummaryReport;
window.createQuarterlyReport = createQuarterlyReport;
window.submitMonthlyReport = submitMonthlyReport;
window.submitQuarterlyReport = submitQuarterlyReport;
window.populateYears = populateYears;
window.onYearChange = onYearChange;
