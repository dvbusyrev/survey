window.AdminArchives = (function () {
  function renderAnswers(data, isArchive, container, title) {
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
              ${index === 0 && isArchive ? `<td rowspan="${rowSpan}" class="organization-cell">${answer.organization_name || 'Не указано'}</td>` : ''}
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
            ${isArchive ? `<td class="organization-cell">${answer.organization_name || 'Не указано'}</td>` : ''}
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

  function closeModalById(id) {
    var modal = document.getElementById(id);
    if (!modal) {
      return;
    }

    if (window.hideSiteModal) {
      window.hideSiteModal(modal);
    } else {
      modal.style.display = 'none';
    }
  }

  function closeAnswersModal() {
    closeModalById('answersModal');
  }

  async function showAnswersModal(surveyId, organizationId) {
    const modal = document.getElementById('answersModal');
    const container = document.getElementById('answersContainer');
    const title = document.getElementById('surveyAnswersTitle');

    if (!modal || !container || !title) {
      return;
    }

    const isArchive = organizationId === null || typeof organizationId === 'undefined';

    try {
      container.innerHTML = '<div class="loading">Загрузка данных...</div>';
      title.textContent = 'Загрузка...';

      if (window.showSiteModal) {
        window.showSiteModal(modal);
      } else {
        modal.style.display = 'flex';
      }

      const url = isArchive
        ? `/answers/${surveyId}/0/archive`
        : `/answers/${surveyId}/${organizationId}/regular`;

      const response = await fetch(url, {
        headers: {
          Accept: 'application/json'
        }
      });

      if (!response.ok) {
        throw new Error(`Ошибка сервера: ${response.status}`);
      }

      const data = await response.json();
      if (!data.success || !data.survey || !data.answers) {
        throw new Error(data.error || 'Неверный формат данных от сервера');
      }

      renderAnswers(data, isArchive, container, title);
    } catch (error) {
      console.error('Ошибка:', error);
      container.innerHTML = `
        <div class="error-message">
          <p>Ошибка загрузки данных:</p>
          <br />
          <p><strong>${error.message}</strong></p>
          <br />
          <button type="button"
                  data-click-call="showAnswersModal"
                  data-click-args='${JSON.stringify([surveyId, isArchive ? null : organizationId])}'
                  class="retry-btn">
            Повторить попытку
          </button>
        </div>
      `;
    }
  }

  function wireEscClose() {
    document.addEventListener('keydown', function (event) {
      if (event.key === 'Escape') {
        closeAnswersModal();
      }
    });
  }

  function wireBackdropClose() {
    window.addEventListener('click', function (event) {
      const modal = document.getElementById('answersModal');
      if (!modal || event.target !== modal) {
        return;
      }

      closeAnswersModal();
    });
  }

  function init() {
    wireEscClose();
    wireBackdropClose();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }

  return {
    closeModalById: closeModalById,
    closeAnswersModal: closeAnswersModal,
    showAnswersModal: showAnswersModal
  };
})();

window.showAnswersModal = window.AdminArchives.showAnswersModal;
