import { CSP } from './user-survey-signature.js';
import {
    applySurveySignedState,
    clearSurveyModalFooter,
    createSurveyHtmlFragment,
    createSurveyModalFooterButton,
    fetchSurveyModalContent,
    getAnswersPageContainer,
    renderSurveyHostError,
    showSurveyError
} from './user-survey-flow-shared.js';

window.createAnswerReport = function createAnswerReport(idSurvey, organizationId, type) {
    window.AppScrollState?.prepareNavigation({ carry: true });
    window.location.assign(`/answers/${idSurvey}/${organizationId}/report/${type}`);
};

window.downloadAnswerDocument = function downloadAnswerDocument(surveyId, organizationId, triggerElement) {
    const page = getAnswersPageContainer(triggerElement);
    const isSigned = page?.dataset.isSigned === 'true';

    if (isSigned) {
        return window.downloadSignedArchive(surveyId, organizationId);
    }

    return window.createPdfReport(surveyId, organizationId);
};

window.fetchSurveyFillContentHtml = function fetchSurveyFillContentHtml(surveyId, organizationId) {
    return fetchSurveyModalContent(
        `/survey/${surveyId}/organizations/${organizationId}/fill-content`,
        'Не удалось загрузить анкету'
    );
};

window.fetchSurveyAnswersContentHtml = function fetchSurveyAnswersContentHtml(surveyId, organizationId) {
    return fetchSurveyModalContent(
        `/answers/${surveyId}/${organizationId}/content`,
        'Не удалось загрузить ответы по анкете'
    );
};

function downloadBlob(blob, fileName) {
    const url = window.URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    window.URL.revokeObjectURL(url);
}

function getSurveyIdentifier(survey) {
    const value = survey?.id_survey
        || survey?.IdSurvey
        || survey?.idSurvey
        || survey?.Id
        || survey?.id
        || 0;
    const numericValue = Number(value);
    return Number.isFinite(numericValue) ? numericValue : 0;
}

async function postSurveyJson(url, payload, fallbackMessage) {
    const response = await fetch(url, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'X-Requested-With': 'XMLHttpRequest'
        },
        body: JSON.stringify(payload)
    });

    if (!response.ok) {
        const errorData = await response.json().catch(() => null);
        throw new Error(errorData?.error || fallbackMessage);
    }

    return response.json().catch(() => null);
}

async function mountSurveyModalHtml({
    host,
    footerHost,
    initialHtml,
    loadHtml,
    bindPage,
    isDestroyed,
    errorMessage
}) {
    try {
        const html = typeof initialHtml === 'string'
            ? initialHtml
            : await loadHtml();
        if (isDestroyed()) {
            return;
        }

        host.replaceChildren(createSurveyHtmlFragment(html));
        bindPage();
    } catch (error) {
        if (isDestroyed()) {
            return;
        }

        renderSurveyHostError(host, error?.message || errorMessage);
        clearSurveyModalFooter(footerHost);
    }
}

