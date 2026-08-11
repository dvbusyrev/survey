(function () {
    const PAGE_SELECTOR = '.app-page[data-page="surveys-list"], .app-page[data-page="surveys-archive"]';
    let extensionModal = null;
    let extensionFrame = null;
    let extensionHost = null;
    let extensionCleanup = null;
    let extensionSubmitButton = null;
    let extensionCancelButton = null;
    let extensionPeriodFrame = null;
    let extensionPeriodHost = null;
    let extensionPeriodSubmitButton = null;
    let extensionPeriodCancelButton = null;
    let extensionPeriodSubmitting = false;
    let signaturesModal = null;
    let signaturesFrame = null;
    let signaturesHost = null;
    let signaturesTitle = null;
    let detailsModal = null;
    let detailsFrame = null;
    let detailsHost = null;
    let detailsRequestToken = 0;
    const loadedStylesheetUrls = new Set();
    let loadedStylesheetsPrimed = false;
    const mountedPageControllers = new Set();
    const mountedPageControllerByPage = new WeakMap();
    let pendingRouteTimer = null;
    let surveyDeletePending = false;
    const SURVEY_ROW_SELECTOR = '.surveys-table tbody tr[data-survey-id]';

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

    function setSurveyEditorModalVisible(isVisible) {
        const modal = document.getElementById('surveyEditorModal');
        if (!modal) {
            return false;
        }

        return setModalVisible(modal, isVisible);
    }

    function setSurveyEditorModalTitle(title) {
        const titleElement = document.querySelector('#surveyEditorModal [data-role="survey-editor-title"]');
        if (titleElement) {
            titleElement.textContent = title;
        }
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

        setSurveyEditorModalTitle('Добавление анкеты');
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
        setSurveyEditorModalTitle('Копирование анкеты');
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

    function ensureDetailsModal() {
        if (detailsModal && detailsHost) {
            return;
        }

        const frame = createSurveyModalFrame({
            id: 'surveyDetailsModal',
            className: 'survey-details-modal',
            title: 'Просмотр анкеты',
            bodyClassName: 'survey-details-modal__body',
            footer: false,
            onClose: closeSurveyDetailsModal
        });

        detailsModal = frame.modal;
        detailsFrame = frame;
        detailsHost = frame.body;
    }

    function closeSurveyDetailsModal() {
        detailsRequestToken += 1;
        detailsHost?.replaceChildren();
        detailsFrame?.hide?.();
    }

    function createDetailsField(label, value, className = '') {
        const field = window.AppUi.createField({
            className,
            text: String(value || '').trim() || 'Не указано'
        });

        return window.AppUi.createFieldGroup({ label, field });
    }

    function createOrganizationField(organizations, label = 'Организации') {
        const values = Array.isArray(organizations)
            ? organizations.map((value) => String(value || '').trim()).filter(Boolean)
            : [];
        const field = window.AppUi.createField({
            className: 'survey-details-modal__organizations'
        });

        if (values.length === 0) {
            field.textContent = label === 'Организация'
                ? 'Организация не выбрана'
                : 'Организации не выбраны';
            field.classList.add('app-field-placeholder');
        } else if (label === 'Организация') {
            field.textContent = values[0];
        } else {
            values.forEach((name) => {
                field.appendChild(window.AppUi.createElement('span', {
                    className: 'app-chip',
                    text: name
                }));
            });
        }

        return window.AppUi.createFieldGroup({ label, field });
    }

    function createCriteriaTable(criteria) {
        const values = Array.isArray(criteria)
            ? criteria.map((value) => String(value || '').trim()).filter(Boolean)
            : [];
        const tableWrap = window.AppUi.createElement('div', {
            className: 'app-modal-table-wrap survey-details-modal__table-wrap'
        });
        const tableParts = window.AppUi.createTable({
            className: 'app-modal-table survey-details-modal__table',
            dataset: { disableColumnSort: 'true' },
            headerCells: [{ className: 'table-th--start table-th--end', text: 'Критерий' }]
        });

        if (values.length === 0) {
            tableParts.appendRow([{
                className: 'table-empty-cell',
                text: 'Критерии не добавлены'
            }]);
        } else {
            values.forEach((criterion) => tableParts.appendRow([{ text: criterion }]));
        }

        tableWrap.appendChild(tableParts.table);
        return tableWrap;
    }

    function renderSurveyDetails(details, organizationLabel = 'Организации') {
        detailsHost.replaceChildren(
            createDetailsField('Название анкеты', details?.name),
            createDetailsField('Описание', details?.description),
            createDetailsField('Дата начала', details?.dateBegin),
            createDetailsField('Дата конца', details?.dateEnd),
            createOrganizationField(details?.organizations, organizationLabel),
            createCriteriaTable(details?.criteria)
        );
    }

    async function fetchSurveyDetails(surveyId) {
        const response = await fetch(`/survey/${surveyId}/details`, {
            cache: 'no-store',
            headers: { Accept: 'application/json' }
        });
        const responseText = await response.text();
        const payload = responseText ? JSON.parse(responseText) : null;

        if (!response.ok) {
            throw new Error(payload?.message || 'Не удалось загрузить анкету.');
        }

        return payload || {};
    }

    async function openSurveyDetailsModalFromRow(row) {
        const survey = buildSurveyData(row);
        const requestToken = ++detailsRequestToken;

        try {
            const details = await fetchSurveyDetails(survey.id_survey);
            if (requestToken !== detailsRequestToken) {
                return;
            }

            ensureDetailsModal();
            detailsFrame?.setTitle?.(survey.is_extension ? 'Просмотр продления' : 'Просмотр анкеты');
            renderSurveyDetails(survey.is_extension
                ? {
                    ...details,
                    name: survey.original_name || details?.name,
                    dateBegin: window.AppDate?.toDisplay?.(survey.date_begin) || survey.date_begin,
                    dateEnd: survey.date_end
                        ? window.AppDate?.toDisplay?.(survey.date_end) || survey.date_end
                        : 'Не указана',
                    organizations: survey.organizations
                }
                : details,
                survey.is_extension ? 'Организация' : 'Организации');
            detailsFrame?.show?.();
        } catch (error) {
            if (requestToken === detailsRequestToken) {
                window.AppUi?.notify?.(error.message || 'Не удалось загрузить анкету.', 'error');
            }
        }
    }

    function handleSurveyCreateSuccess(result) {
        closeSurveyEditorModal();
        const target = resolveSurveyListTarget();
        if (typeof window.handleAdminMutationSuccess === 'function') {
            window.handleAdminMutationSuccess({
                message: result?.message || 'Анкета успешно создана.',
                tabName: target.tabName,
                fallbackUrl: target.fallbackUrl,
                options: {
                    force: true,
                    historyMode: 'replace',
                    scrollMode: 'carry'
                }
            });
            return;
        }

        window.location.assign('/survey');
    }

    function handleSurveyUpdateSuccess(result) {
        closeSurveyEditorModal();
        const target = resolveSurveyListTarget();
        if (typeof window.handleAdminMutationSuccess === 'function') {
            window.handleAdminMutationSuccess({
                message: result?.message || 'Анкета успешно обновлена.',
                tabName: target.tabName,
                fallbackUrl: target.fallbackUrl,
                options: {
                    force: true,
                    historyMode: 'replace',
                    scrollMode: 'carry'
                }
            });
            return;
        }

        window.location.assign('/survey');
    }

    function buildSurveyData(trigger) {
        const surveyId = Number.parseInt(trigger?.dataset?.surveyId || '', 10);
        if (!Number.isFinite(surveyId) || surveyId <= 0) {
            throw new Error('Не найден идентификатор анкеты.');
        }

        let organizations = [];
        try {
            const parsedOrganizations = JSON.parse(trigger?.dataset?.surveyOrganizations || '[]');
            if (Array.isArray(parsedOrganizations)) {
                organizations = parsedOrganizations
                    .map((name) => String(name || '').trim())
                    .filter(Boolean);
            }
        } catch (error) {
            organizations = [];
        }

        return {
            id_survey: surveyId,
            name_survey: trigger?.dataset?.surveyName || '',
            original_name: trigger?.dataset?.surveyOriginalName || '',
            date_begin: trigger?.dataset?.surveyDateBegin || '',
            date_end: trigger?.dataset?.surveyDateEnd || '',
            organizations,
            is_extension: trigger?.dataset?.isExtension === 'true'
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

    function createSurveyNameField(surveyName) {
        const field = window.AppUi.createField({
            className: 'survey-signatures-modal__survey-name',
            text: String(surveyName || '').trim() || 'Без названия'
        });

        return window.AppUi.createFieldGroup({
            className: 'survey-signatures-modal__survey-name-group',
            label: 'Название анкеты',
            field
        });
    }

    function extractSignaturesContent(parsedDocument) {
        const sourceTable = parsedDocument.querySelector('.answers-page__signatures-table');
        if (sourceTable) {
            const tableWrap = window.AppUi.createElement('div', {
                className: 'app-modal-table-wrap survey-signatures-modal__table-wrap'
            });
            tableWrap.appendChild(sourceTable.cloneNode(true));
            return tableWrap;
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
            title: 'Просмотр прохождения',
            bodyClassName: 'app-modal-body--compact survey-signatures-modal__body',
            footer: false,
            onClose: closeSurveySignaturesModal
        });

        signaturesModal = frame.modal;
        signaturesFrame = frame;
        signaturesHost = frame.body;
        signaturesTitle = frame.title;
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
                    : `Не удалось загрузить прохождение. Сервер вернул ошибку (${response.status}).`
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

            signaturesTitle.textContent = 'Просмотр прохождения';

            const content = await loadSurveySignaturesContent(survey);
            signaturesHost.replaceChildren(
                createSurveyNameField(survey.name_survey),
                content
            );
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
            title: 'Продление доступа',
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

    function closeSurveyExtensionPeriodModal() {
        extensionPeriodSubmitting = false;
        extensionPeriodHost?.replaceChildren();
        extensionPeriodFrame?.hide?.();
    }

    function createExtensionPeriodDateField(id, label, value, minimumDate = '') {
        const input = window.AppUi.createField({
            tagName: 'input',
            id,
            type: 'date',
            required: true,
            dataset: {
                dateNative: 'true',
                dateLabel: label,
                ...(minimumDate ? { dateMin: minimumDate } : {})
            },
            attrs: {
                lang: 'ru-RU',
                required: true,
                ...(minimumDate ? { min: minimumDate } : {})
            }
        });

        input.value = value || '';
        return {
            input,
            group: window.AppUi.createFieldGroup({
                label,
                labelFor: id,
                field: input
            })
        };
    }

    function ensureSurveyExtensionPeriodModal() {
        if (extensionPeriodFrame && extensionPeriodHost) {
            return;
        }

        extensionPeriodFrame = createSurveyModalFrame({
            id: 'surveyExtensionPeriodModal',
            className: 'admin-extension-modal',
            title: 'Редактирование продления',
            bodyClassName: 'admin-extension-modal__body',
            onClose: closeSurveyExtensionPeriodModal
        });
        extensionPeriodHost = extensionPeriodFrame.body;
        extensionPeriodCancelButton = window.AppUi.createButton({
            variant: 'secondary',
            text: 'Отмена'
        });
        extensionPeriodSubmitButton = window.AppUi.createButton({
            variant: 'primary',
            text: 'Сохранить'
        });
        extensionPeriodCancelButton.addEventListener('click', closeSurveyExtensionPeriodModal);
        extensionPeriodFrame.footer.appendChild(extensionPeriodCancelButton);
        extensionPeriodFrame.footer.appendChild(extensionPeriodSubmitButton);
    }

    function readExtensionPeriodData(trigger) {
        const surveyId = Number.parseInt(trigger?.dataset?.surveyId || '', 10);
        const organizationId = Number.parseInt(trigger?.dataset?.organizationId || '', 10);
        if (!Number.isFinite(surveyId) || surveyId <= 0 || !Number.isFinite(organizationId) || organizationId <= 0) {
            throw new Error('Продлённое назначение не найдено.');
        }

        return {
            surveyId,
            organizationId,
            surveyName: trigger?.dataset?.surveyName || '',
            organizationName: trigger?.dataset?.organizationName || '',
            dateBegin: trigger?.dataset?.dateBegin || '',
            dateEnd: trigger?.dataset?.dateEnd || ''
        };
    }

    async function saveSurveyExtensionPeriod(extension, dateEndInput) {
        if (extensionPeriodSubmitting) {
            return;
        }

        const dateEnd = window.AppDate?.getInputIso?.(dateEndInput) || dateEndInput.value;
        const errors = [];

        if (!dateEnd) {
            errors.push('Укажите дату конца.');
        }
        if (extension.dateBegin && dateEnd
            && (window.AppDate?.compare?.(dateEnd, extension.dateBegin) ?? 0) <= 0) {
            errors.push('Дата конца должна быть позже даты начала.');
        }

        const today = window.AppDate?.todayIso?.() || new Date().toISOString().split('T')[0];
        if (dateEnd && (window.AppDate?.compare?.(dateEnd, today) ?? 0) < 0) {
            errors.push('Дата конца не может быть раньше сегодняшней даты.');
        }

        if (errors.length > 0) {
            window.AppValidation?.notifyErrors?.([...new Set(errors)]);
            return;
        }

        extensionPeriodSubmitting = true;
        extensionPeriodSubmitButton.disabled = true;
        extensionPeriodSubmitButton.textContent = 'Сохранение...';

        try {
            const response = await fetch(
                `/survey/${extension.surveyId}/extensions/${extension.organizationId}/period`,
                {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': window.AppHttp?.getAntiforgeryToken() || ''
                    },
                    body: JSON.stringify({ dateEnd })
                }
            );
            const responseText = await response.text();
            let payload = null;

            try {
                payload = responseText ? JSON.parse(responseText) : null;
            } catch (parseError) {
                console.warn('Не удалось разобрать ответ изменения периода продления:', parseError);
            }

            if (!response.ok || !payload?.success) {
                throw new Error(payload?.message || responseText || 'Не удалось изменить период продления.');
            }

            closeSurveyExtensionPeriodModal();
            const target = resolveSurveyListTarget();
            if (typeof window.handleAdminMutationSuccess === 'function') {
                await window.handleAdminMutationSuccess({
                    message: payload.message || 'Дата конца продления успешно изменена.',
                    tabName: target.tabName,
                    fallbackUrl: target.fallbackUrl
                });
                return;
            }

            window.AppUi?.notify?.(payload.message || 'Дата конца продления успешно изменена.', 'success');
            window.location.assign(target.fallbackUrl);
        } catch (error) {
            window.AppUi?.notify?.(error.message || 'Не удалось изменить период продления.', 'error');
        } finally {
            extensionPeriodSubmitting = false;
            extensionPeriodSubmitButton.disabled = false;
            extensionPeriodSubmitButton.textContent = 'Сохранить';
        }
    }

    function openSurveyExtensionPeriodModalFromTrigger(trigger) {
        try {
            const extension = readExtensionPeriodData(trigger);
            ensureSurveyExtensionPeriodModal();
            extensionPeriodHost.replaceChildren();

            const dateEndField = createExtensionPeriodDateField(
                'extensionPeriodDateEnd',
                'Дата конца',
                extension.dateEnd,
                window.AppDate?.todayIso?.() || ''
            );

            extensionPeriodHost.appendChild(createSurveyNameField(extension.surveyName));
            extensionPeriodHost.appendChild(window.AppUi.createFieldGroup({
                label: 'Организация',
                field: window.AppUi.createField({ text: extension.organizationName })
            }));
            extensionPeriodHost.appendChild(dateEndField.group);

            window.AppDate?.enhanceDateInputs?.(extensionPeriodHost);
            window.AppDate?.setInputValue?.(dateEndField.input, extension.dateEnd);
            extensionPeriodSubmitButton.onclick = () => saveSurveyExtensionPeriod(
                extension,
                dateEndField.input
            );
            extensionPeriodFrame?.show?.();
        } catch (error) {
            window.AppUi?.notify?.(error.message || 'Не удалось открыть редактирование продления.', 'error');
        }
    }

    async function deleteSurveyFromTrigger(trigger) {
        if (surveyDeletePending) {
            return;
        }

        surveyDeletePending = true;
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
                    message: payload?.message || 'Анкета успешно удалена.',
                    tabName: target.tabName,
                    fallbackUrl: target.fallbackUrl
                });
                return;
            }

            window.location.assign(target.fallbackUrl);
        } catch (error) {
            window.AppUi?.notify?.(error.message || 'Не удалось удалить анкету.', 'error');
        } finally {
            surveyDeletePending = false;
        }
    }

    async function deleteSurveyExtensionFromTrigger(trigger) {
        if (surveyDeletePending) {
            return;
        }

        surveyDeletePending = true;
        try {
            const extension = readExtensionPeriodData(trigger);
            const organizationSuffix = extension.organizationName
                ? ` для организации "${extension.organizationName}"`
                : '';
            const isConfirmed = await window.siteConfirm(
                `Удалить продление анкеты "${extension.surveyName || 'Без названия'}"${organizationSuffix}?`,
                {
                    title: 'Удаление продления',
                    confirmText: 'Удалить',
                    cancelText: 'Отмена'
                }
            );

            if (!isConfirmed) {
                return;
            }

            const response = await fetch(
                `/survey/${extension.surveyId}/extensions/${extension.organizationId}/delete`,
                { method: 'POST' }
            );
            const responseText = await response.text();
            let payload = null;

            try {
                payload = responseText ? JSON.parse(responseText) : null;
            } catch (parseError) {
                console.warn('Не удалось разобрать ответ удаления продления:', parseError);
            }

            if (!response.ok || !payload?.success) {
                throw new Error(payload?.message || responseText || 'Не удалось удалить продление.');
            }

            const target = resolveSurveyListTarget();
            if (typeof window.handleAdminMutationSuccess === 'function') {
                await window.handleAdminMutationSuccess({
                    message: payload.message || 'Продление успешно удалено.',
                    tabName: target.tabName,
                    fallbackUrl: target.fallbackUrl
                });
                return;
            }

            window.AppUi?.notify?.(payload.message || 'Продление успешно удалено.', 'success');
            window.location.assign(target.fallbackUrl);
        } catch (error) {
            window.AppUi?.notify?.(error.message || 'Не удалось удалить продление.', 'error');
        } finally {
            surveyDeletePending = false;
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
        const rowViewer = window.AppUi?.mountRowViewer?.({
            root: page,
            rowSelector: SURVEY_ROW_SELECTOR,
            label: 'Смотреть',
            onOpen: openSurveyDetailsModalFromRow
        });

        const controller = {
            page,
            destroy() {
                if (isDestroyed) {
                    return;
                }

                isDestroyed = true;
                page.removeEventListener('page:unmount', controller.destroy);
                document.removeEventListener('site-modal:hidden', handleSurveyEditorHidden);
                rowViewer?.destroy?.();
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
    window.openSurveyExtensionPeriodModalFromTrigger = openSurveyExtensionPeriodModalFromTrigger;
    window.openSurveyCompletionModalFromTrigger = openSurveyCompletionModalFromTrigger;
    window.openSurveySignaturesModalFromTrigger = openSurveyCompletionModalFromTrigger;
    window.deleteSurveyFromTrigger = deleteSurveyFromTrigger;
    window.deleteSurveyExtensionFromTrigger = deleteSurveyExtensionFromTrigger;
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
