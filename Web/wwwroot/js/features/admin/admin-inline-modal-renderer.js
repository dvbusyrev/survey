export function createClosedAdminModalState() {
    return {
        isOpen: false,
        content: '',
        data: null,
        message: null,
        isSuccess: false
    };
}

function appendDialog(root, header, body, footer) {
    root.appendChild(header);
    root.appendChild(body);
    root.appendChild(footer);
}

function createDialogSection(className, text) {
    const section = document.createElement('div');
    section.className = className;
    if (text) {
        section.textContent = text;
    }
    return section;
}

export function createAdminModalRenderer({
    pageContainer,
    getExtensionModalMount,
    onClose,
    onCopySurvey,
    onUpdateSurvey,
    onDeleteSurvey,
    onCreateMonthlyReport,
    onCreateMonthlySummaryReport,
    onCreateQuarterlyReport
}) {
    let cleanup = null;
    let modalNode = document.getElementById('admin-inline-modal-host');
    if (modalNode) {
        modalNode.remove();
    }

    modalNode = document.createElement('div');
    modalNode.id = 'admin-inline-modal-host';
    const modalContent = document.createElement('div');
    modalContent.className = 'modal-content';
    const modalClose = document.createElement('span');
    modalClose.className = 'modal-close';
    const modalIcon = document.createElement('i');
    modalIcon.className = 'fas fa-xmark';
    const bodyHost = document.createElement('div');
    bodyHost.className = 'modal-body';
    modalClose.appendChild(modalIcon);
    modalContent.appendChild(modalClose);
    modalContent.appendChild(bodyHost);
    modalNode.appendChild(modalContent);
    pageContainer.appendChild(modalNode);

    function syncPageState() {
        window.syncSiteModalBodyState?.();
    }

    function reveal() {
        modalNode.classList.add('modal--visible');
        modalNode.setAttribute('aria-hidden', 'false');
        syncPageState();
    }

    function renderReport(modalState) {
        const root = document.createElement('div');
        const title = document.createElement('h2');
        title.className = 'modal-title';
        title.textContent = 'Создать отчёт';
        root.appendChild(title);

        const actions = document.createElement('div');
        actions.style.display = 'flex';
        actions.style.gap = '10px';
        actions.style.justifyContent = 'space-between';
        actions.style.marginTop = '1.5rem';

        const month = document.createElement('div');
        month.className = 'submenu2-container';
        month.style.flex = '1';
        const monthButton = document.createElement('button');
        monthButton.style.width = '100%';
        monthButton.textContent = 'Отчёт за месяц';
        const monthMenu = document.createElement('div');
        monthMenu.className = 'submenu2';
        const bySurvey = document.createElement('div');
        bySurvey.textContent = 'По выбранной анкете';
        bySurvey.addEventListener('click', () => onCreateMonthlyReport(modalState.data?.id_survey));
        const allSurveys = document.createElement('div');
        allSurveys.textContent = 'По всем анкетам';
        allSurveys.addEventListener('click', () => onCreateMonthlySummaryReport());
        monthMenu.appendChild(bySurvey);
        monthMenu.appendChild(allSurveys);
        month.appendChild(monthButton);
        month.appendChild(monthMenu);

        const quarter = document.createElement('div');
        quarter.className = 'submenu2-container';
        quarter.style.flex = '1';
        const quarterButton = document.createElement('button');
        quarterButton.style.width = '100%';
        quarterButton.textContent = 'Отчёт за квартал';
        const quarterMenu = document.createElement('div');
        quarterMenu.className = 'submenu2';
        [1, 2, 3, 4].forEach((quarterNumber) => {
            const item = document.createElement('div');
            item.textContent = `${quarterNumber} квартал`;
            item.addEventListener('click', () => onCreateQuarterlyReport(quarterNumber));
            quarterMenu.appendChild(item);
        });
        quarter.appendChild(quarterButton);
        quarter.appendChild(quarterMenu);

        actions.appendChild(month);
        actions.appendChild(quarter);
        root.appendChild(actions);
        bodyHost.appendChild(root);
    }

    function renderSurveyAction(modalState) {
        const isCopy = modalState.content === 'copy';
        const isUpdate = modalState.content === 'update';
        const titleText = isCopy ? 'Копирование анкеты' : isUpdate ? 'Редактирование анкеты' : 'Удаление анкеты';
        const messageText = isCopy
            ? `Вы уверены, что хотите создать копию анкеты "${modalState.data?.name_survey}"?`
            : isUpdate
                ? `Вы переходите к редактированию анкеты "${modalState.data?.name_survey}".`
                : `Вы уверены, что хотите удалить анкету "${modalState.data?.name_survey}"?`;
        const okText = isCopy ? 'Копировать' : isUpdate ? 'Продолжить' : 'Удалить';
        const onConfirm = isCopy ? onCopySurvey : isUpdate ? onUpdateSurvey : onDeleteSurvey;
        const root = document.createElement('div');
        const header = createDialogSection('modal-header', '');
        const title = document.createElement('h2');
        title.className = 'h2_modal';
        title.textContent = titleText;
        header.replaceChildren(title);
        const body = createDialogSection('modal-body');
        const message = createDialogSection('modal-message', messageText);
        body.appendChild(message);
        const footer = document.createElement('div');
        footer.className = 'modal-footer';
        const cancel = document.createElement('button');
        cancel.className = 'modal_btn modal_btn-secondary';
        cancel.textContent = 'Отмена';
        cancel.addEventListener('click', onClose);
        const confirm = document.createElement('button');
        confirm.className = 'modal_btn modal_btn-primary';
        confirm.textContent = okText;
        confirm.addEventListener('click', onConfirm);
        footer.appendChild(cancel);
        footer.appendChild(confirm);
        appendDialog(root, header, body, footer);
        bodyHost.appendChild(root);
    }

    function renderMessage(modalState) {
        const root = document.createElement('div');
        const header = createDialogSection('modal-header', '');
        const title = document.createElement('h2');
        title.className = 'h2_modal';
        title.textContent = modalState.isSuccess ? 'Успешно' : 'Ошибка';
        header.replaceChildren(title);
        const body = document.createElement('div');
        body.className = 'modal-body';
        const message = createDialogSection(
            `modal-message ${modalState.isSuccess ? 'success-message' : 'error-message'}`,
            modalState.message || ''
        );
        body.appendChild(message);
        const footer = document.createElement('div');
        footer.className = 'modal-footer';
        const confirm = document.createElement('button');
        confirm.className = 'modal_btn modal_btn-primary';
        confirm.textContent = 'OK';
        confirm.addEventListener('click', onClose);
        footer.appendChild(confirm);
        appendDialog(root, header, body, footer);
        bodyHost.appendChild(root);
    }

    function render(modalState) {
        modalNode.className = 'modal';
        modalNode.setAttribute('aria-hidden', 'true');
        if (typeof cleanup === 'function') {
            cleanup();
            cleanup = null;
        }
        bodyHost.replaceChildren();
        if (!modalState.isOpen) {
            syncPageState();
            return;
        }

        if (modalState.content === 'extend') {
            const mountExtensionModal = getExtensionModalMount();
            if (typeof mountExtensionModal === 'function') {
                cleanup = mountExtensionModal(bodyHost, { survey: modalState.data, onClose }) || null;
            } else {
                const message = document.createElement('div');
                message.textContent = 'Модуль продления не загружен.';
                bodyHost.appendChild(message);
            }
        } else if (modalState.content === 'report') {
            renderReport(modalState);
        } else if (['copy', 'update', 'delete'].includes(modalState.content)) {
            renderSurveyAction(modalState);
        } else if (modalState.content === 'message') {
            renderMessage(modalState);
        } else {
            return;
        }

        reveal();
    }

    modalClose.addEventListener('click', onClose);

    return {
        render,
        destroy() {
            if (typeof cleanup === 'function') {
                cleanup();
            }
            modalNode.remove();
        }
    };
}
