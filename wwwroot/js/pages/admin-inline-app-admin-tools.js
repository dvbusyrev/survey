function downloadReport(url, defaultFileName) {
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
        .then(response => {
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            
            // Получаем имя файла из заголовка или используем default
            let fileName = defaultFileName;
            const contentDisposition = response.headers.get('Content-Disposition');
            
            if (contentDisposition) {
                const utf8FilenameMatch = contentDisposition.match(/filename\*=UTF-8''(.+)/);
                if (utf8FilenameMatch) {
                    fileName = decodeURIComponent(utf8FilenameMatch[1]);
                } else {
                    const regularMatch = contentDisposition.match(/filename="(.+)"/);
                    if (regularMatch) {
                        fileName = regularMatch[1];
                    }
                }
            }
            
            return response.blob().then(blob => ({ blob, fileName }));
        })
        .then(({ blob, fileName }) => {
            // Создаем ссылку для скачивания
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = sanitizeFileName(fileName);
            document.body.appendChild(a);
            a.click();
            
            setTimeout(() => {
                document.body.removeChild(a);
                window.URL.revokeObjectURL(url);
                document.body.removeChild(loader);
            }, 100);
        })
        .catch(error => {
            console.error('Ошибка при скачивании файла:', error);
            document.body.removeChild(loader);
            alert('Произошла ошибка при скачивании отчета. Пожалуйста, попробуйте позже.');
        });
}

function create_otchet_month(id) {
    downloadReport(`/create_otchet_month/${id}`, 'Отчет.docx');
}

function create_otchetAll_month() {
    downloadReport('/create_otchetAll_month', 'Отчет_по_всем_анкетам.docx');
}

