// stage10: user survey flow extracted from survey_list_user.cshtml

window.populateMonthOptions = function() {
  const select = document.getElementById('filterOrganization');

  const cards = document.querySelectorAll('.survey-card');
  const months = new Set();

  cards.forEach(card => {
    const dateElement = card.querySelector('.dates');
    const dateText = dateElement.textContent.trim();
    const match = dateText.match(/(\d{2})\.(\d{2})\.(\d{4})/);
    const month = match[2];
    months.add(month);
  });


  const currentValue = select.value;
  select.innerHTML = '';
  const defaultMonthOption = document.createElement('option');
  defaultMonthOption.value = '';
  defaultMonthOption.textContent = 'За все месяцы';
  select.appendChild(defaultMonthOption);
  Array.from(months).sort().forEach(month => {
    const option = document.createElement('option');
    option.value = month;
    option.textContent = getMonthName(month);
    select.appendChild(option);
  });
  select.value = currentValue;
};

window.populateYearOptions = function() {
  const select = document.getElementById('filterSurvey');

  const cards = document.querySelectorAll('.survey-card');
  const years = new Set();

  cards.forEach(card => {
    const dateElement = card.querySelector('.dates');
    const dateText = dateElement.textContent.trim();
    const match = dateText.match(/(\d{2})\.(\d{2})\.(\d{4})/);
    const year = match[3];
    years.add(year);
  });

  const currentValue = select.value;
  select.innerHTML = '';
  const defaultYearOption = document.createElement('option');
  defaultYearOption.value = '';
  defaultYearOption.textContent = 'По всем годам';
  select.appendChild(defaultYearOption);
  Array.from(years).sort().forEach(year => {
    const option = document.createElement('option');
    option.value = year;
    option.textContent = year;
    select.appendChild(option);
  });
  select.value = currentValue;

};

window.filterByDate = function() {
  const monthSelect = document.getElementById('filterOrganization');
  const yearSelect = document.getElementById('filterSurvey');

  const month = monthSelect.value;
  const year = yearSelect.value;

  const cards = document.querySelectorAll('.survey-card');
  let visibleCount = 0;

  cards.forEach(card => {
    const dateElement = card.querySelector('.dates');
    if (!dateElement) {
      card.style.display = 'none';
      return;
    }
    const dateText = dateElement.textContent.trim();
    const match = dateText.match(/(\d{2})\.(\d{2})\.(\d{4})/);
    if (!match) {
      card.style.display = 'none';
      return;
    }
    const rowDay = match[1];
    const rowMonth = match[2];
    const rowYear = match[3];

    const matchMonth = !month || rowMonth === month;
    const matchYear = !year || rowYear === year;

    if (matchMonth && matchYear) {
      card.style.display = '';
      visibleCount++;
    } else {
      card.style.display = 'none';
    }
  });

  const noSurveysElement = document.querySelector('.no-surveys');
  if (noSurveysElement) {
    noSurveysElement.style.display = visibleCount === 0 ? '' : 'none';
  }

};

function getMonthName(monthNum) {
  const months = {
    '01': 'Январь', '02': 'Февраль', '03': 'Март', '04': 'Апрель',
    '05': 'Май', '06': 'Июнь', '07': 'Июль', '08': 'Август',
    '09': 'Сентябрь', '10': 'Октябрь', '11': 'Ноябрь', '12': 'Декабрь'
  };
  return months[monthNum] || monthNum;
}

// Автоматический вызов при загрузке страницы (если нужно)
window.addEventListener('load', function() {
  window.populateMonthOptions();
  window.populateYearOptions();
  window.filterByDate();
});



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

