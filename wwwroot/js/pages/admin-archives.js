window.AdminArchives = (function () {
  function getTemplateHtml(templateId) {
    const template = document.getElementById(templateId);
    if (!template || !template.content) {
      return '';
    }

    return template.innerHTML;
  }

  function renderTemplate(templateId, data) {
    const templateHtml = getTemplateHtml(templateId);
    return templateHtml.replace(/\{\{\s*([\w]+)\s*\}\}/g, function (_, key) {
      return Object.prototype.hasOwnProperty.call(data, key) ? data[key] : '';
    });
  }

  function escapeHtml(value) {
    return String(value)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  function renderAnswers(data, isArchive, container, title) {
    title.textContent = `Ответы на архивную анкету: ${data.survey.name}`;

    const description = data.survey.description
      ? renderTemplate('answers-survey-description-template', {
        description: escapeHtml(data.survey.description)
      })
      : '';

    const organizationHeader = isArchive
      ? getTemplateHtml('answers-organization-header-template')
      : '';

    let rows = '';

    data.answers.forEach(answer => {
      const answerItems = Array.isArray(answer.answers) ? answer.answers : [];
      const rowSpan = answerItems.length > 0 ? answerItems.length : 1;
      const signatureMark = answer.is_signed
        ? getTemplateHtml('answers-signed-mark-template')
        : getTemplateHtml('answers-not-signed-mark-template');
      const organizationCell = isArchive
        ? renderTemplate('answers-organization-cell-template', {
          rowSpan: String(rowSpan),
          organizationName: escapeHtml(answer.organization_name || 'Не указано')
        })
        : '';

      if (answerItems.length > 0) {
        answerItems.forEach((item, index) => {
          rows += renderTemplate('answers-row-template', {
            organizationCell: index === 0 ? organizationCell : '',
            question: escapeHtml(item.question_text || 'Не указан'),
            rating: escapeHtml(item.rating || '0'),
            comment: escapeHtml(item.comment || 'Нет комментария'),
            dateCell: index === 0 ? renderTemplate('answers-date-cell-template', {
              rowSpan: String(rowSpan),
              date: escapeHtml(answer.date || 'Не указана')
            }) : '',
            signatureCell: index === 0 ? renderTemplate('answers-signature-cell-template', {
              rowSpan: String(rowSpan),
              signatureMark: signatureMark
            }) : ''
          });
        });
      } else {
        rows += renderTemplate('answers-empty-row-template', {
          organizationCell: isArchive
            ? renderTemplate('answers-organization-cell-template', {
              rowSpan: '1',
              organizationName: escapeHtml(answer.organization_name || 'Не указано')
            })
            : '',
          date: escapeHtml(answer.date || 'Не указана'),
          signatureMark: signatureMark
        });
      }
    });

    container.innerHTML = renderTemplate('answers-table-template', {
      description: description,
      organizationHeader: organizationHeader,
      rows: rows
    });
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
      container.innerHTML = getTemplateHtml('answers-loading-template');
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
      container.innerHTML = renderTemplate('answers-error-template', {
        errorMessage: escapeHtml(error.message),
        retryArgs: escapeHtml(JSON.stringify([surveyId, isArchive ? null : organizationId]))
      });
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
