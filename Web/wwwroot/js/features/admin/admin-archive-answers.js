window.AdminArchives = (function () {
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

  function createDateContent(value) {
    const content = document.createElement('span');
    content.className = 'answers-modal__date-text';
    const normalizedValue = String(value || '').trim();

    if (!normalizedValue) {
      content.textContent = 'Не указана';
      return content;
    }

    content.textContent = normalizedValue;
    return content;
  }

  function renderModalTitle(title, surveyName) {
    clearNode(title);

    const mainLine = document.createElement('span');
    mainLine.className = 'answers-modal__title-main';
    mainLine.textContent = 'Просмотр анкеты';

    const nameLine = document.createElement('span');
    nameLine.className = 'answers-modal__title-name';
    nameLine.textContent = surveyName || 'Без названия';

    title.appendChild(mainLine);
    title.appendChild(nameLine);
  }

  function createSignatureContent(isSigned) {
    const signatureCellContent = document.createElement('span');
    signatureCellContent.className = 'answers-modal__signature-text';
    signatureCellContent.textContent = isSigned ? 'Подписана' : 'Нет подписи';
    return signatureCellContent;
  }

  function renderSignatureBlock(answers, container) {
    const firstAnswer = Array.isArray(answers) && answers.length > 0 ? answers[0] : null;
    const block = document.createElement('div');
    block.className = 'answers-modal__info-block answers-modal__signature-block';

    const label = document.createElement('div');
    label.className = 'answers-modal__field-label';
    label.textContent = 'Подпись';

    block.appendChild(label);
    block.appendChild(createSignatureContent(Boolean(firstAnswer?.is_signed)));
    container.appendChild(block);
  }

  function renderDateBlock(answers, container) {
    const firstAnswer = Array.isArray(answers) && answers.length > 0 ? answers[0] : null;
    const block = document.createElement('div');
    block.className = 'answers-modal__info-block answers-modal__date-block';

    const label = document.createElement('div');
    label.className = 'answers-modal__field-label';
    label.textContent = 'Дата';

    block.appendChild(label);
    block.appendChild(createDateContent(firstAnswer?.date));
    container.appendChild(block);
  }

  function renderAnswers(data, isArchive, container, title) {
    renderModalTitle(title, data.survey.name);
    clearNode(container);
    renderDateBlock(data.answers, container);
    renderSignatureBlock(data.answers, container);

    const tableContainer = document.createElement('div');
    tableContainer.className = 'answers-table-container table-responsive answers-modal__table-wrap';
    const table = document.createElement('table');
    table.className = 'answers-table answers-modal__table';
    table.dataset.role = 'main-table';
    table.dataset.disableColumnSort = 'true';
    const thead = document.createElement('thead');
    const headRow = document.createElement('tr');
    headRow.className = 'table_tr';
    if (isArchive) {
      const orgHeader = document.createElement('th');
      orgHeader.className = 'table-th--start';
      orgHeader.textContent = 'Организация';
      headRow.appendChild(orgHeader);
    }
    ['Вопрос', 'Оценка', 'Комментарий'].forEach((headerText, index, headers) => {
      const th = document.createElement('th');
      if (!isArchive && index === 0) {
        th.classList.add('table-th--start');
      }
      if (index === headers.length - 1) {
        th.classList.add('table-th--end');
      }
      if (headerText === 'Оценка') {
        th.classList.add('answers-modal__rating-column');
      }
      th.textContent = headerText;
      headRow.appendChild(th);
    });
    thead.appendChild(headRow);
    table.appendChild(thead);
    const tbody = document.createElement('tbody');
    data.answers.forEach(answer => {
      const answerItems = Array.isArray(answer.answers) ? answer.answers : [];
      const rowSpan = answerItems.length > 0 ? answerItems.length : 1;

      if (answerItems.length > 0) {
        answerItems.forEach((item, index) => {
          const row = document.createElement('tr');
          if (isArchive && index === 0) {
            const organizationCell = createTextCell(answer.organization_name || 'Не указано', 'organization-cell');
            organizationCell.rowSpan = rowSpan;
            row.appendChild(organizationCell);
          }
          row.appendChild(createTextCell(item.question_text || 'Не указан', 'question-cell'));
          row.appendChild(createTextCell(item.rating || '0', 'rating-cell'));
          row.appendChild(createTextCell(item.comment || 'Нет комментария', 'comment-cell'));
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

  function openPreparedAnswersModal(modal) {
    if (window.showSiteModal) {
      window.showSiteModal(modal);
    } else {
      modal.style.display = 'flex';
    }
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

      const url = isArchive
        ? `/answers/${surveyId}/0/archive`
        : `/answers/${surveyId}/${organizationId}/regular`;

      const response = await fetch(url, {
        headers: {
          Accept: 'application/json'
        }
      });

      if (!response.ok) {
        let errorMessage = response.status === 404
          ? 'Ответы по выбранной архивной анкете не найдены.'
          : `Ошибка сервера: ${response.status}`;

        try {
          const errorData = await response.json();
          errorMessage = errorData?.error || errorData?.message || errorMessage;
        } catch (parseError) {
          // Ignore invalid JSON error payloads and keep fallback message.
        }

        throw new Error(errorMessage);
      }

      const data = await response.json();
      if (!data.success || !data.survey || !data.answers) {
        throw new Error(data.error || 'Неверный формат данных от сервера');
      }

      renderAnswers(data, isArchive, container, title);
      openPreparedAnswersModal(modal);
    } catch (error) {
      console.error('Ошибка:', error);
      clearNode(container);
      title.textContent = 'Ошибка загрузки ответов';
      const errorWrap = document.createElement('div');
      errorWrap.className = 'error-message';
      const p1 = document.createElement('p');
      p1.textContent = 'Ошибка загрузки данных:';
      const br1 = document.createElement('br');
      const p2 = document.createElement('p');
      const strong = document.createElement('strong');
      strong.textContent = typeof window.normalizeClientErrorMessage === 'function'
        ? window.normalizeClientErrorMessage(error.message || 'Неизвестная ошибка')
        : (error.message || 'Неизвестная ошибка');
      p2.appendChild(strong);
      errorWrap.appendChild(p1);
      errorWrap.appendChild(br1);
      errorWrap.appendChild(p2);
      container.appendChild(errorWrap);
      openPreparedAnswersModal(modal);
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
