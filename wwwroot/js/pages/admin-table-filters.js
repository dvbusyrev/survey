(function () {
  function getRows(selector) {
    const tbody = document.querySelector(selector);
    return tbody ? Array.from(tbody.querySelectorAll('tr.active')) : [];
  }

  function getSelect(id) {
    return document.getElementById(id);
  }

  function fillSelectOptions(selectElem, valuesSet, defaultText) {
    if (!selectElem) return;

    const currentValue = selectElem.value;
    selectElem.innerHTML = '';

    const defaultOption = document.createElement('option');
    defaultOption.value = '';
    defaultOption.textContent = defaultText;
    selectElem.appendChild(defaultOption);

    Array.from(valuesSet)
      .sort((a, b) => String(a).localeCompare(String(b), 'ru'))
      .forEach(value => {
        const option = document.createElement('option');
        option.value = value;
        option.textContent = value;
        selectElem.appendChild(option);
      });

    if (Array.from(selectElem.options).some(opt => opt.value === currentValue)) {
      selectElem.value = currentValue;
    } else {
      selectElem.value = '';
    }
  }

  function populateOrganizationOptions() {
    const select = getSelect('filterOrganization');
    const rows = getRows('#data_table tbody');
    const orgSet = new Set();

    rows.forEach(row => {
      if (row.cells[0]) {
        orgSet.add(row.cells[0].textContent.trim());
      }
    });

    fillSelectOptions(select, orgSet, 'Все организации');
  }

  function populateSurveyOptions() {
    const select = getSelect('filterSurvey');
    const rows = getRows('#data_table tbody');
    const surveySet = new Set();

    rows.forEach(row => {
      if (row.cells[1]) {
        surveySet.add(row.cells[1].textContent.trim());
      }
    });

    fillSelectOptions(select, surveySet, 'Все анкеты');
  }

  function filterTable() {
    const orgSelect = getSelect('filterOrganization');
    const surveySelect = getSelect('filterSurvey');
    const tbody = document.querySelector('#data_table tbody');
    const noneResultRow = document.getElementById('none_result');

    if (!tbody) return;

    const rows = Array.from(tbody.rows);
    const selectedOrg = orgSelect ? orgSelect.value : '';
    const selectedSurvey = surveySelect ? surveySelect.value : '';
    let visibleCount = 0;

    rows.forEach(row => {
      if (!row.classList.contains('active')) return;

      const orgName = row.cells[0] ? row.cells[0].textContent.trim() : '';
      const surveyName = row.cells[1] ? row.cells[1].textContent.trim() : '';
      const matchOrg = !selectedOrg || orgName === selectedOrg;
      const matchSurvey = !selectedSurvey || surveyName === selectedSurvey;

      if (matchOrg && matchSurvey) {
        row.style.display = '';
        visibleCount += 1;
      } else {
        row.style.display = 'none';
      }
    });

    if (noneResultRow) {
      noneResultRow.style.display = visibleCount === 0 ? '' : 'none';
    }
  }

  function loadSurveyOptions() {
    const select = getSelect('filterSurvey');
    const rows = getRows('#data_table tbody');
    const surveySet = new Set();

    rows.forEach(row => {
      if (row.cells[0]) {
        surveySet.add(row.cells[0].textContent.trim());
      }
    });

    fillSelectOptions(select, surveySet, 'Все анкеты');
  }

  function applySurveyFilter() {
    const select = getSelect('filterSurvey');
    const rows = getRows('#data_table tbody');
    const noneResultRow = document.getElementById('none_result');
    const selectedSurvey = select ? select.value : '';
    let visibleCount = 0;

    rows.forEach(row => {
      const surveyName = row.cells[0] ? row.cells[0].textContent.trim() : '';
      const matchSurvey = !selectedSurvey || surveyName === selectedSurvey;

      if (matchSurvey) {
        row.style.display = '';
        visibleCount += 1;
      } else {
        row.style.display = 'none';
      }
    });

    if (noneResultRow) {
      noneResultRow.style.display = visibleCount === 0 ? '' : 'none';
    }
  }

  window.fillSelectOptions = fillSelectOptions;
  window.populateOrganizationOptions = populateOrganizationOptions;
  window.populateSurveyOptions = populateSurveyOptions;
  window.filterTable = filterTable;
  window.loadSurveyOptions = loadSurveyOptions;
  window.applySurveyFilter = applySurveyFilter;
})();
