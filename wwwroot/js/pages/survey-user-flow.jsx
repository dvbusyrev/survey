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
  select.innerHTML = '<option value="">За все месяцы</option>';
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
  select.innerHTML = '<option value="">По всем годам</option>';
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
};



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

window.HelpContent = ({ content, loading, error }) => {
  // Немедленно открываем документ при рендеринге
  const openDoc = () => {
    window.open('/help_files/instruction_for_user_anketirovanie.docx', '_blank');
    return null; // Не возвращаем JSX, так как сразу открываем файл
  };

  // Обработка состояний
  if (loading) return <div>Загрузка...</div>;
  if (error) return <div>Проверьте, скачался ли файл. {error.message}</div>;

  // Основной рендер
  return openDoc();
};


window.SurveyFillPage = ({ survey, omsuId, userRole, onBack }) => {
    const [questions, setQuestions] = React.useState([]);
    const [loading, setLoading] = React.useState(true);
    const [error, setError] = React.useState(null);
    const [answers, setAnswers] = React.useState({});
    const [submissionState, setSubmissionState] = React.useState({
        isSubmitted: false,
        showResults: false,
        resultsData: null
    });

    React.useEffect(() => {
        const loadQuestions = async () => {
            try {
                const response = await fetch(`/zapolnenie_anketi/${survey.id_survey}/${survey.id_omsu}`);
                if (!response.ok) throw new Error('Не удалось загрузить вопросы анкеты');
                const data = await response.json();
                setQuestions(data.questions || []);
            } catch (err) {
                setError(err.message);
            } finally {
                setLoading(false);
            }
        };
        loadQuestions();
    }, [survey.id_survey, survey.id_omsu]);

    const submitAnswers = async () => {
        try {
            const answersArray = Object.entries(answers).map(([questionId, answer]) => ({
                question_id: questionId,
                question_text: questions.find(q => q.Id == questionId)?.Text || '',
                rating: answer.rating,
                comment: answer.comment || ''
            }));

            const response = await fetch('/api/insert_answer', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    id_omsu: omsuId,
                    id_survey: survey.id_survey,
                    answers: JSON.stringify(answersArray)
                })
            });

            if (!response.ok) throw new Error('Ошибка при отправке ответов');

            setSubmissionState({
                isSubmitted: true,
                showResults: false,
                resultsData: {
                    Survey: survey,
                    Answers: answersArray,
                    IdOmsu: omsuId
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
            <button onClick={onBack} className="back-button">
                ← Вернуться к списку анкет
            </button>
            
            <div className="note">
                <h2>{survey.name_survey}</h2>
                <p>{survey.description || 'Анкета без описания'}</p>
            </div>
            
           {questions.map((question, qIndex) => (
    <div key={`question-${question.Id}-${qIndex}`} className="question-container">
        <h3>{question.Text}</h3>
        <div className="rating-buttons">
            {[1, 2, 3, 4, 5].map(rating => (
                <button
                    key={`rating-${question.Id}-${rating}`}
                    className={`btn_crit ${answers[question.Id]?.rating === rating ? 'active' : ''}`}
                    onClick={() => setAnswers(prev => ({
                        ...prev,
                        [question.Id]: {
                            rating,
                            comment: rating < 5 ? prev[question.Id]?.comment || '' : ''
                        }
                    }))}
                >
                    {rating}
                </button>
            ))}
        </div>
        {answers[question.Id]?.rating < 5 && (
            <div className="comment-container">
                <textarea 
                    value={answers[question.Id]?.comment || ''}
                    onChange={(e) => setAnswers(prev => ({
                        ...prev,
                        [question.Id]: {
                            ...prev[question.Id],
                            comment: e.target.value
                        }
                    }))}
                    placeholder="Ваш комментарий..."
                />
            </div>
        )}
    </div>
))}
            
            <div className="submit-container">
                <button 
                    onClick={submitAnswers}
                    className="submit-button"
                    disabled={!Object.values(answers).every(a => a.rating) || 
                             Object.values(answers).some(a => a.rating < 5 && !a.comment)}
                >
                    Отправить ответы
                </button>
            </div>
        </div>
    );
};

// Компонент для отображения результатов
window.CheckAnswersView = ({ data, userRole, onBack }) => {
    return (
        <div className="content" id="default_content">
            <button onClick={onBack} className="back-button">
                ← Вернуться к списку анкет
            </button>
            
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
                    onClick={() => CSP(data.Survey.id_survey, data.IdOmsu)}
                >
                    Подписать
                </button>
                <button 
                    className="submit-button" 
                    onClick={() => createPdfReport(data.Survey.id_survey, data.IdOmsu)}
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
        onClick={() => downloadSignedArchive(data.Survey.id_survey, data.IdOmsu)}
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


window.createPdfReport = async function(surveyId, omsuId) {
    try {
        const response = await fetch(`/create_pdf_report/${surveyId}/${omsuId}`);
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



window.downloadSignedArchive = async function(surveyId, omsuId) {
    try {
        const loadingIndicator = document.createElement('div');
        loadingIndicator.className = 'loading-overlay';
        loadingIndicator.innerHTML = `
            <div class="loading-content">
                <div class="loading-spinner"></div>
                <p>Подготовка архива...</p>
            </div>
        `;
        document.body.appendChild(loadingIndicator);

        const response = await fetch(`/download_signed_archive/${surveyId}/${omsuId}`);
        
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


window.CheckAnswersPage = ({ survey, omsuId, userRole, onBack }) => {
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
                
                const response = await fetch(`/answers/${survey.id_survey}/${omsuId}/${userRole}`);
                
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
    }, [survey.id_survey, omsuId, userRole]);

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
                <div onClick={onBack} className="back-link">
                    ← Вернуться к списку анкет
                </div>
            </div>
        );
    }

    return (
        <div className="answers-container">
            <div onClick={onBack} className="back-link">
                ← Вернуться к списку анкет
            </div>
            
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
            )}
        </div>
    );
}
