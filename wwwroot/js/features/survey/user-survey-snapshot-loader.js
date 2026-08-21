import { createSnapshotFromHtml } from './user-survey-page-helpers.js';

export function normalizeSurveyArchiveFilterQuery(filterQuery) {
    const params = new URLSearchParams(String(filterQuery || '').replace(/^\?/, ''));
    ['page', 'searchTerm', 'signedOnly'].forEach((key) => params.delete(key));
    return params.toString();
}

function buildSnapshotRequest({ tab, userId, page, searchTerm, signedOnly, filterQuery }) {
    if (tab === 'help') {
        return { url: '/help', filterQuery: '' };
    }

    if (tab === 'active') {
        return {
            url: `/survey?page=${page}&searchTerm=${encodeURIComponent(searchTerm || '')}`,
            filterQuery: ''
        };
    }

    const archiveFilterQuery = normalizeSurveyArchiveFilterQuery(filterQuery ?? window.location.search);
    const params = new URLSearchParams(archiveFilterQuery);
    params.set('page', String(page));
    params.set('searchTerm', searchTerm || '');
    params.set('signedOnly', signedOnly ? 'true' : 'false');
    return {
        url: `/archive/${userId}?${params.toString()}`,
        filterQuery: archiveFilterQuery
    };
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
    const request = buildSnapshotRequest({ tab, userId, page, searchTerm, signedOnly, filterQuery });
    const response = await fetch(request.url, {
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

    snapshot.filterQuery = request.filterQuery;
    return snapshot;
}