async function CSP(id, organization_id) {
    try {
        await ensureCadesPluginLoaded();

        if (!await checkCSPAvailable()) {
            console.error("CSP не доступен");
            showCSPInstallInstructions();
            return;
        }

        const dataToSign = await getDataForSignature(id, organization_id);
        
        const signature = await createDigitalSignature(dataToSign);
        
        await sendSignatureToServer(id, organization_id, signature);
        
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


async function getDataForSignature(id, organization_id) {
    const response = await fetch(`/signatures/${id}/${organization_id}`);
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


async function sendSignatureToServer(id, organization_id, signature) {
    const response = await fetch(`/signatures/${id}/${organization_id}`, {
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

function normalizeSurveyQuestion(question, index) {
    return {
        ...question,
        id: question?.id ?? question?.Id ?? index + 1,
        text: question?.text ?? question?.Text ?? `Вопрос ${index + 1}`
    };
}

window.mountSurveyFillPage = function mountSurveyFillPage(host, { survey, organizationId, userRole, onBack }) {
    if (!host) {
        return null;
    }

    let destroyed = false;
    let checkAnswersCleanup = null;
    let showResultsTimer = null;
    const state = {
        questions: [],
        loading: true,
        error: null,
        answers: {},
        submissionState: {
            isSubmitted: false,
            showResults: false,
            resultsData: null
        }
    };

    const setError = (value) => {
        state.error = value;
    };

    const rerender = () => {
        if (destroyed) {
            return;
        }

        if (typeof checkAnswersCleanup === 'function') {
            checkAnswersCleanup();
            checkAnswersCleanup = null;
        }

        host.innerHTML = '';

        if (state.loading) {
            const loadingNode = document.createElement('div');
            loadingNode.className = 'loading';
            loadingNode.textContent = 'Загрузка анкеты...';
            host.appendChild(loadingNode);
            return;
        }

        if (state.error) {
            const errorNode = document.createElement('div');
            errorNode.className = 'error-message';
            errorNode.textContent = state.error;
            host.appendChild(errorNode);
            return;
        }

        if (state.submissionState.isSubmitted && !state.submissionState.showResults) {
            const successTemplate = document.getElementById('survey-user-fill-success-template');
            if (successTemplate?.content?.firstElementChild) {
                host.appendChild(successTemplate.content.firstElementChild.cloneNode(true));
            }
            return;
        }

        if (state.submissionState.showResults && state.submissionState.resultsData) {
            const checkContainer = document.createElement('div');
            host.appendChild(checkContainer);
            if (typeof window.mountCheckAnswersView === 'function') {
                checkAnswersCleanup = window.mountCheckAnswersView(checkContainer, {
                    data: state.submissionState.resultsData,
                    userRole,
                    onBack
                });
            }
            return;
        }

        const fillTemplate = document.getElementById('survey-user-fill-template');
        const questionTemplate = document.getElementById('survey-user-fill-question-template');
        if (!fillTemplate?.content?.firstElementChild || !questionTemplate?.content?.firstElementChild) {
            return;
        }

        const fillNode = fillTemplate.content.firstElementChild.cloneNode(true);
        const titleNode = fillNode.querySelector('[data-role="survey-title"]');
        const descriptionNode = fillNode.querySelector('[data-role="survey-description"]');
        const errorNode = fillNode.querySelector('[data-role="fill-error"]');
        const questionsHost = fillNode.querySelector('[data-role="questions-host"]');
        const submitButton = fillNode.querySelector('[data-role="submit-btn"]');
        const cancelButton = fillNode.querySelector('[data-role="cancel-btn"]');

        if (titleNode) {
            titleNode.textContent = survey.name_survey || '';
        }
        if (descriptionNode) {
            descriptionNode.textContent = survey.description || 'Анкета без описания';
        }
        if (errorNode) {
            errorNode.style.display = state.error ? '' : 'none';
            errorNode.textContent = state.error || '';
        }

        state.questions.forEach((question) => {
            const questionNode = questionTemplate.content.firstElementChild.cloneNode(true);
            const questionTextNode = questionNode.querySelector('[data-role="question-text"]');
            const ratingsHost = questionNode.querySelector('[data-role="rating-buttons"]');
            const commentWrap = questionNode.querySelector('[data-role="comment-wrap"]');
            const textarea = questionNode.querySelector('textarea');
            const answer = state.answers[question.id] || {};

            if (questionTextNode) {
                questionTextNode.textContent = question.text;
            }

            for (let rating = 1; rating <= 5; rating += 1) {
                const ratingButton = document.createElement('button');
                ratingButton.type = 'button';
                ratingButton.className = `btn_crit ${answer.rating === rating ? 'active' : ''}`;
                ratingButton.textContent = String(rating);
                ratingButton.addEventListener('click', () => {
                    setError(null);
                    state.answers = {
                        ...state.answers,
                        [question.id]: {
                            rating,
                            comment: rating < 5 ? state.answers[question.id]?.comment || '' : ''
                        }
                    };
                    rerender();
                });
                ratingsHost?.appendChild(ratingButton);
            }

            const showComment = answer.rating > 0 && answer.rating < 5;
            if (commentWrap) {
                commentWrap.style.display = showComment ? '' : 'none';
            }
            if (textarea) {
                textarea.value = answer.comment || '';
                textarea.addEventListener('input', (event) => {
                    setError(null);
                    state.answers = {
                        ...state.answers,
                        [question.id]: {
                            ...state.answers[question.id],
                            comment: event.target.value
                        }
                    };
                });
            }

            questionsHost?.appendChild(questionNode);
        });

        submitButton?.addEventListener('click', async () => {
            try {
                setError(null);
                const answersArray = Object.entries(state.answers).map(([questionId, answer]) => ({
                    question_id: questionId,
                    question_text: state.questions.find((q) => String(q.id) === String(questionId))?.text || '',
                    rating: answer.rating,
                    comment: answer.comment || ''
                }));

                const response = await fetch('/answers/create', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    body: JSON.stringify({
                        organization_id: organizationId,
                        id_survey: survey.id_survey,
                        answers: answersArray
                    })
                });

                if (!response.ok) {
                    let errorMessage = 'Ошибка при отправке ответов';
                    try {
                        const errorData = await response.json();
                        errorMessage = errorData?.error || errorData?.message || errorMessage;
                    } catch {
                        const errorText = await response.text();
                        if (errorText) {
                            errorMessage = errorText;
                        }
                    }
                    throw new Error(errorMessage);
                }

                await response.json().catch(() => null);
                state.submissionState = {
                    isSubmitted: true,
                    showResults: false,
                    resultsData: {
                        Survey: survey,
                        Answers: answersArray,
                        IdOrganization: organizationId
                    }
                };
                rerender();
                showResultsTimer = window.setTimeout(() => {
                    state.submissionState = { ...state.submissionState, showResults: true };
                    rerender();
                }, 2000);
            } catch (err) {
                setError(err.message);
                rerender();
            }
        });
        cancelButton?.addEventListener('click', () => onBack?.());

        host.appendChild(fillNode);
    };

    const loadQuestions = async () => {
        try {
            const response = await fetch(`/surveys/${survey.id_survey}/organizations/${survey.organization_id}/questions`);
            if (!response.ok) {
                throw new Error('Не удалось загрузить вопросы анкеты');
            }
            const data = await response.json();
            state.questions = (data.questions || []).map((question, index) => normalizeSurveyQuestion(question, index));
        } catch (err) {
            setError(err.message);
        } finally {
            state.loading = false;
            rerender();
        }
    };

    rerender();
    loadQuestions();

    return () => {
        destroyed = true;
        if (showResultsTimer) {
            window.clearTimeout(showResultsTimer);
        }
        if (typeof checkAnswersCleanup === 'function') {
            checkAnswersCleanup();
        }
        host.innerHTML = '';
    };
};

// Компонент для отображения результатов
window.mountCheckAnswersView = function mountCheckAnswersView(host, { data }) {
    const template = document.getElementById('survey-user-checkanswers-template');
    if (!host || !template?.content?.firstElementChild) {
        return null;
    }

    host.innerHTML = '';
    const viewNode = template.content.firstElementChild.cloneNode(true);
    const tbody = viewNode.querySelector('[data-role="answers-body"]');
    const signBtn = viewNode.querySelector('[data-role="sign-btn"]');
    const pdfBtn = viewNode.querySelector('[data-role="pdf-btn"]');
    const archiveBtn = viewNode.querySelector('[data-role="archive-btn"]');

    (data?.Answers || []).forEach((answer) => {
        const row = document.createElement('tr');
        const questionCell = document.createElement('td');
        questionCell.textContent = answer.question_text || '';
        const ratingCell = document.createElement('td');
        ratingCell.textContent = String(answer.rating ?? '');
        const commentCell = document.createElement('td');
        commentCell.textContent = answer.comment || '';
        row.appendChild(questionCell);
        row.appendChild(ratingCell);
        row.appendChild(commentCell);
        tbody?.appendChild(row);
    });

    signBtn?.addEventListener('click', () => CSP(data?.Survey?.id_survey, data?.IdOrganization));
    pdfBtn?.addEventListener('click', () => createPdfReport(data?.Survey?.id_survey, data?.IdOrganization));
    archiveBtn?.addEventListener('click', () => downloadSignedArchive(data?.Survey?.id_survey, data?.IdOrganization));

    host.appendChild(viewNode);
    return () => {
        host.innerHTML = '';
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


window.mountCheckAnswersPage = function mountCheckAnswersPage(host, { survey, organizationId, userRole, onBack }) {
    if (!host) {
        return null;
    }

    let destroyed = false;
    const data = {
        loading: true,
        error: null,
        surveyName: survey.name_survey || '',
        answers: [],
        csp: survey.csp || null
    };

    const render = () => {
        if (destroyed) {
            return;
        }

        host.innerHTML = '';
        if (data.loading) {
            const loadingNode = document.createElement('div');
            loadingNode.className = 'loading-container';
            const p = document.createElement('p');
            p.textContent = 'Загрузка данных анкеты...';
            loadingNode.appendChild(p);
            host.appendChild(loadingNode);
            return;
        }

        if (data.error) {
            const errorNode = document.createElement('div');
            errorNode.className = 'error-container';
            const p = document.createElement('p');
            p.textContent = data.error;
            errorNode.appendChild(p);
            host.appendChild(errorNode);
            return;
        }

        const template = document.getElementById('survey-user-checkanswers-page-template');
        if (!template?.content?.firstElementChild) {
            return;
        }

        const root = template.content.firstElementChild.cloneNode(true);
        const surveyName = root.querySelector('[data-role="survey-name"]');
        const signatureInfo = root.querySelector('[data-role="signature-info"]');
        const signatureStatus = root.querySelector('[data-role="signature-status"]');
        const emptyMessage = root.querySelector('[data-role="empty-message"]');
        const answersContent = root.querySelector('[data-role="answers-content"]');
        const pdfBtn = root.querySelector('[data-role="pdf-btn"]');

        if (surveyName) {
            surveyName.textContent = data.surveyName || '';
        }
        if (signatureInfo && signatureStatus) {
            signatureInfo.style.display = data.csp ? '' : 'none';
            signatureStatus.textContent = data.csp ? 'подписано' : 'не подписано';
            signatureStatus.classList.toggle('signed', Boolean(data.csp));
            signatureStatus.classList.toggle('not-signed', !data.csp);
        }

        if ((data.answers || []).length === 0) {
            if (emptyMessage) {
                emptyMessage.style.display = '';
            }
            if (answersContent) {
                answersContent.style.display = 'none';
            }
        } else {
            if (emptyMessage) {
                emptyMessage.style.display = 'none';
            }
            (data.answers || []).forEach((group) => {
                const block = document.createElement('div');
                block.className = 'answer-block';
                const date = document.createElement('div');
                date.className = 'answer-date';
                const calendar = document.createElement('span');
                calendar.className = 'calendar-icon';
                calendar.textContent = '📅';
                date.appendChild(calendar);
                date.appendChild(document.createTextNode(` ${group.date || 'Дата не указана'}`));

                const table = document.createElement('table');
                table.className = 'answers-table';
                const thead = document.createElement('thead');
                const headerRow = document.createElement('tr');
                const thQuestion = document.createElement('th');
                thQuestion.textContent = 'Вопрос';
                const thRating = document.createElement('th');
                thRating.textContent = 'Оценка';
                const thComment = document.createElement('th');
                thComment.textContent = 'Комментарий';
                headerRow.appendChild(thQuestion);
                headerRow.appendChild(thRating);
                headerRow.appendChild(thComment);
                thead.appendChild(headerRow);
                const tbody = document.createElement('tbody');
                (group.answers || []).forEach((answer) => {
                    const row = document.createElement('tr');
                    const q = document.createElement('td');
                    q.setAttribute('data-label', 'Вопрос');
                    q.textContent = answer.question_text || '';
                    const r = document.createElement('td');
                    r.setAttribute('data-label', 'Оценка');
                    r.className = 'rating-cell';
                    const badge = document.createElement('span');
                    badge.className = 'rating-badge';
                    badge.textContent = String(answer.rating ?? '');
                    r.appendChild(badge);
                    const c = document.createElement('td');
                    c.setAttribute('data-label', 'Комментарий');
                    c.textContent = answer.comment || '';
                    row.appendChild(q);
                    row.appendChild(r);
                    row.appendChild(c);
                    tbody.appendChild(row);
                });
                table.appendChild(thead);
                table.appendChild(tbody);
                block.appendChild(date);
                block.appendChild(table);
                answersContent?.appendChild(block);
            });
        }

        pdfBtn?.addEventListener('click', () => createPdfReport(survey.id_survey, organizationId));
        host.appendChild(root);
    };

    const fetchSurveyAnswers = async () => {
        try {
            data.loading = true;
            data.error = null;
            render();

            const response = await fetch(`/answers/${survey.id_survey}/${organizationId}/${userRole}`);

            if (!response.ok) {
                const errorData = await response.json().catch(() => null);
                const errorMsg = errorData?.error || `Ошибка ${response.status}`;
                throw new Error(errorMsg);
            }

            const result = await response.json();

            if (!result?.success) {
                throw new Error(result?.error || 'Неверный формат ответа');
            }

            data.loading = false;
            data.error = null;
            data.surveyName = result.survey?.name || survey.name_survey || '';
            data.answers = result.answers || [];
            data.csp = result.survey?.csp || null;
            render();
        } catch (error) {
            console.error('Ошибка:', error);
            data.loading = false;
            data.error = error.message;
            data.surveyName = survey.name_survey || '';
            data.answers = [];
            data.csp = null;
            render();
        }
    };

    render();
    fetchSurveyAnswers();

    return () => {
        destroyed = true;
        host.innerHTML = '';
    };
};