function sanitizeFileName(name) {
    return name
        .replace(/[/\\?%*:|"<>]/g, '_') // Замена запрещенных символов
        .replace(/\s+/g, ' ') // Удаление лишних пробелов
        .trim() // Удаление пробелов в начале и конце
        .substring(0, 255); // Ограничение длины имени файла
}


function create_otchetAll_kvartal(kvartal, year) {
    const xhr = new XMLHttpRequest();
    xhr.responseType = 'blob';
    
    xhr.onreadystatechange = function() {
        if (xhr.readyState === 4) {
            if (xhr.status === 200) {
                const contentDisposition = xhr.getResponseHeader('Content-Disposition');
                let fileName = `Otchet_za_${kvartal}_kvartal.xlsx`;
                
                if (contentDisposition) {
                    const fileNameMatch = contentDisposition.match(/filename="?([^"]+)"?/);
                    if (fileNameMatch && fileNameMatch[1]) {
                        fileName = fileNameMatch[1];
                    }
                }
                
                const blob = new Blob([xhr.response], {type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'});
                const link = document.createElement('a');
                link.href = window.URL.createObjectURL(blob);
                link.download = fileName;
                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);
            } else {
                console.error("Ошибочка: " + xhr.status);
            }
        }
    };

    xhr.onerror = function() {
        console.error("Проблемы с интернетом");
    };

    xhr.open("GET", `/create_otchet_kvartal/${kvartal}/${year}`, true);
    xhr.send();
}

function submitExtension(id) {
    const rows = document.querySelectorAll('.form-row');
    const data = [];

    if (rows.length === 0) {
        alert("Пожалуйста, добавьте хотя бы одну организацию для продления.");
        return;
    }

    let isValid = true;
    rows.forEach(row => {
        const organizationSelect = row.querySelector('select.form-control');
        const endDateInput = row.querySelector('input.form-control[type="date"]');

        if (organizationSelect && endDateInput) {
            const organization = organizationSelect.value;
            const endDate = endDateInput.value;

            if (!organization || !endDate) {
                isValid = false;
                return;
            }

            const today = new Date().toISOString().split('T')[0];
            if (endDate <= today) {
                alert("Дата окончания должна быть в будущем!");
                isValid = false;
                return;
            }

            data.push({ 
                id_omsu: parseInt(organization), 
                new_end_date: endDate, 
                id_survey: id 
            });
        }
    });

    if (!isValid) {
        alert("Пожалуйста, заполните все поля перед применением.");
        return;
    }

    const xhr = new XMLHttpRequest();
    xhr.open("POST", "/prodlenie_omsus", true);
    xhr.setRequestHeader("Content-Type", "application/json;charset=UTF-8");
    xhr.onreadystatechange = function () {
        if (xhr.readyState === 4) {
            if (xhr.status === 200) {
                alert("Доступ к анкете успешно продлён!");
                closeModal();
                window.location.reload();
            } else {
                console.error("Ошибка при отправке данных:", xhr.status, xhr.responseText);
                try {
                    const response = JSON.parse(xhr.responseText);
                    alert("Ошибка: " + (response.message || xhr.statusText));
                } catch {
                    alert("Ошибка при отправке данных: " + xhr.status);
                }
            }
        }
    };
    
    try {
        xhr.send(JSON.stringify(data));
    } catch (error) {
        console.error("Ошибка при отправке запроса:", error);
        alert("Произошла ошибка при отправке запроса");
    }
}


async function delete_omsu(id) {
    const confirmed = await window.siteConfirm("Вы уверены, что хотите удалить эту организацию?", {
        title: "Удаление организации",
        confirmText: "Удалить",
        cancelText: "Отмена"
    });

    if (!confirmed) {
        return;
    }

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

    xhr.open("POST", `/organizations/${id}/delete`, true);
    xhr.setRequestHeader("Content-Type", "application/x-www-form-urlencoded");
    xhr.send();
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
    xhr.open("GET", "/organizations/data", true);
    xhr.send();
}

function submitFormAdd() {
    const messageElement = document.getElementById('message');
    messageElement.textContent = '';
    messageElement.className = '';

    if (!document.getElementById('username').value){
        alert("Введите никнейм пользователя!");
        return;
    }

        if (!document.getElementById('password').value){
        alert("Введите пароль!");
        return;
    }

const password = document.getElementById('password').value;
if (password.length < 12) {
    alert("Пароль должен содержать не меньше 12 символов!");
    return;
}

            if (!document.getElementById('organization').value){
        alert("Выберите организацию пользователя!");
        return;
    }

                if (!document.getElementById('role_bd').value){
        alert("Выберите роль пользователя!");
        return;
    }

    const formData = {
        username: document.getElementById('username')?.value || '',
        password: document.getElementById('password')?.value || '',
        fullName: document.getElementById('fullName')?.value || '',
        email: document.getElementById('email_input')?.value || '', // Используем value, а не innerHTML
        organizationId: document.getElementById('organization')?.value || '0',
        role: document.getElementById('role_bd')?.value || 'user'
    };

    console.log("Email value:", document.getElementById('email').value); // Правильный способ получения значения

    console.log("Фактические данные формы:", formData);

    fetch('/users/create/save', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
        },
        body: JSON.stringify(formData)
    })
    .then(response => response.json())
    .then(data => {
        console.log("Ответ сервера:", data);
        messageElement.textContent = data.message;
        messageElement.className = data.success ? 'success-message' : 'error-message';
        if (data.success) {alert("Пользователь успешно добавлен!"); handleTabClick("get_users");}
    })
    .catch(error => {
        console.error("Ошибка:", error);
        messageElement.textContent = 'Ошибка соединения';
        messageElement.className = 'error-message';
    });
}

    let currentEditingUser = null;

    function openEditModal(id, fullName, userName, email, omsu, role, dateBegin, dateEnd) {
        currentEditingUser = id;
        
        document.getElementById('editUserId').value = id;
        document.getElementById('username').value = userName || '';
        document.getElementById('fullName').value = fullName || '';
        document.getElementById('email').value = email || '';
        document.getElementById('organization').value = omsu || '';
        document.getElementById('role_bd').value = role || 'user';
        document.getElementById('editDateBegin').value = dateBegin || '';
        document.getElementById('editDateEnd').value = dateEnd || '';
        document.getElementById('password').value = ''; // Очищаем пароль
        
        if (window.showSiteModal) {
            window.showSiteModal('editUserModal');
        } else {
            document.getElementById('editUserModal').style.display = 'flex';
        }
    }

    
