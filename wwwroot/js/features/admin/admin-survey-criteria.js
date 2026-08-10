(function () {
    window.createSurveyCriteriaController = function createSurveyCriteriaController({ getElementByRole, showError }) {
        let lastValidationMessage = '';

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
            const createElement = window.AppUi.createElement;
            const wrapper = createElement('div', { className: 'app-field-group survey-editor-page__criteria-item' });
            const label = createElement('label');
            const control = createElement('div', { className: 'survey-editor-page__criteria-control' });
            const inputWrap = createElement('div', { className: 'survey-editor-page__criteria-input-wrap' });
            const input = window.AppUi.createField({
                tagName: 'input',
                type: 'text',
                className: 'criteriy',
                placeholder: 'Введите критерий оценки',
                value: value || '',
                attrs: { required: true }
            });
            const removeButton = window.AppUi.createElement('button', {
                type: 'button',
                className: 'survey-editor-page__criteria-remove',
                dataset: {
                    clickCall: 'removeSurveyCriterion',
                    clickPassElement: 'true'
                }
            });
            const icon = createElement('i', {
                className: 'fas fa-trash',
                attrs: { 'aria-hidden': 'true' }
            });
            removeButton.appendChild(icon);
            const action = createElement('div', { className: 'survey-editor-page__criteria-action' });
            const addButton = window.AppUi.createButton({
                variant: 'primary',
                className: 'criteria-btn criteria-btn--info survey-editor-page__criteria-add-inline',
                text: 'Добавить критерий',
                dataset: {
                    role: 'criteria-add',
                    clickCall: document.getElementById('surveyId') ? 'surveyEditAddCriteria' : 'addRowCriteriy'
                }
            });
            inputWrap.append(input, removeButton);
            control.appendChild(inputWrap);
            action.appendChild(addButton);
            wrapper.append(label, control, action);
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
            if (list && getItems(list).length <= 1) {
                if (input) {
                    input.value = '';
                    window.SurveyAdminValidation?.clearFieldError(input);
                }
                refresh(list);
                return;
            }
            item.remove();
            refresh(list);
        }

        function validate() {
            const items = getItems();
            lastValidationMessage = '';
            if (items.length === 0) {
                lastValidationMessage = 'Добавьте хотя бы один критерий оценки.';
                return false;
            }
            let hasErrors = false;
            let hasValue = false;
            items.forEach((item) => {
                const input = item.querySelector('.criteriy');
                if (input?.value.trim()) {
                    hasValue = true;
                    window.SurveyAdminValidation?.clearFieldError(input);
                    return;
                }
                hasErrors = true;
                window.SurveyAdminValidation?.setFieldError(input, 'Введите критерий оценки.');
            });
            if (!hasValue) lastValidationMessage = 'Добавьте хотя бы один критерий оценки.';
            else if (hasErrors) lastValidationMessage = 'Заполните все критерии оценки или удалите пустые поля.';
            return hasValue && !hasErrors;
        }

        function replace(values) {
            const list = getList();
            if (!list) return;
            list.replaceChildren();
            (values?.length ? values : ['']).forEach((value) => append(value));
        }

        return {
            append,
            remove,
            validate,
            replace,
            getValidationMessage: () => lastValidationMessage,
            values: () => getItems().map((item) => item.querySelector('.criteriy')?.value.trim() || '')
        };
    };
})();
