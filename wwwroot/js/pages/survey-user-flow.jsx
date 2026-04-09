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
  console.info("[INFO] Запуск populateYearOptions");
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

async function CSP(id, organization_id) {
    console.log("Начало работы CSP");
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
    document.getElementById("block_btn_not_csp").style.display = "none";
    document.getElementById("block_btn_csp").style.display = "block";
    
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

window.HelpContent = ({ content, loading, error }) => {
  // Немедленно открываем документ при рендеринге
  const openDoc = () => {
    window.open('/help_files/user_survey_guide.docx', '_blank');
    return null; // Не возвращаем JSX, так как сразу открываем файл
  };

  // Обработка состояний
  if (loading) return <div>Загрузка...</div>;
  if (error) return <div>Проверьте, скачался ли файл. {error.message}</div>;

  // Основной рендер
  return openDoc();
};

window.UserSurveyModal = ({ isOpen, onClose, title = '', children, className = '' }) => {
    React.useEffect(() => {
        if (!isOpen) {
            return undefined;
        }

        const handleEscape = (event) => {
            if (event.key === 'Escape') {
                onClose?.();
            }
        };

        document.body.classList.add('modal-open');
        document.addEventListener('keydown', handleEscape);

        return () => {
            document.body.classList.remove('modal-open');
            document.removeEventListener('keydown', handleEscape);
        };
    }, [isOpen, onClose]);

    if (!isOpen) {
        return null;
    }

    return (
        <div
            className={`modal modal--visible user-survey-modal ${className}`.trim()}
            aria-hidden={!isOpen}
            onClick={() => onClose?.()}
        >
            <div className="modal-content user-survey-modal__content" onClick={(event) => event.stopPropagation()}>
                <button type="button" className="modal-close" onClick={() => onClose?.()}>
                    <i className="fas fa-xmark"></i>
                </button>
                {title ? (
                    <div className="modal-header user-survey-modal__header">
                        <h2 className="h2_modal user-survey-modal__title">{title}</h2>
                    </div>
                ) : null}
                <div className="modal-body user-survey-modal__body">
                    {children}
                </div>
            </div>
        </div>
    );
};


window.SurveyFillPage = ({ survey, organizationId, userRole, onBack }) => {
    const [questions, setQuestions] = React.useState([]);
    const [loading, setLoading] = React.useState(true);
    const [error, setError] = React.useState(null);
    const [answers, setAnswers] = React.useState({});
    const [submissionState, setSubmissionState] = React.useState({
        isSubmitted: false,
        showResults: false,
        resultsData: null
    });
    const normalizeQuestion = React.useCallback((question, index) => ({
        ...question,
        id: question?.id ?? question?.Id ?? (index + 1),
        text: question?.text ?? question?.Text ?? `Вопрос ${index + 1}`
    }), []);

    React.useEffect(() => {
        const loadQuestions = async () => {
            try {
                const response = await fetch(`/surveys/${survey.id_survey}/organizations/${survey.organization_id}/questions`);
                if (!response.ok) throw new Error('Не удалось загрузить вопросы анкеты');
                const data = await response.json();
                setQuestions((data.questions || []).map((question, index) => normalizeQuestion(question, index)));
            } catch (err) {
                setError(err.message);
            } finally {
                setLoading(false);
            }
        };
        loadQuestions();
    }, [survey.id_survey, survey.organization_id, normalizeQuestion]);

    const submitAnswers = async () => {
        try {
            setError(null);
            const answersArray = Object.entries(answers).map(([questionId, answer]) => ({
                question_id: questionId,
                question_text: questions.find(q => String(q.id) === String(questionId))?.text || '',
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

            setSubmissionState({
                isSubmitted: true,
                showResults: false,
                resultsData: {
                    Survey: survey,
                    Answers: answersArray,
                    IdOrganization: organizationId
                }
            });


            setTimeout(() => {
                setSubmissionState(prev => ({
                    ...prev,
                    showResults: true
                }));
            }, 2000);

        } catch (err) {
            setError(err.message);
        }
    };

    if (loading) return <div className="loading">Загрузка анкеты...</div>;
    if (error) return <div className="error-message">{error}</div>;

    if (submissionState.isSubmitted && !submissionState.showResults) {
        return (
            <div className="success-message">
                <svg xmlns="http://www.w3.org/2000/svg" width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path>
                    <polyline points="22 4 12 14.01 9 11.01"></polyline>
                </svg>
                <h2>Анкета успешно заполнена!</h2>
                <p>Вы будете перенаправлены к результатам...</p>
            </div>
        );
    }

    if (submissionState.showResults && submissionState.resultsData) {
        return <CheckAnswersView 
            data={submissionState.resultsData} 
            userRole={userRole} 
            onBack={onBack} 
        />;
    }

    return (
        <div className="survey-fill-container">
            <div className="note">
                <h2>{survey.name_survey}</h2>
                <p>{survey.description || 'Анкета без описания'}</p>
            </div>
            
           {questions.map((question, qIndex) => (
    <div key={`question-${question.id}-${qIndex}`} className="question-container">
        <h3>{question.text}</h3>
        <div className="rating-buttons">
            {[1, 2, 3, 4, 5].map(rating => (
                <button
                    key={`rating-${question.id}-${rating}`}
                    className={`btn_crit ${answers[question.id]?.rating === rating ? 'active' : ''}`}
                    onClick={() => {
                        setError(null);
                        setAnswers(prev => ({
                            ...prev,
                            [question.id]: {
                                rating,
                                comment: rating < 5 ? prev[question.id]?.comment || '' : ''
                            }
                        }));
                    }}
                >
                    {rating}
                </button>
            ))}
        </div>
        {answers[question.id]?.rating > 0 && answers[question.id]?.rating < 5 && (
            <div className="comment-container">
                <label className="comment-label">Ваш комментарий</label>
                <textarea 
                    value={answers[question.id]?.comment || ''}
                    onChange={(e) => {
                        setError(null);
                        setAnswers(prev => ({
                            ...prev,
                            [question.id]: {
                                ...prev[question.id],
                                comment: e.target.value
                            }
                        }));
                    }}
                    placeholder="Напишите комментарий"
                />
            </div>
        )}
    </div>
))}
            
            <div className="submit-container">
                <button 
                    onClick={submitAnswers}
                    className="submit-button"
                >
                    Отправить ответы
                </button>
                <button
                    type="button"
                    className="modal_btn modal_btn-secondary user-survey-cancel-btn"
                    onClick={onBack}
                >
                    Отмена
                </button>
            </div>
        </div>
    );
};

// Компонент для отображения результатов
window.CheckAnswersView = ({ data, userRole, onBack }) => {
    return (
        <div className="content" id="default_content">
            <div className="note">
                <h2>Вы успешно прошли анкету!</h2>
                <p>На этой странице вы можете ознакомиться с ответами на анкету.</p>
            </div>

            <div className="table-container">
                <table>
                    <thead>
                        <tr>
                            <th>Вопрос</th>
                            <th>Ответ</th>
                            <th>Комментарий</th>
                        </tr>
                    </thead>
                    <tbody>
                        {data.Answers.map((answer, index) => (
                            <tr key={`answer-${index}-${answer.question_id}`}>
                                <td>{answer.question_text}</td>
                                <td>{answer.rating}</td>
                                <td>{answer.comment}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>

            <div id="block_btn_not_csp" className="button-container">
                <button 
                    className="submit-button" 
                    onClick={() => CSP(data.Survey.id_survey, data.IdOrganization)}
                >
                    Подписать
                </button>
                <button 
                    className="submit-button" 
                    onClick={() => createPdfReport(data.Survey.id_survey, data.IdOrganization)}
                >

                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
            <polyline points="7 10 12 15 17 10"></polyline>
            <line x1="12" y1="15" x2="12" y2="3"></line>
        </svg><span> </span>

                     Скачать PDF с анкетой
                </button>
            </div>

<div id="block_btn_csp" className="button-container" style={{display: 'none'}}>
    <button 
        className="submit-button" 
        onClick={() => downloadSignedArchive(data.Survey.id_survey, data.IdOrganization)}
    >
        <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
            <polyline points="7 10 12 15 17 10"></polyline>
            <line x1="12" y1="15" x2="12" y2="3"></line>
        </svg><span> </span>
         Скачать архив (PDF + подпись)
    </button>
</div>
        </div>
    );
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


window.CheckAnswersPage = ({ survey, organizationId, userRole, onBack }) => {
    const [data, setData] = React.useState({
        loading: true,
        error: null,
        surveyName: survey.name_survey || '',
        answers: [],
        csp: survey.csp || null
    });

    React.useEffect(() => {
        const fetchSurveyAnswers = async () => {
            try {
                console.log(`Загрузка ответов для анкеты ${survey.id_survey}`);
                setData(prev => ({ ...prev, loading: true, error: null }));
                
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

                setData({
                    loading: false,
                    error: null,
                    surveyName: result.survey?.name || survey.name_survey || '',
                    answers: result.answers || [],
                    csp: result.survey?.csp || null
                });

            } catch (error) {
                console.error('Ошибка:', error);
                setData({
                    loading: false,
                    error: error.message,
                    surveyName: survey.name_survey || '',
                    answers: [],
                    csp: null
                });
            }
        };

        fetchSurveyAnswers();
    }, [survey.id_survey, organizationId, userRole]);

    if (data.loading) {
        return (
            <div className="loading-container">
                <p>Загрузка данных анкеты...</p>
            </div>
        );
    }

    if (data.error) {
        return (
            <div className="error-container">
                <p>{data.error}</p>
            </div>
        );
    }

    return (
        <div className="answers-container">
            <h1 className="survey-title">
                Ответы на анкету: <span className="survey-name">{data.surveyName}</span>
            </h1>

            {data.csp && (
                <div className="signature-info">
                    <div className="signature-icon-container">
                        <svg className="signature-icon" xmlns="http://www.w3.org/2000/svg" width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                            <path d="M6 17L10 13L13 16L18 11M20 12C20 16.4183 16.4183 20 12 20C7.58172 20 4 16.4183 4 12C4 7.58172 7.58172 4 12 4C16.4183 4 20 7.58172 20 12Z" stroke="#5c6bc0" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"></path>
                            <polygon points="18 2 22 6 12 16 8 16 8 12 18 2"></polygon>
                        </svg>
                    </div>
                    <div className="signature-label">Подпись:</div>
                    <div className={`signature-status ${data.csp ? 'signed' : 'not-signed'}`}>
                        {data.csp ? 'подписано' : 'не подписано'}
                    </div>
                </div>
            )}

            {data.answers.length === 0 ? (
                <div className="empty-message">
                    Нет данных для отображения
                </div>
            ) : (
                <>
                    <div className="answers-content">
                        {data.answers.map((group, groupIndex) => (
                            <div key={`group-${groupIndex}-${group.date}`} className="answer-block">
                                <div className="answer-date">
                                    <span className="calendar-icon">📅</span> {group.date || 'Дата не указана'}
                                </div>
                                
                                <table className="answers-table">
                                    <thead>
                                        <tr>
                                            <th>Вопрос</th>
                                            <th>Оценка</th>
                                            <th>Комментарий</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {group.answers.map((answer, answerIndex) => (
                                            <tr key={`answer-${groupIndex}-${answerIndex}-${answer.question_text}`}>
                                                <td data-label="Вопрос">{answer.question_text}</td>
                                                <td data-label="Оценка" className="rating-cell">
                                                    <span className="rating-badge">
                                                        {answer.rating}
                                                    </span>
                                                </td>
                                                <td data-label="Комментарий">{answer.comment}</td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>
                        ))}
                    </div>

                    <div className="submit-container user-survey-modal-actions">
                        <button
                            type="button"
                            className="submit-button"
                            onClick={() => createPdfReport(survey.id_survey, organizationId)}
                        >
                            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                                <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
                                <polyline points="7 10 12 15 17 10"></polyline>
                                <line x1="12" y1="15" x2="12" y2="3"></line>
                            </svg>
                            <span> </span>
                            Скачать PDF с анкетой
                        </button>
                    </div>
                </>
            )}
        </div>
    );
}
