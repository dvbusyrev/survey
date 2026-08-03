(function () {
    const PAGE_SELECTOR = '.app-page[data-page="surveys-list"], .app-page[data-page="surveys-archive"]';
    let extensionModal = null;
    let extensionFrame = null;
    let extensionHost = null;
    let extensionCleanup = null;
    let extensionSubmitButton = null;
    let extensionCancelButton = null;
    let signaturesModal = null;
    let signaturesFrame = null;
    let signaturesHost = null;
    let signaturesTitle = null;
    const loadedStylesheetUrls = new Set();
    let loadedStylesheetsPrimed = false;
    const mountedPageControllers = new Set();
    const mountedPageControllerByPage = new WeakMap();
    let pendingRouteTimer = null;

    function createSurveyModalFrame(options) {
        if (typeof window.createSiteModalFrame !== 'function') {
            throw new Error('Модуль модальных окон не загружен.');
        }

        const frame = window.createSiteModalFrame(options);
        if (frame.modal && !frame.modal.parentNode) {
            document.body.appendChild(frame.modal);
        }
        return frame;
    }

    function setModalVisible(modal, isVisible) {
        if (!modal) {
            return false;
        }

        const action = isVisible ? window.showSiteModal : window.hideSiteModal;
        return typeof action === 'function' ? action(modal) : false;
    }

    function isArchiveSurveyListRoute(path) {
        const currentPath = String(path || window.location.pathname || '').toLowerCase();
        return currentPath === '/survey/archive'
            || currentPath === '/surveys/archive'
            || /\/survey\/archive\/\d+\/edit$/.test(currentPath)
            || /\/surveys\/archive\/\d+\/edit$/.test(currentPath);
    }

    function resolveSurveyListTarget(path) {
        return isArchiveSurveyListRoute(path)
            ? { tabName: 'archived_surveys', fallbackUrl: '/survey/archive' }
            : { tabName: 'get_surveys', fallbackUrl: '/survey' };
    }

    function isSurveyEditorRoute(path) {
        const currentPath = String(path || window.location.pathname || '').toLowerCase();
        return currentPath === '/survey/create'
            || currentPath === '/surveys/create'
            || /\/survey\/\d+\/edit$/.test(currentPath)
            || /\/surveys\/\d+\/edit$/.test(currentPath)
            || /\/survey\/archive\/\d+\/edit$/.test(currentPath)
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

        return false;
    }

    function setSurveyEditorModalVisible(isVisible) {
        const modal = document.getElementById('surveyEditorModal');
        if (!modal) {
            return false;
        }

        return setModalVisible(modal, isVisible);
    }

    function closeSurveyEditorModal() {
        setSurveyEditorModalVisible(false);
        syncSurveyListHistory();
    }

    function openAddSurveyModal() {
        if (document.getElementById('surveyId')) {
            if (typeof window.refreshAdminUi === 'function') {
                window.refreshAdminUi({
                    tabName: 'add_survey',
                    fallbackUrl: '/survey/create',
                    options: {
                        historyMode: 'replace',
                        scrollMode: 'carry'
                    }
                });
            } else {
                window.location.assign('/survey/create');
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
        const response = await fetch(`/survey/${surveyId}/copy-template`, {
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

        if (typeof window.refreshAdminUi === 'function') {
            await window.refreshAdminUi({
                tabName: target.tabName,
                fallbackUrl: target.fallbackUrl,
                options: {
                    historyMode: 'replace',
                    scrollMode: 'carry'
                }
            });
        } else {
            window.location.assign(target.fallbackUrl);
        }

        return false;
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
            window.AppUi?.notify?.(error.message || 'Не удалось подготовить копирование анкеты.', 'error');
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

            const editUrl = isArchiveSurveyListRoute()
                ? `/survey/archive/${survey.id_survey}/edit`
                : `/survey/${survey.id_survey}/edit`;

            if (typeof window.refreshAdminUi === 'function') {
                window.refreshAdminUi({
                    tabName: isArchiveSurveyListRoute() ? 'update_archived_survey' : 'update_survey',
                    id: survey.id_survey,
                    fallbackUrl: editUrl,
                    options: {
                        scrollMode: 'restore'
                    }
                });
                return;
            }

            window.location.assign(editUrl);
        } catch (error) {
            window.AppUi?.notify?.(error.message || 'Не удалось открыть редактирование анкеты.', 'error');
        }
    }

    function handleSurveyCreateSuccess(result) {
        window.AppUi?.notify?.(result?.message || 'Анкета успешно создана', 'success');
        closeSurveyEditorModal();
        if (!refreshSurveyListPreservingScroll()) {
            window.location.assign('/survey');
        }
    }

    function handleSurveyUpdateSuccess(result) {
        window.AppUi?.notify?.(result?.message || 'Анкета успешно обновлена', 'success');
        closeSurveyEditorModal();
        if (!refreshSurveyListPreservingScroll()) {
            window.location.assign('/survey');
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

    function primeLoadedStylesheets() {
        if (loadedStylesheetsPrimed) {
            return;
        }

        document.querySelectorAll('link[rel="stylesheet"][href]').forEach((link) => {
            const href = normalizeAssetUrl(link.href);
            if (href) {
                loadedStylesheetUrls.add(href);
            }
        });

        loadedStylesheetsPrimed = true;
    }

    function loadStylesheetsFromDocument(parsedDocument) {
        primeLoadedStylesheets();

        parsedDocument.querySelectorAll('link[rel="stylesheet"][href]').forEach((sourceLink) => {
            const href = normalizeAssetUrl(sourceLink.getAttribute('href'));
            if (!href || loadedStylesheetUrls.has(href)) {
                return;
            }

            loadedStylesheetUrls.add(href);

            const link = window.AppUi.createElement('link', {
                attrs: {
                    rel: 'stylesheet',
                    href,
                    media: sourceLink.media || null
                }
            });

            document.head.appendChild(link);
        });
    }

    function parseHtmlDocument(html) {
        const parser = new DOMParser();
        return parser.parseFromString(html || '', 'text/html');
    }

    function createStatusMessage(message, type = 'loading') {
        return window.AppUi.createElement('div', {
            className: type === 'error'
                ? 'error survey-signatures-modal__error'
                : 'loading survey-signatures-modal__loading',
            text: message
        });
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

        const frame = createSurveyModalFrame({
            id: 'surveySignaturesModal',
            className: 'survey-signatures-modal',
            title: 'Проверка прохождения',
            bodyClassName: 'survey-signatures-modal__body',
            onClose: closeSurveySignaturesModal
        });

        const footerCloseButton = window.AppUi.createButton({
            variant: 'secondary',
            text: 'Закрыть'
        });
        footerCloseButton.addEventListener('click', closeSurveySignaturesModal);

        signaturesModal = frame.modal;
        signaturesFrame = frame;
        signaturesHost = frame.body;
        signaturesTitle = frame.title;
        frame.footer.appendChild(footerCloseButton);
    }

    function closeSurveySignaturesModal() {
        if (signaturesHost) {
            signaturesHost.replaceChildren();
        }

        signaturesFrame?.hide?.();
    }

    async function loadSurveySignaturesContent(survey) {
        const response = await fetch(`/survey/${survey.id_survey}/signatures`, {
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

            const content = await loadSurveySignaturesContent(survey);
            signaturesHost.replaceChildren(content);
            window.mountSortableTables?.(signaturesHost);

            signaturesFrame?.show?.();
        } catch (error) {
            if (signaturesHost) {
                signaturesHost.replaceChildren();
                window.AppUi?.notify?.(error.message || 'Не удалось загрузить прохождение.', 'error');
                return;
            }

            window.AppUi?.notify?.(error.message || 'Не удалось загрузить прохождение.', 'error');
        }
    }

    function ensureExtensionModal() {
        if (extensionModal && extensionHost) {
            return;
        }

        const frame = createSurveyModalFrame({
            id: 'surveyExtensionModal',
            className: 'admin-extension-modal',
            title: 'Продлить доступ',
            bodyClassName: 'admin-extension-modal__body',
            onClose: closeSurveyExtensionModal
        });

        extensionSubmitButton = window.AppUi.createButton({
            variant: 'primary',
            text: 'Продлить доступ'
        });

        extensionCancelButton = window.AppUi.createButton({
            variant: 'secondary',
            text: 'Отмена'
        });

        extensionModal = frame.modal;
        extensionFrame = frame;
        extensionHost = frame.body;
        frame.footer.appendChild(extensionCancelButton);
        frame.footer.appendChild(extensionSubmitButton);
    }

    function closeSurveyExtensionModal() {
        if (typeof extensionCleanup === 'function') {
            extensionCleanup();
            extensionCleanup = null;
        }

        if (extensionHost) {
            extensionHost.replaceChildren();
        }

        extensionFrame?.hide?.();
    }

    function openSurveyExtensionModalFromTrigger(trigger) {
        try {
            const survey = buildSurveyData(trigger);
            const mountExtensionModal = window.AdminSurveyExtensionModal?.mount;
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

            extensionFrame?.show?.();
        } catch (error) {
            window.AppUi?.notify?.(error.message || 'Не удалось открыть форму продления.', 'error');
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

            const response = await fetch(`/survey/${survey.id_survey}/delete`, {
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
                    message: payload?.message || 'Анкета успешно удалена',
                    tabName: target.tabName,
                    fallbackUrl: target.fallbackUrl
                });
                return;
            }

            window.location.assign(target.fallbackUrl);
        } catch (error) {
            window.AppUi?.notify?.(error.message || 'Не удалось удалить анкету.', 'error');
        }
    }

    function handleSurveyEditorHidden(event) {
        if (event.target?.id !== 'surveyEditorModal') {
            return;
        }

        syncSurveyListHistory();
    }

    function openEditorFromCurrentRoute() {
        if (window.location.pathname.toLowerCase() === '/survey/create'
            || window.location.pathname.toLowerCase() === '/surveys/create') {
            openAddSurveyModal();
            return;
        }

        if (/\/survey\/\d+\/edit$/i.test(window.location.pathname)
            || /\/surveys\/\d+\/edit$/i.test(window.location.pathname)
            || /\/survey\/archive\/\d+\/edit$/i.test(window.location.pathname)
            || /\/surveys\/archive\/\d+\/edit$/i.test(window.location.pathname)) {
            openEditSurveyModal();
        }
    }

    function mount(page) {
        if (!(page instanceof Element) || !page.matches(PAGE_SELECTOR)) {
            return null;
        }

        const existingController = mountedPageControllerByPage.get(page);
        if (existingController) {
            return existingController;
        }

        let isDestroyed = false;
        const controller = {
            page,
            destroy() {
                if (isDestroyed) {
                    return;
                }

                isDestroyed = true;
                page.removeEventListener('page:unmount', controller.destroy);
                document.removeEventListener('site-modal:hidden', handleSurveyEditorHidden);
                mountedPageControllerByPage.delete(page);
                mountedPageControllers.delete(controller);

                if (pendingRouteTimer) {
                    window.clearTimeout(pendingRouteTimer);
                    pendingRouteTimer = null;
                }
            }
        };

        page.addEventListener('page:unmount', controller.destroy);
        document.addEventListener('site-modal:hidden', handleSurveyEditorHidden);

        if (pendingRouteTimer) {
            window.clearTimeout(pendingRouteTimer);
        }
        pendingRouteTimer = window.setTimeout(() => {
            pendingRouteTimer = null;
            if (!isDestroyed) {
                openEditorFromCurrentRoute();
            }
        }, 0);

        mountedPageControllerByPage.set(page, controller);
        mountedPageControllers.add(controller);
        return controller;
    }

    function destroy(root = document) {
        if (root === document || root?.nodeType === Node.DOCUMENT_NODE) {
            Array.from(mountedPageControllers).forEach((controller) => controller.destroy());
            return;
        }

        if (!(root instanceof Element)) {
            return;
        }

        Array.from(mountedPageControllers).forEach((controller) => {
            if (controller.page === root || root.contains(controller.page)) {
                controller.destroy();
            }
        });
    }

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
    window.SurveyAdminList = {
        mount,
        destroy
    };
})();
