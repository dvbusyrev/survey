const CADESCOM_CONTAINER_STORE = 100;
const CAPICOM_STORE_OPEN_READ_ONLY = 0;
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

async function CSP(id, organizationId) {
    try {
        await ensureCadesPluginLoaded();

        if (!await checkCSPAvailable()) {
            console.error("CSP не доступен");
            showCSPInstallInstructions();
            return;
        }

        const dataToSign = await getDataForSignature(id, organizationId);
        
        const signature = await createDigitalSignature(dataToSign);
        
        await sendSignatureToServer(id, organizationId, signature);
        
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

async function checkCSPAvailable() {
    try {
        await ensureCadesPluginLoaded();
        console.log
        ("1. Плагин обнаружен, версия:", await cadesplugin.version);

        const about = await cadesplugin.CreateObjectAsync("CAdESCOM.About");

        const store = await cadesplugin.CreateObjectAsync("CAdESCOM.Store");

        return true;
    } catch (error) {
        console.error("❌ Ошибка при проверке CSP:", error);
        return false;
    }
}


async function getDataForSignature(id, organizationId) {
    const response = await fetch(`/signatures/${id}/${organizationId}`);
    if (!response.ok) throw new Error('Ошибка получения данных');
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


async function sendSignatureToServer(id, organizationId, signature) {
    const response = await fetch(`/signatures/${id}/${organizationId}`, {
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
    const content = document.createElement('div');
    content.className = 'csp-modal-content';
    const title = document.createElement('h3');
    title.textContent = 'Требуется установка КриптоПРО';
    const body = document.createElement('div');
    body.className = 'csp-modal-body';
    const intro = document.createElement('p');
    intro.textContent = 'Для подписи документов необходимо:';
    const steps = document.createElement('ol');
    const step1 = document.createElement('li');
    const link1 = document.createElement('a');
    link1.href = 'https://www.cryptopro.ru/products/cades/plugin';
    link1.target = '_blank';
    link1.textContent = 'КриптоПРО ЭЦП Browser plug-in';
    step1.appendChild(document.createTextNode('Установить '));
    step1.appendChild(link1);
    const step2 = document.createElement('li');
    const link2 = document.createElement('a');
    link2.href = 'https://www.cryptopro.ru/products/csp';
    link2.target = '_blank';
    link2.textContent = 'КриптоПРО CSP';
    step2.appendChild(document.createTextNode('Установить '));
    step2.appendChild(link2);
    step2.appendChild(document.createTextNode(' (версия 4.0+)'));
    const step3 = document.createElement('li');
    step3.textContent = 'Обновить страницу после установки';
    steps.appendChild(step1);
    steps.appendChild(step2);
    steps.appendChild(step3);
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


function updateUISuccess() {
    const signActions = document.querySelector('[data-role="sign-actions"]');
    const signedActions = document.querySelector('[data-role="signed-actions"]');
    if (signActions) {
        signActions.style.display = "none";
    }
    if (signedActions) {
        signedActions.style.display = "block";
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

function showError(message) {
    const notification = document.createElement('div');
    notification.className = 'csp-notification error';
    const icon = document.createElement('span');
    icon.className = 'csp-notification-icon';
    icon.textContent = '!';
    const text = document.createElement('span');
    text.className = 'csp-notification-text';
    text.textContent = message;
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
    errorNode.textContent = message;
    host.replaceChildren(errorNode);
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

window.mountSurveyFillPage = function mountSurveyFillPage(host, { survey, organizationId, userRole, onBack, onSubmitted, initialHtml }) {
    if (!host) {
        return null;
    }

    let destroyed = false;
    const answers = {};
    let loading = false;
    let error = null;
    let refs = {
        page: null,
        errorBlock: null,
        errorText: null,
        submitButton: null,
        submitLabel: null,
        cancelButton: null
    };

    function getQuestionNodes() {
        return Array.from(host.querySelectorAll('[data-role="survey-question"]'));
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

        questionElement.querySelectorAll('[data-role="rating-button"]').forEach((button) => {
            button.addEventListener('click', () => {
                error = null;
                const rating = Number(button.dataset.rating || 0);
                answers[questionId] = {
                    ...answers[questionId],
                    rating,
                    comment: rating < 5 ? answers[questionId]?.comment || '' : ''
                };
                renderError();
                updateQuestionState(questionId, questionElement);
            });
        });

        const commentInput = questionElement.querySelector('[data-role="comment-input"]');
        commentInput?.addEventListener('input', (event) => {
            error = null;
            answers[questionId] = {
                ...answers[questionId],
                comment: event.target.value
            };
            renderError();
        });

        updateQuestionState(questionId, questionElement);
    }

    async function submitAnswers() {
        try {
            loading = true;
            error = null;
            renderError();
            renderSubmitState();

            const payloadAnswers = Object.entries(answers).map(([questionId, answer]) => {
                const questionNode = getQuestionNodes().find((node) => node.dataset.questionId === questionId);
                const questionText = questionNode?.querySelector('[data-role="question-title"]')?.textContent?.trim() || '';

                return {
                    question_id: questionId,
                    question_text: questionText,
                    rating: answer.rating,
                    comment: answer.comment || ''
                };
            });

            const response = await fetch('/answers/create', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify({
                    id_survey: survey.id_survey,
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

    function bindPage() {
        refs = {
            page: host.querySelector('[data-role="survey-fill-page"]'),
            errorBlock: host.querySelector('[data-role="error"]'),
            errorText: host.querySelector('[data-role="error-text"]'),
            submitButton: host.querySelector('[data-role="submit"]'),
            submitLabel: host.querySelector('[data-role="submit-label"]'),
            cancelButton: host.querySelector('[data-role="cancel-btn"]')
        };

        refs.submitButton?.addEventListener('click', submitAnswers);
        refs.cancelButton?.addEventListener('click', () => onBack?.());
        getQuestionNodes().forEach(bindQuestion);
        renderError();
        renderSubmitState();
    }

    const loadFillContent = async () => {
        try {
            const html = typeof initialHtml === 'string'
                ? initialHtml
                : await window.fetchSurveyFillContentHtml(survey.id_survey, organizationId);
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
        }
    };

    loadFillContent();

    return () => {
        destroyed = true;
        host.replaceChildren();
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
        const loadingIndicator = document.createElement('div');
        loadingIndicator.className = 'loading-overlay';
        const loadingContent = document.createElement('div');
        loadingContent.className = 'loading-content';
        const spinner = document.createElement('div');
        spinner.className = 'loading-spinner';
        const label = document.createElement('p');
        label.textContent = 'Подготовка архива...';
        loadingContent.appendChild(spinner);
        loadingContent.appendChild(label);
        loadingIndicator.appendChild(loadingContent);
        document.body.appendChild(loadingIndicator);

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
    } finally {
        const overlay = document.querySelector('.loading-overlay');
        if (overlay) {
            document.body.removeChild(overlay);
        }
    }
}


window.mountCheckAnswersPage = function mountCheckAnswersPage(host, { survey, organizationId, userRole, onBack, initialHtml }) {
    if (!host) {
        return null;
    }

    let destroyed = false;
    function bindPage() {
        const pdfButton = host.querySelector('[data-role="pdf-btn"]');
        pdfButton?.addEventListener('click', () => createPdfReport(survey.id_survey, organizationId));
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
        }
    };

    loadAnswersContent();

    return () => {
        destroyed = true;
        host.replaceChildren();
    };
};
