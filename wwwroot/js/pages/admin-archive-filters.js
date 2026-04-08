function fillSelectOptions(selectElement, valuesSet, defaultText) {
    const currentValue = selectElement.value;
    selectElement.innerHTML = '';

    const defaultOption = document.createElement('option');
    defaultOption.value = '';
    defaultOption.textContent = defaultText;
    selectElement.appendChild(defaultOption);

    valuesSet.forEach(value => {
        const option = document.createElement('option');
        option.value = value;
        option.textContent = value;
        selectElement.appendChild(option);
    });

    if (Array.from(selectElement.options).some(option => option.value === currentValue)) {
        selectElement.value = currentValue;
        return;
    }

    selectElement.value = '';
}

function populateOrganizationOptions() {
    const select = document.getElementById('filterOrganization');
    const tbody = document.querySelector('#data_table tbody');
    if (!select || !tbody) {
        return;
    }

    const rows = tbody.rows;
    const organizationSet = new Set();

    for (let index = 0; index < rows.length; index += 1) {
        const row = rows[index];
        if (!row.classList.contains('active')) {
            continue;
        }

        organizationSet.add(row.cells[0].textContent.trim());
    }

    fillSelectOptions(select, organizationSet, 'Все организации');
}

function populateSurveyOptions() {
    const select = document.getElementById('filterSurvey');
    const tbody = document.querySelector('#data_table tbody');
    if (!select || !tbody) {
        return;
    }

    const rows = tbody.rows;
    const surveySet = new Set();

    for (let index = 0; index < rows.length; index += 1) {
        const row = rows[index];
        if (!row.classList.contains('active')) {
            continue;
        }

        surveySet.add(row.cells[1].textContent.trim());
    }

    fillSelectOptions(select, surveySet, 'Все анкеты');
}

function filterTable() {
    const orgSelect = document.getElementById('filterOrganization');
    const surveySelect = document.getElementById('filterSurvey');
    const tbody = document.querySelector('#data_table tbody');
    const noneResultRow = document.getElementById('none_result');

    if (!orgSelect || !surveySelect || !tbody) {
        return;
    }

    const rows = tbody.rows;
    const selectedOrg = orgSelect.value;
    const selectedSurvey = surveySelect.value;
    let visibleCount = 0;

    for (let index = 0; index < rows.length; index += 1) {
        const row = rows[index];
        if (!row.classList.contains('active')) {
            continue;
        }

        const orgName = row.cells[0].textContent.trim();
        const surveyName = row.cells[1].textContent.trim();
        const matchOrg = !selectedOrg || orgName === selectedOrg;
        const matchSurvey = !selectedSurvey || surveyName === selectedSurvey;

        if (matchOrg && matchSurvey) {
            row.style.display = '';
            visibleCount += 1;
        } else {
            row.style.display = 'none';
        }
    }

    if (noneResultRow) {
        noneResultRow.style.display = visibleCount === 0 ? '' : 'none';
    }
}

function loadSurveyOptions() {
    const select = document.getElementById('filterSurvey');
    const tbody = document.querySelector('#data_table tbody');
    if (!select || !tbody) {
        return;
    }

    const rows = tbody.querySelectorAll('tr.active');
    const surveySet = new Set();

    rows.forEach(row => {
        surveySet.add(row.cells[0].textContent.trim());
    });

    fillSelectOptions(select, surveySet, 'Все анкеты');
}

function applySurveyFilter() {
    const select = document.getElementById('filterSurvey');
    const tbody = document.querySelector('#data_table tbody');
    const noneResultRow = document.getElementById('none_result');
    if (!select || !tbody) {
        return;
    }

    const rows = tbody.querySelectorAll('tr.active');
    const selectedSurvey = select.value;
    let visibleCount = 0;

    rows.forEach(row => {
        const surveyName = row.cells[0].textContent.trim();
        const matches = !selectedSurvey || surveyName === selectedSurvey;

        if (matches) {
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

function copyArchivedSurvey(surveyId) {
    const data = {
        survey_id: surveyId
    };

    fetch('/surveys/archive/copy', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(data)
    })
        .then(response => {
            if (!response.ok) {
                throw new Error('Ошибка при добавлении анкеты');
            }

            return response.json();
        })
        .then(() => {
            alert('Анкета успешно добавлена!');
            window.location.reload();
        })
        .catch(error => {
            alert(error.message);
        });
}
