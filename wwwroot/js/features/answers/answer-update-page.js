(function () {
    const page = document.querySelector('[data-answer-page="update"]');
    if (!page) {
        return;
    }

    const surveyId = Number.parseInt(page.dataset.surveyId || '', 10);
    const organizationId = Number.parseInt(page.dataset.organizationId || '', 10);
    const errorBox = document.getElementById('error-message');
    const errorText = document.getElementById('error-text');
    const closeErrorButton = document.getElementById('close-error');
    const saveButton = document.getElementById('saveUpdatedAnswers');

    function hideError() {
        if (errorBox) {
            errorBox.style.visibility = 'hidden';
        }
    }

    function showError(message) {
        if (errorText) {
            errorText.textContent = message;
        }

        if (errorBox) {
            errorBox.style.visibility = 'visible';
        }
    }

    function updateCommentVisibility(questionContainer, ratingValue) {
        const commentContainer = questionContainer.querySelector('.comment-container');
        const commentField = questionContainer.querySelector('.answers-page__update-comment');

        if (!commentContainer || !commentField) {
            return;
        }

        if (ratingValue >= 5) {
            commentContainer.classList.add('is-hidden');
            commentField.value = '';
            return;
        }

        commentContainer.classList.remove('is-hidden');
    }

    function setRating(questionContainer, ratingValue) {
        const buttons = questionContainer.querySelectorAll('[data-answer-rating]');

        buttons.forEach(function (button) {
            const buttonRating = Number.parseInt(button.dataset.answerRating || '', 10);
            const isActive = buttonRating === ratingValue;
            button.classList.toggle('active', isActive);
            button.setAttribute('aria-pressed', isActive ? 'true' : 'false');
        });

        updateCommentVisibility(questionContainer, ratingValue);
        hideError();
    }

    function getPlainTextFromHtml(html) {
        if (!html) {
            return '';
        }

        const parser = new DOMParser();
        return parser.parseFromString(html, 'text/html').body.textContent?.trim() || '';
    }

    function collectAnswers() {
        const questionContainers = page.querySelectorAll('.question-container[data-question-id]');
        const answers = [];

        for (const questionContainer of questionContainers) {
            const questionId = questionContainer.dataset.questionId || '';
            const activeButton = questionContainer.querySelector('[data-answer-rating].active');
            const commentField = questionContainer.querySelector('.answers-page__update-comment');

            if (!activeButton) {
                return {
                    error: 'Выберите оценку для каждого вопроса перед сохранением.'
                };
            }

            const ratingValue = Number.parseInt(activeButton.dataset.answerRating || '', 10);
            if (!Number.isFinite(ratingValue) || ratingValue < 1 || ratingValue > 5) {
                return {
                    error: 'Обнаружена некорректная оценка. Обновите страницу и попробуйте снова.'
                };
            }

            const comment = commentField?.value?.trim() || '';
            if (ratingValue < 5 && !comment) {
                return {
                    error: 'Для каждой оценки ниже 5 требуется комментарий.'
                };
            }

            answers.push({
                question_id: questionId,
                rating: ratingValue,
                comment: comment
            });
        }

        return { answers: answers };
    }

    async function submitUpdatedAnswers() {
        hideError();

        if (!Number.isFinite(surveyId) || surveyId <= 0 || !Number.isFinite(organizationId) || organizationId <= 0) {
            showError('Не удалось определить анкету или организацию для сохранения.');
            return;
        }

        const payload = collectAnswers();
        if (payload.error) {
            showError(payload.error);
            return;
        }

        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        const originalText = saveButton?.textContent || '';

        if (saveButton) {
            saveButton.disabled = true;
            saveButton.textContent = 'Сохранение...';
        }

        try {
            const response = await fetch('/answers/update', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'text/html, application/xhtml+xml, application/json',
                    ...(token ? { RequestVerificationToken: token } : {})
                },
                body: JSON.stringify({
                    id_survey: surveyId,
                    organization_id: organizationId,
                    answers: payload.answers
                })
            });

            const responseBody = await response.text();

            if (!response.ok) {
                const errorMessage = getPlainTextFromHtml(responseBody)
                    || `Ошибка при сохранении ответа (${response.status}).`;
                throw new Error(errorMessage);
            }

            document.open();
            document.write(responseBody);
            document.close();
        } catch (error) {
            console.error('Ошибка при обновлении ответа:', error);
            showError(error instanceof Error ? error.message : 'Не удалось сохранить изменения.');
        } finally {
            if (saveButton) {
                saveButton.disabled = false;
                saveButton.textContent = originalText || 'Сохранить изменения';
            }
        }
    }

    page.addEventListener('click', function (event) {
        const ratingButton = event.target.closest('[data-answer-rating]');
        if (!ratingButton) {
            return;
        }

        const questionContainer = ratingButton.closest('.question-container[data-question-id]');
        const ratingValue = Number.parseInt(ratingButton.dataset.answerRating || '', 10);

        if (!questionContainer || !Number.isFinite(ratingValue)) {
            return;
        }

        setRating(questionContainer, ratingValue);
    });

    page.addEventListener('input', function () {
        hideError();
    });

    if (closeErrorButton) {
        closeErrorButton.addEventListener('click', hideError);
    }

    if (saveButton) {
        saveButton.addEventListener('click', submitUpdatedAnswers);
    }
})();
