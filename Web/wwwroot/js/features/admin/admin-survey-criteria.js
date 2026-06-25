(function () {
    window.createSurveyCriteriaController = function createSurveyCriteriaController({ getElementByRole, showError }) {
        function getList() {
            return getElementByRole('criteria-list');
        }

        function getItems(container) {
            const list = container || getList();
            return list ? Array.from(list.querySelectorAll('.survey-editor-page__criteria-item')) : [];
        }

        function refresh(container) {
            getItems(container).forEach((item, index) => {
                const number = index + 1;
                const label = item.querySelector('label');
                const input = item.querySelector('.criteriy');
                const removeButton = item.querySelector('.survey-editor-page__criteria-remove');
                if (input) input.id = `criterion${number}`;
                if (label) {
                    label.htmlFor = `criterion${number}`;
                    label.textContent = `Критерий №${number}`;
                }
                removeButton?.setAttribute('aria-label', `Удалить критерий ${number}`);
            });
        }

        function createField(value) {
            const wrapper = document.createElement('div');
            wrapper.className = 'form-group survey-editor-page__criteria-item';
            const label = document.createElement('label');
            const control = document.createElement('div');
            control.className = 'survey-editor-page__criteria-control';
            const inputWrap = document.createElement('div');
            inputWrap.className = 'survey-editor-page__criteria-input-wrap';
            const input = document.createElement('input');
            input.type = 'text';
            input.className = 'form-control criteriy';
            input.placeholder = 'Введите критерий оценки';
            input.required = true;
            input.value = value || '';
            const removeButton = document.createElement('button');
            removeButton.type = 'button';
            removeButton.className = 'survey-editor-page__criteria-remove';
            removeButton.dataset.clickCall = 'removeSurveyCriterion';
            removeButton.dataset.clickPassElement = 'true';
            const icon = document.createElement('i');
            icon.className = 'fas fa-trash';
            icon.setAttribute('aria-hidden', 'true');
            removeButton.appendChild(icon);
            const action = document.createElement('div');
            action.className = 'survey-editor-page__criteria-action';
            const addButton = document.createElement('button');
            addButton.type = 'button';
            addButton.className = 'criteria-btn criteria-btn--info survey-editor-page__criteria-add-inline';
            addButton.dataset.role = 'criteria-add';
            addButton.dataset.clickCall = document.getElementById('surveyId') ? 'surveyEditAddCriteria' : 'addRowCriteriy';
            addButton.textContent = 'Добавить критерий';
            const error = document.createElement('div');
            error.className = 'error-message';
            inputWrap.append(input, removeButton);
            control.appendChild(inputWrap);
            action.appendChild(addButton);
            wrapper.append(label, control, action, error);
            return wrapper;
        }

        function append(value) {
            const list = getList();
            if (!list) return null;
            const field = createField(value);
            list.appendChild(field);
            refresh(list);
            return field;
        }

        function remove(trigger) {
            const item = trigger?.closest('.survey-editor-page__criteria-item');
            if (!item) return;
            const list = item.parentElement;
            const input = item.querySelector('.criteriy');
            const error = item.querySelector('.error-message');
            if (list && getItems(list).length <= 1) {
                if (input) {
                    input.value = '';
                    input.classList.remove('invalid');
                }
                if (error) {
                    error.textContent = '';
                    error.style.display = '';
                }
                refresh(list);
                return;
            }
            item.remove();
            refresh(list);
        }

        function validate() {
            const items = getItems();
            if (items.length === 0) {
                showError('Ошибка', 'Добавьте хотя бы один критерий оценки.');
                return false;
            }
            let hasErrors = false;
            let hasValue = false;
            items.forEach((item) => {
                const input = item.querySelector('.criteriy');
                const error = item.querySelector('.error-message');
                if (input?.value.trim()) {
                    hasValue = true;
                    input.classList.remove('invalid');
                    if (error) {
                        error.textContent = '';
                        error.style.display = 'none';
                    }
                    return;
                }
                hasErrors = true;
                input?.classList.add('invalid');
                if (error) {
                    error.textContent = 'Заполните критерий или удалите это поле.';
                    error.style.display = 'block';
                }
            });
            if (!hasValue) showError('Ошибка', 'Добавьте хотя бы один критерий оценки.');
            else if (hasErrors) showError('Ошибка', 'Заполните все критерии оценки или удалите пустые поля.');
            return hasValue && !hasErrors;
        }

        function replace(values) {
            const list = getList();
            if (!list) return;
            list.replaceChildren();
            (values?.length ? values : ['']).forEach((value) => append(value));
        }

        return { append, remove, validate, replace, values: () => getItems().map((item) => item.querySelector('.criteriy')?.value.trim() || '') };
    };
})();
