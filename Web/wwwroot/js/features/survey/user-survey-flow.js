const CADESCOM_CONTAINER_STORE = 100;
const CAPICOM_STORE_OPEN_READ_ONLY = 0;
const CADESCOM_CADES_BES = 1;
const CADESCOM_BASE64_TO_BINARY = 1;

let cadesPluginLoadPromise = null;

function isEmbeddedBrowserEnvironment() {
    const userAgent = String(window.navigator.userAgent || '');
    const vendor = String(window.navigator.vendor || '');

    return /Electron|WebView|; wv\)|QtWebEngine|QtWebKit|Slack|Teams/i.test(userAgent)
        || (userAgent.includes('Macintosh') && vendor === 'Apple Computer, Inc.' && !/Safari\//i.test(userAgent));
}

function getCryptoProUnavailableMessage() {
    if (isEmbeddedBrowserEnvironment()) {
        return 'Подпись через CryptoPro Browser plug-in не поддерживается во встроенном браузере. Откройте систему в Chrome, Edge, Яндекс.Браузере или Safari с установленным CryptoPro Browser plug-in.';
    }

    return 'CryptoPro Browser plug-in недоступен. Проверьте, что расширение и КриптоПРО CSP установлены в поддерживаемом браузере.';
}

function extractErrorMessage(error) {
    if (typeof error === 'string') {
        return error.trim();
    }

    if (error instanceof Error) {
        return String(error.message || '').trim();
    }

    if (error && typeof error === 'object' && 'message' in error) {
        return String(error.message || '').trim();
    }

    return '';
}

function normalizeCryptoProError(error) {
    const rawMessage = extractErrorMessage(error);
    const message = rawMessage || 'Ошибка при работе с CryptoPro Browser plug-in.';

    if (isEmbeddedBrowserEnvironment()) {
        return {
            message: getCryptoProUnavailableMessage(),
            showInstallHelp: true
        };
    }

    if (/нет доступных сертификатов/i.test(message)) {
        return {
            message: 'Не найдено ни одного доступного сертификата для подписи.',
            showInstallHelp: false
        };
    }

    if (/сертификат не выбран/i.test(message)) {
        return {
            message: 'Сертификат для подписи не выбран.',
            showInstallHelp: false
        };
    }

    if (/истекло время ожидания загрузки плагина/i.test(message)) {
        return {
            message: 'CryptoPro Browser plug-in не ответил. Обычно это означает, что расширение не установлено, выключено в браузере или страница открыта во встроенном браузере/вебвью, где CryptoPro не работает.',
            showInstallHelp: true
        };
    }

    if (/плагин недоступен|ошибка при загрузке плагина|chrome-extension:\/\/invalid/i.test(message)) {
        return {
            message: 'CryptoPro Browser plug-in не установлен, отключен или не может загрузиться в текущем браузере. Проверьте расширение, КриптоПРО CSP и откройте страницу во внешнем поддерживаемом браузере.',
            showInstallHelp: true
        };
    }

    if (/не удалось загрузить скрипт/i.test(message)) {
        return {
            message: 'Не удалось загрузить модуль подписи CryptoPro со страницы приложения.',
            showInstallHelp: false
        };
    }

    if (/CAdESCOM|CreateObjectAsync|объект/i.test(message)) {
        return {
            message: 'CryptoPro установлен, но браузер не смог создать объекты плагина. Проверьте версию КриптоПРО CSP и расширение.',
            showInstallHelp: true
        };
    }

    return {
        message,
        showInstallHelp: false
    };
}

function loadScriptOnce(src) {
    return new Promise((resolve, reject) => {
        const existing = document.querySelector(`script[data-dynamic-src="${src}"]`);
        if (existing) {
            if (existing.dataset.loaded === 'true') {
                resolve();
                return;
            }
            existing.addEventListener('load', () => resolve(), { once: true });
            existing.addEventListener('error', () => reject(new Error(`Не удалось загрузить скрипт ${src}`)), { once: true });
            return;
        }

        const script = document.createElement('script');
        script.src = src;
        script.async = true;
        script.dataset.dynamicSrc = src;
        script.onload = () => {
            script.dataset.loaded = 'true';
            resolve();
        };
        script.onerror = () => reject(new Error(`Не удалось загрузить скрипт ${src}`));
        document.head.appendChild(script);
    });
}

async function ensureCadesPluginLoaded() {
    if (isEmbeddedBrowserEnvironment()) {
        throw new Error(getCryptoProUnavailableMessage());
    }

    if (typeof window.cadesplugin !== 'undefined') {
        await window.cadesplugin;
        return window.cadesplugin;
    }

    if (!cadesPluginLoadPromise) {
        cadesPluginLoadPromise = loadScriptOnce('/js/cadesplugin_api.js').then(async () => {
            if (typeof window.cadesplugin === 'undefined') {
                throw new Error(getCryptoProUnavailableMessage());
            }
            await window.cadesplugin;
            return window.cadesplugin;
        });
    }

    return cadesPluginLoadPromise;
}

