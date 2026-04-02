(function () {
  function byId(id) {
    return document.getElementById(id);
  }

  function parseDateFromCard(card) {
    const dateElement = card?.querySelector('.dates');
    const dateText = dateElement?.textContent?.trim() || '';
    const match = dateText.match(/(\d{2})\.(\d{2})\.(\d{4})/);
    if (!match) return null;
    return { day: match[1], month: match[2], year: match[3] };
  }

  function getMonthName(monthNum) {
    const months = {
      '01': 'Январь', '02': 'Февраль', '03': 'Март', '04': 'Апрель',
      '05': 'Май', '06': 'Июнь', '07': 'Июль', '08': 'Август',
      '09': 'Сентябрь', '10': 'Октябрь', '11': 'Ноябрь', '12': 'Декабрь'
    };
    return months[monthNum] || monthNum;
  }

  function populateMonthOptions() {
    const select = byId('filterOrganization');
    if (!select) return;

    const cards = document.querySelectorAll('.survey-card');
    const months = new Set();

    cards.forEach(card => {
      const parsed = parseDateFromCard(card);
      if (parsed?.month) months.add(parsed.month);
    });

    const currentValue = select.value;
    select.innerHTML = '<option value="">За все месяцы</option>';
    Array.from(months).sort().forEach(month => {
      const option = document.createElement('option');
      option.value = month;
      option.textContent = getMonthName(month);
      select.appendChild(option);
    });
    select.value = currentValue;
  }

  function populateYearOptions() {
    const select = byId('filterSurvey');
    if (!select) return;

    const cards = document.querySelectorAll('.survey-card');
    const years = new Set();

    cards.forEach(card => {
      const parsed = parseDateFromCard(card);
      if (parsed?.year) years.add(parsed.year);
    });

    const currentValue = select.value;
    select.innerHTML = '<option value="">По всем годам</option>';
    Array.from(years).sort().forEach(year => {
      const option = document.createElement('option');
      option.value = year;
      option.textContent = year;
      select.appendChild(option);
    });
    select.value = currentValue;
  }

  function filterByDate() {
    const month = byId('filterOrganization')?.value || '';
    const year = byId('filterSurvey')?.value || '';
    const cards = document.querySelectorAll('.survey-card');
    let visibleCount = 0;

    cards.forEach(card => {
      const parsed = parseDateFromCard(card);
      if (!parsed) {
        card.style.display = 'none';
        return;
      }

      const matchMonth = !month || parsed.month === month;
      const matchYear = !year || parsed.year === year;

      if (matchMonth && matchYear) {
        card.style.display = '';
        visibleCount++;
      } else {
        card.style.display = 'none';
      }
    });

    const noSurveysElement = document.querySelector('.no-surveys');
    if (noSurveysElement) {
      noSurveysElement.style.display = visibleCount === 0 ? '' : 'none';
    }
  }

  async function createPdfReport(surveyId, omsuId) {
    try {
      const response = await fetch(`/create_pdf_report/${surveyId}/${omsuId}`);
      if (!response.ok) throw new Error('Не удалось сформировать PDF-отчёт');

      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `otchet_${surveyId}_${omsuId}.pdf`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      window.URL.revokeObjectURL(url);
    } catch (error) {
      console.error('createPdfReport error:', error);
      alert(error.message || 'Ошибка при формировании PDF-отчёта');
    }
  }

  function removeLoadingOverlay() {
    const overlay = document.querySelector('.loading-overlay');
    if (overlay) overlay.remove();
  }

  async function downloadSignedArchive(surveyId, omsuId) {
    let loadingIndicator = null;
    try {
      loadingIndicator = document.createElement('div');
      loadingIndicator.className = 'loading-overlay';
      loadingIndicator.innerHTML = `
        <div class="loading-overlay__content">
          <div class="spinner"></div>
          <div class="loading-text">Подготавливаем архив с подписью...</div>
        </div>`;
      document.body.appendChild(loadingIndicator);

      const response = await fetch(`/download_signed_archive/${surveyId}/${omsuId}`);
      if (!response.ok) {
        const errorData = await response.json().catch(() => null);
        throw new Error(errorData?.error || 'Ошибка загрузки архива');
      }

      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `signed_archive_${surveyId}_${omsuId}.zip`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      window.URL.revokeObjectURL(url);
    } catch (error) {
      console.error('downloadSignedArchive error:', error);
      alert(error.message || 'Не удалось загрузить архив с подписью');
    } finally {
      removeLoadingOverlay();
    }
  }

  function scheduleFilterInit() {
    setTimeout(() => {
      populateMonthOptions();
      populateYearOptions();
      filterByDate();
    }, 0);
  }

  function install() {
    window.populateMonthOptions = populateMonthOptions;
    window.populateYearOptions = populateYearOptions;
    window.filterByDate = filterByDate;
    window.createPdfReport = createPdfReport;
    window.downloadSignedArchive = downloadSignedArchive;
  }

  install();
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', scheduleFilterInit);
  } else {
    scheduleFilterInit();
  }
  window.addEventListener('load', () => {
    install();
    scheduleFilterInit();
  });
})();
