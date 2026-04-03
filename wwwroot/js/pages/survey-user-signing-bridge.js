(function () {
const CADESCOM_CONTAINER_STORE = 100;
const CAPICOM_STORE_OPEN_READ_ONLY = 0;
const CAPICOM_CERTIFICATE_FIND_TIME_VALID = 9;
const CADESCOM_CADES_BES = 1;

let cadesPluginLoadPromise = null;

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
    if (typeof window.cadesplugin !== 'undefined') {
        await window.cadesplugin;
        return window.cadesplugin;
    }

    if (!cadesPluginLoadPromise) {
        cadesPluginLoadPromise = loadScriptOnce('/js/cadesplugin_api.js').then(async () => {
            if (typeof window.cadesplugin === 'undefined') {
                throw new Error('CAdESCOM плагин не загружен! Установите КриптоПРО ЭЦП Browser plug-in.');
            }
            await window.cadesplugin;
            return window.cadesplugin;
        });
    }

    return cadesPluginLoadPromise;
}

async function CSP(id, id_omsu) {
    console.log("Начало работы CSP");
    try {
        await ensureCadesPluginLoaded();

        if (!await checkCSPAvailable()) {
            console.error("CSP не доступен");
            showCSPInstallInstructions();
            return;
        }

        const dataToSign = await getDataForSignature(id, id_omsu);
        
        const signature = await createDigitalSignature(dataToSign);
        
        await sendSignatureToServer(id, id_omsu, signature);
        
        updateUISuccess();
    } catch (error) {
        console.error("Ошибка в CSP:", error);
        showError(error.message);
    }
}

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

async function listCertificates() {
    try {
        const store = await cadesplugin.CreateObjectAsync("CAdESCOM.Store");
        await store.Open(CADESCOM_CONTAINER_STORE, "My", CAPICOM_STORE_OPEN_READ_ONLY);
        
        const certs = await store.Certificates;
        const count = await certs.Count;
        
        for (let i = 1; i <= count; i++) {
            const cert = await certs.Item(i);
            const subj = await cert.SubjectName;
        }
    } catch (error) {
        console.error("Ошибка при перечислении сертификатов:", error);
    }
}

async function checkCSPAvailable() {
    try {
        await ensureCadesPluginLoaded();
        console.log
        ("1. Плагин обнаружен, версия:", await cadesplugin.version);

        const about = await cadesplugin.CreateObjectAsync("CAdESCOM.About");
        console.log("2. Объект About создан");

        const store = await cadesplugin.CreateObjectAsync("CAdESCOM.Store");
        console.log("3. Хранилище сертификатов доступно");

        return true;
    } catch (error) {
        console.error("❌ Ошибка при проверке CSP:", error);
        return false;
    }
}


async function getDataForSignature(id, id_omsu) {
    const response = await fetch(`/get_signing_data/${id}/${id_omsu}`);
    if (!response.ok) throw new Error('Ошибка получения данных');
    return await response.text();
}

async function showCertificateSelectionDialog(certificates) {
    return new Promise((resolve) => {
        const modal = document.createElement('div');
        modal.className = 'csp-modal';
        
        const certItems = certificates.map(cert => `
            <div class="cert-item" data-index="${cert.index}">
                <div class="cert-subject">${cert.subject}</div>
                <div class="cert-details">
                    <div><strong>Издатель:</strong> ${cert.issuer}</div>
                    <div><strong>Действителен:</strong> ${new Date(cert.validFrom).toLocaleDateString()} - ${new Date(cert.validTo).toLocaleDateString()}</div>
                    <div><strong>Отпечаток:</strong> ${cert.thumbprint}</div>
                </div>
            </div>
        `).join('');
        
        modal.innerHTML = `
            <div class="csp-modal-content">
                <h3>Выберите сертификат для подписи</h3>
                <div class="csp-modal-body">
                    <div class="cert-list-container">
                        <div class="cert-list">
                            ${certItems}
                        </div>
                    </div>
                </div>
                <div class="csp-modal-footer">
                    <button class="csp-btn csp-btn-secondary" id="cert-cancel">Отмена</button>
                </div>
            </div>
        `;
        
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
        await signedData.propset_Content(data);

        return await signedData.SignCades(signer, CADESCOM_CADES_BES);
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


async function sendSignatureToServer(id, id_omsu, signature) {
    const response = await fetch(`/csp/${id}/${id_omsu}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ signature })
    });
    
    if (!response.ok) {
        const error = await response.text();
        throw new Error(error || 'Ошибка сервера');
    }
}

function showCSPInstallInstructions() {
    const modal = document.createElement('div');
    modal.className = 'csp-modal';
    modal.innerHTML = `
        <div class="csp-modal-content">
            <h3>Требуется установка КриптоПРО</h3>
            <div class="csp-modal-body">
                <p>Для подписи документов необходимо:</p>
                <ol>
                    <li>Установить <a href="https://www.cryptopro.ru/products/cades/plugin" target="_blank">КриптоПРО ЭЦП Browser plug-in</a></li>
                    <li>Установить <a href="https://www.cryptopro.ru/products/csp" target="_blank">КриптоПРО CSP</a> (версия 4.0+)</li>
                    <li>Обновить страницу после установки</li>
                </ol>
            </div>
            <div class="csp-modal-footer">
                <button class="csp-modal-close">Закрыть</button>
            </div>
        </div>
    `;

    modal.querySelector('.csp-modal-close').addEventListener('click', () => {
        document.body.removeChild(modal);
    });

    document.body.appendChild(modal);
}



async function showCertConfirmDialog(certInfo) {
    return new Promise((resolve) => {
        const modal = document.createElement('div');
        modal.className = 'csp-modal';
        
        const certDetails = certInfo ? `
            <div class="cert-details">
                <p><strong>Владелец:</strong> ${certInfo.subject}</p>
                <p><strong>Издатель:</strong> ${certInfo.issuer}</p>
                <p><strong>Действителен:</strong> ${certInfo.validFrom} - ${certInfo.validTo}</p>
            </div>
        ` : '<p>Информация о сертификате недоступна</p>';

        modal.innerHTML = `
            <div class="csp-modal-content">
                <h3>Подтверждение сертификата</h3>
                <div class="csp-modal-body">
                    ${certDetails}
                    <p>Вы подтверждаете использование этого сертификата для подписи?</p>
                </div>
                <div class="csp-modal-footer">
                    <button class="csp-btn csp-btn-secondary" id="cert-cancel">Отмена</button>
                    <button class="csp-btn csp-btn-primary" id="cert-confirm">Подписать</button>
                </div>
            </div>
        `;

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


function updateUISuccess() {
    document.getElementById("block_btn_not_csp").style.display = "none";
    document.getElementById("block_btn_csp").style.display = "block";
    
    const notification = document.createElement('div');
    notification.className = 'csp-notification success';
    notification.innerHTML = `
        <span class="csp-notification-icon">✓</span>
        <span class="csp-notification-text">Документ успешно подписан</span>
    `;
    
    document.body.appendChild(notification);
    
    setTimeout(() => {
        notification.classList.add('fade-out');
        setTimeout(() => notification.remove(), 300);
    }, 5000);
}

function showError(message) {
    const notification = document.createElement('div');
    notification.className = 'csp-notification error';
    notification.innerHTML = `
        <span class="csp-notification-icon">!</span>
        <span class="csp-notification-text">${message}</span>
    `;
    
    document.body.appendChild(notification);
    
    setTimeout(() => {
        notification.classList.add('fade-out');
        setTimeout(() => notification.remove(), 300);
    }, 5000);
}

async function initCadesPlugin() {
    try {
        await ensureCadesPluginLoaded();

        if (typeof cadesplugin === 'undefined') {
            throw new Error('CAdESCOM плагин не загружен! Установите КриптоПРО ЭЦП Browser plug-in.');
        }

        await cadesplugin;

        const pluginVersion = await cadesplugin.version;
        console.log("Версия cadesplugin:", pluginVersion);

        return true;
    } catch (error) {
        console.error('Ошибка инициализации cadesplugin:', error);
        return false;
    }
}


async function initCSP() {
    try {
        await ensureCadesPluginLoaded();
        await window.cadesplugin;
        console.log('CryptoPRO готов к работе');
        return true;
    } catch (error) {
        console.error('Ошибка инициализации CSP:', error);
        return false;
    }
}


async function checkLicense() {
    try {
        const cadesAbout = await cadesplugin.CreateObjectAsync("CAdESCOM.About");
        const licenseStatus = await cadesAbout.LicenseStatus;
        console.log("Статус лицензии:", licenseStatus);
        return licenseStatus === 0; // 0 означает, что лицензия активна
    } catch (error) {
        console.error("Ошибка проверки лицензии:", error);
        return false;
    }
}

  window.CSP = CSP;
  window.listAllCertificates = listAllCertificates;
  window.listCertificates = listCertificates;
  window.checkCSPAvailable = checkCSPAvailable;
  window.getDataForSignature = getDataForSignature;
  window.showCertificateSelectionDialog = showCertificateSelectionDialog;
  window.createDigitalSignature = createDigitalSignature;
  window.getCertificateInfo = getCertificateInfo;
  window.sendSignatureToServer = sendSignatureToServer;
  window.showCSPInstallInstructions = showCSPInstallInstructions;
  window.updateUISuccess = updateUISuccess;
  window.showError = showError;
  window.initCadesPlugin = initCadesPlugin;
  window.initCSP = initCSP;
  window.checkLicense = checkLicense;
})();
