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
        if (!refs.errorBlock || !refs.errorText) {
            return;
        }

        if (error) {
            refs.errorText.textContent = error;
            refs.errorBlock.classList.remove('u-hidden');
        } else {
            refs.errorText.textContent = '';
            refs.errorBlock.classList.add('u-hidden');
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
            commentInput.value = answer.comment || '';
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

    async function submitAnswers() {
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
                    comment: answer.comment || ''
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
                throw new Error(errorData?.error || 'Ошибка при отправке ответов');
            }

            window.location.assign('/my-surveys/archive');
        } catch (err) {
            error = err?.message || 'Не удалось отправить ответы';
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
