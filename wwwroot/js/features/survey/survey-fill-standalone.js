import {
    ANSWER_SUBMISSION_FAILED_MESSAGE,
    storePendingAnswerSubmittedNotification
} from './user-survey-notifications.js';

window.bindStandaloneSurveyFillPage = function bindStandaloneSurveyFillPage(initialData) {
    const page = document.querySelector('[data-page="survey-fill-standalone"]');
    if (!page) {
        return;
    }

    const refs = {
        errorBlock: page.querySelector('[data-role="error"]'),
        errorText: page.querySelector('[data-role="error-text"]'),
        submitButton: page.querySelector('[data-role="submit"]'),
        submitLabel: page.querySelector('[data-role="submit-label"]')
    };

    const answers = {};
    let loading = false;
    let error = null;

    function renderChrome() {
        const headerHost = document.getElementById('chrome-header');
        const navHost = document.getElementById('chrome-navigation');
        const footerHost = document.getElementById('chrome-footer');
        const chromeContext = typeof window.readAppChromeContext === 'function'
            ? window.readAppChromeContext()
            : null;
        const chromeProps = {
            userRole: chromeContext?.userRole || initialData.userRole,
            displayName: chromeContext?.displayName || initialData.displayName,
            userName: chromeContext?.userName || initialData.userName,
            organizationName: chromeContext?.organizationName || initialData.organizationName
        };

        if (headerHost && typeof window.mountHeader === 'function') {
            window.mountHeader(headerHost, chromeProps);
        }

        if (navHost && typeof window.mountNavigation === 'function') {
            window.mountNavigation(navHost, {
                activeTab: 'answers_tab',
                userRole: chromeProps.userRole,
                userId: chromeContext?.userId || initialData.userId
            });
        }

        if (footerHost && typeof window.mountFooter === 'function') {
            window.mountFooter(footerHost);
        }
    }

    function getQuestionNodes() {
        return Array.from(page.querySelectorAll('[data-role="survey-question"]'));
    }

    function renderError() {
        refs.errorText && (refs.errorText.textContent = '');
        refs.errorBlock?.classList.add('u-hidden');

        if (error) {
            window.AppUi.notify(error, 'error', { title: 'Ошибка' });
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
            commentInput.required = showComment;
            commentInput.value = showComment ? answer.comment || '' : '';
            if (!showComment) {
                window.AppValidation?.clearFieldError?.(commentInput);
            }
        }
    }

    function bindQuestion(questionElement) {
        const questionId = questionElement.dataset.questionId || '';
        if (!questionId) {
            return;
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
                renderError();
                updateQuestionState(questionId, questionElement);
                window.AppValidation?.clearFieldError?.(
                    questionElement.querySelector('[data-role="ratings"]')
                );
            });
        });

        const commentInput = questionElement.querySelector('[data-role="comment-input"]');
        commentInput?.addEventListener('input', (event) => {
            error = null;
            answers[questionId] = {
                ...answers[questionId],
                comment: event.target.value
            };
            renderError();
        });

        updateQuestionState(questionId, questionElement);
    }

    function validateCompleteAnswers() {
        const errors = [];
        const invalidFields = [];

        getQuestionNodes().forEach((questionNode) => {
            const questionId = questionNode.dataset.questionId || '';
            const answer = answers[questionId] || {};
            const rating = Number(answer.rating || 0);
            const ratings = questionNode.querySelector('[data-role="ratings"]');
            const commentInput = questionNode.querySelector('[data-role="comment-input"]');

            if (!Number.isFinite(rating) || rating < 1 || rating > 5) {
                const message = 'Выберите оценку для каждого вопроса.';
                window.AppValidation?.setFieldError?.(ratings, message);
                errors.push(message);
                invalidFields.push(ratings);
            } else {
                window.AppValidation?.clearFieldError?.(ratings);
            }

            if (rating > 0 && rating < 5 && !String(answer.comment || '').trim()) {
                const message = 'Для каждой оценки ниже 5 требуется комментарий.';
                window.AppValidation?.setFieldError?.(commentInput, message);
                errors.push(message);
                invalidFields.push(commentInput);
            } else {
                window.AppValidation?.clearFieldError?.(commentInput);
            }
        });

        if (errors.length === 0) {
            return true;
        }

        window.AppValidation?.notifyErrors?.(errors);
        window.AppValidation?.focusFirstInvalid?.({ invalidFields });
        return false;
    }

    async function submitAnswers() {
        if (!validateCompleteAnswers()) {
            return;
        }

        try {
            loading = true;
            error = null;
            renderError();
            renderSubmitState();

            const payloadAnswers = Object.entries(answers).map(([questionId, answer]) => {
                const questionNode = getQuestionNodes().find((node) => node.dataset.questionId === questionId);
                const questionText = questionNode?.querySelector('[data-role="question-title"]')?.textContent?.trim() || '';

                return {
                    question_id: questionId,
                    question_text: questionText,
                    rating: answer.rating,
                    comment: answer.rating === 5 ? '' : answer.comment || ''
                };
            });

            const response = await fetch('/answers/create', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify({
                    id_survey: initialData.surveyId,
                    id_organization: initialData.organizationId,
                    answers: payloadAnswers
                })
            });

            if (!response.ok) {
                const errorData = await response.json().catch(() => null);
                throw new Error(errorData?.error || ANSWER_SUBMISSION_FAILED_MESSAGE);
            }

            const responseData = await response.json().catch(() => null);
            storePendingAnswerSubmittedNotification(responseData?.message);
            window.location.assign('/archive');
        } catch (err) {
            error = err?.message || ANSWER_SUBMISSION_FAILED_MESSAGE;
            renderError();
        } finally {
            loading = false;
            renderSubmitState();
        }
    }

    refs.submitButton?.addEventListener('click', submitAnswers);
    getQuestionNodes().forEach(bindQuestion);
    renderChrome();
    renderError();
    renderSubmitState();
};

function getStandaloneBootstrapData() {
    const bootstrapElement = document.getElementById('survey-fill-bootstrap');
    if (!bootstrapElement?.textContent) {
        return null;
    }

    try {
        return JSON.parse(bootstrapElement.textContent.trim());
    } catch (error) {
        console.error('Не удалось прочитать bootstrap-данные страницы анкеты:', error);
        return null;
    }
}

const standaloneBootstrapData = getStandaloneBootstrapData();
if (document.querySelector('[data-page="survey-fill-standalone"]') && standaloneBootstrapData) {
    window.bindStandaloneSurveyFillPage(standaloneBootstrapData);
}