// Путь к файлу настроек
const settingsFilePath = '/email_settings.txt';

// Функция загрузки настроек
function loadEmailSettings() {
    fetch(settingsFilePath)
        .then(response => {
            if (!response.ok) {
                if (response.status === 404) {
                    return Promise.resolve('Кому:\nТема:\nСодержание:');
                }
                throw new Error('Ошибка загрузки файла');
            }
            return response.text();
        })
        .then(text => {
            const settings = parseSettingsFile(text);
            document.getElementById('email-to').value = settings.to || '';
            document.getElementById('email-subject').value = settings.subject || '';
            document.getElementById('email-content').value = settings.content || '';
            alert('Настройки успешно загружены!');
        })
        .catch(error => {
            console.error('Ошибка:', error);
            alert('Ошибка при загрузке настроек');
        });
}

// Функция сохранения настроек
function saveEmailSettings() {
    const emailTo = document.getElementById('email-to').value;
    const emailSubject = document.getElementById('email-subject').value;
    const emailContent = document.getElementById('email-content').value;
    
    const settingsData = {
        to: emailTo,
        subject: emailSubject,
        content: emailContent
    };

    fetch('/save_email_settings', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(settingsData)
    })
    .then(response => {
        if (!response.ok) throw new Error('Ошибка сохранения');
        return response.json();
    })
    .then(data => {
        alert('Настройки успешно сохранены!');
    })
    .catch(error => {
        console.error('Ошибка:', error);
        alert('Ошибка при сохранении настроек');
    });
}

// Парсинг файла настроек
function parseSettingsFile(text) {
    const lines = text.split('\n');
    const settings = { to: '', subject: '', content: '' };
    
    lines.forEach(line => {
        if (line.startsWith('Кому:')) settings.to = line.replace('Кому:', '').trim();
        else if (line.startsWith('Тема:')) settings.subject = line.replace('Тема:', '').trim();
        else if (line.startsWith('Содержание:')) settings.content = line.replace('Содержание:', '').trim();
        else if (settings.content) settings.content += '\n' + line.trim();
    });
    
    return settings;
}



async function deleteUser(id, fullName) {
    const confirmed = await window.siteConfirm(`Вы уверены, что хотите удалить пользователя ${fullName || ''}?`, {
        title: "Удаление пользователя",
        confirmText: "Удалить",
        cancelText: "Отмена"
    });

    if (!confirmed) {
        return;
    }

    fetch(`/users/${id}/delete`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        }
    })
    .then(response => {
        if (!response.ok) {
            throw new Error('Ошибка при удалении');
        }
        return response.text();
    })
    .then(result => {
        alert(result); // Показываем ответ сервера
        closeModal('deleteUserModal');
        handleTabClick("get_users");
    })
    .catch(error => {
        console.error("Ошибка:", error);
        alert("Произошла ошибка: " + error.message);
    });
}


// Глобальное открытие addUserModal вынесено в ~/js/pages/admin-password-tools.js


// Вспомогательная функция для безопасного получения элементов
function getSafeElement(id) {
    const element = document.getElementById(id);
    if (!element) {
        console.error(`Element with ID ${id} not found`);
        throw new Error(`Элемент ${id} не найден`);
    }
    return element;
}

// Функция загрузки организаций
async function loadOrganizations2(selectedOrgId = null) {
    const orgSelect = getSafeElement('editOrganization');
    
    try {
        orgSelect.innerHTML = '<option value="">Загрузка организаций...</option>';
        
        const response = await fetch('/organizations/data');
        if (!response.ok) throw new Error('Не удалось загрузить организации');
        
        const organizations = await response.json();
        orgSelect.innerHTML = '';
        
        organizations.forEach(org => {
            const option = document.createElement('option');
            option.value = org.id;
            option.textContent = org.name;
            if (selectedOrgId && org.id == selectedOrgId) {
                option.selected = true;
            }
            orgSelect.appendChild(option);
        });

    } catch (error) {
        console.error('Ошибка загрузки организаций:', error);
        orgSelect.innerHTML = '<option value="">Ошибка загрузки</option>';
    }
}

