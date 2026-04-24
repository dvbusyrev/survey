(function () {
    const page = document.querySelector('.app-page[data-page="surveys-list"], .app-page[data-page="surveys-archive"]');
    if (!page) {
        return;
    }

    let extensionModal = null;
    let extensionHost = null;
    let extensionCleanup = null;

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
        if (typeof window.resetSurveyCreateForm === 'function') {
            window.resetSurveyCreateForm();
        }

        syncSurveyListHistory();
        setSurveyEditorModalVisible(true);
        refreshSurveyListPreservingScroll();
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

    function ensureExtensionModal() {
        if (extensionModal && extensionHost) {
            return;
        }

        extensionModal = document.createElement('div');
        extensionModal.id = 'surveyExtensionModal';
        extensionModal.className = 'modal';
        extensionModal.setAttribute('aria-hidden', 'true');

        const modalContent = document.createElement('div');
        modalContent.className = 'modal-content';

        const closeButton = document.createElement('button');
        closeButton.type = 'button';
        closeButton.className = 'modal-close';
        closeButton.setAttribute('aria-label', 'Закрыть');

        const closeIcon = document.createElement('i');
        closeIcon.className = 'fas fa-xmark';
        closeIcon.setAttribute('aria-hidden', 'true');
        closeButton.appendChild(closeIcon);

        extensionHost = document.createElement('div');
        extensionHost.className = 'modal-body';

        closeButton.addEventListener('click', closeSurveyExtensionModal);

        modalContent.appendChild(closeButton);
        modalContent.appendChild(extensionHost);
        extensionModal.appendChild(modalContent);
        document.body.appendChild(extensionModal);
    }

    function closeSurveyExtensionModal() {
        if (typeof extensionCleanup === 'function') {
            extensionCleanup();
            extensionCleanup = null;
        }

        if (extensionHost) {
            extensionHost.innerHTML = '';
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
                onClose: closeSurveyExtensionModal
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
    window.deleteSurveyFromTrigger = deleteSurveyFromTrigger;
    window.openAddSurveyModal = openAddSurveyModal;
    window.openEditSurveyModal = openEditSurveyModal;
    window.openEditSurveyModalFromTrigger = openEditSurveyModalFromTrigger;
    window.closeSurveyEditorModal = closeSurveyEditorModal;
    window.handleSurveyCreateSuccess = handleSurveyCreateSuccess;
    window.handleSurveyUpdateSuccess = handleSurveyUpdateSuccess;
})();
