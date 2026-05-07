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
            return { tab: 'active', url: '/my-surveys' };
        case 'archived':
        case 'archived_surveys_for_user':
            return { tab: 'archived', url: '/my-surveys/archive' };
        case 'help':
            return { tab: 'help', url: '/help' };
        default:
            return null;
    }
}

export function getSurveyUserHistoryEntryFromLocation(pathname) {
    const normalizedPath = normalizeSurveyUserPathname(pathname);

    if (normalizedPath === '/my-surveys') {
        return buildSurveyUserHistoryEntry('active');
    }

    if (normalizedPath === '/my-surveys/archive') {
        return buildSurveyUserHistoryEntry('archived');
    }

    if (normalizedPath === '/help') {
        return buildSurveyUserHistoryEntry('help');
    }

    return null;
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

    const activeTab = contentRoot.dataset.activeTab === 'archived' ? 'archived' : 'active';
    const currentPage = Number(contentRoot.dataset.currentPage || 1);
    const totalPages = Number(contentRoot.dataset.totalPages || 1);
    const totalCount = Number(contentRoot.dataset.totalCount || 0);
    const searchTerm = contentRoot.dataset.searchTerm || '';
    const signedOnly = contentRoot.dataset.signedOnly === 'true';

    return {
        activeTab,
        currentPage: Number.isFinite(currentPage) && currentPage > 0 ? currentPage : 1,
        totalPages: Number.isFinite(totalPages) && totalPages > 0 ? totalPages : 1,
        totalCount: Number.isFinite(totalCount) && totalCount >= 0 ? totalCount : 0,
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

export function mountSurveyUserModal(host, { mountBody, onClose }) {
    const template = document.getElementById('survey-user-modal-template');
    if (!host || !template?.content?.firstElementChild) {
        return null;
    }

    host.replaceChildren();
    const modalNode = template.content.firstElementChild.cloneNode(true);
    const modalContent = modalNode.querySelector('.modal-content');
    const closeButton = modalNode.querySelector('[data-role="close-btn"]');
    const bodyHost = modalNode.querySelector('[data-role="body"]');

    const handleEscape = (event) => {
        if (event.key === 'Escape') {
            onClose?.();
        }
    };

    modalNode.addEventListener('click', () => onClose?.());
    modalContent?.addEventListener('click', (event) => event.stopPropagation());
    closeButton?.addEventListener('click', () => onClose?.());
    const bodyCleanup = typeof mountBody === 'function' && bodyHost
        ? mountBody(bodyHost)
        : null;

    host.appendChild(modalNode);
    modalNode.classList.add('modal--visible');
    modalNode.setAttribute('aria-hidden', 'false');

    if (typeof window.syncSiteModalBodyState === 'function') {
        window.syncSiteModalBodyState();
    } else {
        document.body.classList.add('modal-open');
    }

    document.addEventListener('keydown', handleEscape);

    return () => {
        if (typeof bodyCleanup === 'function') {
            bodyCleanup();
        }

        document.removeEventListener('keydown', handleEscape);
        host.replaceChildren();

        if (typeof window.syncSiteModalBodyState === 'function') {
            window.syncSiteModalBodyState();
        } else {
            document.body.classList.remove('modal-open');
        }
    };
}
