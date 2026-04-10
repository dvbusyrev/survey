(function () {
    const page = document.querySelector('.app-page[data-page="surveys-list"]');
    if (!page) {
        return;
    }

    let extensionModal = null;
    let extensionHost = null;
    let extensionCleanup = null;

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

            window.location.assign('/surveys');
        } catch (error) {
            window.siteNotify?.(error.message || 'Не удалось удалить анкету.', 'error');
        }
    }

    window.openSurveyExtensionModalFromTrigger = openSurveyExtensionModalFromTrigger;
    window.deleteSurveyFromTrigger = deleteSurveyFromTrigger;
})();
