(function () {
    const page = document.querySelector('.app-page[data-page="surveys-list"], .app-page[data-page="surveys-archive"]');
    if (!page) {
        return;
    }

    let extensionModal = null;
    let extensionHost = null;
    let extensionCleanup = null;
    let extensionSubmitButton = null;
    let extensionCancelButton = null;
    let signaturesModal = null;
    let signaturesHost = null;
    let signaturesTitle = null;

    function isArchiveSurveyListRoute(path) {
        const currentPath = String(path || window.location.pathname || '').toLowerCase();
        return currentPath === '/surveys/archive' || /\/surveys\/archive\/\d+\/edit$/.test(currentPath);
    }

    function resolveSurveyListTarget(path) {
        return isArchiveSurveyListRoute(path)
            ? { tabName: 'archived_surveys', fallbackUrl: '/surveys/archive' }
            : { tabName: 'get_surveys', fallbackUrl: '/surveys' };
    }

    function isSurveyEditorRoute(path) {
        const currentPath = String(path || window.location.pathname || '').toLowerCase();
        return currentPath === '/surveys/create'
            || /\/surveys\/\d+\/edit$/.test(currentPath)
            || /\/surveys\/archive\/\d+\/edit$/.test(currentPath);
    }

    function syncSurveyListHistory() {
        if (!isSurveyEditorRoute()) {
            return;
        }

        const target = resolveSurveyListTarget();
        const nextState = window.history.state && typeof window.history.state === 'object'
            ? { ...window.history.state, tab: target.tabName, id: null }
            : { tab: target.tabName, id: null };

        window.history.replaceState(nextState, document.title, target.fallbackUrl);
    }

    function refreshSurveyListPreservingScroll() {
        const target = resolveSurveyListTarget();

        if (typeof window.refreshAdminUi === 'function') {
            window.refreshAdminUi({
                tabName: target.tabName,
                fallbackUrl: target.fallbackUrl,
                options: {
                    force: true,
                    historyMode: 'replace',
                    scrollMode: 'carry'
                }
            });
            return true;
        }

        if (typeof window.refreshAdminTab === 'function') {
            window.refreshAdminTab(target.tabName, null, {
                force: true,
                historyMode: 'replace',
                scrollMode: 'carry'
            });
            return true;
        }

        if (typeof window.handleTabClick === 'function') {
            window.handleTabClick(target.tabName, {
                force: true,
                historyMode: 'replace',
                scrollMode: 'carry'
            });
            return true;
        }

        return false;
    }

    function setSurveyEditorModalVisible(isVisible) {
        const modal = document.getElementById('surveyEditorModal');
        if (!modal) {
            return false;
        }

        if (isVisible) {
            if (typeof window.showSiteModal === 'function') {
                window.showSiteModal(modal);
            } else {
                modal.style.display = 'flex';
            }
            return true;
        }

        if (typeof window.hideSiteModal === 'function') {
            window.hideSiteModal(modal);
        } else {
            modal.style.display = 'none';
        }
        return true;
    }

    function closeSurveyEditorModal() {
        setSurveyEditorModalVisible(false);
        syncSurveyListHistory();
    }

    function openAddSurveyModal() {
        if (document.getElementById('surveyId')) {
            if (typeof window.refreshAdminTab === 'function') {
                window.refreshAdminTab('get_surveys', null, {
                    force: true,
                    historyMode: 'replace',
                    scrollMode: 'carry'
                }).then(() => {
                    if (typeof window.openAddSurveyModal === 'function') {
                        window.openAddSurveyModal();
                    }
                });
            }
            return;
        }

        if (typeof window.resetSurveyCreateForm === 'function') {
            window.resetSurveyCreateForm();
        }

        syncSurveyListHistory();
        setSurveyEditorModalVisible(true);
    }

    async function fetchSurveyCopyTemplate(surveyId) {
        const response = await fetch(`/surveys/${surveyId}/copy-template`, {
            headers: {
                Accept: 'application/json'
            }
        });

        const responseText = await response.text();
        let payload = null;

        try {
            payload = responseText ? JSON.parse(responseText) : null;
        } catch (parseError) {
            payload = null;
        }

        if (!response.ok) {
            throw new Error(payload?.message || responseText || 'Не удалось загрузить данные для копирования анкеты.');
        }

        return payload || {};
    }

    async function ensureCreateSurveyModalAvailable() {
        if (document.getElementById('surveyEditorModal') && !document.getElementById('surveyId')) {
            return true;
        }

        const target = resolveSurveyListTarget();

        if (typeof window.refreshAdminTab === 'function') {
            await window.refreshAdminTab(target.tabName, null, {
                force: true,
                historyMode: 'replace',
                scrollMode: 'carry'
            });
        } else if (typeof window.handleTabClick === 'function') {
            await window.handleTabClick(target.tabName, {
                force: true,
                historyMode: 'replace',
                scrollMode: 'carry'
            });
        } else {
            window.location.assign(target.fallbackUrl);
            return false;
        }

        return Boolean(document.getElementById('surveyEditorModal') && !document.getElementById('surveyId'));
    }

    async function openCopySurveyModalById(surveyId, options = {}) {
        const resolvedSurveyId = Number.parseInt(String(surveyId || ''), 10);
        if (!Number.isFinite(resolvedSurveyId) || resolvedSurveyId <= 0) {
            throw new Error('Не найден идентификатор анкеты.');
        }

        const skipListRefresh = options?.skipListRefresh === true;
        const template = await fetchSurveyCopyTemplate(resolvedSurveyId);

        if (!skipListRefresh) {
            const isReady = await ensureCreateSurveyModalAvailable();
            if (!isReady) {
                return;
            }
        }

        if (typeof window.openAddSurveyModal !== 'function' || typeof window.prefillSurveyCreateForm !== 'function') {
            throw new Error('Форма создания анкеты недоступна.');
        }

        window.openAddSurveyModal();
        window.prefillSurveyCreateForm(template);
    }

    async function openCopySurveyModalFromTrigger(trigger) {
        try {
            const survey = buildSurveyData(trigger);
            await openCopySurveyModalById(survey.id_survey);
        } catch (error) {
            window.siteNotify?.(error.message || 'Не удалось подготовить копирование анкеты.', 'error');
        }
    }

    function openEditSurveyModal() {
        syncSurveyListHistory();
        setSurveyEditorModalVisible(true);
        window.setTimeout(function () {
            if (typeof window.surveyEditInit === 'function') {
                window.surveyEditInit();
            }
        }, 0);
    }

    function openEditSurveyModalFromTrigger(trigger) {
        try {
            const survey = buildSurveyData(trigger);

            if (typeof window.refreshAdminTab === 'function') {
                window.refreshAdminTab('update_survey', survey.id_survey, {
                    force: true,
                    scrollMode: 'restore'
                });
                return;
            }

            const editUrl = isArchiveSurveyListRoute()
                ? `/surveys/archive/${survey.id_survey}/edit`
                : `/surveys/${survey.id_survey}/edit`;
            window.location.assign(editUrl);
        } catch (error) {
            window.siteNotify?.(error.message || 'Не удалось открыть редактирование анкеты.', 'error');
        }
    }

    function handleSurveyCreateSuccess(result) {
        window.siteNotify?.(result?.message || 'Анкета успешно создана.', 'success');
        closeSurveyEditorModal();
        if (!refreshSurveyListPreservingScroll()) {
            window.location.assign('/surveys');
        }
    }

    function handleSurveyUpdateSuccess(result) {
        window.siteNotify?.(result?.message || 'Анкета успешно обновлена.', 'success');
        closeSurveyEditorModal();
        if (!refreshSurveyListPreservingScroll()) {
            window.location.assign('/surveys');
        }
    }

    function buildSurveyData(trigger) {
        const surveyId = Number.parseInt(trigger?.dataset?.surveyId || '', 10);
        if (!Number.isFinite(surveyId) || surveyId <= 0) {
            throw new Error('Не найден идентификатор анкеты.');
        }

        return {
            id_survey: surveyId,
            name_survey: trigger?.dataset?.surveyName || ''
        };
    }

    function normalizeAssetUrl(url) {
        if (!url) {
            return '';
        }

        try {
            return new URL(url, window.location.origin).href;
        } catch (error) {
            return '';
        }
    }

    function isStylesheetLoaded(href) {
        return Array.from(document.querySelectorAll('link[rel="stylesheet"][href]')).some((link) => {
            return normalizeAssetUrl(link.href) === href;
        });
    }

    function loadStylesheetsFromDocument(parsedDocument) {
        parsedDocument.querySelectorAll('link[rel="stylesheet"][href]').forEach((sourceLink) => {
            const href = normalizeAssetUrl(sourceLink.getAttribute('href'));
            if (!href || isStylesheetLoaded(href)) {
                return;
            }

            const link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = href;

            if (sourceLink.media) {
                link.media = sourceLink.media;
            }

            document.head.appendChild(link);
        });
    }

    function parseHtmlDocument(html) {
        const parser = new DOMParser();
        return parser.parseFromString(html || '', 'text/html');
    }

    function createStatusMessage(message, type = 'loading') {
        const node = document.createElement('div');
        node.className = type === 'error'
            ? 'error survey-signatures-modal__error'
            : 'loading survey-signatures-modal__loading';
        node.textContent = message;
        return node;
    }

    function extractSignaturesContent(parsedDocument) {
        const pageContent = parsedDocument.getElementById('default_content')
            || parsedDocument.querySelector('.answers-page__signatures');
        const tableContainer = pageContent?.querySelector('.table-responsive')
            || parsedDocument.querySelector('.answers-page__signatures-table')?.closest('.table-responsive');

        if (tableContainer) {
            return tableContainer.cloneNode(true);
        }

        if (pageContent) {
            const clone = pageContent.cloneNode(true);
            clone.removeAttribute('id');
            clone.classList.add('answers-page__signatures--modal');
            return clone;
        }

        return createStatusMessage('Не удалось прочитать данные о прохождении.', 'error');
    }

    function ensureSignaturesModal() {
        if (signaturesModal && signaturesHost && signaturesTitle) {
            return;
        }

        signaturesModal = document.createElement('div');
        signaturesModal.id = 'surveySignaturesModal';
        signaturesModal.className = 'modal survey-signatures-modal';
        signaturesModal.setAttribute('aria-hidden', 'true');

        const modalContent = document.createElement('div');
        modalContent.className = 'modal-content';

        const modalHeader = document.createElement('div');
        modalHeader.className = 'modal-header';

        signaturesTitle = document.createElement('h2');
        signaturesTitle.className = 'h2_modal';
        signaturesTitle.textContent = 'Проверка прохождения';

        const closeButton = document.createElement('button');
        closeButton.type = 'button';
        closeButton.className = 'modal-close';
        closeButton.setAttribute('aria-label', 'Закрыть');

        const closeIcon = document.createElement('i');
        closeIcon.className = 'fas fa-xmark';
        closeIcon.setAttribute('aria-hidden', 'true');
        closeButton.appendChild(closeIcon);

        signaturesHost = document.createElement('div');
        signaturesHost.className = 'modal-body survey-signatures-modal__body';

        const modalFooter = document.createElement('div');
        modalFooter.className = 'modal-footer';

        const footerCloseButton = document.createElement('button');
        footerCloseButton.type = 'button';
        footerCloseButton.className = 'modal_btn modal_btn-secondary';
        footerCloseButton.textContent = 'Закрыть';

        closeButton.addEventListener('click', closeSurveySignaturesModal);
        footerCloseButton.addEventListener('click', closeSurveySignaturesModal);

        modalHeader.appendChild(signaturesTitle);
        modalHeader.appendChild(closeButton);
        modalFooter.appendChild(footerCloseButton);
        modalContent.appendChild(modalHeader);
        modalContent.appendChild(signaturesHost);
        modalContent.appendChild(modalFooter);
        signaturesModal.appendChild(modalContent);
        document.body.appendChild(signaturesModal);
    }

    function closeSurveySignaturesModal() {
        if (signaturesHost) {
            signaturesHost.replaceChildren();
        }

        if (signaturesModal && typeof window.hideSiteModal === 'function') {
            window.hideSiteModal(signaturesModal);
        } else if (signaturesModal) {
            signaturesModal.style.display = 'none';
        }
    }

    async function loadSurveySignaturesContent(survey) {
        const response = await fetch(`/surveys/${survey.id_survey}/signatures`, {
            cache: 'no-store',
            headers: {
                'X-Admin-Inline-Request': 'true'
            }
        });

        if (!response.ok) {
            throw new Error(
                window.getResponseErrorMessage
                    ? window.getResponseErrorMessage(response, 'Не удалось загрузить прохождение')
                    : `Не удалось загрузить прохождение: ${response.status}`
            );
        }

        const html = await response.text();
        const parsedDocument = parseHtmlDocument(html);
        loadStylesheetsFromDocument(parsedDocument);
        return extractSignaturesContent(parsedDocument);
    }

    async function openSurveyCompletionModalFromTrigger(trigger) {
        try {
            const survey = buildSurveyData(trigger);
            ensureSignaturesModal();

            signaturesTitle.textContent = survey.name_survey
                ? `Проверка прохождения: ${survey.name_survey}`
                : 'Проверка прохождения';
            signaturesHost.replaceChildren(createStatusMessage('Загрузка прохождения...', 'loading'));

            if (typeof window.showSiteModal === 'function') {
                window.showSiteModal(signaturesModal);
            } else {
                signaturesModal.style.display = 'flex';
            }

            const content = await loadSurveySignaturesContent(survey);
            signaturesHost.replaceChildren(content);
            window.mountSortableTables?.(signaturesHost);
        } catch (error) {
            if (signaturesHost) {
                signaturesHost.replaceChildren(createStatusMessage(error.message || 'Не удалось загрузить прохождение.', 'error'));
                return;
            }

            window.siteNotify?.(error.message || 'Не удалось загрузить прохождение.', 'error');
        }
    }

    function ensureExtensionModal() {
        if (extensionModal && extensionHost) {
            return;
        }

        extensionModal = document.createElement('div');
        extensionModal.id = 'surveyExtensionModal';
        extensionModal.className = 'modal admin-extension-modal';
        extensionModal.setAttribute('aria-hidden', 'true');

        const modalContent = document.createElement('div');
        modalContent.className = 'modal-content';

        const modalHeader = document.createElement('div');
        modalHeader.className = 'modal-header';

        const title = document.createElement('h2');
        title.className = 'h2_modal';
        title.textContent = 'Продлить доступ';

        const closeButton = document.createElement('button');
        closeButton.type = 'button';
        closeButton.className = 'modal-close';
        closeButton.setAttribute('aria-label', 'Закрыть');

        const closeIcon = document.createElement('i');
        closeIcon.className = 'fas fa-xmark';
        closeIcon.setAttribute('aria-hidden', 'true');
        closeButton.appendChild(closeIcon);

        extensionHost = document.createElement('div');
        extensionHost.className = 'modal-body admin-extension-modal__body';

        const modalFooter = document.createElement('div');
        modalFooter.className = 'modal-footer';

        extensionSubmitButton = document.createElement('button');
        extensionSubmitButton.type = 'button';
        extensionSubmitButton.className = 'modal_btn modal_btn-primary';
        extensionSubmitButton.textContent = 'Продлить доступ';

        extensionCancelButton = document.createElement('button');
        extensionCancelButton.type = 'button';
        extensionCancelButton.className = 'modal_btn modal_btn-secondary';
        extensionCancelButton.textContent = 'Отмена';

        closeButton.addEventListener('click', closeSurveyExtensionModal);

        modalHeader.appendChild(title);
        modalHeader.appendChild(closeButton);
        modalFooter.appendChild(extensionSubmitButton);
        modalFooter.appendChild(extensionCancelButton);
        modalContent.appendChild(modalHeader);
        modalContent.appendChild(extensionHost);
        modalContent.appendChild(modalFooter);
        extensionModal.appendChild(modalContent);
        document.body.appendChild(extensionModal);
    }

    function closeSurveyExtensionModal() {
        if (typeof extensionCleanup === 'function') {
            extensionCleanup();
            extensionCleanup = null;
        }

        if (extensionHost) {
            extensionHost.replaceChildren();
        }

        if (extensionModal && typeof window.hideSiteModal === 'function') {
            window.hideSiteModal(extensionModal);
        } else if (extensionModal) {
            extensionModal.style.display = 'none';
        }
    }

    function openSurveyExtensionModalFromTrigger(trigger) {
        try {
            const survey = buildSurveyData(trigger);
            const mountExtensionModal = window.AdminInlineAppPages?.mountExtensionModal;
            if (typeof mountExtensionModal !== 'function') {
                throw new Error('Модуль продления анкеты не загружен.');
            }

            ensureExtensionModal();
            closeSurveyExtensionModal();

            extensionCleanup = mountExtensionModal(extensionHost, {
                survey,
                onClose: closeSurveyExtensionModal,
                submitButton: extensionSubmitButton,
                cancelButton: extensionCancelButton
            }) || null;

            if (typeof window.showSiteModal === 'function') {
                window.showSiteModal(extensionModal);
            } else {
                extensionModal.style.display = 'flex';
            }
        } catch (error) {
            window.siteNotify?.(error.message || 'Не удалось открыть форму продления.', 'error');
        }
    }

    async function deleteSurveyFromTrigger(trigger) {
        try {
            const survey = buildSurveyData(trigger);
            const isConfirmed = await window.siteConfirm(
                `Удалить анкету "${survey.name_survey || 'Без названия'}"?`,
                {
                    title: 'Удаление анкеты',
                    confirmText: 'Удалить',
                    cancelText: 'Отмена'
                }
            );

            if (!isConfirmed) {
                return;
            }

            const response = await fetch(`/surveys/${survey.id_survey}/delete`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ surveyId: survey.id_survey })
            });

            const responseText = await response.text();
            let payload = null;

            try {
                payload = responseText ? JSON.parse(responseText) : null;
            } catch (parseError) {
                console.warn('Не удалось разобрать ответ удаления анкеты:', parseError);
            }

            if (!response.ok || !payload?.success) {
                throw new Error(payload?.message || responseText || 'Не удалось удалить анкету.');
            }

            const target = resolveSurveyListTarget();
            if (typeof window.handleAdminMutationSuccess === 'function') {
                await window.handleAdminMutationSuccess({
                    message: payload?.message || 'Анкета успешно удалена.',
                    tabName: target.tabName,
                    fallbackUrl: target.fallbackUrl
                });
                return;
            }

            window.location.assign(target.fallbackUrl);
        } catch (error) {
            window.siteNotify?.(error.message || 'Не удалось удалить анкету.', 'error');
        }
    }

    document.addEventListener('site-modal:hidden', function (event) {
        if (event.target?.id !== 'surveyEditorModal') {
            return;
        }

        syncSurveyListHistory();
    });

    window.setTimeout(function () {
        if (window.location.pathname.toLowerCase() === '/surveys/create') {
            openAddSurveyModal();
            return;
        }

        if (/\/surveys\/\d+\/edit$/i.test(window.location.pathname)
            || /\/surveys\/archive\/\d+\/edit$/i.test(window.location.pathname)) {
            openEditSurveyModal();
        }
    }, 0);

    window.openSurveyExtensionModalFromTrigger = openSurveyExtensionModalFromTrigger;
    window.openSurveyCompletionModalFromTrigger = openSurveyCompletionModalFromTrigger;
    window.openSurveySignaturesModalFromTrigger = openSurveyCompletionModalFromTrigger;
    window.deleteSurveyFromTrigger = deleteSurveyFromTrigger;
    window.openAddSurveyModal = openAddSurveyModal;
    window.openCopySurveyModalById = openCopySurveyModalById;
    window.openCopySurveyModalFromTrigger = openCopySurveyModalFromTrigger;
    window.openEditSurveyModal = openEditSurveyModal;
    window.openEditSurveyModalFromTrigger = openEditSurveyModalFromTrigger;
    window.closeSurveyEditorModal = closeSurveyEditorModal;
    window.handleSurveyCreateSuccess = handleSurveyCreateSuccess;
    window.handleSurveyUpdateSuccess = handleSurveyUpdateSuccess;
})();
