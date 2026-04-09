// stage12: standalone survey fill page extracted from get_survey_questions.cshtml
window.renderStandaloneSurveyFill = function (initialData) {
    const root = document.getElementById('root');
    const pageTemplate = document.getElementById('survey-fill-page-template');
    const questionTemplate = document.getElementById('survey-fill-question-template');
    const successTemplate = document.getElementById('survey-fill-success-template');
    if (!root || !pageTemplate?.content?.firstElementChild || !questionTemplate?.content?.firstElementChild) {
        return;
    }

    const answers = {};
    let loading = false;
    let error = null;

    root.innerHTML = '';
    const page = pageTemplate.content.firstElementChild.cloneNode(true);
    root.appendChild(page);

    const headerHost = page.querySelector('[data-component="header"]');
    const navHost = page.querySelector('[data-component="navigation"]');
    const footerHost = page.querySelector('[data-component="footer"]');
    const questionsHost = page.querySelector('[data-role="questions"]');
    const errorBlock = page.querySelector('[data-role="error"]');
    const errorText = page.querySelector('[data-role="error-text"]');
    const submitButton = page.querySelector('[data-role="submit"]');
    const submitLabel = page.querySelector('[data-role="submit-label"]');

    if (headerHost && typeof window.mountHeader === 'function') {
        window.mountHeader(headerHost, {
            userRole: initialData.userRole,
            displayName: initialData.displayName,
            userName: initialData.userName,
            organizationName: initialData.organizationName
        });
    }
    if (navHost && typeof window.mountNavigation === 'function') {
        window.mountNavigation(navHost, {
            activeTab: 'answers_tab',
            userRole: initialData.userRole,
            userId: initialData.userId
        });
    }
    if (footerHost && typeof window.mountFooter === 'function') {
        window.mountFooter(footerHost);
    }

    function renderError() {
        if (!errorBlock || !errorText) {
            return;
        }
        if (error) {
            errorText.textContent = error;
            errorBlock.style.display = 'flex';
        } else {
            errorText.textContent = '';
            errorBlock.style.display = 'none';
        }
    }

    function renderSubmitState() {
        if (!submitButton || !submitLabel) {
            return;
        }
        submitButton.disabled = loading;
        submitButton.querySelector('.loading-spinner')?.remove();
        if (loading) {
            const spinner = document.createElement('span');
            spinner.className = 'loading-spinner';
            submitButton.insertBefore(spinner, submitLabel);
            submitLabel.textContent = 'Отправка...';
        } else {
            submitLabel.textContent = 'Отправить ответы';
        }
    }

    function updateQuestionState(questionId, questionElement) {
        const answer = answers[questionId] || {};
        questionElement.querySelectorAll('.btn_crit').forEach((button) => {
            const rating = Number(button.dataset.rating || 0);
            button.classList.toggle('active', answer.rating === rating);
        });

        const commentBlock = questionElement.querySelector('[data-role="comment-block"]');
        const commentInput = questionElement.querySelector('textarea');
        const showComment = answer.rating > 0 && answer.rating < 5;
        if (commentBlock) {
            commentBlock.style.display = showComment ? '' : 'none';
        }
        if (commentInput) {
            commentInput.value = answer.comment || '';
        }
    }

    function buildQuestion(question, index) {
        const questionId = String(question.id || question.Id || index);
        const questionText = question.text || question.Text || `Вопрос ${index + 1}`;
        const questionNode = questionTemplate.content.firstElementChild.cloneNode(true);
        const title = questionNode.querySelector('[data-role="question-title"]');
        const ratingsHost = questionNode.querySelector('[data-role="ratings"]');
        const commentInput = questionNode.querySelector('textarea');

        if (title) {
            title.textContent = questionText;
        }

        for (let rating = 1; rating <= 5; rating += 1) {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'btn_crit';
            button.dataset.rating = String(rating);
            button.textContent = String(rating);
            button.addEventListener('click', () => {
                error = null;
                answers[questionId] = {
                    ...answers[questionId],
                    rating,
                    comment: rating < 5 ? answers[questionId]?.comment || '' : ''
                };
                renderError();
                updateQuestionState(questionId, questionNode);
            });
            ratingsHost?.appendChild(button);
        }

        commentInput?.addEventListener('input', (event) => {
            error = null;
            answers[questionId] = {
                ...answers[questionId],
                comment: event.target.value
            };
            renderError();
        });

        updateQuestionState(questionId, questionNode);
        return questionNode;
    }

    function showSuccessAndRedirect() {
        if (!successTemplate?.content?.firstElementChild) {
            window.location.href = '/survey/thank-you';
            return;
        }
        root.innerHTML = '';
        root.appendChild(successTemplate.content.firstElementChild.cloneNode(true));
        window.setTimeout(() => {
            window.location.href = '/survey/thank-you';
        }, 2000);
    }

    async function submitAnswers() {
        try {
            loading = true;
            error = null;
            renderError();
            renderSubmitState();

            const payloadAnswers = Object.entries(answers).map(([questionId, answer]) => ({
                question_id: questionId,
                question_text: initialData.questions.find((q) => String(q.id || q.Id) === String(questionId))?.text
                    || initialData.questions.find((q) => String(q.id || q.Id) === String(questionId))?.Text
                    || '',
                rating: answer.rating,
                comment: answer.comment || ''
            }));

            const response = await fetch('/answers/create', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify({
                    id_survey: initialData.surveyId,
                    organization_id: initialData.organizationId,
                    answers: payloadAnswers
                })
            });

            if (!response.ok) {
                const errorData = await response.json().catch(() => null);
                throw new Error(errorData?.error || 'Ошибка при отправке ответов');
            }

            showSuccessAndRedirect();
        } catch (err) {
            error = err?.message || 'Не удалось отправить ответы';
            renderError();
        } finally {
            loading = false;
            renderSubmitState();
        }
    }

    submitButton?.addEventListener('click', submitAnswers);
    renderError();
    renderSubmitState();

    (initialData.questions || []).forEach((question, index) => {
        questionsHost?.appendChild(buildQuestion(question, index));
    });
};

function getStandaloneBootstrapData() {
    const bootstrapElement = document.getElementById('survey-fill-bootstrap');
    if (!bootstrapElement?.content?.textContent) {
        return null;
    }

    try {
        return JSON.parse(bootstrapElement.content.textContent.trim());
    } catch (error) {
        console.error('Не удалось прочитать bootstrap-данные страницы анкеты:', error);
        return null;
    }
}

const standaloneBootstrapData = getStandaloneBootstrapData();
if (document.getElementById('root') && standaloneBootstrapData) {
    window.renderStandaloneSurveyFill(standaloneBootstrapData);
}
