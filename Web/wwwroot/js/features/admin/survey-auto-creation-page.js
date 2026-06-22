(function () {
    function getPageRoot() {
        return document.querySelector('[data-page="survey-auto-creation"]');
    }

    function parseBootstrap() {
        const node = document.getElementById('survey-auto-creation-bootstrap');
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

    function getRequestVerificationToken() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    }

    const state = {
        initializedRoot: null,
        selectedSurveys: [],
        availableSurveys: null
    };

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

        if (typeof window.siteNotify === 'function') {
            window.siteNotify(normalizedMessage, type, {
                title: options.title,
                duration: options.duration ?? (type === 'error' ? 0 : 4000)
            });
            return;
        }

        window.alert(normalizedMessage);
    }

    function renderSelectedSurveys() {
        const host = document.querySelector('[data-role="survey-auto-creation-selected-list"]');
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
        const list = document.getElementById('surveyAutoCreationModalList');
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
        const loading = document.getElementById('surveyAutoCreationModalLoading');
        const list = document.getElementById('surveyAutoCreationModalList');
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
        return document.querySelector('[data-role="survey-auto-creation-dropdown"]');
    }

    function getSurveyDropdownMenu() {
        return document.querySelector('[data-role="survey-auto-creation-dropdown-menu"]')
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
        const creationPattern = document.getElementById('surveyAutoCreationPattern')?.value || '';
        const startPattern = document.getElementById('surveyAutoCreationStartPattern')?.value || '';
        const endOffsetValue = document.getElementById('surveyAutoCreationEndOffset')?.value || '';
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
                RequestVerificationToken: getRequestVerificationToken()
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

    // Capture phase keeps this reliable when another interactive component stops click propagation.
    document.addEventListener('pointerdown', closeSurveyDropdownOnOutsidePointer, true);
    document.addEventListener('click', closeSurveyDropdownOnOutsidePointer, true);

    document.addEventListener('keydown', function (event) {
        if (event.key !== 'Escape') {
            return;
        }

        closeSurveyDropdown();
    });

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

    window.initSurveyAutoCreationPage = function initSurveyAutoCreationPage() {
        const pageRoot = getPageRoot();
        if (!pageRoot || state.initializedRoot === pageRoot) {
            return;
        }

        state.initializedRoot = pageRoot;
        const bootstrap = parseBootstrap();
        state.selectedSurveys = cloneSurveys(bootstrap.selectedSurveys).sort((left, right) => left.name.localeCompare(right.name, 'ru'));
        renderSelectedSurveys();
    };

    window.initSurveyAutoCreationPage();
})();
