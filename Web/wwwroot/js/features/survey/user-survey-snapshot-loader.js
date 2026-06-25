import { createSnapshotFromHtml } from './user-survey-page-helpers.js';

function buildSnapshotUrl({ tab, userId, page, searchTerm, signedOnly }) {
    if (tab === 'help') {
        return '/help';
    }

    if (tab === 'active') {
        return `/survey?page=${page}&searchTerm=${encodeURIComponent(searchTerm || '')}`;
    }

    return `/archive/${userId}?page=${page}&searchTerm=${encodeURIComponent(searchTerm || '')}&signedOnly=${signedOnly ? 'true' : 'false'}`;
}

function getSnapshotLoadError(tab) {
    return tab === 'help'
        ? 'Ошибка загрузки справки'
        : 'Ошибка загрузки данных анкет';
}

function getSnapshotParseError(tab) {
    return tab === 'help'
        ? 'Не удалось построить содержимое справки'
        : 'Не удалось построить содержимое страницы анкет';
}

export async function fetchSurveyUserSnapshot({ tab, userId, page, searchTerm, signedOnly }) {
    const response = await fetch(buildSnapshotUrl({ tab, userId, page, searchTerm, signedOnly }), {
        headers: {
            'X-Requested-With': 'XMLHttpRequest'
        }
    });
    if (!response.ok) {
        throw new Error(getSnapshotLoadError(tab));
    }

    const snapshot = createSnapshotFromHtml(await response.text());
    if (!snapshot) {
        throw new Error(getSnapshotParseError(tab));
    }

    return snapshot;
}
