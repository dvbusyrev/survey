(function () {
  function token() {
    return document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
  }

  function showMessage(el, text, ok) {
    if (!el) return;
    el.textContent = text;
    el.className = ok ? 'alert alert-success' : 'alert alert-danger';
    el.style.display = 'block';
  }

  function parseJsonSafe(response) {
    return response.text().then(t => {
      try { return t ? JSON.parse(t) : {}; }
      catch { return { raw: t }; }
    });
  }

  function validateDates(beginId, endId) {
    const begin = document.getElementById(beginId)?.value;
    const end = document.getElementById(endId)?.value;
    if (!begin) {
      alert('Выберите дату начала!');
      return false;
    }
    if (begin && end && new Date(begin) >= new Date(end)) {
      alert('Дата начала должна быть раньше даты окончания!');
      return false;
    }
    return true;
  }

  async function addOmsu() {
    const form = document.getElementById('organizationForm');
    const message = document.getElementById('message');
    const name = document.getElementById('Name')?.value?.trim();
    const email = document.getElementById('omsu_email')?.value?.trim() || '';
    const dateBegin = document.getElementById('DateBegin')?.value || '';
    const dateEnd = document.getElementById('DateEnd')?.value || '';

    if (!name) {
      alert('Введите название организации!');
      return;
    }
    if (!validateDates('DateBegin', 'DateEnd')) return;

    try {
      const response = await fetch('/add_omsu_bd', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(token() ? { 'RequestVerificationToken': token() } : {})
        },
        body: JSON.stringify({ Name: name, Email: email, DateBegin: dateBegin, DateEnd: dateEnd })
      });

      const result = await parseJsonSafe(response);
      if (!response.ok || result.success === false) {
        throw new Error(result.error || result.message || 'Не удалось добавить организацию');
      }

      if (form) form.reset();
      showMessage(message, 'Организация успешно создана!', true);
      alert('Организация успешно создана!');
      if (typeof handleTabClick === 'function') handleTabClick('get_omsu');
    } catch (error) {
      console.error('add_omsu_bd error:', error);
      showMessage(message, 'Ошибка: ' + error.message, false);
    }
  }

  function openEditOmsuModal(id, name, email, dateBegin, dateEnd) {
    const modal = document.getElementById('editOmsuModal');
    const map = {
      editOmsuId: id,
      omsuName: name || '',
      omsuEmail: email || '',
      omsuDateBegin: dateBegin || '',
      omsuDateEnd: dateEnd || ''
    };
    Object.entries(map).forEach(([key, value]) => {
      const el = document.getElementById(key);
      if (el) el.value = value;
    });
    if (modal) modal.style.display = 'block';
  }

  async function updateOmsu() {
    const id = document.getElementById('editOmsuId')?.value;
    const name = document.getElementById('omsuName')?.value?.trim();
    const email = document.getElementById('omsuEmail')?.value?.trim() || '';
    const dateBegin = document.getElementById('omsuDateBegin')?.value || '';
    const dateEnd = document.getElementById('omsuDateEnd')?.value || '';
    const saveBtn = document.getElementById('saveOmsuBtn');

    if (!name) {
      alert('Введите название организации!');
      return;
    }
    if (!validateDates('omsuDateBegin', 'omsuDateEnd')) return;

    try {
      if (saveBtn) {
        saveBtn.disabled = true;
        saveBtn.dataset.originalText = saveBtn.textContent;
        saveBtn.textContent = 'Сохранение...';
      }

      const response = await fetch(`/update_omsu_bd/${id}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(token() ? { 'RequestVerificationToken': token() } : {})
        },
        body: JSON.stringify([name, email, dateBegin, dateEnd])
      });

      const result = await parseJsonSafe(response);
      if (!response.ok || result.success === false) {
        throw new Error(result.error || result.message || 'Не удалось обновить организацию');
      }

      alert('Организация успешно отредактирована!');
      const modal = document.getElementById('editOmsuModal');
      if (modal) modal.style.display = 'none';
      if (typeof handleTabClick === 'function') handleTabClick('get_omsu');
    } catch (error) {
      console.error('updateOmsu error:', error);
      alert('Ошибка: ' + error.message);
    } finally {
      if (saveBtn) {
        saveBtn.disabled = false;
        saveBtn.textContent = saveBtn.dataset.originalText || 'Сохранить';
      }
    }
  }

  async function deleteOmsu(id) {
    if (!confirm('Вы уверены, что хотите удалить эту организацию?')) return;

    try {
      const response = await fetch(`/delete_omsu/${id}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/x-www-form-urlencoded',
          ...(token() ? { 'RequestVerificationToken': token() } : {})
        }
      });

      if (!response.ok) {
        const result = await parseJsonSafe(response);
        throw new Error(result.error || result.message || `Ошибка удаления: ${response.status}`);
      }

      alert('Организация успешно удалена!');
      if (typeof handleTabClick === 'function') handleTabClick('get_omsu');
    } catch (error) {
      console.error('delete_omsu error:', error);
      alert('Ошибка: ' + error.message);
    }
  }

  function archiveListOmsus() {
    if (typeof handleTabClick === 'function') handleTabClick('archive_list_omsus');
  }

  function install() {
    window.add_omsu_bd = addOmsu;
    window.openEditOmsuModal = openEditOmsuModal;
    window.updateOmsu = updateOmsu;
    window.update_omsu_bd = updateOmsu;
    window.delete_omsu = deleteOmsu;
    window.archive_list_omsus = archiveListOmsus;
  }

  window.addEventListener('load', install);
  install();
})();
