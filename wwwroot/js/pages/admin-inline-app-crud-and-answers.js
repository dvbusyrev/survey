function surveyEditGetOrganizationItems() {
    const organizationList = document.getElementById('organizationList');
    return organizationList ? organizationList.querySelectorAll('.organization-item') : [];
}

function surveyEditCreateIconButton(iconClass, label) {
    const button = document.createElement('button');
    button.type = 'button';
    button.setAttribute('aria-label', label);

    const icon = document.createElement('i');
    icon.className = iconClass;
    icon.setAttribute('aria-hidden', 'true');
    button.appendChild(icon);

    return button;
}

function surveyEditToggleOrganizationSelection(element) {
    const orgId = parseInt(element.dataset.id, 10);
    const orgName = element.dataset.name;
    const index = surveyEditSelectedOrganization.findIndex(org => org.id === orgId);

    if (element.dataset.selected === 'true') {
        element.dataset.selected = 'false';
        element.classList.remove('selected');
        if (index !== -1) {
            surveyEditSelectedOrganization.splice(index, 1);
        }
    } else {
        if (index === -1) {
            surveyEditSelectedOrganization.push({ id: orgId, name: orgName });
        }
        element.dataset.selected = 'true';
        element.classList.add('selected');
    }
}

            function surveyEditSaveSelectedOrganization() {
                surveyEditCloseModal('organizationModal');
                surveyEditUpdateSelectedOrganizationDisplay();
            }

 function surveyEditUpdateSelectedOrganizationDisplay() {
    var container = document.getElementById('selectedOrganizationContainer');
    var list = document.getElementById('selectedOrganizationList');
    var idsInput = document.getElementById('selectedOrganizationIds');

    // Находим все выбранные элементы
    var selectedElements = document.querySelectorAll('#organizationList .organization-item.selected');

    // Очищаем массив выбранных организаций
    surveyEditSelectedOrganization = [];

    // Заполняем массив из выбранных элементов
    selectedElements.forEach(function(el) {
        var id = parseInt(el.dataset.id, 10);
        var name = el.dataset.name;
        surveyEditSelectedOrganization.push({ id: id, name: name });
    });

    if (surveyEditSelectedOrganization.length === 0) {
        container.style.display = 'none';
        if (idsInput) idsInput.value = '';
        list.innerHTML = '';
        return;
    }

    container.style.display = 'block';
    list.innerHTML = '';

    // Создаем элементы с выбранными организациями
    surveyEditSelectedOrganization.forEach(function(org) {
        var item = document.createElement('span');
        item.className = 'selected-organization-item';

        item.appendChild(document.createTextNode(org.name + ' '));

        var removeButton = surveyEditCreateIconButton('fas fa-xmark', 'Убрать организацию');
        removeButton.dataset.clickCall = 'surveyEditRemoveOrganization';
        removeButton.dataset.clickArgs = JSON.stringify([org.id]);
        item.appendChild(removeButton);

        list.appendChild(item);
    });

    // Обновляем скрытое поле с id выбранных организаций
    if (idsInput) {
        idsInput.value = surveyEditSelectedOrganization.map(function(org) { return org.id; }).join(',');
    }
}


            function surveyEditRemoveOrganization(orgId) {
                surveyEditSelectedOrganization = surveyEditSelectedOrganization.filter(function (org) {
                    return org.id !== orgId;
                });

                surveyEditUpdateSelectedOrganizationDisplay();
                
                if (surveyEditModalOpen) {
                    var orgItems = surveyEditGetOrganizationItems();
                    for (var i = 0; i < orgItems.length; i++) {
                        if (parseInt(orgItems[i].dataset.id, 10) === orgId) {
                            orgItems[i].dataset.selected = 'false';
                            orgItems[i].classList.remove('selected');
                        }
                    }
                }
            }

            function surveyEditAddCriteria() {
                var container = document.getElementById('cont_criteries');
                if (!container) return;
                
                var count = container.querySelectorAll('.criteriy').length + 1;
                var div = document.createElement('div');
                div.className = 'form-group cont_criteries';
                div.innerHTML = '<label>Критерий оценки</label><input type="text" class="form-control criteriy" required /><div class="error-message"></div>';
                container.appendChild(div);
            }

            function surveyEditConfirmCriteria() {
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
                for (var i = 0; i < criteriaInputs.length; i++) {
                    if (criteriaInputs[i].value.trim() === '') {
                        criteriaInputs[i].classList.add('invalid');
                        allCriteriaValid = false;
                    } else {
                        criteriaInputs[i].classList.remove('invalid');
                    }
                }
                
                if (!allCriteriaValid) {
                    alert('Пожалуйста, заполните все добавленные критерии оценки');
                    return;
                }
                
                var container = document.getElementById('two_step');
                var criteriyInputs = container.querySelectorAll('.criteriy');
                for (var i = 0; i < criteriyInputs.length; i++) {
                    criteriyInputs[i].readOnly = true;
                }
                
                container.classList.add('confirmed');
                document.getElementById('add_survey_btn').style.display = 'inline-block';
                document.getElementById('send_email').style.display = 'inline-block';
                document.getElementById('add_crit').style.display = 'none';
                document.getElementById('conf_btn').style.display = 'none';
                alert('Критерии подтверждены. Теперь вы можете обновить анкету.');
            }

    async function surveyEditUpdate() {
        const surveyTitle = document.getElementById('surveyTitle');
        const surveyDescription = document.getElementById('surveyDescription');
        const startDate = document.getElementById('startDate');
        const endDate = document.getElementById('endDate');
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        const surveyId = document.getElementById('surveyId')?.value;
        try {
            if (!surveyTitle?.value.trim() || !startDate?.value || !endDate?.value) {
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

            if(surveyEditSelectedOrganization.length==0)
            {
                alert('Пожалуйста, выберите хотя бы одну организацию!');
                return;
            }


            const formData = {
                Title: surveyTitle.value.trim(),
                Description: surveyDescription?.value.trim() || '',
                StartDate: new Date(startDate.value).toISOString(),
                EndDate: new Date(endDate.value).toISOString(),
                Organizations: surveyEditSelectedOrganization.map(org => org.id),
                Criteria: Array.from(document.querySelectorAll('.criteriy'))
                    .map(input => input.value.trim())
                    .filter(text => text !== '')
            };

            const response = await fetch(`/surveys/${surveyId}/update`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token,
                    'Accept': 'application/json'
                },
                body: JSON.stringify(formData)
            });

            if (!response.ok) {
                let errorMessage = 'Ошибка сервера';
                try {
                    const errorData = await response.json();
                    errorMessage = errorData.message || errorData.error || errorMessage;
                } catch (e) {
                    console.error('Ошибка при чтении ответа:', e);
                }
                throw new Error(errorMessage);
            }

            const result = await response.json();
            if (result.success) {
                alert(result.message || 'Анкета успешно обновлена!');
                window.location.reload();
            } else {
                throw new Error(result.message || 'Неизвестная ошибка');
            }

        } catch (error) {
            console.error('Ошибка при обновлении анкеты:', error);
            
            let userMessage = error.message;
            
            if (error.message.includes('jsonb') && error.message.includes('text')) {
                userMessage = 'Ошибка формата данных. Пожалуйста, обновите страницу и попробуйте снова.';
            } else if (error.message.includes('date')) {
                userMessage = 'Ошибка в датах. Проверьте правильность введенных дат.';
            } else if (error.message.includes('validation')) {
                userMessage = 'Ошибка валидации данных: ' + error.message;
            }
            
            alert(`Ошибка: ${userMessage}`);
            
            const showDetails = await window.siteConfirm('Показать технические подробности ошибки?', {
                title: 'Подробности ошибки',
                confirmText: 'Показать',
                cancelText: 'Закрыть'
            });

            if (showDetails) {
                console.error('Техническая информация:', error.stack || error.message);
                window.siteNotify('Подробности ошибки выведены в консоль браузера.', 'info');
            }
        }
    }

    function surveyEditValidateForm() {
        let isValid = true;
        
        const requiredFields = [
            { element: document.getElementById('surveyTitle'), errorId: 'titleError' },
            { element: document.getElementById('startDate'), errorId: 'startDateError' },
            { element: document.getElementById('endDate'), errorId: 'endDateError' }
        ];

        requiredFields.forEach(field => {
            const errorElement = document.getElementById(field.errorId);
            if (!field.element.value.trim()) {
                field.element.classList.add('invalid');
                if (errorElement) errorElement.style.display = 'block';
                isValid = false;
            } else {
                field.element.classList.remove('invalid');
                if (errorElement) errorElement.style.display = 'none';
            }
        });

        const startDate = document.getElementById('startDate');
        const endDate = document.getElementById('endDate');
        const endDateError = document.getElementById('endDateError');
        
        if (startDate.value && endDate.value && new Date(endDate.value) <= new Date(startDate.value)) {
            endDate.classList.add('invalid');
            if (endDateError) {
                endDateError.textContent = 'Дата окончания должна быть позже даты начала';
                endDateError.style.display = 'block';
            }
            isValid = false;
        }

        const organizationError = document.getElementById('organizationError');
        if (surveyEditSelectedOrganization.length === 0) {
            if (organizationError) organizationError.style.display = 'block';
            isValid = false;
        } else {
            if (organizationError) organizationError.style.display = 'none';
        }

        return isValid;
    }
    // Общие helper-функции вынесены в ~/js/pages/admin-common-helpers.js

        // СКРИПТЫ ДЛЯ ВКЛАДКИ КОПИРОВАНИЯ АНКЕТЫ

            function hideNotification2() {
                document.getElementById('notification').style.display = 'none';
                
                const messageElement = document.getElementById('notificationMessage');
                if (messageElement.className.includes('notification-success')) {
                    window.location.reload();
                }
            }

            function copySurvey(id) {
                const startDate = document.getElementById('startDate').value;
                const endDate = document.getElementById('endDate').value;
                const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
                
                if (!startDate || !endDate) {
                    showNotification('Пожалуйста, заполните все обязательные поля', false);
                    return;
                }
                
                if (new Date(endDate) <= new Date(startDate)) {
                    showNotification('Дата окончания должна быть позже даты начала', false);
                    return;
                }

                document.getElementById('loadingOverlay').style.display = 'flex';
                
                fetch('/surveys/' + id + '/copy', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token
                    },
                    body: JSON.stringify({
                        StartDate: startDate,
                        EndDate: endDate
                    })
                })
                .then(response => {
                    if (!response.ok) {
                        return response.json().then(err => {
                            throw new Error(err.message || 'Ошибка сервера');
                        });
                    }
                    return response.json();
                })
                .then(data => {
                    document.getElementById('loadingOverlay').style.display = 'none';
                    
                    if (data.success) {
                        alert("Анкета успешно скопирована!");
                        window.location.reload();
                    } else {
                        throw new Error(data.message || 'Ошибка при копировании анкеты');
                    }
                })
                .catch(error => {
                    document.getElementById('loadingOverlay').style.display = 'none';
                    showNotification(error.message, false);
                    console.error('Error:', error);
                });
            }
