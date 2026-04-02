(function(){
  function byId(id){ return document.getElementById(id); }

  function clearAddForm(){
    ['fullName','username','password','email_input'].forEach(function(id){
      var el = byId(id);
      if (el) el.value = '';
    });
    var roleEl = byId('role_bd');
    if (roleEl) roleEl.value = 'user';
    var orgEl = byId('organization');
    if (orgEl) orgEl.selectedIndex = 0;
    if (window.ModalHelpers) ModalHelpers.clearMessage('message');
  }

  function fillEditForm(id, fullName, username, email, orgId, role, dateBegin, dateEnd){
    if (!window.DomUtils) return;
    DomUtils.setValue('editUserId', id || '');
    DomUtils.setValue('editFullName', fullName || '');
    DomUtils.setValue('editUsername', username || '');
    DomUtils.setValue('editEmail', email || '');
    DomUtils.setValue('editRole', role || 'user');
    DomUtils.setValue('editDateBegin', (dateBegin || '').split('T')[0]);
    DomUtils.setValue('editDateEnd', (dateEnd || '').split('T')[0]);
    var orgEl = DomUtils.setValue('editOrganization', orgId || '');
    if (orgEl && orgId != null && orgId !== '') orgEl.value = String(orgId);
  }

  async function submitFormAddBridge(){
    var messageElement = byId('message');
    if (messageElement) { messageElement.textContent = ''; messageElement.className = ''; }

    var username = byId('username')?.value || '';
    var password = byId('password')?.value || '';
    var organizationId = byId('organization')?.value || '';
    var role = byId('role_bd')?.value || '';

    if (!username) { alert('Введите никнейм пользователя!'); return; }
    if (!password) { alert('Введите пароль!'); return; }
    if (password.length < 12) { alert('Пароль должен содержать не меньше 12 символов!'); return; }
    if (!organizationId) { alert('Выберите организацию пользователя!'); return; }
    if (!role) { alert('Выберите роль пользователя!'); return; }

    var payload = {
      username: username,
      password: password,
      fullName: byId('fullName')?.value || '',
      email: byId('email_input')?.value || '',
      organizationId: organizationId,
      role: role
    };

    try {
      var data = await Http.requestJson('/add_user_bd', {
        method: 'POST',
        headers: Http.jsonHeaders(),
        body: JSON.stringify(payload)
      });

      if (window.ModalHelpers) ModalHelpers.setMessage('message', data.message || '', !!data.success);
      if (data.success) {
        ModalHelpers && ModalHelpers.close('addUserModal');
        if (typeof handleTabClick === 'function') handleTabClick('get_users');
        else location.reload();
      }
    } catch (error) {
      if (window.ModalHelpers) ModalHelpers.setMessage('message', error.message || 'Ошибка соединения', false);
    }
  }

  async function loadOrganizations(selectedOrgId){
    var orgSelect = byId('editOrganization');
    if (!orgSelect) return;

    try {
      orgSelect.innerHTML = '<option value="">Загрузка организаций...</option>';
      var response = await fetch('/get_omsu/data');
      if (!response.ok) throw new Error('Не удалось загрузить организации');
      var organizations = await response.json();
      orgSelect.innerHTML = '';
      organizations.forEach(function(org){
        var option = document.createElement('option');
        option.value = org.id;
        option.textContent = org.name;
        if (selectedOrgId != null && String(org.id) === String(selectedOrgId)) option.selected = true;
        orgSelect.appendChild(option);
      });
    } catch (error) {
      console.error('Ошибка загрузки организаций:', error);
      orgSelect.innerHTML = '<option value="">Ошибка загрузки</option>';
    }
  }

  async function openEditUserModalBridge(id, fullName, username, email, orgId, role, dateBegin, dateEnd){
    fillEditForm(id, fullName, username, email, orgId, role, dateBegin, dateEnd);
    await loadOrganizations(orgId);
    var emailEl = byId('editEmail');
    if (emailEl) emailEl.value = email || '';
    var passwordEl = byId('editPassword');
    if (passwordEl) passwordEl.value = '';
    ModalHelpers && ModalHelpers.open('editUserModal');
  }

  async function updateUserBridge(){
    if (!byId('editUsername')?.value) { alert('Введите никнейм пользователя!'); return; }
    if (!byId('editOrganization')?.value) { alert('Выберите организацию пользователя!'); return; }
    if (!byId('editRole')?.value) { alert('Выберите роль пользователя!'); return; }
    if (!byId('editDateBegin')?.value) { alert('Введите дату начала!'); return; }

    var startValue = byId('editDateBegin')?.value || '';
    var endValue = byId('editDateEnd')?.value || '';
    if (startValue && endValue) {
      var startDate = new Date(startValue);
      var endDate = new Date(endValue);
      if (startDate >= endDate) { alert('Дата начала должна быть раньше даты окончания!'); return; }
    }

    var payload = {
      username: byId('editUsername')?.value || '',
      password: byId('editPassword')?.value || 'keep_original',
      fullName: byId('editFullName')?.value || '',
      email: byId('editEmail')?.value || '',
      organizationId: byId('editOrganization')?.value || '',
      role: byId('editRole')?.value || '',
      dateBegin: startValue,
      dateEnd: endValue
    };

    try {
      var id = byId('editUserId')?.value || '';
      await Http.requestJson('/update_user_bd/' + id, {
        method: 'POST',
        headers: Http.jsonHeaders(),
        body: JSON.stringify(payload)
      });
      alert('Пользователь успешно обновлён!');
      ModalHelpers && ModalHelpers.close('editUserModal');
      if (typeof handleTabClick === 'function') handleTabClick('get_users');
      else location.reload();
    } catch (error) {
      alert('Ошибка: ' + (error.message || 'Не удалось обновить пользователя'));
    }
  }

  function deleteUserBridge(id, fullName){
    if (!id) {
      var storedId = byId('deleteUserId')?.value || '';
      if (storedId) id = storedId;
    }

    if (arguments.length >= 2) {
      if (!confirm('Вы уверены, что хотите удалить пользователя?')) return;
      fetch('/delete_user/' + id, { method: 'POST', headers: { 'Content-Type': 'application/json' } })
        .then(function(response){ if (!response.ok) throw new Error('Ошибка при удалении'); return response.text(); })
        .then(function(result){ alert(result); if (typeof handleTabClick === 'function') handleTabClick('get_users'); else location.reload(); })
        .catch(function(error){ alert('Произошла ошибка: ' + error.message); });
      return;
    }

    fetch('/delete_user/' + id, { method: 'POST', headers: { 'Content-Type': 'application/json' } })
      .then(function(response){ if (!response.ok) throw new Error('Ошибка при удалении'); return response.text(); })
      .then(function(result){ alert(result); ModalHelpers && ModalHelpers.close('deleteUserModal'); if (typeof handleTabClick === 'function') handleTabClick('get_users'); else location.reload(); })
      .catch(function(error){ alert('Произошла ошибка: ' + error.message); });
  }

  function bindOverrides(){
    window.openAddUserModal = function(){
      if (!byId('addUserModal')) {
        alert('Форма добавления пользователя не загружена. Обновите вкладку "Список пользователей".');
        return;
      }
      clearAddForm();
      ModalHelpers && ModalHelpers.open('addUserModal');
      if (typeof initUserModalPasswordEyes === 'function') setTimeout(initUserModalPasswordEyes, 0);
    };

    window.submitFormAdd = submitFormAddBridge;
    window.openEditUserModal = openEditUserModalBridge;
    window.updateUser = updateUserBridge;
    window.deleteUser = deleteUserBridge;
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', bindOverrides);
  } else {
    bindOverrides();
  }
  window.addEventListener('load', bindOverrides);
  setTimeout(bindOverrides, 0);
  setTimeout(bindOverrides, 300);
})();
