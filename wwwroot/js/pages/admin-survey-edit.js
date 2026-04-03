(function () {
  'use strict';

  var surveyEditSelectedOmsu = [];
  var surveyEditModalOpen = false;

  window.surveyEditInit = function () {
    surveyEditSelectedOmsu = [];
    var selectedIdsInput = document.getElementById('selectedOmsuIds');
    var names = Array.isArray(window.selectedOmsuNames) ? window.selectedOmsuNames : [];

    if (selectedIdsInput && selectedIdsInput.value) {
      var ids = selectedIdsInput.value.split(',');
      for (var i = 0; i < ids.length; i++) {
        var parsedId = parseInt(ids[i], 10);
        if (!isNaN(parsedId) && names[i]) {
          surveyEditSelectedOmsu.push({ id: parsedId, name: names[i] });
        }
      }
    }

    var orgItems = document.querySelectorAll('.organization-item');
    orgItems.forEach(function (item) {
      if (item.dataset.selected === 'true') {
        item.classList.add('selected');
      }
    });

    window.surveyEditUpdateSelectedOmsuDisplay();
  };

  window.surveyEditOpenOmsuModal = function () {
    var modal = document.getElementById('omsuModal');
    if (modal) modal.style.display = 'block';
    surveyEditModalOpen = true;
  };

  window.surveyEditCloseModal = function (modalId) {
    var modal = document.getElementById(modalId);
    if (modal) modal.style.display = 'none';
    surveyEditModalOpen = false;
  };

  window.surveyEditToggleOmsuSelection = function (element) {
    var orgId = parseInt(element.dataset.id, 10);
    var orgName = element.dataset.name;

    if (element.dataset.selected === 'true') {
      element.dataset.selected = 'false';
      element.classList.remove('selected');
      surveyEditSelectedOmsu = surveyEditSelectedOmsu.filter(function (org) { return org.id !== orgId; });
    } else {
      element.dataset.selected = 'true';
      element.classList.add('selected');
      if (!surveyEditSelectedOmsu.some(function (org) { return org.id === orgId; })) {
        surveyEditSelectedOmsu.push({ id: orgId, name: orgName });
      }
    }
  };

  window.surveyEditSaveSelectedOmsu = function () {
    window.surveyEditCloseModal('omsuModal');
    window.surveyEditUpdateSelectedOmsuDisplay();
  };

  window.surveyEditUpdateSelectedOmsuDisplay = function () {
    var container = document.getElementById('selectedOmsuContainer');
    var list = document.getElementById('selectedOmsuList');
    var idsInput = document.getElementById('selectedOmsuIds');
    if (!container || !list) return;

    var selectedElements = document.querySelectorAll('.organization-item.selected');
    surveyEditSelectedOmsu = [];
    selectedElements.forEach(function (el) {
      var id = parseInt(el.dataset.id, 10);
      var name = el.dataset.name;
      if (!isNaN(id) && name) {
        surveyEditSelectedOmsu.push({ id: id, name: name });
      }
    });

    if (surveyEditSelectedOmsu.length === 0) {
      container.style.display = 'none';
      if (idsInput) idsInput.value = '';
      list.innerHTML = '';
      return;
    }

    container.style.display = 'block';
    list.innerHTML = '';
    surveyEditSelectedOmsu.forEach(function (org) {
      var item = document.createElement('span');
      item.className = 'selected-omsu-item';
      var escapedName = String(org.name).replace(/'/g, "\\'");
      item.innerHTML = org.name + ' <button type="button" onclick="surveyEditRemoveOmsu(this, \'" + escapedName + "\')">×</button>';
      list.appendChild(item);
    });

    if (idsInput) {
      idsInput.value = surveyEditSelectedOmsu.map(function (org) { return org.id; }).join(',');
    }
  };

  window.surveyEditRemoveOmsu = function (_button, name) {
    surveyEditSelectedOmsu = surveyEditSelectedOmsu.filter(function (org) { return org.name !== name; });
    var orgItems = document.querySelectorAll('.organization-item');
    orgItems.forEach(function (item) {
      if (item.dataset.name === name) {
        item.dataset.selected = 'false';
        item.classList.remove('selected');
      }
    });
    window.surveyEditUpdateSelectedOmsuDisplay();
  };

  window.surveyEditAddCriteria = function () {
    var container = document.getElementById('cont_criteries');
    if (!container) return;
    var div = document.createElement('div');
    div.className = 'form-group cont_criteries';
    div.innerHTML = '<label>Критерий оценки</label><input type="text" class="form-control criteriy" required /><div class="error-message">Это поле обязательно для заполнения.</div>';
    container.appendChild(div);
  };

  window.surveyEditConfirmCriteria = function () {
    var criteriaInputs = document.querySelectorAll('.criteriy');
    var hasValidCriteria = false;
    for (var i = 0; i < criteriaInputs.length; i++) {
      if (criteriaInputs[i].value.trim() !== '') {
        hasValidCriteria = true;
        break;
      }
    }
    if (!hasValidCriteria) {
      alert('Пожалуйста, добавьте и заполните хотя бы один критерий оценки');
      return;
    }

    var allCriteriaValid = true;
    for (var j = 0; j < criteriaInputs.length; j++) {
      if (criteriaInputs[j].value.trim() === '') {
        criteriaInputs[j].classList.add('invalid');
        allCriteriaValid = false;
      } else {
        criteriaInputs[j].classList.remove('invalid');
      }
    }
    if (!allCriteriaValid) {
      alert('Пожалуйста, заполните все добавленные критерии оценки');
      return;
    }

    var container = document.getElementById('two_step');
    if (container) {
      container.querySelectorAll('.criteriy').forEach(function (input) { input.readOnly = true; });
      container.classList.add('confirmed');
    }
    var addBtn = document.getElementById('add_survey_btn');
    var sendEmailBtn = document.getElementById('send_email');
    var addCritBtn = document.getElementById('add_crit');
    var confBtn = document.getElementById('conf_btn');
    if (addBtn) addBtn.style.display = 'inline-block';
    if (sendEmailBtn) sendEmailBtn.style.display = 'inline-block';
    if (addCritBtn) addCritBtn.style.display = 'none';
    if (confBtn) confBtn.style.display = 'none';
    alert('Критерии подтверждены. Теперь вы можете обновить анкету.');
  };

  window.surveyEditValidateForm = function () {
    var isValid = true;
    [
      { element: document.getElementById('surveyTitle'), errorId: 'titleError' },
      { element: document.getElementById('startDate'), errorId: 'startDateError' },
      { element: document.getElementById('endDate'), errorId: 'endDateError' }
    ].forEach(function (field) {
      if (!field.element) return;
      var errorElement = document.getElementById(field.errorId);
      if (!field.element.value.trim()) {
        field.element.classList.add('invalid');
        if (errorElement) errorElement.style.display = 'block';
        isValid = false;
      } else {
        field.element.classList.remove('invalid');
        if (errorElement) errorElement.style.display = 'none';
      }
    });

    var startDate = document.getElementById('startDate');
    var endDate = document.getElementById('endDate');
    var endDateError = document.getElementById('endDateError');
    if (startDate && endDate && startDate.value && endDate.value && new Date(endDate.value) <= new Date(startDate.value)) {
      endDate.classList.add('invalid');
      if (endDateError) {
        endDateError.textContent = 'Дата окончания должна быть позже даты начала';
        endDateError.style.display = 'block';
      }
      isValid = false;
    }

    var omsuError = document.getElementById('omsuError');
    if (surveyEditSelectedOmsu.length === 0) {
      if (omsuError) omsuError.style.display = 'block';
      isValid = false;
    } else if (omsuError) {
      omsuError.style.display = 'none';
    }
    return isValid;
  };

  window.surveyEditUpdate = async function () {
    var surveyTitle = document.getElementById('surveyTitle');
    var surveyDescription = document.getElementById('surveyDescription');
    var startDate = document.getElementById('startDate');
    var endDate = document.getElementById('endDate');
    var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    var surveyIdInput = document.getElementById('surveyId');
    var token = tokenInput ? tokenInput.value : null;
    var surveyId = surveyIdInput ? surveyIdInput.value : null;

    try {
      if (!surveyTitle || !surveyTitle.value.trim() || !startDate || !startDate.value || !endDate || !endDate.value) {
        alert('Пожалуйста, заполните все обязательные поля');
        return;
      }
      if (new Date(endDate.value) <= new Date(startDate.value)) {
        alert('Дата окончания должна быть позже даты начала');
        return;
      }
      if (!token || !surveyId) {
        alert('Ошибка безопасности. Пожалуйста, обновите страницу.');
        return;
      }
      if (surveyEditSelectedOmsu.length === 0) {
        alert('Пожалуйста, выберите хотя бы одну организацию!');
        return;
      }

      var formData = {
        Title: surveyTitle.value.trim(),
        Description: surveyDescription ? surveyDescription.value.trim() : '',
        StartDate: new Date(startDate.value).toISOString(),
        EndDate: new Date(endDate.value).toISOString(),
        Organizations: surveyEditSelectedOmsu.map(function (org) { return org.id; }),
        Criteria: Array.from(document.querySelectorAll('.criteriy')).map(function (input) { return input.value.trim(); }).filter(function (text) { return text !== ''; })
      };

      var response = await fetch('/update_survey_bd/' + surveyId, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'RequestVerificationToken': token,
          'Accept': 'application/json'
        },
        body: JSON.stringify(formData)
      });

      if (!response.ok) {
        var errorMessage = 'Ошибка сервера';
        try {
          var errorData = await response.json();
          errorMessage = errorData.message || errorData.error || errorMessage;
        } catch (e) {
          console.error('Ошибка при чтении ответа:', e);
        }
        throw new Error(errorMessage);
      }

      var result = await response.json();
      if (result.success) {
        alert(result.message || 'Анкета успешно обновлена!');
        window.location.reload();
      } else {
        throw new Error(result.message || 'Неизвестная ошибка');
      }
    } catch (error) {
      console.error('Ошибка при обновлении анкеты:', error);
      var userMessage = error.message;
      if (error.message.includes('jsonb') && error.message.includes('text')) {
        userMessage = 'Ошибка формата данных. Пожалуйста, обновите страницу и попробуйте снова.';
      } else if (error.message.includes('date')) {
        userMessage = 'Ошибка в датах. Проверьте правильность введенных дат.';
      } else if (error.message.includes('validation')) {
        userMessage = 'Ошибка валидации данных: ' + error.message;
      }
      alert('Ошибка: ' + userMessage);
      if (confirm('Показать подробности ошибки? (для разработчиков)')) {
        alert('Техническая информация:\n' + (error.stack || error.message));
      }
    }
  };
})();