window.mountSurveyFillPage = function mountSurveyFillPage(host, { survey, organizationId, onBack, onSubmitted, initialHtml, footerHost }) {
    if (!host) {
        return null;
    }

    let destroyed = false;
    const answers = {};
    let loading = false;
    let error = null;
    let draftSaveTimer = 0;
    let refs = {
        page: null,
        draftSignButton: null,
        submitButton: null,
        submitLabel: null,
        cancelButton: null
    };

    function getQuestionNodes() {
        return Array.from(host.querySelectorAll('[data-role="survey-question"]'));
    }

    function getCurrentSurveyId() {
        const rawValue = refs.page?.dataset.surveyId
            || host.querySelector('[data-role="survey-fill-page"]')?.dataset.surveyId
            || getSurveyIdentifier(survey)
            || 0;
        const numericValue = Number(rawValue);
        return Number.isFinite(numericValue) ? numericValue : 0;
    }

    function renderError(options = {}) {
        const shouldNotify = options.notify === true;
        host.querySelector('[data-role="error"]')?.classList.add('u-hidden');

        if (shouldNotify && error) {
            showSurveyError(error);
        }
    }

    function renderSubmitState() {
        if (!refs.submitButton || !refs.submitLabel) {
            return;
        }

        refs.submitButton.disabled = loading;
        refs.submitButton.querySelector('.loading-spinner')?.remove();

        if (loading) {
            const spinner = document.createElement('span');
            spinner.className = 'loading-spinner';
            refs.submitButton.insertBefore(spinner, refs.submitLabel);
            refs.submitLabel.textContent = 'Отправка...';
            return;
        }

        refs.submitLabel.textContent = 'Отправить ответы';
    }

    function buildPayloadAnswers({ requireComplete = false } = {}) {
        const payloadAnswers = [];
        const questionNodes = getQuestionNodes();

        questionNodes.forEach((questionNode) => {
            const questionId = questionNode.dataset.questionId || '';
            const questionText = questionNode.querySelector('[data-role="question-title"]')?.textContent?.trim() || '';
            const answer = answers[questionId] || {};
            const rating = Number(answer.rating || 0);
            const comment = String(answer.comment || '').trim();

            if (!rating && !comment && !requireComplete) {
                return;
            }

            if (requireComplete && (!Number.isFinite(rating) || rating < 1 || rating > 5)) {
                throw new Error('Необходимо ответить на все вопросы анкеты.');
            }

            if (requireComplete && rating < 5 && !comment) {
                throw new Error('Для оценки ниже 5 требуется комментарий.');
            }

            payloadAnswers.push({
                question_id: questionId,
                question_text: questionText,
                rating: rating || null,
                comment
            });
        });

        if (requireComplete && payloadAnswers.length !== questionNodes.length) {
            throw new Error('Необходимо ответить на все вопросы анкеты.');
        }

        return payloadAnswers;
    }

    function updateDraftSignedState(isSigned) {
        if (refs.page) {
            refs.page.dataset.isDraftSigned = isSigned ? 'true' : 'false';
        }

        applySurveySignedState(refs.page || host, isSigned, 'draft');
    }

    async function saveDraft({ showErrorOnFailure = false } = {}) {
        const payloadAnswers = buildPayloadAnswers();
        if (payloadAnswers.length === 0) {
            return true;
        }

        const surveyId = getCurrentSurveyId();
        if (surveyId <= 0 || organizationId <= 0) {
            const message = 'Не удалось определить анкету для сохранения черновика.';
            if (showErrorOnFailure) {
                throw new Error(message);
            }

            console.error(message);
            return false;
        }

        try {
            await postSurveyJson('/answers/draft', {
                id_survey: surveyId,
                id_organization: organizationId,
                answers: payloadAnswers
            }, 'Ошибка при сохранении черновика');
        } catch (error) {
            if (showErrorOnFailure) {
                throw error;
            }

            console.error(error?.message || 'Ошибка при сохранении черновика');
            return false;
        }

        return true;
    }

    function scheduleDraftSave() {
        if (draftSaveTimer) {
            window.clearTimeout(draftSaveTimer);
        }

        draftSaveTimer = window.setTimeout(() => {
            draftSaveTimer = 0;
            saveDraft().catch((err) => console.error('Ошибка при сохранении черновика:', err));
        }, 450);
    }

    function renderFooter() {
        if (!footerHost) {
            return {};
        }

        const isDraftSigned = refs.page?.dataset.isDraftSigned === 'true'
            || host.querySelector('[data-role="survey-fill-page"]')?.dataset.isDraftSigned === 'true';
        const signButton = createSurveyModalFooterButton({
            role: 'draft-sign-button',
            text: isDraftSigned ? 'Подписано' : 'Подписать',
            variant: 'primary',
            disabled: isDraftSigned
        });
        const cancelButton = createSurveyModalFooterButton({
            role: 'cancel-btn',
            text: 'Отмена',
            variant: 'secondary'
        });
        const submitButton = createSurveyModalFooterButton({
            role: 'submit',
            text: 'Отправить ответы',
            variant: 'primary',
            labelRole: 'submit-label'
        });

        signButton.classList.add('survey-user-modal__footer-left');
        footerHost.replaceChildren(signButton, cancelButton, submitButton);

        return {
            draftSignButton: signButton,
            cancelButton,
            submitButton,
            submitLabel: submitButton.querySelector('[data-role="submit-label"]')
        };
    }

    function updateQuestionState(questionId, questionElement) {
        const answer = answers[questionId] || {};

        questionElement.querySelectorAll('[data-role="rating-button"]').forEach((button) => {
            const rating = Number(button.dataset.rating || 0);
            button.classList.toggle('active', answer.rating === rating);
        });

        const commentBlock = questionElement.querySelector('[data-role="comment-block"]');
        const commentInput = questionElement.querySelector('[data-role="comment-input"]');
        const showComment = answer.rating > 0 && answer.rating < 5;

        if (commentBlock) {
            commentBlock.classList.toggle('u-hidden', !showComment);
        }

        if (commentInput) {
            commentInput.value = answer.comment || '';
        }
    }

    function bindQuestion(questionElement) {
        const questionId = questionElement.dataset.questionId || '';
        if (!questionId) {
            return;
        }

        const activeButton = questionElement.querySelector('[data-role="rating-button"].active');
        const activeRating = Number(activeButton?.dataset.rating || 0);
        const commentInput = questionElement.querySelector('[data-role="comment-input"]');
        if (activeRating > 0 || commentInput?.value) {
            answers[questionId] = {
                rating: activeRating || null,
                comment: commentInput?.value || ''
            };
        }

        questionElement.querySelectorAll('[data-role="rating-button"]').forEach((button) => {
            button.addEventListener('click', () => {
                error = null;
                const rating = Number(button.dataset.rating || 0);
                answers[questionId] = {
                    ...answers[questionId],
                    rating,
                    comment: rating < 5 ? answers[questionId]?.comment || '' : ''
                };
                updateDraftSignedState(false);
                renderError();
                updateQuestionState(questionId, questionElement);
                scheduleDraftSave();
            });
        });

        commentInput?.addEventListener('input', (event) => {
            error = null;
            answers[questionId] = {
                ...answers[questionId],
                comment: event.target.value
            };
            updateDraftSignedState(false);
            renderError();
            scheduleDraftSave();
        });

        updateQuestionState(questionId, questionElement);
    }

    async function submitAnswers() {
        try {
            loading = true;
            error = null;
            renderError();
            renderSubmitState();

            const payloadAnswers = buildPayloadAnswers({ requireComplete: true });
            const surveyId = getCurrentSurveyId();
            if (surveyId <= 0 || organizationId <= 0) {
                throw new Error('Не удалось определить анкету для отправки ответов.');
            }

            await postSurveyJson('/answers/create', {
                id_survey: surveyId,
                id_organization: organizationId,
                answers: payloadAnswers
            }, 'Ошибка при отправке ответов');
            onSubmitted?.({
                survey,
                answers: payloadAnswers,
                organizationId
            });
        } catch (err) {
            error = err?.message || 'Не удалось отправить ответы';
            renderError({ notify: true });
        } finally {
            loading = false;
            renderSubmitState();
        }
    }

    async function signDraft() {
        try {
            error = null;
            renderError();
            buildPayloadAnswers({ requireComplete: true });
            await saveDraft({ showErrorOnFailure: true });
            const surveyId = getCurrentSurveyId();
            if (surveyId <= 0 || organizationId <= 0) {
                throw new Error('Не удалось определить анкету для подписи.');
            }

            await CSP(surveyId, organizationId, {
                mode: 'draft',
                source: refs.page || host
            });
        } catch (err) {
            error = err?.message || 'Не удалось подписать черновик';
            renderError({ notify: true });
        }
    }

    function bindPage() {
        host.querySelector('[data-role="body-actions"]')?.classList.add('u-hidden');

        refs = {
            page: host.querySelector('[data-role="survey-fill-page"]'),
            draftSignButton: null,
            submitButton: null,
            submitLabel: null,
            cancelButton: null
        };
        const footerRefs = renderFooter();
        refs.draftSignButton = footerRefs.draftSignButton || host.querySelector('[data-role="draft-sign-button"]');
        refs.submitButton = footerRefs.submitButton || host.querySelector('[data-role="submit"]');
        refs.submitLabel = footerRefs.submitLabel || host.querySelector('[data-role="submit-label"]');
        refs.cancelButton = footerRefs.cancelButton || host.querySelector('[data-role="cancel-btn"]');

        refs.draftSignButton?.addEventListener('click', signDraft);
        refs.submitButton?.addEventListener('click', submitAnswers);
        refs.cancelButton?.addEventListener('click', () => onBack?.());
        getQuestionNodes().forEach(bindQuestion);
        updateDraftSignedState(refs.page?.dataset.isDraftSigned === 'true');
        renderError();
        renderSubmitState();
    }

    mountSurveyModalHtml({
        host,
        footerHost,
        initialHtml,
        loadHtml: () => window.fetchSurveyFillContentHtml(getCurrentSurveyId(), organizationId),
        bindPage,
        isDestroyed: () => destroyed,
        errorMessage: 'Не удалось загрузить анкету'
    });

    return () => {
        destroyed = true;
        if (draftSaveTimer) {
            window.clearTimeout(draftSaveTimer);
        }
        host.replaceChildren();
        clearSurveyModalFooter(footerHost);
    };
};

