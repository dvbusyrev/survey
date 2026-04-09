window.AdminArchives = (function () {
  function getTemplateContent(templateId) {
    const template = document.getElementById(templateId);
    if (!template || !template.content) {
      return null;
    }
    return template.content;
  }

  function clearNode(node) {
    if (!node) {
      return;
    }
    while (node.firstChild) {
      node.removeChild(node.firstChild);
    }
  }

  function createTextCell(text, className) {
    const cell = document.createElement('td');
    if (className) {
      cell.className = className;
    }
    cell.textContent = text;
    return cell;
  }

  function renderAnswers(data, isArchive, container, title) {
    title.textContent = `Ответы на архивную анкету: ${data.survey.name}`;
    clearNode(container);

    if (data.survey.description) {
      const info = document.createElement('div');
      info.className = 'survey-info';
      const description = document.createElement('p');
      description.className = 'survey-description';
      description.textContent = `Описание: ${data.survey.description}`;
      info.appendChild(description);
      info.appendChild(document.createElement('br'));
      container.appendChild(info);
    }

    const tableContainer = document.createElement('div');
    tableContainer.className = 'answers-table-container';
    const table = document.createElement('table');
    table.className = 'answers-table';
    const thead = document.createElement('thead');
    const headRow = document.createElement('tr');
    headRow.className = 'table-tr';
    if (isArchive) {
      const orgHeader = document.createElement('th');
      orgHeader.textContent = 'Организация';
      headRow.appendChild(orgHeader);
    }
    ['Вопрос', 'Оценка', 'Комментарий', 'Дата', 'Подпись'].forEach((headerText) => {
      const th = document.createElement('th');
      th.textContent = headerText;
      headRow.appendChild(th);
    });
    thead.appendChild(headRow);
    table.appendChild(thead);
    const tbody = document.createElement('tbody');
    data.answers.forEach(answer => {
      const answerItems = Array.isArray(answer.answers) ? answer.answers : [];
      const rowSpan = answerItems.length > 0 ? answerItems.length : 1;
      const signatureCellContent = document.createElement('span');
      signatureCellContent.className = answer.is_signed ? 'signed' : 'not-signed';
      signatureCellContent.textContent = answer.is_signed ? '✓' : '✗';

      if (answerItems.length > 0) {
        answerItems.forEach((item, index) => {
          const row = document.createElement('tr');
          if (isArchive && index === 0) {
            const organizationCell = createTextCell(answer.organization_name || 'Не указано', 'organization-cell');
            organizationCell.rowSpan = rowSpan;
            row.appendChild(organizationCell);
          }
          row.appendChild(createTextCell(item.question_text || 'Не указан', 'question-cell'));
          row.appendChild(createTextCell(`${item.rating || '0'}/5`, 'rating-cell'));
          row.appendChild(createTextCell(item.comment || 'Нет комментария', 'comment-cell'));
          if (index === 0) {
            const dateCell = createTextCell(answer.date || 'Не указана', 'date-cell');
            dateCell.rowSpan = rowSpan;
            row.appendChild(dateCell);
            const signatureCell = document.createElement('td');
            signatureCell.className = 'signature-cell';
            signatureCell.rowSpan = rowSpan;
            signatureCell.appendChild(signatureCellContent.cloneNode(true));
            row.appendChild(signatureCell);
          }
          tbody.appendChild(row);
        });
      } else {
        const row = document.createElement('tr');
        if (isArchive) {
          const organizationCell = createTextCell(answer.organization_name || 'Не указано', 'organization-cell');
          organizationCell.rowSpan = 1;
          row.appendChild(organizationCell);
        }
        row.appendChild(createTextCell('Нет данных', 'question-cell'));
        row.appendChild(createTextCell('-', 'rating-cell'));
        row.appendChild(createTextCell('-', 'comment-cell'));
        row.appendChild(createTextCell(answer.date || 'Не указана', 'date-cell'));
        const signatureCell = document.createElement('td');
        signatureCell.className = 'signature-cell';
        signatureCell.appendChild(signatureCellContent.cloneNode(true));
        row.appendChild(signatureCell);
        tbody.appendChild(row);
      }
    });

    table.appendChild(tbody);
    tableContainer.appendChild(table);
    container.appendChild(tableContainer);
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
      clearNode(container);
      const loadingTemplate = getTemplateContent('answers-loading-template');
      if (loadingTemplate) {
        container.appendChild(loadingTemplate.cloneNode(true));
      } else {
        const loadingNode = document.createElement('div');
        loadingNode.className = 'loading';
        loadingNode.textContent = 'Загрузка данных...';
        container.appendChild(loadingNode);
      }
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
      clearNode(container);
      const errorWrap = document.createElement('div');
      errorWrap.className = 'error-message';
      const p1 = document.createElement('p');
      p1.textContent = 'Ошибка загрузки данных:';
      const br1 = document.createElement('br');
      const p2 = document.createElement('p');
      const strong = document.createElement('strong');
      strong.textContent = error.message || 'Неизвестная ошибка';
      p2.appendChild(strong);
      const br2 = document.createElement('br');
      const retryButton = document.createElement('button');
      retryButton.type = 'button';
      retryButton.className = 'retry-btn';
      retryButton.textContent = 'Повторить попытку';
      retryButton.addEventListener('click', () => showAnswersModal(surveyId, isArchive ? null : organizationId));
      errorWrap.appendChild(p1);
      errorWrap.appendChild(br1);
      errorWrap.appendChild(p2);
      errorWrap.appendChild(br2);
      errorWrap.appendChild(retryButton);
      container.appendChild(errorWrap);
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
