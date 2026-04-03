(function () {
  if (window.__adminSurveysBridgeLoaded) return;
  window.__adminSurveysBridgeLoaded = true;

  function token() {
    return document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
  }

  function getMainContainer() {
    return document.querySelector('main') || document.getElementById('content_admin') || document.body;
  }

  function setLegacyContent(html) {
    const main = getMainContainer();
    if (main) {
      main.innerHTML = html;
    }
    const content = document.querySelector('.content');
    if (content) {
      content.style.width = '78%';
      content.style.marginLeft = '19%';
    }
  }

  function requestHtml(method, url, body) {
    return fetch(url, {
      method,
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8'
      },
      body: body || undefined
    }).then(async (response) => {
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }
      return response.text();
    });
  }

  function closeLegacyModal() {
    const modal = document.getElementById('myModal');
    if (modal) {
      modal.style.display = 'none';
      modal.innerHTML = '';
    }
  }

  function showNotFoundMessage(table) {
    if (!table) return;
    const tbody = table.querySelector('tbody') || table;
    const old = document.getElementById('none_search');
    if (old) old.remove();
    tbody.insertAdjacentHTML('beforeend', "<p id='none_search' style='color:red'>Результаты не найдены!</p>");
  }

  function removeNotFoundMessage() {
    document.getElementById('none_search')?.remove();
  }

  window.closeLegacyAdminModal = closeLegacyModal;

  window.add_survey = function () {
    requestHtml('GET', '/add_survey')
      .then(setLegacyContent)
      .catch((error) => console.error('Ошибка при открытии добавления анкеты:', error));
  };

  window.update_survey = function (id) {
    requestHtml('POST', `/update_survey/${id}`)
      .then(setLegacyContent)
      .catch((error) => console.error('Ошибка при редактировании анкеты:', error));
  };

  window.copy_survey = function (id) {
    requestHtml('POST', `/copy_survey/${id}`)
      .then(setLegacyContent)
      .catch((error) => console.error('Ошибка при копировании анкеты:', error));
  };

  window.delete_survey = function (id) {
    if (!confirm('Вы уверены, что хотите удалить эту анкету?')) return;

    fetch(`/surveys/delete/${id}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8' }
    })
      .then((response) => {
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        alert('Анкета успешно удалена!');
        if (typeof window.handleTabClick === 'function') {
          window.handleTabClick('get_surveys');
        } else {
          window.location.reload();
        }
      })
      .catch((error) => console.error('Ошибка при удалении анкеты:', error));
  };

  window.update_survey_bd = async function (id) {
    const surveyTitle = document.getElementById('surveyTitle')?.value || '';
    const surveyDescription = document.getElementById('surveyDescription')?.value || '';
    const startDate = document.getElementById('startDate')?.value || '';
    const endDate = document.getElementById('endDate')?.value || '';

    if (!surveyTitle || !surveyDescription || !startDate || !endDate) {
      alert('Все поля должны быть заполнены!');
      return;
    }

    const startDateObj = new Date(startDate);
    const endDateObj = new Date(endDate);
    if (endDateObj <= startDateObj) {
      alert('Дата завершения не может быть раньше или равна дате начала.');
      return;
    }

    const data = [surveyTitle, surveyDescription, startDate, endDate];
    const selectedOmsuIds = [];
    document.querySelectorAll('select#organizationSelect').forEach((select) => {
      if (select.selectedIndex !== -1) {
        selectedOmsuIds.push(select.options[select.selectedIndex].value);
      }
    });
    data.push(selectedOmsuIds.join(','));

    const criteriaInputs = document.getElementsByClassName('criteriy');
    for (let i = 0; i < criteriaInputs.length; i++) {
      const value = criteriaInputs[i].value.trim();
      if (!value) {
        alert('Все критерии должны быть заполнены!');
        return;
      }
      data.push(value);
    }

    try {
      const response = await fetch(`/update_survey_bd/${id}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json;charset=UTF-8' },
        body: JSON.stringify(data)
      });

      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      alert('Анкета успешно отредактирована');
      if (typeof window.handleTabClick === 'function') window.handleTabClick('get_surveys');
    } catch (error) {
      console.error('Ошибка обновления анкеты:', error);
      alert('Ошибка при обновлении анкеты');
    }
  };

  window.copy_survey_bd = async function (id) {
    const startDate = document.getElementById('startDate')?.value || '';
    const endDate = document.getElementById('endDate')?.value || '';

    try {
      const response = await fetch(`/copy_survey_bd/${id}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json;charset=UTF-8' },
        body: JSON.stringify({ startDate, endDate })
      });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      alert('Анкета успешно скопирована');
      if (typeof window.handleTabClick === 'function') window.handleTabClick('get_surveys');
    } catch (error) {
      console.error('Ошибка копирования анкеты:', error);
      alert('Ошибка при копировании анкеты');
    }
  };

  window.downloadReport = function (url, defaultFileName) {
    const loader = document.createElement('div');
    loader.style.position = 'fixed';
    loader.style.top = '0';
    loader.style.left = '0';
    loader.style.width = '100%';
    loader.style.height = '3px';
    loader.style.backgroundColor = '#007bff';
    loader.style.zIndex = '9999';
    document.body.appendChild(loader);

    fetch(url)
      .then((response) => {
        if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);
        let fileName = defaultFileName;
        const contentDisposition = response.headers.get('Content-Disposition');
        if (contentDisposition) {
          const utf8Match = contentDisposition.match(/filename\*=UTF-8''(.+)/);
          if (utf8Match) fileName = decodeURIComponent(utf8Match[1]);
          else {
            const regularMatch = contentDisposition.match(/filename="(.+)"/);
            if (regularMatch) fileName = regularMatch[1];
          }
        }
        return response.blob().then((blob) => ({ blob, fileName }));
      })
      .then(({ blob, fileName }) => {
        const tempUrl = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = tempUrl;
        a.download = window.sanitizeFileName(fileName);
        document.body.appendChild(a);
        a.click();
        setTimeout(() => {
          document.body.removeChild(a);
          window.URL.revokeObjectURL(tempUrl);
          loader.remove();
        }, 100);
      })
      .catch((error) => {
        console.error('Ошибка при скачивании файла:', error);
        loader.remove();
        alert('Произошла ошибка при скачивании отчета. Пожалуйста, попробуйте позже.');
      });
  };

  window.create_otchet_month = function (id) {
    window.downloadReport(`/create_otchet_month/${id}`, 'Отчет.docx');
  };

  window.create_otchetAll_month = function () {
    window.downloadReport('/create_otchetAll_month', 'Отчет_по_всем_анкетам.docx');
  };

  window.sanitizeFileName = function (name) {
    return String(name || '')
      .replace(/[/\\?%*:|"<>]/g, '_')
      .replace(/\s+/g, ' ')
      .trim()
      .substring(0, 255);
  };

  window.create_otchetAll_kvartal = function (kvartal, year) {
    const xhr = new XMLHttpRequest();
    xhr.responseType = 'blob';
    xhr.onreadystatechange = function () {
      if (xhr.readyState !== 4) return;
      if (xhr.status === 200) {
        const contentDisposition = xhr.getResponseHeader('Content-Disposition');
        let fileName = `Otchet_za_${kvartal}_kvartal.xlsx`;
        if (contentDisposition) {
          const match = contentDisposition.match(/filename="?([^"]+)"?/);
          if (match && match[1]) fileName = match[1];
        }
        const blob = new Blob([xhr.response], {
          type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
        });
        const link = document.createElement('a');
        link.href = window.URL.createObjectURL(blob);
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
      } else {
        console.error('Ошибка квартального отчета:', xhr.status);
      }
    };
    xhr.onerror = function () {
      console.error('Проблемы с интернетом');
    };
    xhr.open('GET', `/create_otchet_kvartal/${kvartal}/${year}`, true);
    xhr.send();
  };

  window.submitExtension = function (id) {
    const rows = document.querySelectorAll('.form-row');
    const data = [];
    if (!rows.length) {
      alert('Пожалуйста, добавьте хотя бы одну организацию для продления.');
      return;
    }

    let isValid = true;
    rows.forEach((row) => {
      const organizationSelect = row.querySelector('select.form-control');
      const endDateInput = row.querySelector('input.form-control[type="date"]');
      if (!organizationSelect || !endDateInput) return;

      const organization = organizationSelect.value;
      const endDate = endDateInput.value;
      if (!organization || !endDate) {
        isValid = false;
        return;
      }
      const today = new Date().toISOString().split('T')[0];
      if (endDate <= today) {
        alert('Дата окончания должна быть в будущем!');
        isValid = false;
        return;
      }
      data.push({ id_omsu: parseInt(organization, 10), new_end_date: endDate, id_survey: id });
    });

    if (!isValid) {
      alert('Пожалуйста, заполните все поля перед применением.');
      return;
    }

    fetch('/prodlenie_omsus', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json;charset=UTF-8' },
      body: JSON.stringify(data)
    })
      .then(async (response) => {
        if (response.ok) {
          alert('Доступ к анкете успешно продлён!');
          closeLegacyModal();
          window.location.reload();
          return;
        }
        let message = `Ошибка при отправке данных: ${response.status}`;
        try {
          const json = await response.json();
          message = `Ошибка: ${json.message || response.statusText}`;
        } catch (_) {}
        throw new Error(message);
      })
      .catch((error) => {
        console.error('Ошибка продления:', error);
        alert(error.message || 'Произошла ошибка при отправке запроса');
      });
  };

  window.openModal = function (idSurvey) {
    const modal = document.getElementById('myModal');
    if (!modal) return;
    modal.innerHTML = `
      <div class="modal-content">
        <div class="modal-header">
          <h2>Продлить доступ к анкете</h2>
          <span class="close" onclick="closeLegacyAdminModal()">&times;</span>
        </div>
        <div class="modal-body">
          <div id="extensionRows"></div>
        </div>
        <div class="modal-footer">
          <button onclick="submitExtension(${idSurvey})" class="modal_btn">Применить</button>
        </div>
      </div>`;
    modal.style.display = 'block';
  };

  const settingsFilePath = '/email_settings.txt';

  window.loadEmailSettings = function () {
    fetch(settingsFilePath)
      .then((response) => {
        if (!response.ok) {
          if (response.status === 404) return 'Кому:\nТема:\nСодержание:';
          throw new Error('Ошибка загрузки файла');
        }
        return response.text();
      })
      .then((text) => {
        const settings = window.parseSettingsFile(text);
        const to = document.getElementById('email-to');
        const subject = document.getElementById('email-subject');
        const content = document.getElementById('email-content');
        if (to) to.value = settings.to || '';
        if (subject) subject.value = settings.subject || '';
        if (content) content.value = settings.content || '';
        alert('Настройки успешно загружены!');
      })
      .catch((error) => {
        console.error('Ошибка:', error);
        alert('Ошибка при загрузке настроек');
      });
  };

  window.saveEmailSettings = function () {
    const settingsData = {
      to: document.getElementById('email-to')?.value || '',
      subject: document.getElementById('email-subject')?.value || '',
      content: document.getElementById('email-content')?.value || ''
    };

    fetch('/save_email_settings', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(settingsData)
    })
      .then((response) => {
        if (!response.ok) throw new Error('Ошибка сохранения');
        return response.json();
      })
      .then(() => alert('Настройки успешно сохранены!'))
      .catch((error) => {
        console.error('Ошибка:', error);
        alert('Ошибка при сохранении настроек');
      });
  };

  window.parseSettingsFile = function (text) {
    const lines = String(text || '').split('\n');
    const settings = { to: '', subject: '', content: '' };
    lines.forEach((line) => {
      if (line.startsWith('Кому:')) settings.to = line.replace('Кому:', '').trim();
      else if (line.startsWith('Тема:')) settings.subject = line.replace('Тема:', '').trim();
      else if (line.startsWith('Содержание:')) settings.content = line.replace('Содержание:', '').trim();
      else if (settings.content) settings.content += `\n${line.trim()}`;
    });
    return settings;
  };

  window.loadFileAdmin = function () {
    window.loadAndUploadFile('admin');
  };

  window.loadFileUser = function () {
    window.loadAndUploadFile('user');
  };

  window.loadAndUploadFile = function (role) {
    const url = role === 'admin'
      ? '/get_file?fileName=Instruction_for_admin_anketirovanie.docx'
      : '/get_file?fileName=Instruction_for_user_anketirovanie.docx';
    window.open(url, '_blank');
  };

  window.restoreSurveys = function () {
    if (typeof window.handleTabClick === 'function') window.handleTabClick('get_surveys');
  };
  window.archive_list_omsus = function () {
    if (typeof window.handleTabClick === 'function') window.handleTabClick('archive_list_omsus');
  };
  window.archive_list_users = function () {
    if (typeof window.handleTabClick === 'function') window.handleTabClick('archive_list_users');
  };

  window.fillSelectOptions = function (selectElem, valuesSet, defaultText) {
    if (!selectElem) return;
    selectElem.innerHTML = '';
    const defaultOption = document.createElement('option');
    defaultOption.value = '';
    defaultOption.textContent = defaultText;
    selectElem.appendChild(defaultOption);
    Array.from(valuesSet).sort().forEach((value) => {
      const option = document.createElement('option');
      option.value = value;
      option.textContent = value;
      selectElem.appendChild(option);
    });
  };

  window.populateOrganizationOptions = function () {
    const tbody = document.querySelector('#data_table tbody');
    const orgSelect = document.getElementById('organizationFilter');
    if (!tbody || !orgSelect) return;
    const values = new Set();
    tbody.querySelectorAll('tr').forEach((row) => {
      const cell = row.cells?.[2];
      if (!cell) return;
      cell.textContent.split(',').map((v) => v.trim()).filter(Boolean).forEach((v) => values.add(v));
    });
    window.fillSelectOptions(orgSelect, values, 'Все ОМСУ');
  };

  window.populateSurveyOptions = function () {
    const tbody = document.querySelector('#data_table tbody');
    const surveySelect = document.getElementById('surveyFilter');
    if (!tbody || !surveySelect) return;
    const values = new Set();
    tbody.querySelectorAll('tr').forEach((row) => {
      const cell = row.cells?.[0];
      if (!cell) return;
      const value = cell.textContent.trim();
      if (value) values.add(value);
    });
    window.fillSelectOptions(surveySelect, values, 'Все анкеты');
  };

  window.filterTable = function () {
    const tbody = document.querySelector('#data_table tbody');
    if (!tbody) return;
    const surveyValue = (document.getElementById('surveyFilter')?.value || '').toLowerCase();
    const organizationValue = (document.getElementById('organizationFilter')?.value || '').toLowerCase();
    let visible = 0;
    tbody.querySelectorAll('tr').forEach((row) => {
      const surveyText = row.cells?.[0]?.textContent.toLowerCase() || '';
      const orgText = row.cells?.[2]?.textContent.toLowerCase() || '';
      const matchesSurvey = !surveyValue || surveyText.includes(surveyValue);
      const matchesOrg = !organizationValue || orgText.includes(organizationValue);
      const show = matchesSurvey && matchesOrg;
      row.style.display = show ? '' : 'none';
      if (show) visible++;
    });
    removeNotFoundMessage();
    if (!visible) showNotFoundMessage(document.getElementById('data_table'));
  };

  window.searchTable = function () {
    const textSearch = (document.getElementById('searchInput')?.value || '').toLowerCase();
    const table = document.getElementById('data_table');
    const tbody = table?.querySelector('tbody');
    if (!table || !tbody) return;
    removeNotFoundMessage();
    let visible = 0;
    tbody.querySelectorAll('tr').forEach((row) => {
      const firstCell = row.querySelector('td');
      const cellText = firstCell?.textContent.toLowerCase() || '';
      const show = !textSearch || cellText.includes(textSearch);
      row.style.visibility = show ? 'visible' : 'hidden';
      row.className = show ? 'active' : 'hidden';
      if (show) visible++;
    });
    if (!visible) showNotFoundMessage(table);
    window.currentPage = 1;
    if (typeof window.showPage === 'function') window.showPage(window.currentPage);
  };

  window.openMonthFilterModal = function () {
    const modal = document.getElementById('myModal');
    if (!modal) return;
    modal.innerHTML = `
      <div class="modal-content">
        <div class="modal-header">
          <h2>Фильтровать по месяцу</h2>
          <span class="close" onclick="closeLegacyAdminModal()">&times;</span>
        </div>
        <div class="modal-body">
          <div class="form-row"><div class="form-group">
            <label for="monthFilter">Месяц:</label>
            <select id="monthFilter">
              <option value="">Выберите месяц</option>
              ${['Январь','Февраль','Март','Апрель','Май','Июнь','Июль','Август','Сентябрь','Октябрь','Ноябрь','Декабрь'].map((m) => `<option value="${m}">${m}</option>`).join('')}
            </select>
          </div></div>
        </div>
        <div class="modal-footer">
          <button onclick="applyMonthFilter()" class="modal_btn">Применить</button>
        </div>
      </div>`;
    modal.style.display = 'block';
  };

  window.applyMonthFilter = function () {
    const monthFilter = (document.getElementById('monthFilter')?.value || '').toLowerCase();
    const table = document.getElementById('data_table');
    if (!table) return;
    removeNotFoundMessage();
    let visible = 0;
    Array.from(table.getElementsByTagName('tr')).slice(1).forEach((row) => {
      const td = row.getElementsByTagName('td')[1];
      const txtValue = (td?.textContent || td?.innerText || '').toLowerCase();
      const show = !monthFilter || txtValue === monthFilter;
      row.style.display = show ? '' : 'none';
      if (show) visible++;
    });
    if (!visible) showNotFoundMessage(table);
    closeLegacyModal();
  };

  window.openOMSUFilterModal = function () {
    const modal = document.getElementById('myModal');
    if (!modal) return;
    modal.innerHTML = `
      <div class="modal-content">
        <div class="modal-header">
          <h2>Фильтровать по ОМСУ</h2>
          <span class="close" onclick="closeLegacyAdminModal()">&times;</span>
        </div>
        <div class="modal-body">
          <div class="form-row"><div class="form-group">
            <label for="organizationSelect">ОМСУ:</label>
            <select id="organizationSelect"></select>
          </div></div>
        </div>
        <div class="modal-footer">
          <button onclick="applyOMSUFilter()" class="modal_btn">Применить</button>
        </div>
      </div>`;
    modal.style.display = 'block';
    if (typeof window.populateNewOrganizations === 'function') window.populateNewOrganizations('filter');
  };

  window.applyOMSUFilter = function () {
    const organizationSelect = document.getElementById('organizationSelect');
    const selectedOption = organizationSelect?.options?.[organizationSelect.selectedIndex];
    const omsuFilter = (selectedOption?.innerHTML || '').toLowerCase();
    const table = document.getElementById('data_table');
    if (!table) return;
    removeNotFoundMessage();
    let visible = 0;
    Array.from(table.getElementsByTagName('tr')).slice(1).forEach((row) => {
      const cells = Array.from(row.getElementsByTagName('td'));
      const rowVisible = !omsuFilter || cells.some((td) => (td.textContent || '').toLowerCase().includes(omsuFilter));
      row.style.display = rowVisible ? '' : 'none';
      if (rowVisible) visible++;
    });
    if (!visible) showNotFoundMessage(table);
    closeLegacyModal();
  };

  window.sortTable = function (columnIndex) {
    const table = document.getElementById('data_table');
    if (!table) return;
    const monthOrder = ['январь','февраль','март','апрель','май','июнь','июль','август','сентябрь','октябрь','ноябрь','декабрь'];
    const direction = table.getAttribute('data-sort-direction') === 'desc' ? 'asc' : 'desc';
    table.setAttribute('data-sort-direction', direction);
    const currentSortIcon = document.getElementById(`sortIcon${columnIndex}`);
    if (currentSortIcon) currentSortIcon.textContent = direction === 'asc' ? '🔽' : '🔼';

    const rowsArray = Array.from(table.rows).slice(1);
    rowsArray.sort((a, b) => {
      const cellA = a.getElementsByTagName('td')[columnIndex]?.innerHTML.toLowerCase() || '';
      const cellB = b.getElementsByTagName('td')[columnIndex]?.innerHTML.toLowerCase() || '';
      if (columnIndex === 1) {
        const monthIndexA = monthOrder.indexOf(cellA);
        const monthIndexB = monthOrder.indexOf(cellB);
        return direction === 'asc' ? monthIndexA - monthIndexB : monthIndexB - monthIndexA;
      }
      return direction === 'asc' ? cellA.localeCompare(cellB) : cellB.localeCompare(cellA);
    });
    const tbody = table.querySelector('tbody');
    if (!tbody) return;
    tbody.innerHTML = '';
    rowsArray.forEach((row) => tbody.appendChild(row));
  };

  window.currentPage = window.currentPage || 1;
  window.itemsPerPage = window.itemsPerPage || 20;
  window.filteredData = window.filteredData || [];
  window.totalPages = window.totalPages || 0;

  window.prevPage = function () {
    if (window.currentPage > 1) {
      window.currentPage--;
      window.showPage(window.currentPage);
    }
  };

  window.nextPage = function () {
    if (window.currentPage < window.totalPages) {
      window.currentPage++;
      window.showPage(window.currentPage);
    }
  };

  window.showPage = function (page) {
    const tableBody = document.getElementById('data_table');
    if (!tableBody) return;
    const rows = window.filteredData.length > 0 ? window.filteredData : Array.from(tableBody.querySelectorAll('tr'));
    const totalRows = rows.length;
    window.totalPages = Math.ceil(totalRows / window.itemsPerPage);
    if (page > window.totalPages || page <= 0) return;
    const startIndex = (page - 1) * window.itemsPerPage;
    const endIndex = startIndex + window.itemsPerPage;
    for (let i = 0; i < totalRows; i++) {
      rows[i].style.display = i >= startIndex && i < endIndex ? '' : 'none';
    }
    const pageInfo = document.getElementById('pageInfo');
    if (pageInfo) pageInfo.textContent = `Страница ${page} из ${window.totalPages}`;
    const prevBtn = document.getElementById('prevPageBtn');
    const nextBtn = document.getElementById('nextPageBtn');
    if (prevBtn) prevBtn.style.display = window.currentPage === 1 ? 'none' : 'inline-block';
    if (nextBtn) nextBtn.style.display = window.currentPage === window.totalPages ? 'none' : 'inline-block';
  };

  window.copy_archive_survey = function (name_survey, description, date_begin, date_end, file_questions) {
    const data = { name_survey, description, date_begin, date_end, questions: file_questions };
    fetch('/copy_archive_survey', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data)
    })
      .then((response) => {
        if (!response.ok) throw new Error('Ошибка при добавлении анкеты');
        return response.json();
      })
      .then(() => {
        alert('Анкета успешно добавлена!');
        window.location.reload();
      })
      .catch((err) => alert(err.message));
  };

  window.populateYears = function (kvartal, select) {
    if (!select || select.options.length > 1) return;
    const currentYear = new Date().getFullYear();
    for (let i = 1; i <= 4; i++) {
      const year = currentYear - i;
      const option = document.createElement('option');
      option.value = year;
      option.textContent = year;
      select.appendChild(option);
    }
  };

  window.onYearChange = function (kvartal, select) {
    const year = select?.value;
    if (year) {
      window.create_otchetAll_kvartal(kvartal, year);
      select.selectedIndex = 0;
    }
  };
})();