async function CSP(id, organizationId, options = {}) {
    try {
        const signatureMode = options.mode === 'draft' ? 'draft' : 'answer';
        const page = signatureMode === 'draft'
            ? getFillPageContainer(options.source || document)
            : getAnswersPageContainer(options.source || document);
        const signedDatasetKey = signatureMode === 'draft' ? 'isDraftSigned' : 'isSigned';
        if (page?.dataset[signedDatasetKey] === 'true') {
            showError('Анкета уже подписана и не может быть подписана повторно.');
            return;
        }

        if (typeof options.beforeSign === 'function') {
            await options.beforeSign();
        }

        await ensureCadesPluginLoaded();
        await checkCSPAvailable();

        const dataToSign = await getDataForSignature(id, organizationId, signatureMode);
        
        const signature = await createDigitalSignature(dataToSign);
        
        await sendSignatureToServer(id, organizationId, signature, dataToSign, signatureMode);
        
        updateUISuccess(signatureMode, page);
        if (signatureMode !== 'draft' && typeof window.refreshSurveyUserPageData === 'function') {
            await window.refreshSurveyUserPageData({ preserveFilters: true });
        }
    } catch (error) {
        console.error("Ошибка в CSP:", error);
        const normalizedError = normalizeCryptoProError(error);
        showError(normalizedError.message);
    }
}

window.CSP = CSP;

async function listAllCertificates() {
    try {
        const store = await cadesplugin.CreateObjectAsync("CAdESCOM.Store");
        await store.Open(CADESCOM_CONTAINER_STORE, "My", CAPICOM_STORE_OPEN_READ_ONLY);
        
        const certs = await store.Certificates;
        const count = await certs.Count;
        
        const certificates = [];
        
        for (let i = 1; i <= count; i++) {
            const cert = await certs.Item(i);
            const subj = await cert.SubjectName;
            const issuer = await cert.IssuerName;
            const validFrom = await cert.ValidFromDate;
            const validTo = await cert.ValidToDate;
            const thumbprint = await cert.Thumbprint;
            
            
            certificates.push({
                index: i,
                subject: subj,
                issuer: issuer,
                validFrom: validFrom,
                validTo: validTo,
                thumbprint: thumbprint,
                certificate: cert
            });
        }
        
        return certificates;
    } catch (error) {
        console.error("Ошибка при перечислении сертификатов:", error);
        throw error;
    }
}

async function checkCSPAvailable() {
    await ensureCadesPluginLoaded();
    await cadesplugin.version;
    await cadesplugin.CreateObjectAsync("CAdESCOM.About");
    await cadesplugin.CreateObjectAsync("CAdESCOM.Store");
    return true;
}


async function getDataForSignature(id, organizationId, mode = 'answer') {
    const route = mode === 'draft' ? 'draft-signatures' : 'signatures';
    const response = await fetch(`/${route}/${id}/${organizationId}`);
    if (!response.ok) {
        const error = await response.text();
        throw new Error(error || 'Ошибка получения данных');
    }

    const contentType = String(response.headers.get('content-type') || '').toLowerCase();
    if (contentType.includes('application/json')) {
        return await response.json();
    }

    return await response.text();
}

async function showCertificateSelectionDialog(certificates) {
    return new Promise((resolve) => {
        const modal = document.createElement('div');
        modal.className = 'csp-modal';

        const content = document.createElement('div');
        content.className = 'csp-modal-content';
        const title = document.createElement('h3');
        title.textContent = 'Выберите сертификат для подписи';
        content.appendChild(title);

        const body = document.createElement('div');
        body.className = 'csp-modal-body';
        const listContainer = document.createElement('div');
        listContainer.className = 'cert-list-container';
        const certList = document.createElement('div');
        certList.className = 'cert-list';

        certificates.forEach(cert => {
            const certItem = document.createElement('div');
            certItem.className = 'cert-item';
            certItem.dataset.index = String(cert.index);

            const subject = document.createElement('div');
            subject.className = 'cert-subject';
            subject.textContent = cert.subject;

            const details = document.createElement('div');
            details.className = 'cert-details';

            const issuerRow = document.createElement('div');
            const issuerLabel = document.createElement('strong');
            issuerLabel.textContent = 'Издатель:';
            issuerRow.appendChild(issuerLabel);
            issuerRow.appendChild(document.createTextNode(` ${cert.issuer}`));

            const validityRow = document.createElement('div');
            const validityLabel = document.createElement('strong');
            validityLabel.textContent = 'Действителен:';
            validityRow.appendChild(validityLabel);
            validityRow.appendChild(
                document.createTextNode(
                    ` ${new Date(cert.validFrom).toLocaleDateString()} - ${new Date(cert.validTo).toLocaleDateString()}`
                )
            );

            const thumbprintRow = document.createElement('div');
            const thumbprintLabel = document.createElement('strong');
            thumbprintLabel.textContent = 'Отпечаток:';
            thumbprintRow.appendChild(thumbprintLabel);
            thumbprintRow.appendChild(document.createTextNode(` ${cert.thumbprint}`));

            details.appendChild(issuerRow);
            details.appendChild(validityRow);
            details.appendChild(thumbprintRow);
            certItem.appendChild(subject);
            certItem.appendChild(details);
            certList.appendChild(certItem);
        });

        listContainer.appendChild(certList);
        body.appendChild(listContainer);
        content.appendChild(body);

        const footer = document.createElement('div');
        footer.className = 'csp-modal-footer';
        const cancelButton = document.createElement('button');
        cancelButton.className = 'csp-btn csp-btn-secondary';
        cancelButton.id = 'cert-cancel';
        cancelButton.textContent = 'Отмена';
        footer.appendChild(cancelButton);
        content.appendChild(footer);
        modal.appendChild(content);
        
        modal.querySelectorAll('.cert-item').forEach(item => {
            item.addEventListener('click', () => {
                const index = parseInt(item.getAttribute('data-index'));
                const selectedCert = certificates.find(c => c.index === index);
                document.body.removeChild(modal);
                resolve(selectedCert);
            });

            item.addEventListener('mouseenter', () => {
                item.style.backgroundColor = '#f0f7ff';
            });
            item.addEventListener('mouseleave', () => {
                item.style.backgroundColor = '';
            });
        });
        
        modal.querySelector('#cert-cancel').addEventListener('click', () => {
            document.body.removeChild(modal);
            resolve(null);
        });

        document.body.appendChild(modal);
    });
}

