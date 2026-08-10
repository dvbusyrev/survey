import {
    applySurveySignedState,
    getAnswersPageContainer,
    getFillPageContainer
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
    const message = rawMessage || 'Не удалось выполнить операцию с CryptoPro Browser plug-in.';

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

function createElement(tagName, options = {}) {
    return window.AppUi.createElement(tagName, options);
}

function notifySignature(message, type = 'error', options = {}) {
    const safeMessage = typeof window.normalizeClientErrorMessage === 'function'
        ? window.normalizeClientErrorMessage(message)
        : message;

    window.AppUi?.notify?.(safeMessage || 'Не удалось выполнить операцию с подписью.', type, {
        title: type === 'success' ? 'Успешно' : 'Ошибка',
        ...options
    });
}

function createSignatureModalFrame(titleText, options = {}) {
    if (typeof window.createSiteModalFrame !== 'function') {
        throw new Error('Модальное окно подписи недоступно.');
    }

    const frame = window.createSiteModalFrame({
        title: titleText,
        className: ['signature-modal', options.className || ''].filter(Boolean).join(' '),
        bodyClassName: ['signature-modal__body', options.bodyClassName || ''].filter(Boolean).join(' '),
        onClose: options.onClose,
        footerButtons: options.footerButtons || []
    });

    document.body.appendChild(frame.modal);
    return frame;
}

function closeSignatureModal(frame) {
    frame?.hide?.();
    frame?.modal?.remove?.();
}

function appendStrongText(parent, tagName, labelText, valueText) {
    const row = createElement(tagName, {
        children: [
            createElement('strong', { text: labelText }),
            createElement('span', { text: ` ${valueText}` })
        ]
    });
    parent.appendChild(row);
    return row;
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
            existing.addEventListener('error', () => reject(new Error(`Не удалось загрузить скрипт ${src}.`)), { once: true });
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
        script.onerror = () => reject(new Error(`Не удалось загрузить скрипт ${src}.`));
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
            notifySignature('Анкета уже подписана и не может быть подписана повторно.');
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
        notifySignature(normalizedError.message);
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
        throw new Error(error || 'Не удалось получить данные для подписи.');
    }

    const contentType = String(response.headers.get('content-type') || '').toLowerCase();
    if (contentType.includes('application/json')) {
        return await response.json();
    }

    return await response.text();
}

async function showCertificateSelectionDialog(certificates) {
    return new Promise((resolve) => {
        let frame = null;
        let isResolved = false;
        const finish = (value) => {
            if (isResolved) {
                return;
            }

            isResolved = true;
            closeSignatureModal(frame);
            resolve(value);
        };

        frame = createSignatureModalFrame('Выберите сертификат для подписи', {
            className: 'signature-certificate-modal',
            onClose: () => finish(null),
            footerButtons: [
                {
                    variant: 'secondary',
                    text: 'Отмена',
                    onClick: (event) => {
                        event.preventDefault();
                        finish(null);
                    }
                }
            ]
        });
        frame.modal.addEventListener('site-modal:hidden', () => finish(null));

        const { body } = frame;
        const listContainer = createElement('div', { className: 'cert-list-container' });
        const certList = createElement('div', { className: 'cert-list' });

        certificates.forEach(cert => {
            const certItem = createElement('button', {
                type: 'button',
                className: 'cert-item',
                dataset: { index: cert.index },
                events: {
                    click: () => finish(cert)
                }
            });

            const subject = createElement('div', {
                className: 'cert-subject',
                text: cert.subject
            });

            const details = createElement('div', { className: 'cert-details' });

            appendStrongText(details, 'div', 'Издатель:', cert.issuer);

            appendStrongText(
                details,
                'div',
                'Действителен:',
                `${new Date(cert.validFrom).toLocaleDateString()} - ${new Date(cert.validTo).toLocaleDateString()}`
            );

            appendStrongText(details, 'div', 'Отпечаток:', cert.thumbprint);
            certItem.appendChild(subject);
            certItem.appendChild(details);
            certList.appendChild(certItem);
        });

        listContainer.appendChild(certList);
        body.appendChild(listContainer);
        frame.show();
    });
}

// Создание подписи
async function createDigitalSignature(data) {
    try {

        const certificates = await listAllCertificates();

        if (certificates.length === 0) {
            throw new Error('Нет доступных сертификатов.');
        }


        const selectedCert = await showCertificateSelectionDialog(certificates);

        if (!selectedCert) {
            throw new Error('Сертификат не выбран.');
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
        throw new Error(error || 'Не удалось сохранить подпись.');
    }
}


function updateUISuccess(mode = 'answer', source = document) {
    applySurveySignedState(source || document, true, mode);
    notifySignature('Документ успешно подписан.', 'success');
}
