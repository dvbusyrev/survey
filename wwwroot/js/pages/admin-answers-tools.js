(function () {
    function renderAnswers2(data, isArchive, container, title) {
        title.textContent = `Ответы на архивную анкету: ${data.survey.name}`;

        let html = `
        <div class="survey-info">
            ${data.survey.description ? `<p class="survey-description">Описание: ${data.survey.description}</p><br />` : ''}
        </div>
        <div class="answers-table-container">
            <table class="answers-table">
                <thead>
                    <tr class="table-tr">
                        ${isArchive ? '<th>Организация</th>' : ''}
                        <th>Вопрос</th>
                        <th>Оценка</th>
                        <th>Комментарий</th>
                        <th>Дата</th>
                        <th>Подпись</th>
                    </tr>
                </thead>
                <tbody>`;

        data.answers.forEach(answer => {
            const answerItems = Array.isArray(answer.answers) ? answer.answers : [];
            const rowSpan = answerItems.length > 0 ? answerItems.length : 1;

            if (answerItems.length > 0) {
                answerItems.forEach((item, index) => {
                    html += `
                    <tr>
                        ${index === 0 && isArchive ? `<td rowspan="${rowSpan}" class="omsu-cell">${answer.omsu_name || 'Не указано'}</td>` : ''}
                        <td class="question-cell">${item.question_text || 'Не указан'}</td>
                        <td class="rating-cell">${item.rating || '0'}/5</td>
                        <td class="comment-cell">${item.comment || 'Нет комментария'}</td>
                        ${index === 0 ? `
                        <td rowspan="${rowSpan}" class="date-cell">${answer.date || 'Не указана'}</td>
                        <td rowspan="${rowSpan}" class="signature-cell">
                            ${answer.is_signed ? '<span class="signed">✓</span>' : '<span class="not-signed">✗</span>'}
                        </td>` : ''}
                    </tr>`;
                });
            } else {
                html += `
                <tr>
                    ${isArchive ? '<td class="omsu-cell">' + (answer.omsu_name || 'Не указано') + '</td>' : ''}
                    <td class="question-cell">Нет данных</td>
                    <td class="rating-cell">-</td>
                    <td class="comment-cell">-</td>
                    <td class="date-cell">${answer.date || 'Не указана'}</td>
                    <td class="signature-cell">
                        ${answer.is_signed ? '<span class="signed">✓</span>' : '<span class="not-signed">✗</span>'}
                    </td>
                </tr>`;
            }
        });

        html += `
                </tbody>
            </table>
        </div>`;

        container.innerHTML = html;
    }

    async function showAnswersModal(surveyId, omsuId = null) {
        const modal = document.getElementById('answersModal');
        const container = document.getElementById('answersContainer');
        const title = document.getElementById('surveyAnswersTitle');

        try {
            container.innerHTML = '<div class="loading">Загрузка данных...</div>';
            title.textContent = 'Загрузка...';
            modal.style.display = 'block';

            const isArchive = omsuId === null;
            const url = isArchive ? `/answers/${surveyId}/0/archive` : `/answers/${surveyId}/${omsuId}/regular`;

            const response = await fetch(url, {
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                throw new Error(`Ошибка сервера: ${response.status}`);
            }

            const data = await response.json();
            if (!data.success) {
                throw new Error(data.error || 'Неизвестная ошибка сервера');
            }
            if (!data.survey || !data.answers) {
                throw new Error('Неверный формат данных от сервера');
            }

            renderAnswers2(data, isArchive, container, title);
        } catch (error) {
            console.error('Ошибка:', error);
            container.innerHTML = `
            <div class="error-message">
                <p>Ошибка загрузки данных:</p>
                <br />
                <p><strong>${error.message}</strong></p>
                <br />
                <button onclick="showAnswersModal(${surveyId}, ${omsuId || 'null'})" class="retry-btn">
                    Повторить попытку
                </button>
            </div>`;
        }
    }

    function openAnswersModal() {
        const modal = document.getElementById('answersModal');
        if (modal) modal.style.display = 'block';
    }

    function closeAnswersModal() {
        const modal = document.getElementById('answersModal');
        if (modal) modal.style.display = 'none';
    }

    async function showAnswers(surveyId, surveyName) {
        const container = document.getElementById('answersContainer');
        const title = document.getElementById('surveyAnswersTitle');

        title.textContent = `Ответы: ${surveyName}`;
        container.innerHTML = '<div class="loading">Загрузка данных...</div>';
        openAnswersModal();

        try {
            const response = await fetch(`/Survey/GetSurveyAnswers?id=${surveyId}`);
            const data = await response.json();

            if (!data.success) {
                throw new Error(data.error || 'Ошибка загрузки данных');
            }

            renderAnswers(data.survey, data.answers);
        } catch (error) {
            container.innerHTML = `<div class="error">${error.message}</div>`;
        }
    }

    function renderAnswers(survey, answers) {
        const container = document.getElementById('answersContainer');

        let html = `
            <div class="survey-info">
                <h3>${survey.name_survey}</h3>
                <p><strong>Описание:</strong> ${survey.description || 'Нет описания'}</p>
                <p><strong>Период:</strong>
                    ${new Date(survey.date_begin).toLocaleDateString()} -
                    ${new Date(survey.date_end).toLocaleDateString()}
                </p>
                <hr>
            </div>`;

        if (answers && answers.length > 0) {
            html += `
                <table class="answers-table">
                    <thead>
                        <tr>
                            <th>Организация</th>
                            <th>Дата ответа</th>
                            <th>Ответы</th>
                        </tr>
                    </thead>
                    <tbody>`;

            answers.forEach(answer => {
                const answersText = formatAnswers(answer.answers);
                html += `
                    <tr>
                        <td>${answer.name_omsu}</td>
                        <td>${new Date(answer.completion_date).toLocaleDateString()}</td>
                        <td>${answersText}</td>
                    </tr>`;
            });

            html += `</tbody></table>`;
        } else {
            html += '<p>Нет данных об ответах</p>';
        }

        container.innerHTML = html;
    }

    function formatAnswers(answersJson) {
        try {
            const answers = JSON.parse(answersJson);
            return Object.entries(answers)
                .map(([question, answer]) => `<strong>${question}:</strong> ${answer}`)
                .join('<br>');
        } catch (e) {
            return 'Ошибка формата ответов';
        }
    }

    window.addEventListener('click', function (event) {
        const modal = document.getElementById('answersModal');
        if (event.target === modal) {
            modal.style.display = 'none';
        }
    });

    window.renderAnswers2 = renderAnswers2;
    window.showAnswersModal = showAnswersModal;
    window.openAnswersModal = openAnswersModal;
    window.closeAnswersModal = closeAnswersModal;
    window.showAnswers = showAnswers;
    window.renderAnswers = renderAnswers;
    window.formatAnswers = formatAnswers;
})();
