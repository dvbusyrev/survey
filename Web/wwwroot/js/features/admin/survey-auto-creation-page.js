(function () {
    const PAGE_SELECTOR = '[data-page="survey-auto-creation"]';

    function getPageRoot(root = document) {
        if (root?.matches?.(PAGE_SELECTOR)) {
            return root;
        }

        return root?.querySelector?.(PAGE_SELECTOR) || document.querySelector(PAGE_SELECTOR);
    }

    function parseBootstrap(root = document) {
        const node = root?.querySelector?.('#survey-auto-creation-bootstrap')
            || document.getElementById('survey-auto-creation-bootstrap');
        if (!node?.textContent) {
            return { isEnabled: false, selectedSurveys: [] };
        }

        try {
            return JSON.parse(node.textContent.trim());
        } catch (error) {
            console.error('Не удалось прочитать bootstrap автосоздания анкет:', error);
            return { isEnabled: false, selectedSurveys: [] };
        }
    }

    const state = {
        pageRoot: null,
        cleanup: null,
        selectedSurveys: [],
        availableSurveys: null
    };

    function getQueryRoot() {
        return state.pageRoot || document;
    }

    function normalizeSurvey(rawSurvey) {
        return {
            id: Number(rawSurvey?.id ?? rawSurvey?.Id ?? rawSurvey?.id_survey ?? 0),
            name: String(rawSurvey?.name ?? rawSurvey?.Name ?? rawSurvey?.name_survey ?? '').trim()
        };
    }

    function cloneSurveys(items) {
        if (!Array.isArray(items)) {
            return [];
        }

        return items
            .map((item) => normalizeSurvey(item))
            .filter((item) => item.id > 0 && item.name);
    }

    function showToast(message, type, options = {}) {
        const normalizedMessage = String(message || '').trim();
        if (!normalizedMessage) {
            return;
        }

        window.AppUi.notify(normalizedMessage, type, {
            title: options.title,
            duration: options.duration ?? (type === 'error' ? 0 : 4000)
        });
    }

    function renderSelectedSurveys() {
        const host = getQueryRoot().querySelector('[data-role="survey-auto-creation-selected-list"]');
        if (!host) {
            return;
        }

        host.innerHTML = '';

        if (state.selectedSurveys.length === 0) {
            const empty = document.createElement('p');
            empty.className = 'survey-auto-creation-page__empty-selection';
            empty.textContent = 'Анкеты не выбраны';
            host.appendChild(empty);
            return;
        }

        state.selectedSurveys.forEach((survey) => {
            const item = document.createElement('div');
            item.className = 'survey-auto-creation-page__selected-item';
            item.textContent = survey.name;
            host.appendChild(item);
        });
    }

    function renderSurveyModalList() {
        const list = getQueryRoot().querySelector('#surveyAutoCreationModalList');
        if (!list) {
            return;
        }

        list.innerHTML = '';

        const selectedIds = new Set(state.selectedSurveys.map((survey) => survey.id));
        (state.availableSurveys || []).forEach((survey) => {
            const item = document.createElement('label');
            const isSelected = selectedIds.has(survey.id);
            item.className = 'app-checkbox-option';
            item.classList.toggle('is-selected', isSelected);

            const checkbox = document.createElement('input');
            checkbox.type = 'checkbox';
            checkbox.className = 'app-checkbox-input';
            checkbox.checked = isSelected;
            checkbox.dataset.surveyId = String(survey.id);
            checkbox.addEventListener('change', () => {
                toggleSurveySelection(survey);
                renderSelectedSurveys();
                renderSurveyModalList();
            });

            const text = document.createElement('span');
            text.className = 'app-checkbox-text';
            text.textContent = survey.name;

            item.appendChild(checkbox);
            item.appendChild(text);
            list.appendChild(item);
        });
    }

    function setLoading(isLoading) {
        const root = getQueryRoot();
        const loading = root.querySelector('#surveyAutoCreationModalLoading');
        const list = root.querySelector('#surveyAutoCreationModalList');
        if (loading) {
            loading.classList.toggle('u-hidden', !isLoading);
        }
        if (list) {
            list.classList.toggle('u-hidden', isLoading);
        }
    }

    function toggleSurveySelection(survey) {
        const index = state.selectedSurveys.findIndex((item) => item.id === survey.id);
        if (index === -1) {
            state.selectedSurveys.push({ id: survey.id, name: survey.name });
        } else {
            state.selectedSurveys.splice(index, 1);
        }

        state.selectedSurveys.sort((left, right) => left.name.localeCompare(right.name, 'ru'));
    }

    function getSurveyDropdown() {
        return getQueryRoot().querySelector('[data-role="survey-auto-creation-dropdown"]');
    }

    function getSurveyDropdownMenu() {
        const root = getQueryRoot();
        return root.querySelector('[data-role="survey-auto-creation-dropdown-menu"]')
            || root.querySelector('#surveyAutoCreationDropdownMenu')
            || document.getElementById('surveyAutoCreationDropdownMenu');
    }

    function getSurveyDropdownTrigger() {
        return getSurveyDropdown()?.querySelector('[data-click-call="toggleSurveyAutoCreationSurveyDropdown"]');
    }

    function setSurveyDropdownVisible(isVisible) {
        const menu = getSurveyDropdownMenu();
        if (!menu) {
            return false;
        }

        getSurveyDropdown()?.classList.toggle('is-open', isVisible);
        menu.classList.toggle('is-hidden', !isVisible);
        return true;
    }

    function closeSurveyDropdown() {
        setSurveyDropdownVisible(false);
    }

    async function openSurveyDropdown() {
        const menu = getSurveyDropdownMenu();
        if (!menu) {
            return;
        }

        setSurveyDropdownVisible(true);
        if (state.availableSurveys) {
            renderSurveyModalList();
            return;
        }

        setLoading(true);
        try {
            await loadSurveyOptions();
            renderSurveyModalList();
        } catch (error) {
            closeSurveyDropdown();
            showToast(error instanceof Error ? error.message : 'Не удалось загрузить список анкет.', 'error', { title: 'Ошибка' });
        } finally {
            setLoading(false);
        }
    }

    function toggleSurveyDropdown() {
        const menu = getSurveyDropdownMenu();
        if (!menu) {
            return;
        }

        if (menu.classList.contains('is-hidden')) {
            openSurveyDropdown();
            return;
        }

        closeSurveyDropdown();
    }

    async function loadSurveyOptions() {
        const response = await fetch('/survey/data', {
            headers: {
                Accept: 'application/json'
            }
        });

        if (!response.ok) {
            throw new Error(
                typeof window.getResponseErrorMessage === 'function'
                    ? window.getResponseErrorMessage(response, 'Не удалось загрузить список анкет.')
                    : `Не удалось загрузить список анкет: ${response.status}`
            );
        }

        const payload = await response.json();
        state.availableSurveys = cloneSurveys(payload).sort((left, right) => left.name.localeCompare(right.name, 'ru'));
    }

    function collectRequest() {
        const root = getQueryRoot();
        const creationPattern = root.querySelector('#surveyAutoCreationPattern')?.value || '';
        const startPattern = root.querySelector('#surveyAutoCreationStartPattern')?.value || '';
        const endOffsetValue = root.querySelector('#surveyAutoCreationEndOffset')?.value || '';
        const endOffsetBusinessDays = endOffsetValue ? Number(endOffsetValue) : null;

        return {
            creationPattern,
            startPattern,
            endOffsetBusinessDays,
            surveyIds: state.selectedSurveys.map((survey) => survey.id)
        };
    }

    async function postAction(url, payload) {
        const options = {
            method: 'POST',
            headers: {
                RequestVerificationToken: window.AppHttp?.getAntiforgeryToken() || ''
            }
        };

        if (payload !== undefined) {
            options.headers['Content-Type'] = 'application/json';
            options.body = JSON.stringify(payload);
        }

        const response = await fetch(url, options);
        const responseText = await response.text();
        let parsed = null;
        if (responseText) {
            try {
                parsed = JSON.parse(responseText);
            } catch (error) {
                parsed = null;
            }
        }

        if (!response.ok) {
            throw new Error(parsed?.message || parsed?.error || responseText || 'Операция не выполнена.');
        }

        return parsed || { success: true };
    }

    function refreshPage() {
        if (typeof window.refreshAdminTab === 'function') {
            window.refreshAdminTab('survey_auto_creation', null, {
                force: true,
                scrollMode: 'restore'
            });
            return;
        }

        window.location.reload();
    }

    async function submitAction(url, payload, successTitle) {
        try {
            const result = await postAction(url, payload);
            showToast(result.message || 'Операция выполнена.', 'success', { title: successTitle });
            refreshPage();
        } catch (error) {
            showToast(error instanceof Error ? error.message : 'Операция не выполнена.', 'error', { title: 'Ошибка' });
        }
    }

    function closeSurveyDropdownOnOutsidePointer(event) {
        const dropdown = getSurveyDropdown();
        const menu = getSurveyDropdownMenu();
        if (!dropdown || !menu || menu.classList.contains('is-hidden')) {
            return;
        }

        const trigger = getSurveyDropdownTrigger();
        if (menu.contains(event.target) || trigger?.contains(event.target)) {
            return;
        }

        closeSurveyDropdown();
    }

    function handleEscape(event) {
        if (event.key !== 'Escape') {
            return;
        }

        closeSurveyDropdown();
    }

    function listen(scope, target, type, handler, options) {
        if (!target) {
            return;
        }

        if (scope && typeof scope.listen === 'function') {
            scope.listen(target, type, handler, options);
            return;
        }

        target.addEventListener(type, handler, options);
    }

    window.openSurveyAutoCreationSurveyModal = openSurveyDropdown;
    window.toggleSurveyAutoCreationSurveyDropdown = toggleSurveyDropdown;

    window.closeSurveyAutoCreationSurveyModal = function closeSurveyAutoCreationSurveyModal() {
        closeSurveyDropdown();
    };

    window.saveSurveyAutoCreationSurveySelection = function saveSurveyAutoCreationSurveySelection() {
        renderSelectedSurveys();
        closeSurveyDropdown();
    };

    window.saveSurveyAutoCreationSettings = function saveSurveyAutoCreationSettings() {
        return submitAction('/settings/survey-creation/save', collectRequest(), 'Настройки сохранены');
    };

    window.startSurveyAutoCreation = function startSurveyAutoCreation() {
        return submitAction('/settings/survey-creation/start', collectRequest(), 'Автосоздание запущено');
    };

    window.stopSurveyAutoCreation = function stopSurveyAutoCreation() {
        return submitAction('/settings/survey-creation/stop', undefined, 'Автосоздание остановлено');
    };

    function mountSurveyAutoCreationPage(pageRoot, scope) {
        if (state.cleanup) {
            state.cleanup();
            state.cleanup = null;
        }

        if (!pageRoot) {
            return;
        }

        state.pageRoot = pageRoot;
        state.availableSurveys = null;
        const bootstrap = parseBootstrap(pageRoot);
        state.selectedSurveys = cloneSurveys(bootstrap.selectedSurveys).sort((left, right) => left.name.localeCompare(right.name, 'ru'));
        renderSelectedSurveys();

        // Capture phase keeps this reliable when another interactive component stops click propagation.
        listen(scope, document, 'pointerdown', closeSurveyDropdownOnOutsidePointer, true);
        listen(scope, document, 'click', closeSurveyDropdownOnOutsidePointer, true);
        listen(scope, document, 'keydown', handleEscape);

        const cleanup = () => {
            closeSurveyDropdown();
            if (state.pageRoot === pageRoot) {
                state.pageRoot = null;
                state.availableSurveys = null;
            }
        };

        state.cleanup = cleanup;
        if (scope && typeof scope.add === 'function') {
            scope.add(cleanup);
        }
    }

    window.initSurveyAutoCreationPage = function initSurveyAutoCreationPage(root = document, scope = null) {
        const pageRoot = getPageRoot(root);
        mountSurveyAutoCreationPage(pageRoot, scope);
    };

    window.teardownSurveyAutoCreationPage = function teardownSurveyAutoCreationPage() {
        if (state.cleanup) {
            state.cleanup();
            state.cleanup = null;
        }
    };

    if (window.AppPageLifecycle && typeof window.AppPageLifecycle.register === 'function') {
        window.AppPageLifecycle.register(
            'survey-auto-creation-page',
            `.app-page${PAGE_SELECTOR}`,
            mountSurveyAutoCreationPage
        );
    } else if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => window.initSurveyAutoCreationPage(document), { once: true });
    } else {
        window.initSurveyAutoCreationPage(document);
    }
})();