window.createPdfReport = async function(surveyId, organizationId) {
    try {
        const response = await fetch(`/answers/${surveyId}/${organizationId}/pdf`);
        if (!response.ok) throw new Error('Ошибка создания PDF');

        downloadBlob(await response.blob(), `Анкета_${surveyId}_${new Date().toISOString().slice(0,10)}.pdf`);
    } catch (error) {
        console.error('Ошибка при создании PDF:', error);
        showSurveyError('Не удалось создать PDF файл');
    }
}

window.downloadSignedArchive = async function(surveyId, organizationId) {
    try {
        const response = await fetch(`/answers/${surveyId}/${organizationId}/signed-archive`);

        if (!response.ok) {
            const errorData = await response.json().catch(() => null);
            const errorMessage = errorData?.error || 'Ошибка загрузки архива';
            throw new Error(errorMessage);
        }

        downloadBlob(await response.blob(), `Анкета_с_подписью_${surveyId}.zip`);
    } catch (error) {
        console.error('Ошибка при загрузке архива:', error);

        const errorMessage = error.message || 'Не удалось загрузить архив с подписью';
        showSurveyError(errorMessage);

        if (error.details) {
            console.error('Детали ошибки:', error.details);
        }
    }
}


window.mountCheckAnswersPage = function mountCheckAnswersPage(host, { survey, organizationId, initialHtml, footerHost }) {
    if (!host) {
        return null;
    }

    let destroyed = false;

    function renderFooter(page, surveyId, currentOrganizationId) {
        if (!footerHost) {
            return {};
        }

        const isSigned = page?.dataset.isSigned === 'true';
        const signButton = createSurveyModalFooterButton({
            role: 'sign-button',
            text: isSigned ? 'Подписано' : 'Подписать',
            variant: 'primary',
            disabled: isSigned
        });
        const downloadButton = createSurveyModalFooterButton({
            role: 'download-btn',
            text: 'Скачать ответы',
            variant: 'secondary'
        });

        signButton.dataset.surveyId = String(surveyId || '');
        signButton.dataset.organizationId = String(currentOrganizationId || '');
        downloadButton.dataset.surveyId = String(surveyId || '');
        downloadButton.dataset.organizationId = String(currentOrganizationId || '');

        footerHost.replaceChildren(downloadButton, signButton);

        return {
            signButton,
            downloadButton
        };
    }

    function bindPage() {
        const page = host.querySelector('[data-role="survey-answers-page"]');
        const surveyId = Number(page?.dataset.surveyId || getSurveyIdentifier(survey));
        const currentOrganizationId = Number(page?.dataset.organizationId || organizationId || 0);
        const footerRefs = renderFooter(page, surveyId, currentOrganizationId);
        host.querySelector('[data-role="body-actions"]')?.classList.add('u-hidden');

        const downloadButton = footerRefs.downloadButton || host.querySelector('[data-role="download-btn"]');
        const signButton = footerRefs.signButton || host.querySelector('[data-role="sign-actions"] button');

        downloadButton?.addEventListener('click', (event) => {
            event.preventDefault();
            if (surveyId > 0 && currentOrganizationId > 0) {
                window.downloadAnswerDocument(surveyId, currentOrganizationId, downloadButton);
            }
        });

        signButton?.addEventListener('click', (event) => {
            event.preventDefault();
            if (signButton.disabled) {
                return;
            }

            if (surveyId > 0 && currentOrganizationId > 0) {
                CSP(surveyId, currentOrganizationId);
            }
        });

    }
    mountSurveyModalHtml({
        host,
        footerHost,
        initialHtml,
        loadHtml: () => window.fetchSurveyAnswersContentHtml(getSurveyIdentifier(survey), organizationId),
        bindPage,
        isDestroyed: () => destroyed,
        errorMessage: 'Не удалось загрузить ответы по анкете'
    });

    return () => {
        destroyed = true;
        host.replaceChildren();
        clearSurveyModalFooter(footerHost);
    };
};
