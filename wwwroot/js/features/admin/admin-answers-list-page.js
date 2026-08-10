(function () {
    if (window.__adminAnswersListPageLoaded) {
        return;
    }

    window.__adminAnswersListPageLoaded = true;

    const tooltip = window.AppUi.createRowTooltip();
    let answerDeletePending = false;

    function removeAnswerRow(trigger) {
        const page = trigger.closest('.answers-page[data-page="answers-list"]');
        const row = trigger.closest('.answers-page__row');
        row?.remove();

        if (!page) {
            return;
        }

        const visibleRows = page.querySelectorAll('.answers-page__row').length;
        const totalCount = Math.max(0, Number.parseInt(page.dataset.totalCount || '0', 10) - 1);
        page.dataset.visibleCount = String(visibleRows);
        page.dataset.totalCount = String(totalCount);

        if (visibleRows === 0) {
            page.querySelector('[data-role="survey-filter-empty-row"]')?.classList.remove('is-hidden');
        }

        if (totalCount === 0) {
            page.querySelector('[data-role="pagination"]')?.remove();
        }
    }

    async function deleteAnswerFromTrigger(trigger) {
        if (answerDeletePending) {
            return;
        }

        const answerId = Number.parseInt(trigger?.dataset?.answerId || '', 10);
        if (!Number.isFinite(answerId) || answerId <= 0) {
            window.AppUi.notify('Не удалось определить ответ.', 'error');
            return;
        }

        const surveyName = trigger.dataset.surveyName || 'Без названия';
        const organizationName = trigger.dataset.organizationName || 'Не указана';
        answerDeletePending = true;

        try {
            const confirmed = await window.siteConfirm(
                `Удалить ответ организации «${organizationName}» на анкету «${surveyName}»?`,
                {
                    title: 'Удаление ответа',
                    confirmText: 'Удалить',
                    cancelText: 'Отмена'
                }
            );
            if (!confirmed) {
                return;
            }

            const response = await fetch(`/answers/${answerId}/delete`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                }
            });
            const responseMessage = window.AppHttp?.readResponseMessage
                ? await window.AppHttp.readResponseMessage(response, 'Не удалось удалить ответ.')
                : await response.text();
            if (!response.ok) {
                throw new Error(responseMessage || 'Не удалось удалить ответ.');
            }

            removeAnswerRow(trigger);
            window.AppUi.notify(responseMessage || 'Ответ успешно удалён.', 'success');
        } catch (error) {
            window.AppUi.notify(error?.message || 'Не удалось удалить ответ.', 'error');
        } finally {
            answerDeletePending = false;
        }
    }

    window.deleteAnswerFromTrigger = deleteAnswerFromTrigger;

    document.addEventListener('mouseover', (event) => {
        const row = event.target.closest('.answers-page[data-page="answers-list"] .answers-page__row');
        if (!row || tooltip.isActiveRow(row)) {
            return;
        }

        tooltip.show(row, event);
    });

    document.addEventListener('mousemove', (event) => {
        if (!tooltip.hasActiveRow()) {
            return;
        }

        tooltip.move(event);
    });

    document.addEventListener('mouseout', (event) => {
        if (!tooltip.hasActiveRow() || tooltip.activeRowContains(event.relatedTarget)) {
            return;
        }

        tooltip.hide();
    });
})();