// Функция открытия модального окна
async function openEditUserModal(id, fullName, username, email, orgId, role, dateBegin, dateEnd) {
    try {
        // Получаем элементы
        const userId = getSafeElement('editUserId');
        const fullNameEl = getSafeElement('editFullName');
        const usernameEl = getSafeElement('editUsername');
        const roleEl = getSafeElement('editRole');
        const dateBeginEl = getSafeElement('editDateBegin');
        const dateEndEl = getSafeElement('editDateEnd');
        const passwordEl = getSafeElement('editPassword');
        const modal = getSafeElement('editUserModal');

        // Заполняем поля
        userId.value = id;
        fullNameEl.value = fullName || '';
        usernameEl.value = username || '';
        roleEl.value = role || 'user';
        dateBeginEl.value = dateBegin?.split('T')[0] || '';
        dateEndEl.value = dateEnd?.split('T')[0] || '';
        passwordEl.value = '';

        // Загружаем организации
        await loadOrganizations2(orgId);

        // Показываем модальное окно
        if (window.showSiteModal) {
            window.showSiteModal(modal);
        } else {
            modal.style.display = 'flex';
        }

    } catch (error) {
        console.error('Ошибка при открытии формы:', error);
        alert('Ошибка: ' + error.message);
    }
}

// Функция обновления пользователя
async function updateUser() {

        if (!document.getElementById('editUsername').value)
    {
        alert("Введите никнейм пользователя!");
        return;
    }

            if (!document.getElementById('editOrganization').value)
    {
        alert("Выберите организацию пользователя!");
        return;
    }

            if (!document.getElementById('editRole').value)
    {
        alert("Выберите роль пользователя!");
        return;
    }

                if (!document.getElementById('editDateBegin').value)
    {
        alert("Введите дату начала!");
        return;
    }

const startDate = new Date(document.getElementById('editDateBegin').value);
const endDate = new Date(document.getElementById('editDateEnd').value);

if (startDate >= endDate) {
    alert("Дата начала должна быть раньше даты окончания!");
    return;
}
    
    
    try {
        // Получаем элементы
        const modal = getSafeElement('editUserModal');
        const messageContainer = document.createElement('div');
        messageContainer.className = 'message';
        modal.querySelector('.modal-body').appendChild(messageContainer);

        // Получаем значения
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

        // Валидация
        if (!elements.username.value || !elements.organization.value) {
            throw new Error('Заполните все обязательные поля');
        }

        if (elements.dateBegin.value && elements.dateEnd.value) {
            const beginDate = new Date(elements.dateBegin.value);
            const endDate = new Date(elements.dateEnd.value);
            if (endDate < beginDate) {
                throw new Error('Дата окончания не может быть раньше даты начала');
            }
        }

        // Формируем данные
        const formData = {
            username: elements.username.value,
            password: elements.password.value || 'keep_original',
            fullName: elements.fullName.value,
            organizationId: elements.organization.value,
            role: elements.role.value,
            dateBegin: elements.dateBegin.value,
            dateEnd: elements.dateEnd.value
        };

        // Отправляем запрос
        const response = await fetch(`/users/${elements.id.value}/update`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
            },
            body: JSON.stringify(formData)
        });

        const result = await response.json();
        
        if (!response.ok) {
            throw new Error(result.message || 'Ошибка сервера');
        }

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

