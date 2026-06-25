import {
    applySurveySignedState,
    getAnswersPageContainer,
    getFillPageContainer,
    showSurveyError
} from './user-survey-flow-shared.js';

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

export async function CSP(id, organizationId, options = {}) {
    try {
        const signatureMode = options.mode === 'draft' ? 'draft' : 'answer';
        const page = signatureMode === 'draft'
            ? getFillPageContainer(options.source || document)
            : getAnswersPageContainer(options.source || document);
        const signedDatasetKey = signatureMode === 'draft' ? 'isDraftSigned' : 'isSigned';
        if (page?.dataset[signedDatasetKey] === 'true') {
            showSurveyError('Анкета уже подписана и не может быть подписана повторно.');
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
        showSurveyError(normalizedError.message);
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
    applySurveySignedState(source || document, true, mode);

    if (typeof window.AppUi?.notify === 'function') {
        window.AppUi.notify('Документ успешно подписан', 'success', { title: 'Успешно' });
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
