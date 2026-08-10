(() => {
  // wwwroot/js/features/survey/user-survey-flow-shared.js
  function getAnswersPageContainer(source) {
    if (source instanceof Element) {
      const closestPage = source.closest('[data-role="survey-answers-page"], [data-page="answers-check"]');
      if (closestPage) {
        return closestPage;
      }
    }
    if (source && typeof source.querySelector === "function") {
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
    if (source && typeof source.querySelector === "function") {
      const nestedPage = source.querySelector('[data-role="survey-fill-page"]');
      if (nestedPage) {
        return nestedPage;
      }
    }
    return document.querySelector('[data-role="survey-fill-page"]');
  }
  function applySurveySignedState(source, isSigned, mode = "answer") {
    const isDraftMode = mode === "draft";
    const page = isDraftMode ? getFillPageContainer(source) : getAnswersPageContainer(source);
    if (!page) {
      return;
    }
    if (isDraftMode) {
      page.dataset.isDraftSigned = isSigned ? "true" : "false";
    } else {
      page.dataset.isSigned = isSigned ? "true" : "false";
    }
    const signatureInfo = page.querySelector('[data-role="signature-info"]');
    const signatureStatus = page.querySelector('[data-role="signature-status"]');
    signatureInfo?.classList.remove("u-hidden", "is-hidden");
    if (signatureStatus) {
      signatureStatus.textContent = isSigned ? "Подписана" : "Нет подписи";
      signatureStatus.classList.toggle("signed", isSigned);
      signatureStatus.classList.toggle("not-signed", !isSigned);
    }
    const signButtons = isDraftMode ? /* @__PURE__ */ new Set([
      ...page.querySelectorAll('[data-role="draft-sign-button"]'),
      ...document.querySelectorAll('[data-role="draft-sign-button"]')
    ]) : /* @__PURE__ */ new Set([
      ...page.querySelectorAll('[data-role="sign-button"], [data-role-sign-button="true"]'),
      ...document.querySelectorAll('[data-role="sign-button"][data-survey-id], [data-role-sign-button="true"][data-survey-id]')
    ]);
    signButtons.forEach((signButton) => {
      if (signButton instanceof HTMLButtonElement) {
        signButton.disabled = isSigned;
        signButton.textContent = isSigned ? "Подписано" : "Подписать";
      }
    });
  }
  function showSurveyError(message) {
    const safeMessage = typeof window.normalizeClientErrorMessage === "function" ? window.normalizeClientErrorMessage(message) : message;
    window.AppUi?.notify?.(safeMessage, "error", { title: "Ошибка" });
  }
  function createSurveyHtmlFragment(html) {
    const range = document.createRange();
    range.selectNode(document.body);
    return range.createContextualFragment(html);
  }
  function renderSurveyHostError(host, message) {
    host.replaceChildren();
    showSurveyError(message);
  }
  function createSurveyModalFooterButton({ role, text, variant = "secondary", disabled = false, labelRole = "" }) {
    const button = window.AppUi.createButton({
      variant,
      disabled,
      dataset: { role }
    });
    if (labelRole) {
      const label = window.AppUi.createElement("span", {
        dataset: { role: labelRole },
        text
      });
      button.appendChild(label);
    } else {
      button.textContent = text;
    }
    return button;
  }
  function clearSurveyModalFooter(footerHost) {
    footerHost?.replaceChildren();
  }
  async function fetchSurveyModalContent(url, fallbackMessage) {
    const response = await fetch(url, {
      headers: {
        "X-Requested-With": "XMLHttpRequest"
      }
    });
    if (!response.ok) {
      throw new Error(fallbackMessage);
    }
    return response.text();
  }

  // wwwroot/js/features/survey/user-survey-signature.js
  var CADESCOM_CONTAINER_STORE = 100;
  var CAPICOM_STORE_OPEN_READ_ONLY = 0;
  var CADESCOM_CADES_BES = 1;
  var CADESCOM_BASE64_TO_BINARY = 1;
  var cadesPluginLoadPromise = null;
  function isEmbeddedBrowserEnvironment() {
    const userAgent = String(window.navigator.userAgent || "");
    const vendor = String(window.navigator.vendor || "");
    return /Electron|WebView|; wv\)|QtWebEngine|QtWebKit|Slack|Teams/i.test(userAgent) || userAgent.includes("Macintosh") && vendor === "Apple Computer, Inc." && !/Safari\//i.test(userAgent);
  }
  function getCryptoProUnavailableMessage() {
    if (isEmbeddedBrowserEnvironment()) {
      return "Подпись через CryptoPro Browser plug-in не поддерживается во встроенном браузере. Откройте систему в Chrome, Edge, Яндекс.Браузере или Safari с установленным CryptoPro Browser plug-in.";
    }
    return "CryptoPro Browser plug-in недоступен. Проверьте, что расширение и КриптоПРО CSP установлены в поддерживаемом браузере.";
  }
  function extractErrorMessage(error) {
    if (typeof error === "string") {
      return error.trim();
    }
    if (error instanceof Error) {
      return String(error.message || "").trim();
    }
    if (error && typeof error === "object" && "message" in error) {
      return String(error.message || "").trim();
    }
    return "";
  }
  function normalizeCryptoProError(error) {
    const rawMessage = extractErrorMessage(error);
    const message = rawMessage || "Ошибка при работе с CryptoPro Browser plug-in.";
    if (isEmbeddedBrowserEnvironment()) {
      return {
        message: getCryptoProUnavailableMessage(),
        showInstallHelp: true
      };
    }
    if (/нет доступных сертификатов/i.test(message)) {
      return {
        message: "Не найдено ни одного доступного сертификата для подписи.",
        showInstallHelp: false
      };
    }
    if (/сертификат не выбран/i.test(message)) {
      return {
        message: "Сертификат для подписи не выбран.",
        showInstallHelp: false
      };
    }
    if (/истекло время ожидания загрузки плагина/i.test(message)) {
      return {
        message: "CryptoPro Browser plug-in не ответил. Обычно это означает, что расширение не установлено, выключено в браузере или страница открыта во встроенном браузере/вебвью, где CryptoPro не работает.",
        showInstallHelp: true
      };
    }
    if (/плагин недоступен|ошибка при загрузке плагина|chrome-extension:\/\/invalid/i.test(message)) {
      return {
        message: "CryptoPro Browser plug-in не установлен, отключен или не может загрузиться в текущем браузере. Проверьте расширение, КриптоПРО CSP и откройте страницу во внешнем поддерживаемом браузере.",
        showInstallHelp: true
      };
    }
    if (/не удалось загрузить скрипт/i.test(message)) {
      return {
        message: "Не удалось загрузить модуль подписи CryptoPro со страницы приложения.",
        showInstallHelp: false
      };
    }
    if (/CAdESCOM|CreateObjectAsync|объект/i.test(message)) {
      return {
        message: "CryptoPro установлен, но браузер не смог создать объекты плагина. Проверьте версию КриптоПРО CSP и расширение.",
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
  function notifySignature(message, type = "error", options = {}) {
    const safeMessage = typeof window.normalizeClientErrorMessage === "function" ? window.normalizeClientErrorMessage(message) : message;
    window.AppUi?.notify?.(safeMessage || "Произошла ошибка.", type, {
      title: type === "success" ? "Успешно" : "Ошибка",
      ...options
    });
  }
  function createSignatureModalFrame(titleText, options = {}) {
    if (typeof window.createSiteModalFrame !== "function") {
      throw new Error("Модальное окно подписи недоступно.");
    }
    const frame = window.createSiteModalFrame({
      title: titleText,
      className: ["signature-modal", options.className || ""].filter(Boolean).join(" "),
      bodyClassName: ["signature-modal__body", options.bodyClassName || ""].filter(Boolean).join(" "),
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
        createElement("strong", { text: labelText }),
        createElement("span", { text: ` ${valueText}` })
      ]
    });
    parent.appendChild(row);
    return row;
  }
  function loadScriptOnce(src) {
    return new Promise((resolve, reject) => {
      const existing = document.querySelector(`script[data-dynamic-src="${src}"]`);
      if (existing) {
        if (existing.dataset.loaded === "true") {
          resolve();
          return;
        }
        existing.addEventListener("load", () => resolve(), { once: true });
        existing.addEventListener("error", () => reject(new Error(`Не удалось загрузить скрипт ${src}`)), { once: true });
        return;
      }
      const script = document.createElement("script");
      script.src = src;
      script.async = true;
      script.dataset.dynamicSrc = src;
      script.onload = () => {
        script.dataset.loaded = "true";
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
    if (typeof window.cadesplugin !== "undefined") {
      await window.cadesplugin;
      return window.cadesplugin;
    }
    if (!cadesPluginLoadPromise) {
      cadesPluginLoadPromise = loadScriptOnce("/js/cadesplugin_api.js").then(async () => {
        if (typeof window.cadesplugin === "undefined") {
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
      const signatureMode = options.mode === "draft" ? "draft" : "answer";
      const page = signatureMode === "draft" ? getFillPageContainer(options.source || document) : getAnswersPageContainer(options.source || document);
      const signedDatasetKey = signatureMode === "draft" ? "isDraftSigned" : "isSigned";
      if (page?.dataset[signedDatasetKey] === "true") {
        notifySignature("Анкета уже подписана и не может быть подписана повторно.");
        return;
      }
      if (typeof options.beforeSign === "function") {
        await options.beforeSign();
      }
      await ensureCadesPluginLoaded();
      await checkCSPAvailable();
      const dataToSign = await getDataForSignature(id, organizationId, signatureMode);
      const signature = await createDigitalSignature(dataToSign);
      await sendSignatureToServer(id, organizationId, signature, dataToSign, signatureMode);
      updateUISuccess(signatureMode, page);
      if (signatureMode !== "draft" && typeof window.refreshSurveyUserPageData === "function") {
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
          issuer,
          validFrom,
          validTo,
          thumbprint,
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
  async function getDataForSignature(id, organizationId, mode = "answer") {
    const route = mode === "draft" ? "draft-signatures" : "signatures";
    const response = await fetch(`/${route}/${id}/${organizationId}`);
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || "Ошибка получения данных");
    }
    const contentType = String(response.headers.get("content-type") || "").toLowerCase();
    if (contentType.includes("application/json")) {
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
      frame = createSignatureModalFrame("Выберите сертификат для подписи", {
        className: "signature-certificate-modal",
        onClose: () => finish(null),
        footerButtons: [
          {
            variant: "secondary",
            text: "Отмена",
            onClick: (event) => {
              event.preventDefault();
              finish(null);
            }
          }
        ]
      });
      frame.modal.addEventListener("site-modal:hidden", () => finish(null));
      const { body } = frame;
      const listContainer = createElement("div", { className: "cert-list-container" });
      const certList = createElement("div", { className: "cert-list" });
      certificates.forEach((cert) => {
        const certItem = createElement("button", {
          type: "button",
          className: "cert-item",
          dataset: { index: cert.index },
          events: {
            click: () => finish(cert)
          }
        });
        const subject = createElement("div", {
          className: "cert-subject",
          text: cert.subject
        });
        const details = createElement("div", { className: "cert-details" });
        appendStrongText(details, "div", "Издатель:", cert.issuer);
        appendStrongText(
          details,
          "div",
          "Действителен:",
          `${new Date(cert.validFrom).toLocaleDateString()} - ${new Date(cert.validTo).toLocaleDateString()}`
        );
        appendStrongText(details, "div", "Отпечаток:", cert.thumbprint);
        certItem.appendChild(subject);
        certItem.appendChild(details);
        certList.appendChild(certItem);
      });
      listContainer.appendChild(certList);
      body.appendChild(listContainer);
      frame.show();
    });
  }
  async function createDigitalSignature(data) {
    try {
      const certificates = await listAllCertificates();
      if (certificates.length === 0) {
        throw new Error("Нет доступных сертификатов");
      }
      const selectedCert = await showCertificateSelectionDialog(certificates);
      if (!selectedCert) {
        throw new Error("Сертификат не выбран");
      }
      const signer = await cadesplugin.CreateObjectAsync("CAdESCOM.CPSigner");
      await signer.propset_Certificate(selectedCert.certificate);
      const signedData = await cadesplugin.CreateObjectAsync("CAdESCOM.CadesSignedData");
      const signaturePayload = typeof data === "string" ? { content: data, contentEncoding: "utf8", detached: false } : {
        content: data?.content || "",
        contentEncoding: data?.contentEncoding || "utf8",
        detached: Boolean(data?.detached)
      };
      if (signaturePayload.contentEncoding === "base64") {
        await signedData.propset_ContentEncoding(CADESCOM_BASE64_TO_BINARY);
      }
      await signedData.propset_Content(signaturePayload.content);
      return await signedData.SignCades(signer, CADESCOM_CADES_BES, signaturePayload.detached);
    } catch (error) {
      console.error("Ошибка при создании подписи:", error);
      throw error;
    }
  }
  async function sendSignatureToServer(id, organizationId, signature, dataToSign, mode = "answer") {
    const request = { signature };
    if (dataToSign && typeof dataToSign === "object") {
      request.signedContent = dataToSign.content || "";
      request.contentEncoding = dataToSign.contentEncoding || "utf8";
      request.detached = Boolean(dataToSign.detached);
    }
    const route = mode === "draft" ? "draft-signatures" : "signatures";
    const response = await fetch(`/${route}/${id}/${organizationId}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    });
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || "Ошибка сервера");
    }
  }
  function updateUISuccess(mode = "answer", source = document) {
    applySurveySignedState(source || document, true, mode);
    notifySignature("Документ успешно подписан", "success");
  }

  // wwwroot/js/features/survey/user-survey-modal-pages.js
  window.createAnswerReport = function createAnswerReport(idSurvey, organizationId, type) {
    window.AppScrollState?.prepareNavigation({ carry: true });
    window.location.assign(`/answers/${idSurvey}/${organizationId}/report/${type}`);
  };
  window.downloadAnswerDocument = function downloadAnswerDocument(surveyId, organizationId, triggerElement) {
    const page = getAnswersPageContainer(triggerElement);
    const isSigned = page?.dataset.isSigned === "true";
    if (isSigned) {
      return window.downloadSignedArchive(surveyId, organizationId);
    }
    return window.createPdfReport(surveyId, organizationId);
  };
  window.fetchSurveyFillContentHtml = function fetchSurveyFillContentHtml(surveyId, organizationId) {
    return fetchSurveyModalContent(
      `/survey/${surveyId}/organizations/${organizationId}/fill-content`,
      "Не удалось загрузить анкету"
    );
  };
  window.fetchSurveyAnswersContentHtml = function fetchSurveyAnswersContentHtml(surveyId, organizationId) {
    return fetchSurveyModalContent(
      `/answers/${surveyId}/${organizationId}/content`,
      "Не удалось загрузить ответы по анкете"
    );
  };
  function downloadBlob(blob, fileName) {
    const url = window.URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    window.URL.revokeObjectURL(url);
  }
  function getSurveyIdentifier(survey) {
    const value = survey?.id_survey || survey?.IdSurvey || survey?.idSurvey || survey?.Id || survey?.id || 0;
    const numericValue = Number(value);
    return Number.isFinite(numericValue) ? numericValue : 0;
  }
  async function postSurveyJson(url, payload, fallbackMessage) {
    const response = await fetch(url, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-Requested-With": "XMLHttpRequest"
      },
      body: JSON.stringify(payload)
    });
    if (!response.ok) {
      const errorData = await response.json().catch(() => null);
      throw new Error(errorData?.error || fallbackMessage);
    }
    return response.json().catch(() => null);
  }
  async function mountSurveyModalHtml({
    host,
    footerHost,
    initialHtml,
    loadHtml,
    bindPage,
    isDestroyed,
    errorMessage
  }) {
    try {
      const html = typeof initialHtml === "string" ? initialHtml : await loadHtml();
      if (isDestroyed()) {
        return;
      }
      host.replaceChildren(createSurveyHtmlFragment(html));
      bindPage();
    } catch (error) {
      if (isDestroyed()) {
        return;
      }
      renderSurveyHostError(host, error?.message || errorMessage);
      clearSurveyModalFooter(footerHost);
    }
  }
  window.mountSurveyFillPage = function mountSurveyFillPage(host, { survey, organizationId, onBack, onSubmitted, initialHtml, footerHost }) {
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
      draftSignButton: null,
      submitButton: null,
      submitLabel: null,
      cancelButton: null
    };
    function getQuestionNodes() {
      return Array.from(host.querySelectorAll('[data-role="survey-question"]'));
    }
    function getCurrentSurveyId() {
      const rawValue = refs.page?.dataset.surveyId || host.querySelector('[data-role="survey-fill-page"]')?.dataset.surveyId || getSurveyIdentifier(survey) || 0;
      const numericValue = Number(rawValue);
      return Number.isFinite(numericValue) ? numericValue : 0;
    }
    function renderError(options = {}) {
      const shouldNotify = options.notify === true;
      host.querySelector('[data-role="error"]')?.classList.add("u-hidden");
      if (shouldNotify && error) {
        showSurveyError(error);
      }
    }
    function renderSubmitState() {
      if (!refs.submitButton || !refs.submitLabel) {
        return;
      }
      refs.submitButton.disabled = loading;
      refs.submitButton.querySelector(".loading-spinner")?.remove();
      if (loading) {
        const spinner = document.createElement("span");
        spinner.className = "loading-spinner";
        refs.submitButton.insertBefore(spinner, refs.submitLabel);
        refs.submitLabel.textContent = "Отправка...";
        return;
      }
      refs.submitLabel.textContent = "Отправить ответы";
    }
    function buildPayloadAnswers({ requireComplete = false } = {}) {
      const payloadAnswers = [];
      const questionNodes = getQuestionNodes();
      questionNodes.forEach((questionNode) => {
        const questionId = questionNode.dataset.questionId || "";
        const questionText = questionNode.querySelector('[data-role="question-title"]')?.textContent?.trim() || "";
        const answer = answers[questionId] || {};
        const rating = Number(answer.rating || 0);
        const comment = rating === 5 ? "" : String(answer.comment || "").trim();
        if (!rating && !comment && !requireComplete) {
          return;
        }
        if (requireComplete && (!Number.isFinite(rating) || rating < 1 || rating > 5)) {
          throw new Error("Необходимо ответить на все вопросы анкеты.");
        }
        if (requireComplete && rating < 5 && !comment) {
          throw new Error("Для оценки ниже 5 требуется комментарий.");
        }
        payloadAnswers.push({
          question_id: questionId,
          question_text: questionText,
          rating: rating || null,
          comment
        });
      });
      if (requireComplete && payloadAnswers.length !== questionNodes.length) {
        throw new Error("Необходимо ответить на все вопросы анкеты.");
      }
      return payloadAnswers;
    }
    function updateDraftSignedState(isSigned) {
      if (refs.page) {
        refs.page.dataset.isDraftSigned = isSigned ? "true" : "false";
      }
      applySurveySignedState(refs.page || host, isSigned, "draft");
    }
    async function saveDraft({ showErrorOnFailure = false } = {}) {
      const payloadAnswers = buildPayloadAnswers();
      if (payloadAnswers.length === 0) {
        return true;
      }
      const surveyId = getCurrentSurveyId();
      if (surveyId <= 0 || organizationId <= 0) {
        const message = "Не удалось определить анкету для сохранения черновика.";
        if (showErrorOnFailure) {
          throw new Error(message);
        }
        console.error(message);
        return false;
      }
      try {
        await postSurveyJson("/answers/draft", {
          id_survey: surveyId,
          id_organization: organizationId,
          answers: payloadAnswers
        }, "Ошибка при сохранении черновика");
      } catch (error2) {
        if (showErrorOnFailure) {
          throw error2;
        }
        console.error(error2?.message || "Ошибка при сохранении черновика");
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
        saveDraft().catch((err) => console.error("Ошибка при сохранении черновика:", err));
      }, 450);
    }
    function renderFooter() {
      if (!footerHost) {
        return {};
      }
      const isDraftSigned = refs.page?.dataset.isDraftSigned === "true" || host.querySelector('[data-role="survey-fill-page"]')?.dataset.isDraftSigned === "true";
      const signButton = createSurveyModalFooterButton({
        role: "draft-sign-button",
        text: isDraftSigned ? "Подписано" : "Подписать",
        variant: "primary",
        disabled: isDraftSigned
      });
      const cancelButton = createSurveyModalFooterButton({
        role: "cancel-btn",
        text: "Отмена",
        variant: "secondary"
      });
      const submitButton = createSurveyModalFooterButton({
        role: "submit",
        text: "Отправить ответы",
        variant: "primary",
        labelRole: "submit-label"
      });
      signButton.classList.add("survey-user-modal__footer-left");
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
        button.classList.toggle("active", answer.rating === rating);
      });
      const commentBlock = questionElement.querySelector('[data-role="comment-block"]');
      const commentInput = questionElement.querySelector('[data-role="comment-input"]');
      const showComment = answer.rating > 0 && answer.rating < 5;
      if (commentBlock) {
        commentBlock.classList.toggle("u-hidden", !showComment);
      }
      if (commentInput) {
        commentInput.value = showComment ? answer.comment || "" : "";
      }
    }
    function bindQuestion(questionElement) {
      const questionId = questionElement.dataset.questionId || "";
      if (!questionId) {
        return;
      }
      const activeButton = questionElement.querySelector('[data-role="rating-button"].active');
      const activeRating = Number(activeButton?.dataset.rating || 0);
      const commentInput = questionElement.querySelector('[data-role="comment-input"]');
      if (activeRating > 0 || commentInput?.value) {
        answers[questionId] = {
          rating: activeRating || null,
          comment: activeRating === 5 ? "" : commentInput?.value || ""
        };
      }
      questionElement.querySelectorAll('[data-role="rating-button"]').forEach((button) => {
        button.addEventListener("click", () => {
          error = null;
          const rating = Number(button.dataset.rating || 0);
          answers[questionId] = {
            ...answers[questionId],
            rating,
            comment: rating < 5 ? answers[questionId]?.comment || "" : ""
          };
          updateDraftSignedState(false);
          renderError();
          updateQuestionState(questionId, questionElement);
          scheduleDraftSave();
        });
      });
      commentInput?.addEventListener("input", (event) => {
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
          throw new Error("Не удалось определить анкету для отправки ответов.");
        }
        await postSurveyJson("/answers/create", {
          id_survey: surveyId,
          id_organization: organizationId,
          answers: payloadAnswers
        }, "Ошибка при отправке ответов");
        onSubmitted?.({
          survey,
          answers: payloadAnswers,
          organizationId
        });
      } catch (err) {
        error = err?.message || "Не удалось отправить ответы";
        renderError({ notify: true });
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
          throw new Error("Не удалось определить анкету для подписи.");
        }
        await CSP(surveyId, organizationId, {
          mode: "draft",
          source: refs.page || host
        });
      } catch (err) {
        error = err?.message || "Не удалось подписать черновик";
        renderError({ notify: true });
      }
    }
    function bindPage() {
      host.querySelector('[data-role="body-actions"]')?.classList.add("u-hidden");
      refs = {
        page: host.querySelector('[data-role="survey-fill-page"]'),
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
      refs.draftSignButton?.addEventListener("click", signDraft);
      refs.submitButton?.addEventListener("click", submitAnswers);
      refs.cancelButton?.addEventListener("click", () => onBack?.());
      getQuestionNodes().forEach(bindQuestion);
      updateDraftSignedState(refs.page?.dataset.isDraftSigned === "true");
      renderError();
      renderSubmitState();
    }
    mountSurveyModalHtml({
      host,
      footerHost,
      initialHtml,
      loadHtml: () => window.fetchSurveyFillContentHtml(getCurrentSurveyId(), organizationId),
      bindPage,
      isDestroyed: () => destroyed,
      errorMessage: "Не удалось загрузить анкету"
    });
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
      if (!response.ok) throw new Error("Ошибка создания PDF");
      downloadBlob(await response.blob(), `Анкета_${surveyId}_${(/* @__PURE__ */ new Date()).toISOString().slice(0, 10)}.pdf`);
    } catch (error) {
      console.error("Ошибка при создании PDF:", error);
      showSurveyError("Не удалось создать PDF файл");
    }
  };
  window.downloadSignedArchive = async function(surveyId, organizationId) {
    try {
      const response = await fetch(`/answers/${surveyId}/${organizationId}/signed-archive`);
      if (!response.ok) {
        const errorData = await response.json().catch(() => null);
        const errorMessage = errorData?.error || "Ошибка загрузки архива";
        throw new Error(errorMessage);
      }
      downloadBlob(await response.blob(), `Анкета_с_подписью_${surveyId}.zip`);
    } catch (error) {
      console.error("Ошибка при загрузке архива:", error);
      const errorMessage = error.message || "Не удалось загрузить архив с подписью";
      showSurveyError(errorMessage);
      if (error.details) {
        console.error("Детали ошибки:", error.details);
      }
    }
  };
  window.mountCheckAnswersPage = function mountCheckAnswersPage(host, { survey, organizationId, initialHtml, footerHost }) {
    if (!host) {
      return null;
    }
    let destroyed = false;
    function renderFooter(page, surveyId, currentOrganizationId) {
      if (!footerHost) {
        return {};
      }
      const isSigned = page?.dataset.isSigned === "true";
      const signButton = createSurveyModalFooterButton({
        role: "sign-button",
        text: isSigned ? "Подписано" : "Подписать",
        variant: "primary",
        disabled: isSigned
      });
      const downloadButton = createSurveyModalFooterButton({
        role: "download-btn",
        text: "Скачать ответы",
        variant: "secondary"
      });
      signButton.dataset.surveyId = String(surveyId || "");
      signButton.dataset.organizationId = String(currentOrganizationId || "");
      downloadButton.dataset.surveyId = String(surveyId || "");
      downloadButton.dataset.organizationId = String(currentOrganizationId || "");
      footerHost.replaceChildren(downloadButton, signButton);
      return {
        signButton,
        downloadButton
      };
    }
    function bindPage() {
      const page = host.querySelector('[data-role="survey-answers-page"]');
      const surveyId = Number(page?.dataset.surveyId || getSurveyIdentifier(survey));
      const currentOrganizationId = Number(page?.dataset.organizationId || organizationId || 0);
      const footerRefs = renderFooter(page, surveyId, currentOrganizationId);
      host.querySelector('[data-role="body-actions"]')?.classList.add("u-hidden");
      const downloadButton = footerRefs.downloadButton || host.querySelector('[data-role="download-btn"]');
      const signButton = footerRefs.signButton || host.querySelector('[data-role="sign-actions"] button');
      downloadButton?.addEventListener("click", (event) => {
        event.preventDefault();
        if (surveyId > 0 && currentOrganizationId > 0) {
          window.downloadAnswerDocument(surveyId, currentOrganizationId, downloadButton);
        }
      });
      signButton?.addEventListener("click", (event) => {
        event.preventDefault();
        if (signButton.disabled) {
          return;
        }
        if (surveyId > 0 && currentOrganizationId > 0) {
          CSP(surveyId, currentOrganizationId);
        }
      });
    }
    mountSurveyModalHtml({
      host,
      footerHost,
      initialHtml,
      loadHtml: () => window.fetchSurveyAnswersContentHtml(getSurveyIdentifier(survey), organizationId),
      bindPage,
      isDestroyed: () => destroyed,
      errorMessage: "Не удалось загрузить ответы по анкете"
    });
    return () => {
      destroyed = true;
      host.replaceChildren();
      clearSurveyModalFooter(footerHost);
    };
  };

  // wwwroot/js/features/answers/check-answers-page.js
  (function() {
    if (typeof window.createAnswerReport !== "function") {
      window.createAnswerReport = function createAnswerReport2(idSurvey, idOrganization, type) {
        window.AppScrollState?.prepareNavigation({ carry: true });
        window.location.assign(`/answers/${idSurvey}/${idOrganization}/report/${type}`);
      };
    }
  })();
})();
//# sourceMappingURL=check-answers-app.js.map