// Создание подписи
async function createDigitalSignature(data) {
    try {

        const certificates = await listAllCertificates();
        
        if (certificates.length === 0) {
            throw new Error('Нет доступных сертификатов');
        }
        

        const selectedCert = await showCertificateSelectionDialog(certificates);
        
        if (!selectedCert) {
            throw new Error('Сертификат не выбран');
        }
        
        const signer = await cadesplugin.CreateObjectAsync("CAdESCOM.CPSigner");
        await signer.propset_Certificate(selectedCert.certificate);

        const signedData = await cadesplugin.CreateObjectAsync("CAdESCOM.CadesSignedData");
        const signaturePayload = typeof data === 'string'
            ? { content: data, contentEncoding: 'utf8', detached: false }
            : {
                content: data?.content || '',
                contentEncoding: data?.contentEncoding || 'utf8',
                detached: Boolean(data?.detached)
            };

        if (signaturePayload.contentEncoding === 'base64') {
            await signedData.propset_ContentEncoding(CADESCOM_BASE64_TO_BINARY);
        }

        await signedData.propset_Content(signaturePayload.content);

        return await signedData.SignCades(signer, CADESCOM_CADES_BES, signaturePayload.detached);
    } catch (error) {
        console.error("Ошибка при создании подписи:", error);
        throw error;
    }
}


async function getCertificateInfo(cert) {
    try {
        const subject = await cert.SubjectName;
        const issuer = await cert.IssuerName;
        const validFrom = await cert.ValidFromDate;
        const validTo = await cert.ValidToDate;
        
        return {
            subject,
            issuer,
            validFrom,
            validTo
        };
    } catch {
        return null;
    }
}


