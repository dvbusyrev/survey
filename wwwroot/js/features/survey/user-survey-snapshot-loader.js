import { createSnapshotFromHtml } from './user-survey-page-helpers.js';

function buildSnapshotUrl({ tab, userId, page, searchTerm, signedOnly, filterQuery }) {
    if (tab === 'help') {
        return '/help';
    }

    if (tab === 'active') {
        return `/survey?page=${page}&searchTerm=${encodeURIComponent(searchTerm || '')}`;
    }

    const params = new URLSearchParams(filterQuery ?? window.location.search);
    ['page', 'searchTerm', 'signedOnly'].forEach((key) => params.delete(key));
    params.set('page', String(page));
    params.set('searchTerm', searchTerm || '');
    params.set('signedOnly', signedOnly ? 'true' : 'false');
    return `/archive/${userId}?${params.toString()}`;
}

function getSnapshotLoadError(tab) {
    return tab === 'help'
        ? 'Не удалось загрузить справку.'
        : 'Не удалось загрузить анкеты.';
}

function getSnapshotParseError(tab) {
    return tab === 'help'
        ? 'Не удалось отобразить справку.'
        : 'Не удалось отобразить страницу анкет.';
}

export async function fetchSurveyUserSnapshot({ tab, userId, page, searchTerm, signedOnly, filterQuery }) {
    const response = await fetch(buildSnapshotUrl({ tab, userId, page, searchTerm, signedOnly, filterQuery }), {
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
