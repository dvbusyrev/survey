export function normalizeSurveyUserPathname(pathname) {
    if (!pathname) {
        return '/';
    }

    return pathname.length > 1 && pathname.endsWith('/')
        ? pathname.slice(0, -1)
        : pathname;
}

export function buildSurveyUserHistoryEntry(tab) {
    switch (tab) {
        case 'active':
            return { tab: 'active', url: '/survey' };
        case 'archived':
        case 'archived_surveys_for_user':
            return { tab: 'archived', url: '/archive' };
        case 'help':
            return { tab: 'help', url: '/help' };
        default:
            return null;
    }
}

export function getSurveyUserHistoryEntryFromLocation(pathname) {
    const normalizedPath = normalizeSurveyUserPathname(pathname);

    if (normalizedPath === '/survey' || normalizedPath === '/my-surveys') {
        return buildSurveyUserHistoryEntry('active');
    }

    if (normalizedPath === '/archive' || normalizedPath === '/my-surveys/archive') {
        return buildSurveyUserHistoryEntry('archived');
    }

    if (normalizedPath === '/help') {
        return buildSurveyUserHistoryEntry('help');
    }

    return null;
}

export function normalizeSurveyUserCount(value) {
    const numericValue = Number(value);
    return Number.isFinite(numericValue) && numericValue >= 0 ? numericValue : null;
}

export function readSurveyUserActiveCountFromSnapshot(snapshot) {
    return normalizeSurveyUserCount(snapshot?.activeCount);
}

export function readSurveyUserActiveCountFromDom(root) {
    const badge = root?.querySelector?.('[data-role="active-count"]');
    return normalizeSurveyUserCount(badge?.textContent?.trim());
}

export function syncSurveyUserActiveCountBadge(root, activeCount) {
    const activeTabButton = root?.querySelector('[data-role="tab-active"]');
    if (!activeTabButton) {
        return;
    }

    const nextCount = normalizeSurveyUserCount(activeCount) ?? 0;
    let badge = activeTabButton.querySelector('[data-role="active-count"]');
    if (!badge) {
        badge = document.createElement('span');
        badge.className = 'count-badge';
        badge.dataset.role = 'active-count';
        activeTabButton.appendChild(badge);
    }

    badge.textContent = String(nextCount);
}

export function getSurveyId(survey) {
    const rawValue = survey?.id_survey ?? survey?.IdSurvey ?? survey?.idSurvey;
    const numericValue = Number(rawValue);
    return Number.isFinite(numericValue) ? numericValue : 0;
}

function createTemplateFromNodes(nodes) {
    const template = document.createElement('template');
    nodes.forEach((node) => {
        template.content.appendChild(node.cloneNode(true));
    });
    return template;
}

function parseSurveyItems(contentRoot) {
    const itemsNode = contentRoot?.querySelector('[data-role="survey-user-items"]');
    if (!itemsNode?.textContent) {
        return [];
    }

    try {
        const items = JSON.parse(itemsNode.textContent.trim());
        return Array.isArray(items) ? items : [];
    } catch (error) {
        console.error('Не удалось разобрать список анкет клиента:', error);
        return [];
    }
}

function parseSnapshotFromContainer(container, template) {
    const contentRoot = container?.querySelector('[data-role="survey-user-content"]');
    if (!contentRoot) {
        return null;
    }

    const rawActiveTab = contentRoot.dataset.activeTab || 'active';
    const activeTab = rawActiveTab === 'archived' || rawActiveTab === 'help'
        ? rawActiveTab
        : 'active';
    const currentPage = Number(contentRoot.dataset.currentPage || 1);
    const totalPages = Number(contentRoot.dataset.totalPages || 1);
    const totalCount = Number(contentRoot.dataset.totalCount || 0);
    const activeCount = Number(contentRoot.dataset.activeCount || (activeTab === 'active' ? totalCount : 0));
    const searchTerm = contentRoot.dataset.searchTerm || '';
    const signedOnly = contentRoot.dataset.signedOnly === 'true';

    return {
        activeTab,
        currentPage: Number.isFinite(currentPage) && currentPage > 0 ? currentPage : 1,
        totalPages: Number.isFinite(totalPages) && totalPages > 0 ? totalPages : 1,
        totalCount: Number.isFinite(totalCount) && totalCount >= 0 ? totalCount : 0,
        activeCount: Number.isFinite(activeCount) && activeCount >= 0 ? activeCount : 0,
        searchTerm,
        signedOnly,
        surveys: parseSurveyItems(contentRoot),
        template
    };
}

