(function () {
  function loadFileAdmin() {
    loadAndUploadFile('administrator');
  }

  function loadFileUser() {
    loadAndUploadFile('user');
  }

  function handleSelectChange(select) {
    const value = select && select.value;
    if (value === 'administrator') {
      loadFileAdmin();
    } else if (value === 'user') {
      loadFileUser();
    }
    if (select) {
      select.value = '';
    }
  }

  function loadAndUploadFile(role) {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = 'docx';
    input.onchange = async (e) => {
      const file = e.target.files && e.target.files[0];
      if (!file) return;

      const formData = new FormData();
      formData.append('file', file);
      formData.append('role', role);

      try {
        const response = await fetch('/upload-instruction', {
          method: 'POST',
          body: formData
        });

        if (response.ok) {
          alert('Файл успешно загружен');
        } else {
          alert('Ошибка загрузки файла');
        }
      } catch (error) {
        alert('Ошибка: ' + error.message);
      }
    };
    input.click();
  }

  function restoreSurveys() {
    handleTabClick('get_surveys');
  }

  function archive_list_omsus() {
    handleTabClick('archive_list_omsus');
  }

  function archive_list_users() {
    handleTabClick('archive_list_users');
  }

  function copy_archive_survey(name_survey, description, date_begin, date_end, file_questions) {
    const data = {
      name_survey,
      description,
      date_open: date_begin,
      date_close: date_end,
      questions: file_questions,
    };

    fetch('/copy_archive_survey', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(data)
    })
      .then(response => {
        if (!response.ok) throw new Error('Ошибка при добавлении анкеты');
        return response.json();
      })
      .then(() => {
        alert('Анкета успешно добавлена!');
        window.location.reload();
      })
      .catch(err => {
        alert(err.message);
      });
  }

  function populateYears(kvartal, select) {
    if (!select || select.options.length > 1) return;

    const currentYear = new Date().getFullYear();
    for (let i = 1; i <= 4; i++) {
      const year = currentYear - i;
      const option = document.createElement('option');
      option.value = year;
      option.textContent = year;
      select.appendChild(option);
    }
  }

  function onYearChange(kvartal, select) {
    const year = select && select.value;
    if (year) {
      create_otchetAll_kvartal(kvartal, year);
      select.selectedIndex = 0;
    }
  }

  window.loadFileAdmin = loadFileAdmin;
  window.loadFileUser = loadFileUser;
  window.handleSelectChange = handleSelectChange;
  window.loadAndUploadFile = loadAndUploadFile;
  window.restoreSurveys = restoreSurveys;
  window.archive_list_omsus = archive_list_omsus;
  window.archive_list_users = archive_list_users;
  window.copy_archive_survey = copy_archive_survey;
  window.populateYears = populateYears;
  window.onYearChange = onYearChange;
})();
