
(function () {
  function token() {
    return document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
  }

  function getSafeElement(id) {
    const element = document.getElementById(id);
    if (!element) {
      console.error(`Element with ID ${id} not found`);
      throw new Error(`Элемент ${id} не найден`);
    }
    return element;
  }

  function delete_omsu(id) {
    if (confirm("Вы уверены, что хотите удалить эту организацию?")) {
      const xhr = new XMLHttpRequest();
      xhr.onreadystatechange = function() {
        if (xhr.readyState === 4) {
          if (xhr.status === 200) {
            alert("Организация успешно удалена!");
            handleTabClick("get_omsu");
          } else {
            console.error("Ошибка при удалении организации: " + xhr.status);
          }
        }
      };

      xhr.onerror = function() {
        console.error("Проблемы с интернетом");
      };

      xhr.open("POST", `/delete_omsu/${id}`, true);
      xhr.setRequestHeader("Content-Type", "application/x-www-form-urlencoded");
      xhr.send();
    }
  }

  function get_omsu_name() {
    var xhr = new XMLHttpRequest();
    xhr.onreadystatechange = function() {
      if (xhr.readyState === 4 && xhr.status === 200) {
        var data = JSON.parse(xhr.responseText);
        var select = document.getElementById('organization');
        data.forEach(function(org) {
          var option = document.createElement('option');
          option.value = org;
          option.text = org;
          select.appendChild(option);
        });
      } else if (xhr.readyState === 4) {
        console.error("Ошибка при загрузке названий организаций: " + xhr.status);
      }
    };
    xhr.onerror = function() {
      console.error("Ошибка при загрузке названий организаций");
    };
    xhr.open("GET", "/get_omsu/data", true);
    xhr.send();
  }

  function submitFormAdd() {
    const messageElement = document.getElementById('message');
    messageElement.textContent = '';
    messageElement.className = '';

    if (!document.getElementById('username').value){ alert("Введите никнейм пользователя!"); return; }
    if (!document.getElementById('password').value){ alert("Введите пароль!"); return; }
    const password = document.getElementById('password').value;
    if (password.length < 12) { alert("Пароль должен содержать не меньше 12 символов!"); return; }
    if (!document.getElementById('organization').value){ alert("Выберите организацию пользователя!"); return; }
    if (!document.getElementById('role_bd').value){ alert("Выберите роль пользователя!"); return; }

    const formData = {
      username: document.getElementById('username')?.value || '',
      password: document.getElementById('password')?.value || '',
      fullName: document.getElementById('fullName')?.value || '',
      email: document.getElementById('email_input')?.value || '',
      organizationId: document.getElementById('organization')?.value || '0',
      role: document.getElementById('role_bd')?.value || 'user'
    };

    fetch('/add_user_bd', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'RequestVerificationToken': token()
      },
      body: JSON.stringify(formData)
    })
    .then(response => response.json())
    .then(data => {
      messageElement.textContent = data.message;
      messageElement.className = data.success ? 'success-message' : 'error-message';
      if (data.success) { alert("Пользователь успешно добавлен!"); handleTabClick("get_users"); }
    })
    .catch(error => {
      console.error("Ошибка:", error);
      messageElement.textContent = 'Ошибка соединения';
      messageElement.className = 'error-message';
    });
  }

  function deleteUser(id, fullName) {
    if (!confirm(`Вы уверены, что хотите удалить пользователя?`)) return;
    fetch(`/delete_user/${id}`, { method: 'POST', headers: { 'Content-Type': 'application/json' } })
      .then(response => {
        if (!response.ok) throw new Error('Ошибка при удалении');
        return response.text();
      })
      .then(result => {
        alert(result);
        if (typeof closeModal === 'function') closeModal('deleteUserModal');
        handleTabClick("get_users");
      })
      .catch(error => {
        console.error("Ошибка:", error);
        alert("Произошла ошибка: " + error.message);
      });
  }

  window.openAddUserModal = function () {
    const modal = document.getElementById('addUserModal');
    if (!modal) {
      console.error('addUserModal not found in DOM');
      alert('Форма добавления пользователя не загружена. Обновите вкладку "Список пользователей".');
      return;
    }

    const messageElement = document.getElementById('message');
    if (messageElement) {
      messageElement.textContent = '';
      messageElement.className = '';
    }

    ['fullName', 'username', 'password', 'email_input'].forEach(id => {
      const el = document.getElementById(id);
      if (el) el.value = '';
    });

    const roleEl = document.getElementById('role_bd');
    if (roleEl) roleEl.value = 'user';

    const orgEl = document.getElementById('organization');
    if (orgEl) orgEl.selectedIndex = 0;

    modal.style.display = 'block';

    if (typeof window.initUserModalPasswordEyes === 'function') {
      setTimeout(window.initUserModalPasswordEyes, 0);
    }
  };

  async function loadOrganizations2(selectedOrgId = null) {
    const orgSelect = getSafeElement('editOrganization');
    try {
      orgSelect.innerHTML = '<option value="">Загрузка организаций...</option>';
      const response = await fetch('/get_omsu/data');
      if (!response.ok) throw new Error('Не удалось загрузить организации');
      const organizations = await response.json();
      orgSelect.innerHTML = '';
      organizations.forEach(org => {
        const option = document.createElement('option');
        option.value = org.id;
        option.textContent = org.name;
        if (selectedOrgId && org.id == selectedOrgId) option.selected = true;
        orgSelect.appendChild(option);
      });
    } catch (error) {
      console.error('Ошибка загрузки организаций:', error);
      orgSelect.innerHTML = '<option value="">Ошибка загрузки</option>';
    }
  }

  async function openEditUserModal(id, fullName, username, email, orgId, role, dateBegin, dateEnd) {
    try {
      const userId = getSafeElement('editUserId');
      const fullNameEl = getSafeElement('editFullName');
      const usernameEl = getSafeElement('editUsername');
      const roleEl = getSafeElement('editRole');
      const dateBeginEl = getSafeElement('editDateBegin');
      const dateEndEl = getSafeElement('editDateEnd');
      const passwordEl = getSafeElement('editPassword');
      const modal = getSafeElement('editUserModal');
      userId.value = id;
      fullNameEl.value = fullName || '';
      usernameEl.value = username || '';
      roleEl.value = role || 'user';
      dateBeginEl.value = dateBegin?.split('T')[0] || '';
      dateEndEl.value = dateEnd?.split('T')[0] || '';
      passwordEl.value = '';
      await loadOrganizations2(orgId);
      modal.style.display = 'block';
      if (typeof window.initUserModalPasswordEyes === 'function') {
        setTimeout(window.initUserModalPasswordEyes, 0);
      }
    } catch (error) {
      console.error('Ошибка при открытии формы:', error);
      alert('Ошибка: ' + error.message);
    }
  }

  async function updateUser() {
    if (!document.getElementById('editUsername').value) { alert("Введите никнейм пользователя!"); return; }
    if (!document.getElementById('editOrganization').value) { alert("Выберите организацию пользователя!"); return; }
    if (!document.getElementById('editRole').value) { alert("Выберите роль пользователя!"); return; }
    if (!document.getElementById('editDateBegin').value) { alert("Введите дату начала!"); return; }

    const startDate = new Date(document.getElementById('editDateBegin').value);
    const endDate = new Date(document.getElementById('editDateEnd').value);
    if (startDate >= endDate) { alert("Дата начала должна быть раньше даты окончания!"); return; }

    try {
      const modal = getSafeElement('editUserModal');
      const messageContainer = document.createElement('div');
      messageContainer.className = 'message';
      modal.querySelector('.modal-body').appendChild(messageContainer);
      const elements = {
        id: getSafeElement('editUserId'),
        fullName: getSafeElement('editFullName'),
        username: getSafeElement('editUsername'),
        password: getSafeElement('editPassword'),
        organization: getSafeElement('editOrganization'),
        role: getSafeElement('editRole'),
        dateBegin: getSafeElement('editDateBegin'),
        dateEnd: getSafeElement('editDateEnd')
      };
      if (!elements.username.value || !elements.organization.value) throw new Error('Заполните все обязательные поля');
      if (elements.dateBegin.value && elements.dateEnd.value) {
        const beginDate = new Date(elements.dateBegin.value);
        const endDate = new Date(elements.dateEnd.value);
        if (endDate < beginDate) throw new Error('Дата окончания не может быть раньше даты начала');
      }
      const formData = {
        username: elements.username.value,
        password: elements.password.value || 'keep_original',
        fullName: elements.fullName.value,
        organizationId: elements.organization.value,
        role: elements.role.value,
        dateBegin: elements.dateBegin.value,
        dateEnd: elements.dateEnd.value
      };
      const response = await fetch(`/update_user_bd/${elements.id.value}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token() },
        body: JSON.stringify(formData)
      });
      const result = await response.json();
      if (!response.ok) throw new Error(result.message || 'Ошибка сервера');
      alert("Пользователь успешно обновлён!");
      handleTabClick("get_users");
    } catch (error) {
      console.error('Ошибка обновления:', error);
      const messageContainer = document.querySelector('#editUserModal .message');
      if (messageContainer) {
        messageContainer.textContent = error.message;
        messageContainer.style.color = 'red';
      } else {
        alert('Ошибка: ' + error.message);
      }
    }
  }

  function closeModal2() {
    const modal = document.getElementById('editUserModal');
    if (modal) modal.style.display = 'none';
  }

  async function add_omsu_bd() {
    const form = document.getElementById('organizationForm');
    const messageDiv = document.getElementById('message');
    messageDiv.style.display = 'none';
    if (!document.getElementById('Name').value) { alert("Введите название организации!"); return; }
    if (!document.getElementById('DateBegin').value) { alert("Выберите дату начала!"); return; }
    const startDate = new Date(document.getElementById('DateBegin').value);
    const endDate = new Date(document.getElementById('DateEnd').value);
    if (startDate >= endDate) { alert("Дата начала должна быть раньше даты окончания!"); return; }
    try {
      const formData = { Name: form.Name.value, Email: form.omsu_email.value, DateBegin: form.DateBegin.value, DateEnd: form.DateEnd.value };
      const response = await fetch('/add_omsu_bd', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token() },
        body: JSON.stringify(formData)
      });
      const result = await response.json();
      messageDiv.textContent = result.success ? 'Организация успешно создана!' : 'Ошибка: ' + (result.error || 'Неизвестная ошибка');
      handleTabClick("get_omsu");
      alert("Организация успешно создана!")
      messageDiv.className = result.success ? 'alert alert-success' : 'alert alert-danger';
      messageDiv.style.display = 'block';
    } catch (error) {
      messageDiv.textContent = 'Ошибка при отправке: ' + error.message;
      messageDiv.className = 'alert alert-danger';
      messageDiv.style.display = 'block';
      console.error('Ошибка:', error);
    }
  }

  function openEditOmsuModal(id, name, email, dateBegin, dateEnd) {
    document.getElementById('editOmsuId').value = id;
    document.getElementById('omsuName').value = name || '';
    document.getElementById('omsuEmail').value = email || '';
    document.getElementById('omsuDateBegin').value = dateBegin || '';
    document.getElementById('omsuDateEnd').value = dateEnd || '';
    document.getElementById('editOmsuModal').style.display = 'block';
  }

  async function updateOmsu() {
    if (!document.getElementById('omsuName').value) { alert("Введите название организации!"); return; }
    if (!document.getElementById('omsuDateBegin').value) { alert("Выберите дату начала!"); return; }
    const startDate = new Date(document.getElementById('omsuDateBegin').value);
    const endDate = new Date(document.getElementById('omsuDateEnd').value);
    if (startDate >= endDate) { alert("Дата начала должна быть раньше даты окончания!"); return; }
    try {
      const id = document.getElementById('editOmsuId').value;
      const name = document.getElementById('omsuName').value.trim();
      const email = document.getElementById('omsuEmail').value.trim();
      const dateBegin = document.getElementById('omsuDateBegin').value;
      const dateEnd = document.getElementById('omsuDateEnd').value;
      if (!name) throw new Error("Название организации обязательно для заполнения");
      const response = await fetch(`/update_omsu_bd/${id}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token() },
        body: JSON.stringify({ Name: name, Email: email, DateBegin: dateBegin, DateEnd: dateEnd })
      });
      const result = await response.json();
      if (!response.ok || result.success === false) throw new Error(result.error || result.message || 'Ошибка обновления организации');
      alert('Организация успешно обновлена!');
      handleTabClick('get_omsu');
    } catch (error) {
      console.error('Ошибка обновления организации:', error);
      alert(error.message || 'Ошибка обновления организации');
    }
  }

  function archive_list_omsus(){handleTabClick("archive_list_omsus");}
  function archive_list_users(){handleTabClick("archive_list_users");}

  window.delete_omsu = delete_omsu;
  window.get_omsu_name = get_omsu_name;
  window.submitFormAdd = submitFormAdd;
  window.deleteUser = deleteUser;
  window.getSafeElement = getSafeElement;
  window.loadOrganizations2 = loadOrganizations2;
  window.openEditUserModal = openEditUserModal;
  window.updateUser = updateUser;
  window.closeModal2 = closeModal2;
  window.add_omsu_bd = add_omsu_bd;
  window.openEditOmsuModal = openEditOmsuModal;
  window.updateOmsu = updateOmsu;
  window.archive_list_omsus = archive_list_omsus;
  window.archive_list_users = archive_list_users;
})();