async function sendSignatureToServer(id, organizationId, signature, dataToSign, mode = 'answer') {
    const request = { signature };

    if (dataToSign && typeof dataToSign === 'object') {
        request.signedContent = dataToSign.content || '';
        request.contentEncoding = dataToSign.contentEncoding || 'utf8';
        request.detached = Boolean(dataToSign.detached);
    }

    const route = mode === 'draft' ? 'draft-signatures' : 'signatures';
    const response = await fetch(`/${route}/${id}/${organizationId}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request)
    });
    
    if (!response.ok) {
        const error = await response.text();
        throw new Error(error || 'Ошибка сервера');
    }
}

function showCSPInstallInstructions(reasonMessage = '') {
    const modal = document.createElement('div');
    modal.className = 'csp-modal';
    const content = document.createElement('div');
    content.className = 'csp-modal-content';
    const title = document.createElement('h3');
    title.textContent = 'Подпись недоступна';
    const body = document.createElement('div');
    body.className = 'csp-modal-body';
    const intro = document.createElement('p');
    intro.textContent = reasonMessage || getCryptoProUnavailableMessage();
    const steps = document.createElement('ol');

    if (isEmbeddedBrowserEnvironment()) {
        const step1 = document.createElement('li');
        step1.textContent = 'Откройте систему во внешнем браузере: Chrome, Edge, Яндекс.Браузер или Safari.';
        const step2 = document.createElement('li');
        const link1 = document.createElement('a');
        link1.href = 'https://www.cryptopro.ru/products/cades/plugin';
        link1.target = '_blank';
        link1.textContent = 'CryptoPro Browser plug-in';
        step2.appendChild(document.createTextNode('Проверьте установку '));
        step2.appendChild(link1);
        const step3 = document.createElement('li');
        const link2 = document.createElement('a');
        link2.href = 'https://www.cryptopro.ru/products/csp';
        link2.target = '_blank';
        link2.textContent = 'КриптоПРО CSP';
        step3.appendChild(document.createTextNode('Проверьте установку '));
        step3.appendChild(link2);
        steps.appendChild(step1);
        steps.appendChild(step2);
        steps.appendChild(step3);
    } else {
        const step1 = document.createElement('li');
        const link1 = document.createElement('a');
        link1.href = 'https://www.cryptopro.ru/products/cades/plugin';
        link1.target = '_blank';
        link1.textContent = 'CryptoPro Browser plug-in';
        step1.appendChild(document.createTextNode('Установите '));
        step1.appendChild(link1);
        const step2 = document.createElement('li');
        const link2 = document.createElement('a');
        link2.href = 'https://www.cryptopro.ru/products/csp';
        link2.target = '_blank';
        link2.textContent = 'КриптоПРО CSP';
        step2.appendChild(document.createTextNode('Установите '));
        step2.appendChild(link2);
        step2.appendChild(document.createTextNode(' версии 4.0 и выше.'));
        steps.appendChild(step1);
        steps.appendChild(step2);
    }

    body.appendChild(intro);
    body.appendChild(steps);
    const footer = document.createElement('div');
    footer.className = 'csp-modal-footer';
    const closeButton = document.createElement('button');
    closeButton.className = 'csp-modal-close';
    closeButton.textContent = 'Закрыть';
    footer.appendChild(closeButton);
    content.appendChild(title);
    content.appendChild(body);
    content.appendChild(footer);
    modal.appendChild(content);

    modal.querySelector('.csp-modal-close').addEventListener('click', () => {
        document.body.removeChild(modal);
    });

    document.body.appendChild(modal);
}



async function showCertConfirmDialog(certInfo) {
    return new Promise((resolve) => {
        const modal = document.createElement('div');
        modal.className = 'csp-modal';
        
        const content = document.createElement('div');
        content.className = 'csp-modal-content';
        const title = document.createElement('h3');
        title.textContent = 'Подтверждение сертификата';
        const body = document.createElement('div');
        body.className = 'csp-modal-body';

        if (certInfo) {
            const certDetails = document.createElement('div');
            certDetails.className = 'cert-details';
            const owner = document.createElement('p');
            const ownerStrong = document.createElement('strong');
            ownerStrong.textContent = 'Владелец:';
            owner.appendChild(ownerStrong);
            owner.appendChild(document.createTextNode(` ${certInfo.subject}`));
            const issuer = document.createElement('p');
            const issuerStrong = document.createElement('strong');
            issuerStrong.textContent = 'Издатель:';
            issuer.appendChild(issuerStrong);
            issuer.appendChild(document.createTextNode(` ${certInfo.issuer}`));
            const validity = document.createElement('p');
            const validityStrong = document.createElement('strong');
            validityStrong.textContent = 'Действителен:';
            validity.appendChild(validityStrong);
            validity.appendChild(document.createTextNode(` ${certInfo.validFrom} - ${certInfo.validTo}`));
            certDetails.appendChild(owner);
            certDetails.appendChild(issuer);
            certDetails.appendChild(validity);
            body.appendChild(certDetails);
        } else {
            const missingInfo = document.createElement('p');
            missingInfo.textContent = 'Информация о сертификате недоступна';
            body.appendChild(missingInfo);
        }

        const question = document.createElement('p');
        question.textContent = 'Вы подтверждаете использование этого сертификата для подписи?';
        body.appendChild(question);

        const footer = document.createElement('div');
        footer.className = 'csp-modal-footer';
        const cancelButton = document.createElement('button');
        cancelButton.className = 'csp-btn csp-btn-secondary';
        cancelButton.id = 'cert-cancel';
        cancelButton.textContent = 'Отмена';
        const confirmButton = document.createElement('button');
        confirmButton.className = 'csp-btn csp-btn-primary';
        confirmButton.id = 'cert-confirm';
        confirmButton.textContent = 'Подписать';
        footer.appendChild(cancelButton);
        footer.appendChild(confirmButton);

        content.appendChild(title);
        content.appendChild(body);
        content.appendChild(footer);
        modal.appendChild(content);

        modal.querySelector('#cert-confirm').addEventListener('click', () => {
            document.body.removeChild(modal);
            resolve(true);
        });

        modal.querySelector('#cert-cancel').addEventListener('click', () => {
            document.body.removeChild(modal);
            resolve(false);
        });

        document.body.appendChild(modal);
    });
}


function updateUISuccess(mode = 'answer', source = document) {
    applySignedState(source || document, true, mode);

    if (typeof window.siteNotify === 'function') {
        window.siteNotify('Документ успешно подписан', 'success', { title: 'Успешно' });
        return;
    }
    
    const notification = document.createElement('div');
    notification.className = 'csp-notification success';
    const icon = document.createElement('span');
    icon.className = 'csp-notification-icon';
    icon.textContent = '✓';
    const text = document.createElement('span');
    text.className = 'csp-notification-text';
    text.textContent = 'Документ успешно подписан';
    notification.appendChild(icon);
    notification.appendChild(text);
    
    document.body.appendChild(notification);
    
    setTimeout(() => {
        notification.classList.add('fade-out');
        setTimeout(() => notification.remove(), 300);
    }, 5000);
}

window.createAnswerReport = function createAnswerReport(idSurvey, organizationId, type) {
    window.AppScrollState?.prepareNavigation({ carry: true });
    window.location.assign(`/answers/${idSurvey}/${organizationId}/report/${type}`);
};

function getAnswersPageContainer(source) {
    if (source instanceof Element) {
        const closestPage = source.closest('[data-role="survey-answers-page"], [data-page="answers-check"]');
        if (closestPage) {
            return closestPage;
        }
    }

    if (source && typeof source.querySelector === 'function') {
        const nestedPage = source.querySelector('[data-role="survey-answers-page"], [data-page="answers-check"]');
        if (nestedPage) {
            return nestedPage;
        }
    }

    return document.querySelector('[data-role="survey-answers-page"], [data-page="answers-check"]');
}

function getFillPageContainer(source) {
    if (source instanceof Element) {
        const closestPage = source.closest('[data-role="survey-fill-page"]');
        if (closestPage) {
            return closestPage;
        }
    }

    if (source && typeof source.querySelector === 'function') {
        const nestedPage = source.querySelector('[data-role="survey-fill-page"]');
        if (nestedPage) {
            return nestedPage;
        }
    }

    return document.querySelector('[data-role="survey-fill-page"]');
}

function applySignedState(source, isSigned, mode = 'answer') {
    const isDraftMode = mode === 'draft';
    const page = isDraftMode
        ? getFillPageContainer(source)
        : getAnswersPageContainer(source);
    if (!page) {
        return;
    }

    if (isDraftMode) {
        page.dataset.isDraftSigned = isSigned ? 'true' : 'false';
    } else {
        page.dataset.isSigned = isSigned ? 'true' : 'false';
    }

    const signatureInfo = page.querySelector('[data-role="signature-info"]');
    const signatureStatus = page.querySelector('[data-role="signature-status"]');
    if (signatureInfo) {
        signatureInfo.classList.remove('u-hidden', 'is-hidden');
    }

    if (signatureStatus) {
        signatureStatus.textContent = isSigned ? 'Подписана' : 'Нет подписи';
        signatureStatus.classList.toggle('signed', isSigned);
        signatureStatus.classList.toggle('not-signed', !isSigned);
    }

    const signButtons = isDraftMode
        ? new Set([
            ...page.querySelectorAll('[data-role="draft-sign-button"]'),
            ...document.querySelectorAll('[data-role="draft-sign-button"]')
        ])
        : new Set([
            ...page.querySelectorAll('[data-role="sign-button"], [data-role-sign-button="true"]'),
            ...document.querySelectorAll('[data-role="sign-button"][data-survey-id], [data-role-sign-button="true"][data-survey-id]')
        ]);

    signButtons.forEach((signButton) => {
        if (signButton instanceof HTMLButtonElement) {
            signButton.disabled = isSigned;
            signButton.textContent = isSigned ? 'Подписано' : 'Подписать';
        }
    });
}

window.downloadAnswerDocument = function downloadAnswerDocument(surveyId, organizationId, triggerElement) {
    const page = getAnswersPageContainer(triggerElement);
    const isSigned = page?.dataset.isSigned === 'true';

    if (isSigned) {
        return window.downloadSignedArchive(surveyId, organizationId);
    }

    return window.createPdfReport(surveyId, organizationId);
};

function showError(message) {
    const safeMessage = typeof window.normalizeClientErrorMessage === 'function'
        ? window.normalizeClientErrorMessage(message)
        : message;
    if (typeof window.siteNotify === 'function') {
        window.siteNotify(safeMessage, 'error', { title: 'Ошибка' });
        return;
    }

    const notification = document.createElement('div');
    notification.className = 'csp-notification error';
    const icon = document.createElement('span');
    icon.className = 'csp-notification-icon';
    icon.textContent = '!';
    const text = document.createElement('span');
    text.className = 'csp-notification-text';
    text.textContent = safeMessage;
    notification.appendChild(icon);
    notification.appendChild(text);
    
    document.body.appendChild(notification);
    
    setTimeout(() => {
        notification.classList.add('fade-out');
        setTimeout(() => notification.remove(), 300);
    }, 5000);
}

function createHtmlFragment(html) {
    const range = document.createRange();
    range.selectNode(document.body);
    return range.createContextualFragment(html);
}

function renderHostError(host, message) {
    const errorNode = document.createElement('div');
    errorNode.className = 'error-message';
    errorNode.textContent = typeof window.normalizeClientErrorMessage === 'function'
        ? window.normalizeClientErrorMessage(message)
        : message;
    host.replaceChildren(errorNode);
}

function createSurveyModalFooterButton({ role, text, variant = 'secondary', disabled = false, labelRole = '' }) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = `modal_btn modal_btn-${variant}`;
    button.dataset.role = role;
    button.disabled = disabled;

    if (labelRole) {
        const label = document.createElement('span');
        label.dataset.role = labelRole;
        label.textContent = text;
        button.appendChild(label);
    } else {
        button.textContent = text;
    }

    return button;
}

function clearSurveyModalFooter(footerHost) {
    footerHost?.replaceChildren();
}

async function fetchModalContentHtml(url, fallbackMessage) {
    const response = await fetch(url, {
        headers: {
            'X-Requested-With': 'XMLHttpRequest'
        }
    });

    if (!response.ok) {
        throw new Error(fallbackMessage);
    }

    return response.text();
}

window.fetchSurveyFillContentHtml = function fetchSurveyFillContentHtml(surveyId, organizationId) {
    return fetchModalContentHtml(
        `/surveys/${surveyId}/organizations/${organizationId}/fill-content`,
        'Не удалось загрузить анкету'
    );
};

window.fetchSurveyAnswersContentHtml = function fetchSurveyAnswersContentHtml(surveyId, organizationId) {
    return fetchModalContentHtml(
        `/answers/${surveyId}/${organizationId}/content`,
        'Не удалось загрузить ответы по анкете'
    );
};

window.mountSurveyFillPage = function mountSurveyFillPage(host, { survey, organizationId, userRole, onBack, onSubmitted, initialHtml, footerHost }) {
    if (!host) {
        return null;
    }

    let destroyed = false;
    const answers = {};
    let loading = false;
    let error = null;
    let draftSaveTimer = 0;
    let refs = {
        page: null,
        errorBlock: null,
        errorText: null,
        draftSignButton: null,
        submitButton: null,
        submitLabel: null,
        cancelButton: null
    };

    function getQuestionNodes() {
        return Array.from(host.querySelectorAll('[data-role="survey-question"]'));
    }

    function getCurrentSurveyId() {
        const rawValue = refs.page?.dataset.surveyId
            || host.querySelector('[data-role="survey-fill-page"]')?.dataset.surveyId
            || survey?.id_survey
            || survey?.IdSurvey
            || survey?.idSurvey
            || survey?.Id
            || survey?.id
            || 0;
        const numericValue = Number(rawValue);
        return Number.isFinite(numericValue) ? numericValue : 0;
    }

    function renderError() {
        if (!refs.errorBlock || !refs.errorText) {
            return;
        }

        if (error) {
            refs.errorText.textContent = error;
            refs.errorBlock.classList.remove('u-hidden');
            return;
        }

        refs.errorText.textContent = '';
        refs.errorBlock.classList.add('u-hidden');
    }

    function renderSubmitState() {
        if (!refs.submitButton || !refs.submitLabel) {
            return;
        }

        refs.submitButton.disabled = loading;
        refs.submitButton.querySelector('.loading-spinner')?.remove();

        if (loading) {
            const spinner = document.createElement('span');
            spinner.className = 'loading-spinner';
            refs.submitButton.insertBefore(spinner, refs.submitLabel);
            refs.submitLabel.textContent = 'Отправка...';
            return;
        }

        refs.submitLabel.textContent = 'Отправить ответы';
    }

    function buildPayloadAnswers({ requireComplete = false } = {}) {
        const payloadAnswers = [];
        const questionNodes = getQuestionNodes();

        questionNodes.forEach((questionNode) => {
            const questionId = questionNode.dataset.questionId || '';
            const questionText = questionNode.querySelector('[data-role="question-title"]')?.textContent?.trim() || '';
            const answer = answers[questionId] || {};
            const rating = Number(answer.rating || 0);
            const comment = String(answer.comment || '').trim();

            if (!rating && !comment && !requireComplete) {
                return;
            }

            if (requireComplete && (!Number.isFinite(rating) || rating < 1 || rating > 5)) {
                throw new Error('Необходимо ответить на все вопросы анкеты.');
            }

            if (requireComplete && rating < 5 && !comment) {
                throw new Error('Для оценки ниже 5 требуется комментарий.');
            }

            payloadAnswers.push({
                question_id: questionId,
                question_text: questionText,
                rating: rating || null,
                comment
            });
        });

        if (requireComplete && payloadAnswers.length !== questionNodes.length) {
            throw new Error('Необходимо ответить на все вопросы анкеты.');
        }

        return payloadAnswers;
    }

    function updateDraftSignedState(isSigned) {
        if (refs.page) {
            refs.page.dataset.isDraftSigned = isSigned ? 'true' : 'false';
        }

        applySignedState(refs.page || host, isSigned, 'draft');
    }

    async function saveDraft({ showErrorOnFailure = false } = {}) {
        const payloadAnswers = buildPayloadAnswers();
        if (payloadAnswers.length === 0) {
            return true;
        }

        const surveyId = getCurrentSurveyId();
        if (surveyId <= 0 || organizationId <= 0) {
            const message = 'Не удалось определить анкету для сохранения черновика.';
            if (showErrorOnFailure) {
                throw new Error(message);
            }

            console.error(message);
            return false;
        }

        const response = await fetch('/answers/draft', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: JSON.stringify({
                id_survey: surveyId,
                id_organization: organizationId,
                answers: payloadAnswers
            })
        });

        if (!response.ok) {
            const errorData = await response.json().catch(() => null);
            const message = errorData?.error || 'Ошибка при сохранении черновика';
            if (showErrorOnFailure) {
                throw new Error(message);
            }

            console.error(message);
            return false;
        }

        return true;
    }

    function scheduleDraftSave() {
        if (draftSaveTimer) {
            window.clearTimeout(draftSaveTimer);
        }

        draftSaveTimer = window.setTimeout(() => {
            draftSaveTimer = 0;
            saveDraft().catch((err) => console.error('Ошибка при сохранении черновика:', err));
        }, 450);
    }

    function renderFooter() {
        if (!footerHost) {
            return {};
        }

        const isDraftSigned = refs.page?.dataset.isDraftSigned === 'true'
            || host.querySelector('[data-role="survey-fill-page"]')?.dataset.isDraftSigned === 'true';
        const signButton = createSurveyModalFooterButton({
            role: 'draft-sign-button',
            text: isDraftSigned ? 'Подписано' : 'Подписать',
            variant: 'primary',
            disabled: isDraftSigned
        });
        const cancelButton = createSurveyModalFooterButton({
            role: 'cancel-btn',
            text: 'Отмена',
            variant: 'secondary'
        });
        const submitButton = createSurveyModalFooterButton({
            role: 'submit',
            text: 'Отправить ответы',
            variant: 'primary',
            labelRole: 'submit-label'
        });

        signButton.classList.add('survey-user-modal__footer-left');
        footerHost.replaceChildren(signButton, cancelButton, submitButton);

        return {
            draftSignButton: signButton,
            cancelButton,
            submitButton,
            submitLabel: submitButton.querySelector('[data-role="submit-label"]')
        };
    }

    function updateQuestionState(questionId, questionElement) {
        const answer = answers[questionId] || {};

        questionElement.querySelectorAll('[data-role="rating-button"]').forEach((button) => {
            const rating = Number(button.dataset.rating || 0);
            button.classList.toggle('active', answer.rating === rating);
        });

        const commentBlock = questionElement.querySelector('[data-role="comment-block"]');
        const commentInput = questionElement.querySelector('[data-role="comment-input"]');
        const showComment = answer.rating > 0 && answer.rating < 5;

        if (commentBlock) {
            commentBlock.classList.toggle('u-hidden', !showComment);
        }

        if (commentInput) {
            commentInput.value = answer.comment || '';
        }
    }

    function bindQuestion(questionElement) {
        const questionId = questionElement.dataset.questionId || '';
        if (!questionId) {
            return;
        }

        const activeButton = questionElement.querySelector('[data-role="rating-button"].active');
        const activeRating = Number(activeButton?.dataset.rating || 0);
        const commentInput = questionElement.querySelector('[data-role="comment-input"]');
        if (activeRating > 0 || commentInput?.value) {
            answers[questionId] = {
                rating: activeRating || null,
                comment: commentInput?.value || ''
            };
        }

        questionElement.querySelectorAll('[data-role="rating-button"]').forEach((button) => {
            button.addEventListener('click', () => {
                error = null;
                const rating = Number(button.dataset.rating || 0);
                answers[questionId] = {
                    ...answers[questionId],
                    rating,
                    comment: rating < 5 ? answers[questionId]?.comment || '' : ''
                };
                updateDraftSignedState(false);
                renderError();
                updateQuestionState(questionId, questionElement);
                scheduleDraftSave();
            });
        });

        commentInput?.addEventListener('input', (event) => {
            error = null;
            answers[questionId] = {
                ...answers[questionId],
                comment: event.target.value
            };
            updateDraftSignedState(false);
            renderError();
            scheduleDraftSave();
        });

        updateQuestionState(questionId, questionElement);
    }

    async function submitAnswers() {
        try {
            loading = true;
            error = null;
            renderError();
            renderSubmitState();

            const payloadAnswers = buildPayloadAnswers({ requireComplete: true });
            const surveyId = getCurrentSurveyId();
            if (surveyId <= 0 || organizationId <= 0) {
                throw new Error('Не удалось определить анкету для отправки ответов.');
            }

            const response = await fetch('/answers/create', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify({
                    id_survey: surveyId,
                    id_organization: organizationId,
                    answers: payloadAnswers
                })
            });

            if (!response.ok) {
                const errorData = await response.json().catch(() => null);
                throw new Error(errorData?.error || 'Ошибка при отправке ответов');
            }

            await response.json().catch(() => null);
            onSubmitted?.({
                survey,
                answers: payloadAnswers,
                organizationId
            });
        } catch (err) {
            error = err?.message || 'Не удалось отправить ответы';
            renderError();
        } finally {
            loading = false;
            renderSubmitState();
        }
    }

    async function signDraft() {
        try {
            error = null;
            renderError();
            buildPayloadAnswers({ requireComplete: true });
            await saveDraft({ showErrorOnFailure: true });
            const surveyId = getCurrentSurveyId();
            if (surveyId <= 0 || organizationId <= 0) {
                throw new Error('Не удалось определить анкету для подписи.');
            }

            await CSP(surveyId, organizationId, {
                mode: 'draft',
                source: refs.page || host
            });
        } catch (err) {
            error = err?.message || 'Не удалось подписать черновик';
            renderError();
            showError(error);
        }
    }

    function bindPage() {
        host.querySelector('[data-role="body-actions"]')?.classList.add('u-hidden');

        refs = {
            page: host.querySelector('[data-role="survey-fill-page"]'),
            errorBlock: host.querySelector('[data-role="error"]'),
            errorText: host.querySelector('[data-role="error-text"]'),
            draftSignButton: null,
            submitButton: null,
            submitLabel: null,
            cancelButton: null
        };
        const footerRefs = renderFooter();
        refs.draftSignButton = footerRefs.draftSignButton || host.querySelector('[data-role="draft-sign-button"]');
        refs.submitButton = footerRefs.submitButton || host.querySelector('[data-role="submit"]');
        refs.submitLabel = footerRefs.submitLabel || host.querySelector('[data-role="submit-label"]');
        refs.cancelButton = footerRefs.cancelButton || host.querySelector('[data-role="cancel-btn"]');

        refs.draftSignButton?.addEventListener('click', signDraft);
        refs.submitButton?.addEventListener('click', submitAnswers);
        refs.cancelButton?.addEventListener('click', () => onBack?.());
        getQuestionNodes().forEach(bindQuestion);
        updateDraftSignedState(refs.page?.dataset.isDraftSigned === 'true');
        renderError();
        renderSubmitState();
    }

    const loadFillContent = async () => {
        try {
            const html = typeof initialHtml === 'string'
                ? initialHtml
                : await window.fetchSurveyFillContentHtml(getCurrentSurveyId(), organizationId);
            if (destroyed) {
                return;
            }

            host.replaceChildren(createHtmlFragment(html));
            bindPage();
        } catch (err) {
            if (destroyed) {
                return;
            }

            renderHostError(host, err?.message || 'Не удалось загрузить анкету');
            clearSurveyModalFooter(footerHost);
        }
    };

    loadFillContent();

    return () => {
        destroyed = true;
        if (draftSaveTimer) {
            window.clearTimeout(draftSaveTimer);
        }
        host.replaceChildren();
        clearSurveyModalFooter(footerHost);
    };
};

window.createPdfReport = async function(surveyId, organizationId) {
    try {
        const response = await fetch(`/answers/${surveyId}/${organizationId}/pdf`);
        if (!response.ok) throw new Error('Ошибка создания PDF');
        
        const blob = await response.blob();
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `Анкета_${surveyId}_${new Date().toISOString().slice(0,10)}.pdf`;
        document.body.appendChild(a);
        a.click();
        a.remove();
        window.URL.revokeObjectURL(url);
    } catch (error) {
        console.error('Ошибка при создании PDF:', error);
        showError('Не удалось создать PDF файл');
    }
}


// Функция для генерации PDF на клиенте
const generatePdf = (surveyData) => {
    // Используем jsPDF для генерации PDF прямо в браузере
    const { jsPDF } = window.jspdf;
    const doc = new jsPDF();
    
    // Заголовок
    doc.setFontSize(18);
    doc.text(`Анкета: ${surveyData.Survey.name_survey}`, 10, 10);
    doc.setFontSize(12);
    doc.text(`Дата заполнения: ${new Date().toLocaleDateString()}`, 10, 20);
    
    // Ответы
    let yPosition = 30;
    surveyData.Answers.forEach((answer, index) => {
        if (yPosition > 280) {
            doc.addPage();
            yPosition = 10;
        }
        
        doc.setFontSize(14);
        doc.text(`${index + 1}. ${answer.question_text}`, 10, yPosition);
        yPosition += 10;
        
        doc.setFontSize(12);
        doc.text(`Оценка: ${answer.rating}/5`, 15, yPosition);
        yPosition += 7;
        
        if (answer.comment) {
            const splitComments = doc.splitTextToSize(answer.comment, 180);
            doc.text(splitComments, 15, yPosition);
            yPosition += splitComments.length * 7;
        }
        
        yPosition += 10;
    });
    
    doc.save(`Анкета_${surveyData.Survey.id_survey}.pdf`);
};

// Функция для создания архива с подписью
const downloadSigned = async (surveyData) => {
    try {
        // Сначала генерируем PDF
        const pdfBlob = await generatePdfBlob(surveyData);
        
        // Создаем архив
        const zip = new JSZip();
        zip.file(`Анкета_${surveyData.Survey.id_survey}.pdf`, pdfBlob);
        zip.file(`Подпись_${surveyData.Survey.id_survey}.sig`, surveyData.signature);
        
        // Генерируем архив
        const content = await zip.generateAsync({ type: 'blob' });
        
        // Скачиваем
        const url = URL.createObjectURL(content);
        const a = document.createElement('a');
        a.href = url;
        a.download = `Анкета_с_подписью_${surveyData.Survey.id_survey}.zip`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    } catch (error) {
        console.error('Ошибка при создании архива:', error);
        alert('Не удалось создать архив');
    }
};



window.downloadSignedArchive = async function(surveyId, organizationId) {
    try {
        const response = await fetch(`/answers/${surveyId}/${organizationId}/signed-archive`);
        
        if (!response.ok) {
            const errorData = await response.json().catch(() => null);
            const errorMessage = errorData?.error || 'Ошибка загрузки архива';
            throw new Error(errorMessage);
        }
        
        const blob = await response.blob();
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `Анкета_с_подписью_${surveyId}.zip`;
        document.body.appendChild(a);
        a.click();
        
        document.body.removeChild(a);
        window.URL.revokeObjectURL(url);
    } catch (error) {
        console.error('Ошибка при загрузке архива:', error);
        
        const errorMessage = error.message || 'Не удалось загрузить архив с подписью';
        showError(errorMessage);
        
        if (error.details) {
            console.error('Детали ошибки:', error.details);
        }
    }
}


window.mountCheckAnswersPage = function mountCheckAnswersPage(host, { survey, organizationId, userRole, onBack, initialHtml, footerHost }) {
    if (!host) {
        return null;
    }

    let destroyed = false;

    function renderFooter(page, surveyId, currentOrganizationId) {
        if (!footerHost) {
            return {};
        }

        const isSigned = page?.dataset.isSigned === 'true';
        const signButton = createSurveyModalFooterButton({
            role: 'sign-button',
            text: isSigned ? 'Подписано' : 'Подписать',
            variant: 'primary',
            disabled: isSigned
        });
        const downloadButton = createSurveyModalFooterButton({
            role: 'download-btn',
            text: 'Скачать ответы',
            variant: 'secondary'
        });

        signButton.dataset.surveyId = String(surveyId || '');
        signButton.dataset.organizationId = String(currentOrganizationId || '');
        downloadButton.dataset.surveyId = String(surveyId || '');
        downloadButton.dataset.organizationId = String(currentOrganizationId || '');

        footerHost.replaceChildren(downloadButton, signButton);

        return {
            signButton,
            downloadButton
        };
    }

    function bindPage() {
        const page = host.querySelector('[data-role="survey-answers-page"]');
        const surveyId = Number(page?.dataset.surveyId || survey?.id_survey || survey?.idSurvey || survey?.Id || 0);
        const currentOrganizationId = Number(page?.dataset.organizationId || organizationId || 0);
        const footerRefs = renderFooter(page, surveyId, currentOrganizationId);
        host.querySelector('[data-role="body-actions"]')?.classList.add('u-hidden');

        const downloadButton = footerRefs.downloadButton || host.querySelector('[data-role="download-btn"]');
        const signButton = footerRefs.signButton || host.querySelector('[data-role="sign-actions"] button');

        downloadButton?.addEventListener('click', (event) => {
            event.preventDefault();
            if (surveyId > 0 && currentOrganizationId > 0) {
                window.downloadAnswerDocument(surveyId, currentOrganizationId, downloadButton);
            }
        });

        signButton?.addEventListener('click', (event) => {
            event.preventDefault();
            if (signButton.disabled) {
                return;
            }

            if (surveyId > 0 && currentOrganizationId > 0) {
                CSP(surveyId, currentOrganizationId);
            }
        });

    }

    const loadAnswersContent = async () => {
        try {
            const html = typeof initialHtml === 'string'
                ? initialHtml
                : await window.fetchSurveyAnswersContentHtml(survey.id_survey, organizationId);
            if (destroyed) {
                return;
            }

            host.replaceChildren(createHtmlFragment(html));
            bindPage();
        } catch (error) {
            console.error('Ошибка:', error);
            if (destroyed) {
                return;
            }

            renderHostError(host, error?.message || 'Не удалось загрузить ответы по анкете');
            clearSurveyModalFooter(footerHost);
        }
    };

    loadAnswersContent();

    return () => {
        destroyed = true;
        host.replaceChildren();
        clearSurveyModalFooter(footerHost);
    };
};