// Функция закрытия модального окна
function closeModal2() {
    const modal = document.getElementById('editUserModal');
    if (modal) {
        if (window.hideSiteModal) {
            window.hideSiteModal(modal);
        } else {
            modal.style.display = 'none';
        }
    }
}
async function add_omsu_bd() {
    const form = document.getElementById('organizationForm');
    const messageDiv = document.getElementById('message');
    messageDiv.style.display = 'none';

    if (!document.getElementById('Name').value)
{
    alert("Введите название организации!");
    return;
}


if (!document.getElementById('DateBegin').value)
{
    alert("Выберите дату начала!");
    return;
}



const startDate = new Date(document.getElementById('DateBegin').value);
const endDate = new Date(document.getElementById('DateEnd').value);

if (startDate >= endDate) {
    alert("Дата начала должна быть раньше даты окончания!");
    return;
}




    try {
        // 1. Собираем данные из формы
        const formData = {
            Name: form.Name.value,
            Email: form.omsu_email.value,
            DateBegin: form.DateBegin.value,
            DateEnd: form.DateEnd.value
        };

        // 2. Получаем CSRF-токен
        const token = document.querySelector('[name="__RequestVerificationToken"]').value;

        // 3. Отправляем на сервер
        const response = await fetch('/organizations/create/save', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify(formData)
        });

        // 4. Обрабатываем ответ
        const result = await response.json();
        
        // 5. Показываем результат
        messageDiv.textContent = result.success 
            ? 'Организация успешно создана!' 
            : 'Ошибка: ' + (result.error || 'Неизвестная ошибка');
        handleTabClick("get_omsu");
        alert("Организация успешно создана!")
        
        messageDiv.className = result.success ? 'alert alert-success' : 'alert alert-danger';
        messageDiv.style.display = 'block';

        console.log('Ответ сервера:', result);

    } catch (error) {
        messageDiv.textContent = 'Ошибка при отправке: ' + error.message;
        messageDiv.className = 'alert alert-danger';
        messageDiv.style.display = 'block';
        console.error('Ошибка:', error);
    }
}
// СКРИПТ ДЛЯ РЕДАКТИРОВАНИЯ ОРГАНИЗАЦИЙ
        
function closeOmsuModal(modalId) {
    const modal = document.getElementById(modalId);
    if (modal) {
        if (window.hideSiteModal) {
            window.hideSiteModal(modal);
        } else {
            modal.style.display = 'none';
        }
    }
}

// Функция открытия модального окна редактирования
function openEditOmsuModal(id, name, email, dateBegin, dateEnd) {
    document.getElementById('editOmsuId').value = id;
    document.getElementById('omsuName').value = name || '';
    document.getElementById('omsuEmail').value = email || '';
    document.getElementById('omsuDateBegin').value = dateBegin || '';
    document.getElementById('omsuDateEnd').value = dateEnd || '';
    if (window.showSiteModal) {
        window.showSiteModal('editOmsuModal');
    } else {
        document.getElementById('editOmsuModal').style.display = 'flex';
    }
}