export function createSnapshotFromHost(host) {
    if (!host) {
        return null;
    }

    const nodes = Array.from(host.childNodes);
    const template = createTemplateFromNodes(nodes);
    return parseSnapshotFromContainer(host, template);
}

export function createSnapshotFromTemplateElement(templateElement) {
    if (!templateElement?.content) {
        return null;
    }

    const template = document.createElement('template');
    template.content.appendChild(templateElement.content.cloneNode(true));

    const probe = document.createElement('div');
    probe.appendChild(template.content.cloneNode(true));

    return parseSnapshotFromContainer(probe, template);
}

export function createSnapshotFromHtml(html) {
    const range = document.createRange();
    range.selectNode(document.body);
    const fragment = range.createContextualFragment(html);

    const template = document.createElement('template');
    template.content.appendChild(fragment.cloneNode(true));

    const probe = document.createElement('div');
    probe.appendChild(fragment.cloneNode(true));

    return parseSnapshotFromContainer(probe, template);
}

export function setSelectOptions(select, options, defaultLabel, currentValue) {
    if (!select) {
        return '';
    }

    select.replaceChildren();

    const defaultOption = document.createElement('option');
    defaultOption.value = '';
    defaultOption.textContent = defaultLabel;
    select.appendChild(defaultOption);

    options.forEach((option) => {
        const optionNode = document.createElement('option');
        optionNode.value = option.value;
        optionNode.textContent = option.label;
        select.appendChild(optionNode);
    });

    const hasCurrentValue = options.some((option) => option.value === currentValue);
    select.value = hasCurrentValue ? currentValue : '';
    return select.value;
}

export function getMonthLabel(month) {
    const monthMap = {
        '01': 'Январь',
        '02': 'Февраль',
        '03': 'Март',
        '04': 'Апрель',
        '05': 'Май',
        '06': 'Июнь',
        '07': 'Июль',
        '08': 'Август',
        '09': 'Сентябрь',
        '10': 'Октябрь',
        '11': 'Ноябрь',
        '12': 'Декабрь'
    };

    return monthMap[month] || month;
}

export function mountSurveyUserModal(host, { title = '', subtitle = '', mountBody, onClose }) {
    const template = document.getElementById('survey-user-modal-template');
    if (!host || !template?.content?.firstElementChild) {
        return null;
    }

    host.replaceChildren();
    const modalNode = template.content.firstElementChild.cloneNode(true);
    const titleNode = modalNode.querySelector('[data-role="title"]');
    const bodyHost = modalNode.querySelector('[data-role="body"]');
    const footerHost = modalNode.querySelector('[data-role="footer"]');

    if (titleNode) {
        titleNode.replaceChildren();

        if (subtitle) {
            const mainLine = document.createElement('span');
            mainLine.className = 'answers-modal__title-main';
            mainLine.textContent = title;

            const nameLine = document.createElement('span');
            nameLine.className = 'answers-modal__title-name';
            nameLine.textContent = subtitle;

            titleNode.appendChild(mainLine);
            titleNode.appendChild(nameLine);
        } else {
            titleNode.textContent = title;
        }
    }

    let isDisposed = false;
    const handleHidden = () => {
        if (!isDisposed) {
            onClose?.();
        }
    };

    modalNode.addEventListener('site-modal:hidden', handleHidden);
    const bodyCleanup = typeof mountBody === 'function' && bodyHost
        ? mountBody(bodyHost, footerHost)
        : null;

    host.appendChild(modalNode);
    if (window.AppUi?.setModalVisibility) {
        window.AppUi.setModalVisibility(modalNode, true);
    } else if (typeof window.showSiteModal === 'function') {
        window.showSiteModal(modalNode);
    }

    return () => {
        isDisposed = true;

        if (typeof bodyCleanup === 'function') {
            bodyCleanup();
        }

        modalNode.removeEventListener('site-modal:hidden', handleHidden);
        if (window.AppUi?.setModalVisibility) {
            window.AppUi.setModalVisibility(modalNode, false);
        } else if (typeof window.hideSiteModal === 'function') {
            window.hideSiteModal(modalNode);
        }

        host.replaceChildren();
    };
}