// Функция обновления организации с улучшенной обработкой данных
async function updateOmsu() {


    if (!document.getElementById('omsuName').value)
{
    alert("Введите название организации!");
    return;
}

if (!document.getElementById('omsuDateBegin').value)
{
    alert("Выберите дату начала!");
    return;
}

const startDate = new Date(document.getElementById('omsuDateBegin').value);
const endDate = new Date(document.getElementById('omsuDateEnd').value);

if (startDate >= endDate) {
    alert("Дата начала должна быть раньше даты окончания!");
    return;
}


    try {
        // 1. Получаем значения из формы
        const id = document.getElementById('editOmsuId').value;
        const name = document.getElementById('omsuName').value.trim();
        const email = document.getElementById('omsuEmail').value.trim();
        const dateBegin = document.getElementById('omsuDateBegin').value;
        const dateEnd = document.getElementById('omsuDateEnd').value;

        // 2. Валидация обязательных полей
        if (!name) {
            throw new Error("Название организации обязательно для заполнения");
        }

        // 3. Проверка дат
        if (dateBegin && dateEnd) {
            const beginDate = new Date(dateBegin);
            const endDate = new Date(dateEnd);
            
            if (endDate < beginDate) {
                throw new Error("Дата окончания не может быть раньше даты начала");
            }
        }

        // 4. Подготовка данных в формате, ожидаемом сервером
        const omsuData = [
            name,        // Название организации (обязательное)
            email || "", // Email (может быть пустым)
            dateBegin || "", // Дата начала (может быть пустой)
            dateEnd || ""    // Дата окончания (может быть пустой)
        ];

        console.log('Отправляемые данные:', {
            id: id,
            data: omsuData
        });

        // 5. Блокируем кнопку на время отправки
        const saveBtn = document.getElementById('saveOmsuBtn');
        saveBtn.disabled = true;
        saveBtn.textContent = 'Сохранение...';

        // 6. Отправка данных с обработкой возможных ошибок сети
        let response;
        try {
            response = await fetch(`/organizations/${id}/update`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify(omsuData)
            });
        } catch (networkError) {
            throw new Error("Ошибка сети. Проверьте соединение.");
        }

        // 7. Проверка ответа сервера
        if (!response.ok) {
            let errorText;
            try {
                errorText = await response.text();
            } catch (parseError) {
                errorText = `Ошибка сервера: ${response.status}`;
            }
            throw new Error(errorText);
        }

        // 8. Успешное завершение
        const result = await response.text();
        console.log("Организация успешно обновлена:", result);
        closeOmsuModal('editOmsuModal');
        alert("Организация успешно отредактирована!");
        handleTabClick("get_omsu");

    } catch (error) {
        console.error('Ошибка при обновлении организации:', error);
        
        // Показываем пользователю понятное сообщение об ошибке
        let errorMessage = error.message;
        if (error.message.includes("Некорректные данные организации")) {
            errorMessage = "Проверьте правильность заполнения всех полей";
        }
        
        alert(`Ошибка: ${errorMessage}`);
    } finally {
        const saveBtn = document.getElementById('saveOmsuBtn');
        if (saveBtn) {
            saveBtn.disabled = false;
            saveBtn.textContent = 'Сохранить';
        }
    }
}


    // Открытие модального окна
    function openAnswersModal() {
        document.getElementById('answersModal').style.display = 'block';
    }
    
    // Закрытие модального окна
    function closeAnswersModal() {
        document.getElementById('answersModal').style.display = 'none';
    }
    
    // Показать ответы по анкете
    async function showAnswers(surveyId, surveyName) {
        const container = document.getElementById('answersContainer');
        const title = document.getElementById('surveyAnswersTitle');
        
        title.textContent = `Ответы: ${surveyName}`;
        container.innerHTML = '<div class="loading">Загрузка данных...</div>';
        openAnswersModal();
        
        try {
            const response = await fetch(`/Survey/GetSurveyAnswers?id=${surveyId}`);
            const data = await response.json();
            
            if (!data.success) {
                throw new Error(data.error || 'Ошибка загрузки данных');
            }
            
            renderAnswers(data.survey, data.answers);
        } catch (error) {
            container.innerHTML = `<div class="error">${error.message}</div>`;
        }
    }
    
    // Отрисовка ответов
    function renderAnswers(survey, answers) {
        const container = document.getElementById('answersContainer');
        
        let html = `
            <div class="survey-info">
                <h3>${survey.name_survey}</h3>
                <p><strong>Описание:</strong> ${survey.description || 'Нет описания'}</p>
                <p><strong>Период:</strong> 
                    ${new Date(survey.date_begin).toLocaleDateString()} - 
                    ${new Date(survey.date_end).toLocaleDateString()}
                </p>
                <hr>
            </div>`;
        
        if (answers && answers.length > 0) {
            html += `
                <table class="answers-table">
                    <thead>
                        <tr>
                            <th>Организация</th>
                            <th>Дата ответа</th>
                            <th>Ответы</th>
                        </tr>
                    </thead>
                    <tbody>`;
            
            answers.forEach(answer => {
                const answersText = formatAnswers(answer.answers);
                html += `
                    <tr>
                        <td>${answer.name_omsu}</td>
                        <td>${new Date(answer.completion_date).toLocaleDateString()}</td>
                        <td>${answersText}</td>
                    </tr>`;
            });
            
            html += `</tbody></table>`;
        } else {
            html += '<p>Нет данных об ответах</p>';
        }
        
        container.innerHTML = html;
    }
    
    // Форматирование ответов
    function formatAnswers(answersJson) {
        try {
            const answers = JSON.parse(answersJson);
            return Object.entries(answers)
                .map(([question, answer]) => `<strong>${question}:</strong> ${answer}`)
                .join('<br>');
        } catch {
            return 'Ошибка формата ответов';
        }
    }


async function send_email() {
    try {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        
        const response = await fetch('/send_message', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token || ''
            },
            body: JSON.stringify({}) 
        });
        
        if (!response.ok) {
            const errorData = await response.json();
            throw new Error(errorData.message || 'Ошибка сервера');
        }
        
        const result = await response.json();
        alert(result.message || 'Письмо успешно отправлено!');
    } catch (error) {
        console.error('Ошибка:', error);
        alert('Ошибка при отправке: ' + error.message);
    }
}


// СКРИПТ ДЛЯ ЗАГРУЗКИ ИНСТРУКЦИИ

function loadFileAdmin() {
  loadAndUploadFile('administrator');
}

function loadFileUser() {
  loadAndUploadFile('user');
}

 function handleSelectChange(select) {
    const value = select.value;
    if (value === 'administrator') {
      loadFileAdmin();
    } else if (value === 'user') {
      loadFileUser();
    }
    // Сброс выбора, если нужно
    select.value = "";
  }

function loadAndUploadFile(role) {
  const input = document.createElement('input');
  input.type = 'file';
  input.accept = 'docx'; // можно настроить нужные типы файлов
  input.onchange = async (e) => {
    const file = e.target.files[0];
    if (!file) return;

    // Создаем FormData для отправки файла
    const formData = new FormData();

    // Переименовывать файл на клиенте нельзя, поэтому отправляем оригинал и роль
    formData.append('file', file);
    formData.append('role', role);

    try {
      const response = await fetch('/upload-instruction', { // URL вашего эндпоинта на сервере
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

function restoreSurveys(){handleTabClick("get_surveys");}

function archive_list_omsus(){handleTabClick("archive_list_omsus");}
function archive_list_users(){handleTabClick("archive_list_users");}


// СКРИПТЫ ДЛЯ ФИЛЬТРАЦИИ ОТВЕТОВ НА ВКЛАДКЕ ОТВЕТЫ

function populateOrganizationOptions() {
  const select = document.getElementById('filterOrganization');
  const tbody = document.querySelector('#data_table tbody');
  const rows = tbody.rows;

  const orgSet = new Set();

  for (let i = 0; i < rows.length; i++) {
    const row = rows[i];
    if (!row.classList.contains('active')) continue;
    const orgName = row.cells[0].textContent.trim();
    orgSet.add(orgName);
  }

  fillSelectOptions(select, orgSet, "Все организации");
}

// Функция для заполнения опций анкет при клике на селект
function populateSurveyOptions() {
  const select = document.getElementById('filterSurvey');
  const tbody = document.querySelector('#data_table tbody');
  const rows = tbody.rows;

  const surveySet = new Set();

  for (let i = 0; i < rows.length; i++) {
    const row = rows[i];
    if (!row.classList.contains('active')) continue;
    const surveyName = row.cells[1].textContent.trim();
    surveySet.add(surveyName);
  }

  fillSelectOptions(select, surveySet, "Все анкеты");
}

// Общая функция для заполнения селекта опциями
function fillSelectOptions(selectElem, valuesSet, defaultText) {
  // Сохраним текущее выбранное значение
  const currentValue = selectElem.value;

  // Очистим все опции
  selectElem.innerHTML = '';

  // Добавим опцию по умолчанию
  const defaultOption = document.createElement('option');
  defaultOption.value = '';
  defaultOption.textContent = defaultText;
  selectElem.appendChild(defaultOption);

  // Добавим уникальные значения
  valuesSet.forEach(value => {
    const option = document.createElement('option');
    option.value = value;
    option.textContent = value;
    selectElem.appendChild(option);
  });

  // Восстановим выбранное значение, если оно есть в новом списке
  if (Array.from(selectElem.options).some(opt => opt.value === currentValue)) {
    selectElem.value = currentValue;
  } else {
    selectElem.value = '';
  }
}

// Функция фильтрации таблицы по выбранным значениям селектов
function filterTable() {
  const orgSelect = document.getElementById('filterOrganization');
  const surveySelect = document.getElementById('filterSurvey');
  const tbody = document.querySelector('#data_table tbody');
  const rows = tbody.rows;
  const noneResultRow = document.getElementById('none_result');

  const selectedOrg = orgSelect.value;
  const selectedSurvey = surveySelect.value;

  let visibleCount = 0;

  for (let i = 0; i < rows.length; i++) {
    const row = rows[i];
    if (!row.classList.contains('active')) continue;

    const orgName = row.cells[0].textContent.trim();
    const surveyName = row.cells[1].textContent.trim();

    const matchOrg = !selectedOrg || orgName === selectedOrg;
    const matchSurvey = !selectedSurvey || surveyName === selectedSurvey;

    if (matchOrg && matchSurvey) {
      row.style.display = '';
      visibleCount++;
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
        const rows = tbody.querySelectorAll('tr.active'); // только строки с данными

        const surveySet = new Set();

        rows.forEach(row => {
            const surveyName = row.cells[0].textContent.trim();
            surveySet.add(surveyName);
        });

        // Сохраняем текущее выбранное значение
        const currentValue = select.value;

        // Очищаем все опции
        select.innerHTML = '';

        // Добавляем опцию по умолчанию
        const defaultOption = document.createElement('option');
        defaultOption.value = '';
        defaultOption.textContent = 'Все анкеты';
        select.appendChild(defaultOption);

        // Добавляем уникальные значения
        surveySet.forEach(value => {
            const option = document.createElement('option');
            option.value = value;
            option.textContent = value;
            select.appendChild(option);
        });

        // Восстанавливаем выбранное значение, если оно осталось
        if ([...select.options].some(opt => opt.value === currentValue)) {
            select.value = currentValue;
        } else {
            select.value = '';
        }
    }

    // Функция фильтрации таблицы по выбранному значению селекта
    function applySurveyFilter() {
        const select = document.getElementById('filterSurvey');
        const tbody = document.querySelector('#data_table tbody');
        const rows = tbody.querySelectorAll('tr.active');
        const noneResultRow = document.getElementById('none_result');

        const selectedSurvey = select.value;
        let visibleCount = 0;

        rows.forEach(row => {
            const surveyName = row.cells[0].textContent.trim();
            const matchSurvey = !selectedSurvey || surveyName === selectedSurvey;

            if (matchSurvey) {
                row.style.display = '';
                visibleCount++;
            } else {
                row.style.display = 'none';
            }
        });

        // Показать или скрыть строку "Результаты не найдены!"
        if (noneResultRow) {
            noneResultRow.style.display = visibleCount === 0 ? '' : 'none';
        }
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
  .then(result => {
    alert('Анкета успешно добавлена!');
    window.location.reload();
  })
  .catch(err => {
    alert(err.message);
  });
}


function populateYears(kvartal, select) {
  // Если уже есть опции кроме заглушки — не добавляем снова
  if (select.options.length > 1) return;

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
  const year = select.value;
  if (year) {
    create_otchetAll_kvartal(kvartal, year);
    select.selectedIndex = 0;
  }
}




            const root = ReactDOM.createRoot(document.getElementById('root'));
            root.render(<App />);

            // Логика глаза пароля вынесена в ~/js/pages/admin-password-tools.js
        
