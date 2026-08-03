(() => {
  // Web/wwwroot/js/features/survey/user-survey-flow-shared.js
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

  // Web/wwwroot/js/features/survey/user-survey-signature.js
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

  // Web/wwwroot/js/features/survey/user-survey-modal-pages.js
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
        const comment = String(answer.comment || "").trim();
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
        commentInput.value = answer.comment || "";
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
          comment: commentInput?.value || ""
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

  // Web/wwwroot/js/features/survey/survey-filter-popover.js
  (function() {
    if (window.SurveyFilterPopover) {
      return;
    }
    function applyOpenState(instance, isOpen) {
      if (!instance?.state || !instance?.refs?.trigger || !instance?.refs?.popover) {
        return;
      }
      instance.state.isOpen = Boolean(isOpen);
      instance.refs.trigger.setAttribute("aria-expanded", instance.state.isOpen ? "true" : "false");
      if (instance.state.isOpen) {
        window.AppCheckboxDropdown?.scheduleListHeightUpdate(instance.refs.popover);
      }
    }
    function setOpen(instance, isOpen) {
      if (instance?.dropdownController?.setOpen && !instance.isSyncingDropdownOpenState) {
        instance.isSyncingDropdownOpenState = true;
        instance.dropdownController.setOpen(Boolean(isOpen));
        instance.isSyncingDropdownOpenState = false;
        return;
      }
      applyOpenState(instance, isOpen);
    }
    function cleanupDetachedInstances(collections) {
      collections.forEach((collection) => {
        Array.from(collection.entries()).forEach(([root]) => {
          if (!document.contains(root)) {
            collection.delete(root);
          }
        });
      });
    }
    function closeAll(collections, exceptRoot = null) {
      cleanupDetachedInstances(collections);
      collections.forEach((collection) => {
        collection.forEach((instance, root) => {
          if (root !== exceptRoot) {
            setOpen(instance, false);
          }
        });
      });
    }
    function containsTarget(collections, target) {
      return collections.some((collection) => Array.from(collection.keys()).some((root) => root.contains(target)));
    }
    function unbindCollection(collection) {
      collection.forEach((instance, root) => {
        if (instance.handlers?.click) {
          root.removeEventListener("click", instance.handlers.click);
        }
        if (instance.handlers?.change) {
          root.removeEventListener("change", instance.handlers.change);
        }
        instance.dropdownController?.destroy?.();
      });
      collection.clear();
    }
    window.SurveyFilterPopover = {
      setOpen,
      applyOpenState,
      cleanupDetachedInstances,
      closeAll,
      containsTarget,
      unbindCollection
    };
  })();

  // Web/wwwroot/js/features/survey/survey-filter-summary.js
  (function() {
    if (window.SurveyFilterSummary) {
      return;
    }
    const {
      getRangeDescription,
      getMonthDescription,
      getYearDescription
    } = window.SurveyFilterCore;
    function getPageItemLabel(page) {
      return page?.dataset?.filterItemLabel || "анкет";
    }
    function getPageDateSummary(page) {
      return page?.dataset?.filterDateSummary || "у которых дата начала и дата конца попадают";
    }
    function shouldHideCountSummary(page) {
      return page?.dataset?.filterHideCountSummary === "true";
    }
    function getOrganizationFilterLabel(selectedOrganizations) {
      if (!Array.isArray(selectedOrganizations) || selectedOrganizations.length === 0) {
        return "Фильтр по организациям";
      }
      return `Организации: ${selectedOrganizations.length}`;
    }
    function getSurveyNameFilterLabel(selectedSurveyNames) {
      if (!Array.isArray(selectedSurveyNames) || selectedSurveyNames.length === 0) {
        return "Фильтр по анкетам";
      }
      return `Анкеты: ${selectedSurveyNames.length}`;
    }
    function updateDate(instance, visibleCount, totalCount) {
      const { state, refs } = instance;
      const itemLabel = getPageItemLabel(instance.page);
      const dateSummary = getPageDateSummary(instance.page);
      const hideCountSummary = shouldHideCountSummary(instance.page);
      let label = "Фильтр по периоду";
      let summary = hideCountSummary ? "" : `Показано ${visibleCount} из ${totalCount} ${itemLabel}.`;
      if (state.activeFilterType === "year" && Number.isInteger(state.activeYear)) {
        const yearLabel = getYearDescription(state.activeYear);
        label = yearLabel;
        if (!hideCountSummary) {
          summary = `Показано ${visibleCount} из ${totalCount} ${itemLabel}, ${dateSummary} в ${yearLabel}.`;
        }
      } else if (state.activeFilterType === "month" && state.activeMonth) {
        const monthLabel = getMonthDescription(state.activeMonth.year, state.activeMonth.monthIndex);
        label = monthLabel;
        if (!hideCountSummary) {
          summary = `Показано ${visibleCount} из ${totalCount} ${itemLabel}, ${dateSummary} в ${monthLabel}.`;
        }
      } else if (state.activeFilterType === "range" && state.rangeStart && state.rangeEnd) {
        const rangeLabel = getRangeDescription(state.rangeStart, state.rangeEnd);
        label = rangeLabel;
        if (!hideCountSummary) {
          summary = `Показано ${visibleCount} из ${totalCount} ${itemLabel}, ${dateSummary} в период ${rangeLabel}.`;
        }
      }
      refs.label.textContent = label;
      if (refs.summary) {
        refs.summary.textContent = summary;
      }
      refs.clearButton.disabled = state.activeFilterType === "all" && !Number.isInteger(state.activeYear) && !state.activeMonth && !state.rangeStart && !state.rangeEnd;
    }
    function updateOrganization(instance, visibleCount, totalCount, serverFilters) {
      const selectedOrganizations = instance.state.serverMode ? serverFilters.getSelectedOptionNames(instance.state.availableOrganizationOptions, instance.state.selectedOrganizationIds) : instance.state.selectedOrganizations;
      const label = getOrganizationFilterLabel(selectedOrganizations);
      const itemLabel = getPageItemLabel(instance.page);
      const hideCountSummary = shouldHideCountSummary(instance.page);
      let summary = hideCountSummary ? "" : `Показано ${visibleCount} из ${totalCount} ${itemLabel}.`;
      if (selectedOrganizations.length === 1) {
        summary = hideCountSummary ? `Организация: ${selectedOrganizations[0]}.` : `Показано ${visibleCount} из ${totalCount} ${itemLabel} для организации ${selectedOrganizations[0]}.`;
      } else if (selectedOrganizations.length > 1) {
        summary = hideCountSummary ? `Выбрано организаций: ${selectedOrganizations.length}.` : `Показано ${visibleCount} из ${totalCount} ${itemLabel} для ${selectedOrganizations.length} организаций.`;
      }
      instance.refs.label.textContent = label;
      if (instance.refs.summary) {
        instance.refs.summary.textContent = summary;
      }
      instance.refs.clearButton.disabled = instance.state.serverMode ? instance.state.selectedOrganizationIds.length === 0 : selectedOrganizations.length === 0;
    }
    function updateSurveyName(instance, visibleCount, totalCount, serverFilters) {
      const selectedSurveyNames = instance.state.serverMode ? serverFilters.getSelectedOptionNames(instance.state.availableSurveyOptions, instance.state.selectedSurveyIds) : instance.state.selectedSurveyNames;
      const label = getSurveyNameFilterLabel(selectedSurveyNames);
      const itemLabel = getPageItemLabel(instance.page);
      const hideCountSummary = shouldHideCountSummary(instance.page);
      let summary = hideCountSummary ? "" : `Показано ${visibleCount} из ${totalCount} ${itemLabel}.`;
      if (selectedSurveyNames.length === 1) {
        summary = hideCountSummary ? `Анкета: ${selectedSurveyNames[0]}.` : `Показано ${visibleCount} из ${totalCount} ${itemLabel} по анкете ${selectedSurveyNames[0]}.`;
      } else if (selectedSurveyNames.length > 1) {
        summary = hideCountSummary ? `Выбрано анкет: ${selectedSurveyNames.length}.` : `Показано ${visibleCount} из ${totalCount} ${itemLabel} по ${selectedSurveyNames.length} анкетам.`;
      }
      instance.refs.label.textContent = label;
      if (instance.refs.summary) {
        instance.refs.summary.textContent = summary;
      }
      instance.refs.clearButton.disabled = instance.state.serverMode ? instance.state.selectedSurveyIds.length === 0 : selectedSurveyNames.length === 0;
    }
    window.SurveyFilterSummary = {
      getPageItemLabel,
      getPageDateSummary,
      shouldHideCountSummary,
      getOrganizationFilterLabel,
      getSurveyNameFilterLabel,
      updateDate,
      updateOrganization,
      updateSurveyName
    };
  })();

  // Web/wwwroot/js/features/survey/survey-date-filter.js
  (function() {
    if (window.SurveyDateFilter) {
      return;
    }
    const {
      MONTH_NAMES,
      WEEKDAY_NAMES,
      toIso,
      parseIso,
      shiftMonth,
      getMonthBounds,
      getYearBounds,
      getDecadeStart,
      getDisplayDate,
      compareIso,
      getRangeDescription,
      getMonthDescription,
      createElement: createElement2
    } = window.SurveyFilterCore;
    function ensurePopoverHeader(root) {
      const popover = root.querySelector('[data-role="survey-date-filter-popover"]');
      const modeSwitch = root.querySelector('[data-role="survey-date-filter-mode-switch"]');
      if (!popover || !modeSwitch) {
        return;
      }
      let header = popover.querySelector(".survey-period-filter__header");
      if (!header) {
        header = createElement2("div", "survey-period-filter__header");
        popover.insertBefore(header, modeSwitch);
        header.appendChild(modeSwitch);
      }
      if (!modeSwitch.querySelector('[data-role="survey-date-filter-mode"][data-mode="year"]')) {
        const yearModeButton = createElement2("button", "app-button app-button--secondary survey-period-filter__mode-button", "По году");
        yearModeButton.type = "button";
        yearModeButton.dataset.role = "survey-date-filter-mode";
        yearModeButton.dataset.mode = "year";
        modeSwitch.insertBefore(yearModeButton, modeSwitch.firstChild);
      }
      if (!header.querySelector('[data-role="survey-date-filter-close"]')) {
        const closeButton = createElement2("button", "survey-period-filter__close-button modal-close");
        closeButton.type = "button";
        closeButton.dataset.role = "survey-date-filter-close";
        closeButton.setAttribute("aria-label", "Закрыть фильтр");
        const closeIcon = createElement2("i", "fas fa-xmark");
        closeIcon.setAttribute("aria-hidden", "true");
        closeButton.appendChild(closeIcon);
        header.appendChild(closeButton);
      }
      if (!popover.querySelector('[data-role="survey-date-filter-year-panel"]')) {
        const yearPanel = createElement2("div", "survey-period-filter__panel is-hidden");
        yearPanel.dataset.role = "survey-date-filter-year-panel";
        const panelNav = createElement2("div", "survey-period-filter__panel-nav");
        const prevButton = createElement2("button", "survey-period-filter__nav-button");
        prevButton.type = "button";
        prevButton.dataset.role = "survey-date-filter-year-range-prev";
        prevButton.setAttribute("aria-label", "Предыдущие годы");
        prevButton.appendChild(createElement2("i", "fas fa-chevron-left"));
        prevButton.firstChild?.setAttribute("aria-hidden", "true");
        const title = createElement2("span", "survey-period-filter__panel-title");
        title.dataset.role = "survey-date-filter-year-range-label";
        const nextButton = createElement2("button", "survey-period-filter__nav-button");
        nextButton.type = "button";
        nextButton.dataset.role = "survey-date-filter-year-range-next";
        nextButton.setAttribute("aria-label", "Следующие годы");
        nextButton.appendChild(createElement2("i", "fas fa-chevron-right"));
        nextButton.firstChild?.setAttribute("aria-hidden", "true");
        panelNav.appendChild(prevButton);
        panelNav.appendChild(title);
        panelNav.appendChild(nextButton);
        const yearsContainer = createElement2("div", "survey-period-filter__years");
        yearsContainer.dataset.role = "survey-date-filter-years";
        yearPanel.appendChild(panelNav);
        yearPanel.appendChild(yearsContainer);
        const monthPanel = popover.querySelector('[data-role="survey-date-filter-month-panel"]');
        if (monthPanel) {
          popover.insertBefore(yearPanel, monthPanel);
        } else {
          popover.appendChild(yearPanel);
        }
      }
    }
    function getInitialState(page, today, serverFilters = window.SurveyServerFilterState) {
      const state = {
        isOpen: false,
        mode: "month",
        monthViewYear: today.getFullYear(),
        yearViewStart: getDecadeStart(today.getFullYear()),
        rangeViewDate: new Date(today.getFullYear(), today.getMonth(), 1),
        activeFilterType: "all",
        activeYear: null,
        activeMonth: null,
        rangeStart: "",
        rangeEnd: ""
      };
      const config = serverFilters.getConfig(page);
      if (!config?.enableDateFilter) {
        return state;
      }
      if (Number.isInteger(config.year)) {
        state.activeFilterType = "year";
        state.activeYear = config.year;
        state.monthViewYear = config.year;
        state.yearViewStart = getDecadeStart(config.year);
        return state;
      }
      const monthMatch = config.month.match(/^(\d{4})-(\d{2})$/);
      if (monthMatch) {
        const year = Number.parseInt(monthMatch[1], 10);
        const monthIndex = Number.parseInt(monthMatch[2], 10) - 1;
        if (Number.isInteger(year) && Number.isInteger(monthIndex) && monthIndex >= 0 && monthIndex < 12) {
          state.activeFilterType = "month";
          state.activeMonth = { year, monthIndex };
          state.monthViewYear = year;
          state.yearViewStart = getDecadeStart(year);
          return state;
        }
      }
      if (config.dateFrom && config.dateTo) {
        state.activeFilterType = "range";
        state.rangeStart = config.dateFrom;
        state.rangeEnd = config.dateTo;
        const rangeDate = parseIso(config.dateFrom);
        if (rangeDate) {
          state.rangeViewDate = new Date(rangeDate.getFullYear(), rangeDate.getMonth(), 1);
        }
      }
      return state;
    }
    function getCurrentRangeDisplayState(state) {
      if (state.mode === "range" && state.rangeStart && !state.rangeEnd) {
        return { start: state.rangeStart, end: "" };
      }
      if (state.rangeStart && state.rangeEnd) {
        return { start: state.rangeStart, end: state.rangeEnd };
      }
      return { start: "", end: "" };
    }
    function getActiveFilterBounds(state) {
      if (state.activeFilterType === "year" && Number.isInteger(state.activeYear)) {
        return getYearBounds(state.activeYear);
      }
      if (state.activeFilterType === "month" && state.activeMonth) {
        return getMonthBounds(state.activeMonth.year, state.activeMonth.monthIndex);
      }
      if (state.activeFilterType === "range" && state.rangeStart && state.rangeEnd) {
        return {
          start: state.rangeStart,
          end: state.rangeEnd
        };
      }
      return null;
    }
    function renderModeSwitch(instance) {
      const { state, refs } = instance;
      refs.yearPanel.classList.toggle("is-hidden", state.mode !== "year");
      refs.monthPanel.classList.toggle("is-hidden", state.mode !== "month");
      refs.rangePanel.classList.toggle("is-hidden", state.mode !== "range");
      refs.yearModeButton.classList.toggle("is-active", state.mode === "year");
      refs.monthModeButton.classList.toggle("is-active", state.mode === "month");
      refs.rangeModeButton.classList.toggle("is-active", state.mode === "range");
    }
    function renderYearPanel(instance) {
      const { state, refs } = instance;
      refs.yearRangeLabel.textContent = `${state.yearViewStart} - ${state.yearViewStart + 9}`;
      refs.yearsContainer.textContent = "";
      for (let year = state.yearViewStart; year < state.yearViewStart + 10; year += 1) {
        const yearButton = createElement2("button", "survey-period-filter__year-button", String(year));
        yearButton.type = "button";
        yearButton.dataset.role = "survey-date-filter-year";
        yearButton.dataset.year = String(year);
        if (state.activeFilterType === "year" && state.activeYear === year) {
          yearButton.classList.add("is-selected");
        }
        refs.yearsContainer.appendChild(yearButton);
      }
    }
    function renderMonthPanel(instance) {
      const { state, refs } = instance;
      refs.yearLabel.textContent = String(state.monthViewYear);
      refs.monthsContainer.textContent = "";
      MONTH_NAMES.forEach((monthName, monthIndex) => {
        const monthButton = createElement2("button", "survey-period-filter__month-button", monthName);
        monthButton.type = "button";
        monthButton.dataset.role = "survey-date-filter-month";
        monthButton.dataset.monthIndex = String(monthIndex);
        const isSelected = state.activeFilterType === "month" && state.activeMonth && state.activeMonth.year === state.monthViewYear && state.activeMonth.monthIndex === monthIndex;
        monthButton.classList.toggle("is-selected", isSelected);
        refs.monthsContainer.appendChild(monthButton);
      });
    }
    function buildWeekdayRow() {
      const weekdaysRow = createElement2("div", "survey-period-filter__weekday-row");
      WEEKDAY_NAMES.forEach((weekday) => {
        weekdaysRow.appendChild(createElement2("span", "survey-period-filter__weekday", weekday));
      });
      return weekdaysRow;
    }
    function buildDayButton(isoValue, displayState) {
      const dayButton = createElement2("button", "survey-period-filter__day-button");
      const date = parseIso(isoValue);
      dayButton.type = "button";
      dayButton.dataset.role = "survey-date-filter-day";
      dayButton.dataset.dateIso = isoValue;
      dayButton.textContent = date ? String(date.getDate()) : "";
      if (date && toIso(/* @__PURE__ */ new Date()) === isoValue) {
        dayButton.classList.add("is-today");
      }
      if (displayState.start && isoValue === displayState.start) {
        dayButton.classList.add("is-range-start");
      }
      if (displayState.end && isoValue === displayState.end) {
        dayButton.classList.add("is-range-end");
      }
      if (displayState.start && displayState.end && compareIso(isoValue, displayState.start) > 0 && compareIso(isoValue, displayState.end) < 0) {
        dayButton.classList.add("is-in-range");
      }
      if (!displayState.end && displayState.start && isoValue === displayState.start) {
        dayButton.classList.add("is-range-single");
      }
      return dayButton;
    }
    function buildCalendarCard(monthDate, displayState) {
      const card = createElement2("div", "survey-period-filter__calendar-card");
      const title = createElement2(
        "h4",
        "survey-period-filter__calendar-title",
        getMonthDescription(monthDate.getFullYear(), monthDate.getMonth())
      );
      const weekdaysRow = buildWeekdayRow();
      const daysGrid = createElement2("div", "survey-period-filter__days-grid");
      const firstDayIndex = (new Date(monthDate.getFullYear(), monthDate.getMonth(), 1).getDay() + 6) % 7;
      const daysInMonth = new Date(monthDate.getFullYear(), monthDate.getMonth() + 1, 0).getDate();
      for (let index = 0; index < firstDayIndex; index += 1) {
        daysGrid.appendChild(createElement2("span", "survey-period-filter__day-placeholder"));
      }
      for (let day = 1; day <= daysInMonth; day += 1) {
        const isoValue = toIso(new Date(monthDate.getFullYear(), monthDate.getMonth(), day));
        daysGrid.appendChild(buildDayButton(isoValue, displayState));
      }
      card.appendChild(title);
      card.appendChild(weekdaysRow);
      card.appendChild(daysGrid);
      return card;
    }
    function renderRangePanel(instance) {
      const { state, refs } = instance;
      const displayState = getCurrentRangeDisplayState(state);
      const firstMonth = new Date(state.rangeViewDate.getFullYear(), state.rangeViewDate.getMonth(), 1);
      const secondMonth = shiftMonth(firstMonth, 1);
      refs.rangeLabel.textContent = `${getMonthDescription(firstMonth.getFullYear(), firstMonth.getMonth())} - ${getMonthDescription(secondMonth.getFullYear(), secondMonth.getMonth())}`;
      refs.calendars.textContent = "";
      refs.calendars.appendChild(buildCalendarCard(firstMonth, displayState));
      refs.calendars.appendChild(buildCalendarCard(secondMonth, displayState));
      if (state.rangeStart && !state.rangeEnd) {
        if (refs.hint) {
          refs.hint.textContent = `Начало диапазона: ${getDisplayDate(state.rangeStart)}. Выберите конечную дату.`;
        }
        return;
      }
      if (state.activeFilterType === "range" && state.rangeStart && state.rangeEnd) {
        if (refs.hint) {
          refs.hint.textContent = window.SurveyFilterSummary.shouldHideCountSummary(instance.page) ? "" : `Выбран диапазон: ${getRangeDescription(state.rangeStart, state.rangeEnd)}.`;
        }
        return;
      }
      if (refs.hint) {
        refs.hint.textContent = "Выберите начальную и конечную дату периода.";
      }
    }
    function render(instance) {
      renderModeSwitch(instance);
      renderYearPanel(instance);
      renderMonthPanel(instance);
      renderRangePanel(instance);
    }
    function clear(instance, callbacks) {
      const serverFilters = callbacks?.serverFilters || window.SurveyServerFilterState;
      instance.state.activeFilterType = "all";
      instance.state.activeYear = null;
      instance.state.activeMonth = null;
      instance.state.rangeStart = "";
      instance.state.rangeEnd = "";
      render(instance);
      if (serverFilters.isServerPage(instance.page)) {
        serverFilters.syncDateState(instance.page, instance.state);
        serverFilters.navigate(instance.page, "date");
        return;
      }
      callbacks?.applyFilter?.(instance);
    }
    function applyYear(instance, year, callbacks) {
      const serverFilters = callbacks?.serverFilters || window.SurveyServerFilterState;
      const { state } = instance;
      const isSameYear = state.activeFilterType === "year" && state.activeYear === year;
      if (isSameYear) {
        clear(instance, callbacks);
        return;
      }
      state.activeFilterType = "year";
      state.activeYear = year;
      state.monthViewYear = year;
      state.yearViewStart = getDecadeStart(year);
      render(instance);
      if (serverFilters.isServerPage(instance.page)) {
        serverFilters.syncDateState(instance.page, instance.state);
        serverFilters.navigate(instance.page, "date");
        return;
      }
      callbacks?.applyFilter?.(instance);
    }
    function applyMonth(instance, monthIndex, callbacks) {
      const serverFilters = callbacks?.serverFilters || window.SurveyServerFilterState;
      const { state } = instance;
      const isSameMonth = state.activeFilterType === "month" && state.activeMonth && state.activeMonth.year === state.monthViewYear && state.activeMonth.monthIndex === monthIndex;
      if (isSameMonth) {
        clear(instance, callbacks);
        return;
      }
      state.activeFilterType = "month";
      state.activeYear = null;
      state.activeMonth = {
        year: state.monthViewYear,
        monthIndex
      };
      render(instance);
      if (serverFilters.isServerPage(instance.page)) {
        serverFilters.syncDateState(instance.page, instance.state);
        serverFilters.navigate(instance.page, "date");
        return;
      }
      callbacks?.applyFilter?.(instance);
    }
    function handleRangeSelection(instance, isoValue, callbacks) {
      const serverFilters = callbacks?.serverFilters || window.SurveyServerFilterState;
      const { state } = instance;
      if (!state.rangeStart || state.rangeEnd) {
        state.rangeStart = isoValue;
        state.rangeEnd = "";
        state.activeFilterType = "all";
        render(instance);
        if (serverFilters.isServerPage(instance.page)) {
          return;
        }
        callbacks?.applyFilter?.(instance);
        return;
      }
      if (compareIso(isoValue, state.rangeStart) < 0) {
        state.rangeEnd = state.rangeStart;
        state.rangeStart = isoValue;
      } else {
        state.rangeEnd = isoValue;
      }
      state.activeFilterType = "range";
      state.activeYear = null;
      render(instance);
      if (serverFilters.isServerPage(instance.page)) {
        serverFilters.syncDateState(instance.page, instance.state);
        serverFilters.navigate(instance.page, "date");
        return;
      }
      callbacks?.applyFilter?.(instance);
    }
    function createInstance(root, {
      pageSelector,
      serverFilters = window.SurveyServerFilterState,
      filterPopover = window.SurveyFilterPopover,
      closeAllPopovers,
      setPopoverOpen,
      applyFilter
    } = {}) {
      ensurePopoverHeader(root);
      const page = root.closest(pageSelector);
      const tableBody = page?.querySelector('[data-role="main-table"] tbody');
      if (!page || !tableBody) {
        return null;
      }
      const instance = {
        root,
        page,
        state: getInitialState(page, /* @__PURE__ */ new Date(), serverFilters),
        refs: {
          trigger: root.querySelector('[data-role="survey-date-filter-trigger"]'),
          label: root.querySelector('[data-role="survey-date-filter-label"]'),
          popover: root.querySelector('[data-role="survey-date-filter-popover"]'),
          yearModeButton: root.querySelector('[data-role="survey-date-filter-mode"][data-mode="year"]'),
          monthModeButton: root.querySelector('[data-role="survey-date-filter-mode"][data-mode="month"]'),
          rangeModeButton: root.querySelector('[data-role="survey-date-filter-mode"][data-mode="range"]'),
          yearPanel: root.querySelector('[data-role="survey-date-filter-year-panel"]'),
          monthPanel: root.querySelector('[data-role="survey-date-filter-month-panel"]'),
          rangePanel: root.querySelector('[data-role="survey-date-filter-range-panel"]'),
          yearRangeLabel: root.querySelector('[data-role="survey-date-filter-year-range-label"]'),
          yearsContainer: root.querySelector('[data-role="survey-date-filter-years"]'),
          yearLabel: root.querySelector('[data-role="survey-date-filter-year-label"]'),
          monthsContainer: root.querySelector('[data-role="survey-date-filter-months"]'),
          rangeLabel: root.querySelector('[data-role="survey-date-filter-range-label"]'),
          hint: root.querySelector('[data-role="survey-date-filter-hint"]'),
          calendars: root.querySelector('[data-role="survey-date-filter-calendars"]'),
          summary: root.querySelector('[data-role="survey-date-filter-summary"]'),
          clearButton: root.querySelector('[data-role="survey-date-filter-clear"]')
        },
        handlers: {},
        dropdownController: null
      };
      const callbacks = { serverFilters, applyFilter };
      const setOpen = (isOpen) => setPopoverOpen?.(instance, isOpen) ?? filterPopover.setOpen(instance, isOpen);
      if (typeof window.AppUi?.createDropdown === "function" && instance.refs.trigger && instance.refs.popover) {
        const dropdown = window.AppUi.createDropdown({
          root,
          trigger: instance.refs.trigger,
          menu: instance.refs.popover,
          openClass: "is-open",
          hiddenClass: "is-hidden",
          onOpen: () => {
            closeAllPopovers?.(root);
            filterPopover.applyOpenState(instance, true);
          },
          onClose: () => {
            filterPopover.applyOpenState(instance, false);
          }
        });
        instance.dropdownController = dropdown.controller;
      }
      instance.handlers.click = function(event) {
        event.stopPropagation();
        const target = event.target instanceof Element ? event.target : null;
        if (!target) {
          return;
        }
        const trigger = target.closest('[data-role="survey-date-filter-trigger"]');
        if (!instance.dropdownController && trigger && root.contains(trigger)) {
          event.preventDefault();
          const shouldOpen = !instance.state.isOpen;
          closeAllPopovers?.(shouldOpen ? root : null);
          setOpen(shouldOpen);
          return;
        }
        const modeButton = target.closest('[data-role="survey-date-filter-mode"]');
        if (modeButton && root.contains(modeButton)) {
          event.preventDefault();
          instance.state.mode = ["year", "range"].includes(modeButton.dataset.mode) ? modeButton.dataset.mode : "month";
          render(instance);
          return;
        }
        const simpleActions = [
          ["survey-date-filter-year-range-prev", () => {
            instance.state.yearViewStart -= 10;
          }],
          ["survey-date-filter-year-range-next", () => {
            instance.state.yearViewStart += 10;
          }],
          ["survey-date-filter-year-prev", () => {
            instance.state.monthViewYear -= 1;
          }],
          ["survey-date-filter-year-next", () => {
            instance.state.monthViewYear += 1;
          }],
          ["survey-date-filter-range-prev", () => {
            instance.state.rangeViewDate = shiftMonth(instance.state.rangeViewDate, -1);
          }],
          ["survey-date-filter-range-next", () => {
            instance.state.rangeViewDate = shiftMonth(instance.state.rangeViewDate, 1);
          }]
        ];
        for (const [role, action] of simpleActions) {
          if (target.closest(`[data-role="${role}"]`)) {
            event.preventDefault();
            action();
            render(instance);
            return;
          }
        }
        if (target.closest('[data-role="survey-date-filter-close"]')) {
          event.preventDefault();
          setOpen(false);
          return;
        }
        const yearButton = target.closest('[data-role="survey-date-filter-year"]');
        if (yearButton && root.contains(yearButton)) {
          event.preventDefault();
          const selectedYear = Number.parseInt(yearButton.dataset.year || "", 10);
          if (Number.isInteger(selectedYear)) {
            applyYear(instance, selectedYear, callbacks);
          }
          return;
        }
        const monthButton = target.closest('[data-role="survey-date-filter-month"]');
        if (monthButton && root.contains(monthButton)) {
          event.preventDefault();
          const monthIndex = Number.parseInt(monthButton.dataset.monthIndex || "", 10);
          if (Number.isInteger(monthIndex) && monthIndex >= 0 && monthIndex < 12) {
            applyMonth(instance, monthIndex, callbacks);
          }
          return;
        }
        const dayButton = target.closest('[data-role="survey-date-filter-day"]');
        if (dayButton && root.contains(dayButton)) {
          event.preventDefault();
          const isoValue = dayButton.dataset.dateIso || "";
          if (parseIso(isoValue)) {
            handleRangeSelection(instance, isoValue, callbacks);
          }
          return;
        }
        if (target.closest('[data-role="survey-date-filter-clear"]')) {
          event.preventDefault();
          clear(instance, callbacks);
        }
      };
      root.addEventListener("click", instance.handlers.click);
      instance.destroy = function destroyDateFilterInstance() {
        root.removeEventListener("click", instance.handlers.click);
        instance.dropdownController?.destroy?.();
      };
      render(instance);
      applyFilter?.(instance);
      return instance;
    }
    window.SurveyDateFilter = {
      ensurePopoverHeader,
      getInitialState,
      getActiveFilterBounds,
      createInstance,
      render,
      clear,
      applyYear,
      applyMonth,
      handleRangeSelection
    };
  })();

  // Web/wwwroot/js/features/survey/survey-checkbox-filter.js
  (function() {
    if (window.SurveyCheckboxFilter) {
      return;
    }
    const { createElement: createElement2 } = window.SurveyFilterCore;
    const configs = {
      organization: {
        availableValuesKey: "availableOrganizations",
        availableOptionsKey: "availableOrganizationOptions",
        selectedValuesKey: "selectedOrganizations",
        selectedIdsKey: "selectedOrganizationIds",
        selectedConfigKey: "selectedOrganizationIds",
        optionRole: "survey-organization-filter-option",
        valueDatasetKey: "organizationName",
        idDatasetKey: "organizationId",
        emptyText: "Организации для фильтрации не найдены.",
        filterName: "organization"
      },
      survey: {
        availableValuesKey: "availableSurveyNames",
        availableOptionsKey: "availableSurveyOptions",
        selectedValuesKey: "selectedSurveyNames",
        selectedIdsKey: "selectedSurveyIds",
        selectedConfigKey: "selectedSurveyIds",
        optionRole: "survey-name-filter-option",
        valueDatasetKey: "surveyName",
        idDatasetKey: "surveyId",
        emptyText: "Анкеты для фильтрации не найдены.",
        filterName: "survey"
      }
    };
    function getConfig(type) {
      return configs[type] || null;
    }
    function getSelectedNames(instance, config, serverFilters = window.SurveyServerFilterState) {
      return instance.state.serverMode ? serverFilters.getSelectedOptionNames(instance.state[config.availableOptionsKey], instance.state[config.selectedIdsKey]) : instance.state[config.selectedValuesKey];
    }
    function render(instance, config) {
      const { state, refs } = instance;
      refs.options.textContent = "";
      const hasOptions = state.serverMode ? state[config.availableOptionsKey].length > 0 : state[config.availableValuesKey].length > 0;
      if (!hasOptions) {
        refs.options.appendChild(
          createElement2("p", "app-checkbox-empty", config.emptyText)
        );
        return;
      }
      const options = state.serverMode ? state[config.availableOptionsKey] : state[config.availableValuesKey];
      options.forEach((option) => {
        const optionId = state.serverMode ? option.id : null;
        const optionName = state.serverMode ? option.name : option;
        const isSelected = state.serverMode ? state[config.selectedIdsKey].includes(optionId) : state[config.selectedValuesKey].includes(optionName);
        const checkboxOption = window.AppUi.createCheckboxOption({
          text: optionName,
          checked: isSelected,
          selected: isSelected
        });
        const optionLabel = checkboxOption.option;
        const checkbox = checkboxOption.checkbox;
        optionLabel.classList.toggle("is-selected", isSelected);
        checkbox.dataset.role = config.optionRole;
        checkbox.dataset[config.valueDatasetKey] = optionName;
        if (state.serverMode) {
          checkbox.dataset[config.idDatasetKey] = String(optionId);
        }
        refs.options.appendChild(optionLabel);
      });
    }
    function toggleValue(instance, config, rawValue, isSelected, callbacks) {
      const normalizedValue = String(rawValue || "").trim();
      if (!normalizedValue) {
        return;
      }
      const nextSelectedValues = new Set(instance.state[config.selectedValuesKey]);
      if (isSelected) {
        nextSelectedValues.add(normalizedValue);
      } else {
        nextSelectedValues.delete(normalizedValue);
      }
      instance.state[config.selectedValuesKey] = Array.from(nextSelectedValues).sort((left, right) => left.localeCompare(right, "ru"));
      render(instance, config);
      callbacks?.applyPageFilters?.(instance.page);
    }
    function toggleId(instance, config, rawId, isSelected, callbacks) {
      const id = Number.parseInt(String(rawId || ""), 10);
      if (!Number.isInteger(id)) {
        return;
      }
      const serverFilters = callbacks?.serverFilters || window.SurveyServerFilterState;
      const nextSelectedIds = new Set(instance.state[config.selectedIdsKey]);
      if (isSelected) {
        nextSelectedIds.add(id);
      } else {
        nextSelectedIds.delete(id);
      }
      instance.state[config.selectedIdsKey] = Array.from(nextSelectedIds).sort((left, right) => left - right);
      const serverConfig = serverFilters.getConfig(instance.page);
      if (serverConfig) {
        serverConfig[config.selectedConfigKey] = [...instance.state[config.selectedIdsKey]];
      }
      render(instance, config);
      serverFilters.navigate(instance.page, config.filterName);
    }
    function clear(instance, config, callbacks) {
      if (instance.state.serverMode) {
        const serverFilters = callbacks?.serverFilters || window.SurveyServerFilterState;
        instance.state[config.selectedIdsKey] = [];
        const serverConfig = serverFilters.getConfig(instance.page);
        if (serverConfig) {
          serverConfig[config.selectedConfigKey] = [];
        }
        render(instance, config);
        serverFilters.navigate(instance.page, config.filterName);
        return;
      }
      instance.state[config.selectedValuesKey] = [];
      render(instance, config);
      callbacks?.applyPageFilters?.(instance.page);
    }
    function createInstance(root, definition, {
      pageSelector,
      serverFilters = window.SurveyServerFilterState,
      filterPopover = window.SurveyFilterPopover,
      closeAllPopovers,
      setPopoverOpen,
      applyPageFilters
    } = {}) {
      const config = getConfig(definition?.pendingFilterName);
      if (!definition || !config || !(root instanceof Element)) {
        return null;
      }
      const page = root.closest(pageSelector);
      const tableBody = page?.querySelector('[data-role="main-table"] tbody');
      if (!page || !tableBody) {
        return null;
      }
      const instance = {
        root,
        page,
        state: definition.createState(page),
        refs: {
          trigger: root.querySelector(`[data-role="${definition.triggerRole}"]`),
          label: root.querySelector(`[data-role="${definition.labelRole}"]`),
          popover: root.querySelector(`[data-role="${definition.popoverRole}"]`),
          options: root.querySelector(`[data-role="${definition.optionsRole}"]`),
          summary: root.querySelector(`[data-role="${definition.summaryRole}"]`),
          clearButton: root.querySelector(`[data-role="${definition.clearRole}"]`)
        },
        handlers: {},
        dropdownController: null
      };
      const callbacks = { serverFilters, applyPageFilters };
      const setOpen = (isOpen) => setPopoverOpen?.(instance, isOpen) ?? filterPopover.setOpen(instance, isOpen);
      if (typeof window.AppUi?.createMultiselect === "function" && instance.refs.trigger && instance.refs.popover) {
        const dropdown = window.AppUi.createMultiselect({
          root,
          trigger: instance.refs.trigger,
          menu: instance.refs.popover,
          openClass: "is-open",
          hiddenClass: "is-hidden",
          onOpen: () => {
            closeAllPopovers?.(root);
            filterPopover.applyOpenState(instance, true);
          },
          onClose: () => {
            filterPopover.applyOpenState(instance, false);
          }
        });
        instance.dropdownController = dropdown.controller;
      }
      instance.handlers.click = function(event) {
        event.stopPropagation();
        const target = event.target instanceof Element ? event.target : null;
        if (!target) {
          return;
        }
        const trigger = target.closest(`[data-role="${definition.triggerRole}"]`);
        if (!instance.dropdownController && trigger && root.contains(trigger)) {
          event.preventDefault();
          const shouldOpen = !instance.state.isOpen;
          closeAllPopovers?.(shouldOpen ? root : null);
          setOpen(shouldOpen);
          return;
        }
        if (target.closest(`[data-role="${definition.closeRole}"]`)) {
          event.preventDefault();
          setOpen(false);
          return;
        }
        if (target.closest(`[data-role="${definition.clearRole}"]`)) {
          event.preventDefault();
          clear(instance, config, callbacks);
        }
      };
      instance.handlers.change = function(event) {
        const target = event.target instanceof Element ? event.target : null;
        const option = target?.closest(`[data-role="${config.optionRole}"]`);
        if (!option || !root.contains(option)) {
          return;
        }
        if (instance.state.serverMode) {
          toggleId(instance, config, option.dataset[config.idDatasetKey], Boolean(option.checked), callbacks);
          return;
        }
        toggleValue(instance, config, option.dataset[config.valueDatasetKey], Boolean(option.checked), callbacks);
      };
      root.addEventListener("click", instance.handlers.click);
      root.addEventListener("change", instance.handlers.change);
      instance.destroy = function destroyCheckboxFilterInstance() {
        root.removeEventListener("click", instance.handlers.click);
        root.removeEventListener("change", instance.handlers.change);
        instance.dropdownController?.destroy?.();
      };
      render(instance, config);
      applyPageFilters?.(instance.page);
      return instance;
    }
    window.SurveyCheckboxFilter = {
      getConfig,
      getSelectedNames,
      createInstance,
      render,
      toggleValue,
      toggleId,
      clear
    };
  })();

  // Web/wwwroot/js/features/survey/survey-row-filtering.js
  (function() {
    if (window.SurveyRowFiltering) {
      return;
    }
    const SURVEY_ROW_SELECTOR = "tr[data-survey-date-begin][data-survey-date-end]";
    function getRows(page) {
      return Array.from(page?.querySelectorAll(SURVEY_ROW_SELECTOR) || []);
    }
    function parseRowOrganizations(row) {
      const rawValue = row?.dataset?.surveyOrganizations || "[]";
      try {
        const parsed = JSON.parse(rawValue);
        return Array.isArray(parsed) ? parsed.map((name) => String(name || "").trim()).filter(Boolean) : [];
      } catch (error) {
        return [];
      }
    }
    function getRowSurveyName(row) {
      return String(row?.dataset?.surveyName || "").trim();
    }
    function collectAvailableOrganizations(page) {
      return Array.from(new Set(
        getRows(page).flatMap((row) => parseRowOrganizations(row)).filter(Boolean)
      )).sort((left, right) => left.localeCompare(right, "ru"));
    }
    function collectAvailableSurveyNames(page) {
      return Array.from(new Set(
        getRows(page).map((row) => getRowSurveyName(row)).filter(Boolean)
      )).sort((left, right) => left.localeCompare(right, "ru"));
    }
    function getVisibleCount(rows) {
      return rows.filter((row) => !row.classList.contains("is-hidden-by-date") && !row.classList.contains("is-hidden-by-organization") && !row.classList.contains("is-hidden-by-survey-name")).length;
    }
    function syncEmptyRow(page, rows, visibleCount) {
      const emptyRow = page?.querySelector('[data-role="survey-filter-empty-row"]');
      if (emptyRow) {
        emptyRow.classList.toggle("is-hidden", rows.length === 0 || visibleCount > 0);
      }
    }
    function applyLocalFilters(page, { dateBounds = null, selectedOrganizations = [], selectedSurveyNames = [], isIsoWithin } = {}) {
      const rows = getRows(page);
      rows.forEach((row) => {
        const beginIso = row.dataset.surveyDateBegin || "";
        const endIso = row.dataset.surveyDateEnd || "";
        const matchesDate = !dateBounds || isIsoWithin(beginIso, dateBounds.start, dateBounds.end) && isIsoWithin(endIso, dateBounds.start, dateBounds.end);
        const rowOrganizations = parseRowOrganizations(row);
        const matchesOrganizations = selectedOrganizations.length === 0 || rowOrganizations.some((name) => selectedOrganizations.includes(name));
        const rowSurveyName = getRowSurveyName(row);
        const matchesSurveyName = selectedSurveyNames.length === 0 || selectedSurveyNames.includes(rowSurveyName);
        row.classList.remove("is-hidden");
        row.classList.toggle("is-hidden-by-date", !matchesDate);
        row.classList.toggle("is-hidden-by-organization", !matchesOrganizations);
        row.classList.toggle("is-hidden-by-survey-name", !matchesSurveyName);
      });
      const visibleCount = getVisibleCount(rows);
      syncEmptyRow(page, rows, visibleCount);
      return {
        rows,
        visibleCount,
        totalCount: rows.length
      };
    }
    window.SurveyRowFiltering = {
      getRows,
      collectAvailableOrganizations,
      collectAvailableSurveyNames,
      syncEmptyRow,
      applyLocalFilters
    };
  })();

  // Web/wwwroot/js/features/survey/survey-admin-date-filter.js
  (function() {
    window.__surveyAdminDateFilterController?.destroy?.();
    const PAGE_SELECTOR = '.app-page[data-page="surveys-list"], .app-page[data-page="surveys-archive"], .app-page[data-page="answers-list"], .app-page[data-page="user-surveys"]';
    const DATE_FILTER_SELECTOR = '[data-role="survey-date-filter"]';
    const ORGANIZATION_FILTER_SELECTOR = '[data-role="survey-organization-filter"]';
    const SURVEY_NAME_FILTER_SELECTOR = '[data-role="survey-name-filter"]';
    const { isIsoWithin } = window.SurveyFilterCore;
    const serverFilters = window.SurveyServerFilterState;
    const dateFilter = window.SurveyDateFilter;
    const checkboxFilter = window.SurveyCheckboxFilter;
    const filterSummary = window.SurveyFilterSummary;
    const filterPopover = window.SurveyFilterPopover;
    const rowFiltering = window.SurveyRowFiltering;
    const mountedControllers = /* @__PURE__ */ new Set();
    const mountedControllerByPage = /* @__PURE__ */ new WeakMap();
    const checkboxDefinitions = {
      organization: {
        selector: ORGANIZATION_FILTER_SELECTOR,
        triggerRole: "survey-organization-filter-trigger",
        labelRole: "survey-organization-filter-label",
        popoverRole: "survey-organization-filter-popover",
        optionsRole: "survey-organization-filter-options",
        summaryRole: "survey-organization-filter-summary",
        clearRole: "survey-organization-filter-clear",
        closeRole: "survey-organization-filter-close",
        createState(page) {
          const config = serverFilters.getConfig(page);
          return {
            isOpen: false,
            serverMode: serverFilters.isServerPage(page),
            availableOrganizations: rowFiltering.collectAvailableOrganizations(page),
            availableOrganizationOptions: config?.organizationOptions || [],
            selectedOrganizations: [],
            selectedOrganizationIds: [...config?.selectedOrganizationIds || []]
          };
        },
        updateSummary(instance, visibleCount, totalCount) {
          filterSummary.updateOrganization(instance, visibleCount, totalCount, serverFilters);
        }
      },
      survey: {
        selector: SURVEY_NAME_FILTER_SELECTOR,
        triggerRole: "survey-name-filter-trigger",
        labelRole: "survey-name-filter-label",
        popoverRole: "survey-name-filter-popover",
        optionsRole: "survey-name-filter-options",
        summaryRole: "survey-name-filter-summary",
        clearRole: "survey-name-filter-clear",
        closeRole: "survey-name-filter-close",
        createState(page) {
          const config = serverFilters.getConfig(page);
          return {
            isOpen: false,
            serverMode: serverFilters.isServerPage(page),
            availableSurveyNames: rowFiltering.collectAvailableSurveyNames(page),
            availableSurveyOptions: config?.surveyOptions || [],
            selectedSurveyNames: [],
            selectedSurveyIds: [...config?.selectedSurveyIds || []]
          };
        },
        updateSummary(instance, visibleCount, totalCount) {
          filterSummary.updateSurveyName(instance, visibleCount, totalCount, serverFilters);
        }
      }
    };
    function getPages(root) {
      if (root === document || root?.nodeType === Node.DOCUMENT_NODE) {
        return Array.from(document.querySelectorAll(PAGE_SELECTOR));
      }
      if (!(root instanceof Element)) {
        return [];
      }
      return Array.from(new Set([
        root.matches(PAGE_SELECTOR) ? root : null,
        root.closest(PAGE_SELECTOR),
        ...root.querySelectorAll(PAGE_SELECTOR)
      ].filter(Boolean)));
    }
    function getAllInstances() {
      return Array.from(mountedControllers).flatMap((controller) => controller.instances);
    }
    function closeAllPopovers(exceptRoot = null) {
      getAllInstances().forEach((instance) => {
        if (instance.root !== exceptRoot) {
          filterPopover.setOpen(instance, false);
        }
      });
    }
    function setPopoverOpen(instance, isOpen) {
      filterPopover.setOpen(instance, isOpen);
    }
    function updateSummaries(controller, visibleCount, totalCount) {
      if (controller.date) {
        filterSummary.updateDate(controller.date, visibleCount, totalCount);
      }
      if (controller.organization) {
        checkboxDefinitions.organization.updateSummary(controller.organization, visibleCount, totalCount);
      }
      if (controller.survey) {
        checkboxDefinitions.survey.updateSummary(controller.survey, visibleCount, totalCount);
      }
    }
    function applyPageFilters(page) {
      const controller = mountedControllerByPage.get(page);
      if (!controller) {
        return;
      }
      const rows = rowFiltering.getRows(page);
      if (serverFilters.isServerPage(page)) {
        const totalCount = Number.parseInt(String(page?.dataset?.totalCount || rows.length), 10) || rows.length;
        updateSummaries(controller, rows.length, totalCount);
        rowFiltering.syncEmptyRow(page, rows, rows.length);
        return;
      }
      const bounds = controller.date ? dateFilter.getActiveFilterBounds(controller.date.state) : null;
      const result = rowFiltering.applyLocalFilters(page, {
        dateBounds: bounds,
        selectedOrganizations: controller.organization?.state?.selectedOrganizations || [],
        selectedSurveyNames: controller.survey?.state?.selectedSurveyNames || [],
        isIsoWithin
      });
      updateSummaries(controller, result.visibleCount, result.totalCount);
    }
    function createDateInstance(page) {
      const root = page.querySelector(DATE_FILTER_SELECTOR);
      if (!root) {
        return null;
      }
      const instance = dateFilter.createInstance(root, {
        pageSelector: PAGE_SELECTOR,
        serverFilters,
        filterPopover,
        closeAllPopovers,
        setPopoverOpen,
        applyFilter: (filterInstance) => applyPageFilters(filterInstance.page)
      });
      if (instance && serverFilters.consumePendingOpenFilter(instance.page, "date")) {
        closeAllPopovers(root);
        setPopoverOpen(instance, true);
      }
      return instance;
    }
    function createCheckboxInstance(page, type) {
      const definition = checkboxDefinitions[type];
      const root = definition ? page.querySelector(definition.selector) : null;
      if (!definition || !root) {
        return null;
      }
      const instance = checkboxFilter.createInstance(root, {
        ...definition,
        pendingFilterName: type
      }, {
        pageSelector: PAGE_SELECTOR,
        serverFilters,
        filterPopover,
        closeAllPopovers,
        setPopoverOpen,
        applyPageFilters
      });
      if (instance && serverFilters.consumePendingOpenFilter(instance.page, type)) {
        closeAllPopovers(root);
        setPopoverOpen(instance, true);
      }
      return instance;
    }
    function mountPage(page) {
      if (!(page instanceof Element) || !page.matches(PAGE_SELECTOR)) {
        return null;
      }
      const mountedController = mountedControllerByPage.get(page);
      if (mountedController) {
        return mountedController;
      }
      let disposed = false;
      const controller = {
        page,
        date: null,
        organization: null,
        survey: null,
        instances: [],
        destroy() {
          if (disposed) {
            return;
          }
          disposed = true;
          page.removeEventListener("page:unmount", controller.destroy);
          controller.instances.forEach((instance) => instance.destroy?.());
          controller.instances = [];
          mountedControllerByPage.delete(page);
          mountedControllers.delete(controller);
        }
      };
      mountedControllerByPage.set(page, controller);
      mountedControllers.add(controller);
      controller.date = createDateInstance(page);
      controller.organization = createCheckboxInstance(page, "organization");
      controller.survey = createCheckboxInstance(page, "survey");
      controller.instances = [controller.date, controller.organization, controller.survey].filter(Boolean);
      if (controller.instances.length === 0) {
        controller.destroy();
        return null;
      }
      page.addEventListener("page:unmount", controller.destroy);
      applyPageFilters(page);
      return controller;
    }
    function createControllerGroup(controllers) {
      let disposed = false;
      return {
        destroy() {
          if (disposed) {
            return;
          }
          disposed = true;
          controllers.slice().reverse().forEach((controller) => controller?.destroy?.());
        }
      };
    }
    function mount(root = document) {
      return createControllerGroup(getPages(root).map(mountPage).filter(Boolean));
    }
    function destroy(root = document) {
      const pages = getPages(root);
      if (root === document || root?.nodeType === Node.DOCUMENT_NODE) {
        Array.from(mountedControllers).forEach((controller) => controller.destroy());
        return;
      }
      Array.from(mountedControllers).forEach((controller) => {
        if (pages.includes(controller.page)) {
          controller.destroy();
        }
      });
    }
    window.SurveyFilters = { mount, destroy };
    window.__surveyAdminDateFilterController = {
      destroy: () => destroy(document)
    };
  })();

  // Web/wwwroot/js/features/survey/surveys-page.js
  (function() {
    const existingController = window.__surveysPageController;
    if (existingController && typeof existingController.destroy === "function") {
      existingController.destroy();
    }
    const PAGE_SELECTOR = '.app-page[data-page="surveys-list"], .app-page[data-page="surveys-archive"], .app-page[data-page="answers-list"], .app-page[data-page="user-surveys"]';
    const ADMIN_SURVEY_PAGE_SELECTOR = '.app-page[data-page="surveys-list"], .app-page[data-page="surveys-archive"]';
    const WORK_PERIOD_PAGE_SELECTOR = '.app-page[data-page="surveys-list"]';
    let unregisterLifecycle = null;
    const mountedControllers = /* @__PURE__ */ new Set();
    const mountedControllerByPage = /* @__PURE__ */ new WeakMap();
    function getPagesFromNode(node) {
      if (node === document || node?.nodeType === Node.DOCUMENT_NODE) {
        return Array.from(document.querySelectorAll(PAGE_SELECTOR));
      }
      if (!(node instanceof Element)) {
        return [];
      }
      const pages = [];
      const ownerPage = node.closest(PAGE_SELECTOR);
      if (ownerPage) {
        pages.push(ownerPage);
      }
      if (node.matches(PAGE_SELECTOR)) {
        pages.push(node);
      }
      node.querySelectorAll(PAGE_SELECTOR).forEach((page) => {
        pages.push(page);
      });
      return Array.from(new Set(pages));
    }
    function createCompositeController(controllers) {
      let isDestroyed = false;
      return {
        destroy() {
          if (isDestroyed) {
            return;
          }
          isDestroyed = true;
          controllers.slice().reverse().forEach((controller) => controller?.destroy?.());
        }
      };
    }
    function mountPage(page) {
      if (!(page instanceof Element) || !page.matches(PAGE_SELECTOR)) {
        return null;
      }
      const existingController2 = mountedControllerByPage.get(page);
      if (existingController2) {
        return existingController2;
      }
      const controllers = [];
      const filtersController = window.SurveyFilters?.mount?.(page);
      if (filtersController) {
        controllers.push(filtersController);
      }
      if (page.matches(WORK_PERIOD_PAGE_SELECTOR)) {
        const workPeriodController = window.SurveyWorkPeriod?.mount?.(page);
        if (workPeriodController) {
          controllers.push(workPeriodController);
        }
      }
      if (page.matches(ADMIN_SURVEY_PAGE_SELECTOR)) {
        const actionsController = window.SurveyAdminList?.mount?.(page);
        if (actionsController) {
          controllers.push(actionsController);
        }
      }
      if (controllers.length === 0) {
        return null;
      }
      let isDestroyed = false;
      const controller = {
        page,
        destroy() {
          if (isDestroyed) {
            return;
          }
          isDestroyed = true;
          page.removeEventListener("page:unmount", controller.destroy);
          createCompositeController(controllers).destroy();
          mountedControllerByPage.delete(page);
          mountedControllers.delete(controller);
        }
      };
      page.addEventListener("page:unmount", controller.destroy);
      mountedControllerByPage.set(page, controller);
      mountedControllers.add(controller);
      return controller;
    }
    function mount(root = document) {
      const controllers = getPagesFromNode(root).map((page) => mountPage(page)).filter(Boolean);
      return createCompositeController(controllers);
    }
    function destroy(root = document) {
      if (root === document || root?.nodeType === Node.DOCUMENT_NODE) {
        Array.from(mountedControllers).forEach((controller) => controller.destroy());
        return;
      }
      if (!(root instanceof Element)) {
        return;
      }
      Array.from(mountedControllers).forEach((controller) => {
        if (controller.page === root || root.contains(controller.page)) {
          controller.destroy();
        }
      });
    }
    window.SurveysPage = {
      mount,
      destroy
    };
    function destroyAll() {
      destroy(document);
      unregisterLifecycle?.();
      unregisterLifecycle = null;
    }
    window.__surveysPageController = {
      destroy: destroyAll
    };
    if (window.AppPageLifecycle?.register) {
      unregisterLifecycle = window.AppPageLifecycle.register(
        "surveys-page",
        PAGE_SELECTOR,
        (page) => mount(page).destroy
      );
      return;
    }
    const mountInitialPages = () => mount(document);
    if (document.readyState === "loading") {
      document.addEventListener("DOMContentLoaded", mountInitialPages, { once: true });
    } else {
      mountInitialPages();
    }
  })();

  // Web/wwwroot/js/features/survey/user-survey-page-helpers.js
  function normalizeSurveyUserPathname(pathname) {
    if (!pathname) {
      return "/";
    }
    return pathname.length > 1 && pathname.endsWith("/") ? pathname.slice(0, -1) : pathname;
  }
  function buildSurveyUserHistoryEntry(tab) {
    switch (tab) {
      case "active":
        return { tab: "active", url: "/survey" };
      case "archived":
      case "archived_surveys_for_user":
        return { tab: "archived", url: "/archive" };
      case "help":
        return { tab: "help", url: "/help" };
      default:
        return null;
    }
  }
  function getSurveyUserHistoryEntryFromLocation(pathname) {
    const normalizedPath = normalizeSurveyUserPathname(pathname);
    if (normalizedPath === "/survey" || normalizedPath === "/my-surveys") {
      return buildSurveyUserHistoryEntry("active");
    }
    if (normalizedPath === "/archive" || normalizedPath === "/my-surveys/archive") {
      return buildSurveyUserHistoryEntry("archived");
    }
    if (normalizedPath === "/help") {
      return buildSurveyUserHistoryEntry("help");
    }
    return null;
  }
  function normalizeSurveyUserCount(value) {
    const numericValue = Number(value);
    return Number.isFinite(numericValue) && numericValue >= 0 ? numericValue : null;
  }
  function readSurveyUserActiveCountFromSnapshot(snapshot) {
    return normalizeSurveyUserCount(snapshot?.activeCount);
  }
  function readSurveyUserActiveCountFromDom(root) {
    const badge = root?.querySelector?.('[data-role="active-count"]');
    return normalizeSurveyUserCount(badge?.textContent?.trim());
  }
  function syncSurveyUserActiveCountBadge(root, activeCount) {
    const activeTabButton = root?.querySelector('[data-role="tab-active"]');
    if (!activeTabButton) {
      return;
    }
    const nextCount = normalizeSurveyUserCount(activeCount) ?? 0;
    let badge = activeTabButton.querySelector('[data-role="active-count"]');
    if (!badge) {
      badge = document.createElement("span");
      badge.className = "count-badge";
      badge.dataset.role = "active-count";
      activeTabButton.appendChild(badge);
    }
    badge.textContent = String(nextCount);
  }
  function getSurveyId(survey) {
    const rawValue = survey?.id_survey ?? survey?.IdSurvey ?? survey?.idSurvey;
    const numericValue = Number(rawValue);
    return Number.isFinite(numericValue) ? numericValue : 0;
  }
  function createTemplateFromNodes(nodes) {
    const template = document.createElement("template");
    nodes.forEach((node) => {
      template.content.appendChild(node.cloneNode(true));
    });
    return template;
  }
  function parseSurveyItems(contentRoot) {
    const itemsNode = contentRoot?.querySelector('[data-role="survey-user-items"]');
    if (!itemsNode?.textContent) {
      return [];
    }
    try {
      const items = JSON.parse(itemsNode.textContent.trim());
      return Array.isArray(items) ? items : [];
    } catch (error) {
      console.error("Не удалось разобрать список анкет клиента:", error);
      return [];
    }
  }
  function parseSnapshotFromContainer(container, template) {
    const contentRoot = container?.querySelector('[data-role="survey-user-content"]');
    if (!contentRoot) {
      return null;
    }
    const rawActiveTab = contentRoot.dataset.activeTab || "active";
    const activeTab = rawActiveTab === "archived" || rawActiveTab === "help" ? rawActiveTab : "active";
    const currentPage = Number(contentRoot.dataset.currentPage || 1);
    const totalPages = Number(contentRoot.dataset.totalPages || 1);
    const totalCount = Number(contentRoot.dataset.totalCount || 0);
    const activeCount = Number(contentRoot.dataset.activeCount || (activeTab === "active" ? totalCount : 0));
    const searchTerm = contentRoot.dataset.searchTerm || "";
    const signedOnly = contentRoot.dataset.signedOnly === "true";
    return {
      activeTab,
      currentPage: Number.isFinite(currentPage) && currentPage > 0 ? currentPage : 1,
      totalPages: Number.isFinite(totalPages) && totalPages > 0 ? totalPages : 1,
      totalCount: Number.isFinite(totalCount) && totalCount >= 0 ? totalCount : 0,
      activeCount: Number.isFinite(activeCount) && activeCount >= 0 ? activeCount : 0,
      searchTerm,
      signedOnly,
      surveys: parseSurveyItems(contentRoot),
      template
    };
  }
  function createSnapshotFromHost(host) {
    if (!host) {
      return null;
    }
    const nodes = Array.from(host.childNodes);
    const template = createTemplateFromNodes(nodes);
    return parseSnapshotFromContainer(host, template);
  }
  function createSnapshotFromTemplateElement(templateElement) {
    if (!templateElement?.content) {
      return null;
    }
    const template = document.createElement("template");
    template.content.appendChild(templateElement.content.cloneNode(true));
    const probe = document.createElement("div");
    probe.appendChild(template.content.cloneNode(true));
    return parseSnapshotFromContainer(probe, template);
  }
  function createSnapshotFromHtml(html) {
    const range = document.createRange();
    range.selectNode(document.body);
    const fragment = range.createContextualFragment(html);
    const template = document.createElement("template");
    template.content.appendChild(fragment.cloneNode(true));
    const probe = document.createElement("div");
    probe.appendChild(fragment.cloneNode(true));
    return parseSnapshotFromContainer(probe, template);
  }
  function setSelectOptions(select, options, defaultLabel, currentValue) {
    if (!select) {
      return "";
    }
    select.replaceChildren();
    const defaultOption = document.createElement("option");
    defaultOption.value = "";
    defaultOption.textContent = defaultLabel;
    select.appendChild(defaultOption);
    options.forEach((option) => {
      const optionNode = document.createElement("option");
      optionNode.value = option.value;
      optionNode.textContent = option.label;
      select.appendChild(optionNode);
    });
    const hasCurrentValue = options.some((option) => option.value === currentValue);
    select.value = hasCurrentValue ? currentValue : "";
    return select.value;
  }
  function getMonthLabel(month) {
    const monthMap = {
      "01": "Январь",
      "02": "Февраль",
      "03": "Март",
      "04": "Апрель",
      "05": "Май",
      "06": "Июнь",
      "07": "Июль",
      "08": "Август",
      "09": "Сентябрь",
      "10": "Октябрь",
      "11": "Ноябрь",
      "12": "Декабрь"
    };
    return monthMap[month] || month;
  }
  function mountSurveyUserModal(host, { title = "", subtitle = "", mountBody, onClose }) {
    const template = document.getElementById("survey-user-modal-template");
    if (!host || !template?.content?.firstElementChild) {
      return null;
    }
    host.replaceChildren();
    const modalNode = template.content.firstElementChild.cloneNode(true);
    const titleNode = modalNode.querySelector('[data-role="title"]');
    const bodyHost = modalNode.querySelector('[data-role="body"]');
    const footerHost = modalNode.querySelector('[data-role="footer"]');
    if (titleNode) {
      titleNode.replaceChildren();
      if (subtitle) {
        const mainLine = document.createElement("span");
        mainLine.className = "answers-modal__title-main";
        mainLine.textContent = title;
        const nameLine = document.createElement("span");
        nameLine.className = "answers-modal__title-name";
        nameLine.textContent = subtitle;
        titleNode.appendChild(mainLine);
        titleNode.appendChild(nameLine);
      } else {
        titleNode.textContent = title;
      }
    }
    let isDisposed = false;
    const handleHidden = () => {
      if (!isDisposed) {
        onClose?.();
      }
    };
    modalNode.addEventListener("site-modal:hidden", handleHidden);
    const bodyCleanup = typeof mountBody === "function" && bodyHost ? mountBody(bodyHost, footerHost) : null;
    host.appendChild(modalNode);
    if (window.AppUi?.setModalVisibility) {
      window.AppUi.setModalVisibility(modalNode, true);
    } else if (typeof window.showSiteModal === "function") {
      window.showSiteModal(modalNode);
    }
    return () => {
      isDisposed = true;
      if (typeof bodyCleanup === "function") {
        bodyCleanup();
      }
      modalNode.removeEventListener("site-modal:hidden", handleHidden);
      if (window.AppUi?.setModalVisibility) {
        window.AppUi.setModalVisibility(modalNode, false);
      } else if (typeof window.hideSiteModal === "function") {
        window.hideSiteModal(modalNode);
      }
      host.replaceChildren();
    };
  }

  // Web/wwwroot/js/features/survey/user-survey-local-filters.js
  function createSurveyUserLocalFilters({
    contentHost,
    emptyTemplate,
    state,
    getContentRefs,
    getMonthLabel: getMonthLabel2,
    setSelectOptions: setSelectOptions2
  }) {
    function ensureFilteredEmptyRow(tableBody, hasVisibleRows) {
      if (!tableBody || !emptyTemplate?.content?.firstElementChild) {
        return;
      }
      const existingEmptyRow = tableBody.querySelector('[data-role="user-survey-filter-empty-row"]');
      if (hasVisibleRows) {
        existingEmptyRow?.remove();
        return;
      }
      if (existingEmptyRow) {
        return;
      }
      const emptyRow = emptyTemplate.content.firstElementChild.cloneNode(true);
      emptyRow.dataset.role = "user-survey-filter-empty-row";
      tableBody.appendChild(emptyRow);
    }
    function populateDateFilters() {
      const refs = getContentRefs();
      const rows = Array.from(contentHost.querySelectorAll('[data-role="user-survey-row"]'));
      const monthOptions = Array.from(new Set(rows.map((row) => row.dataset.filterMonth || "").filter(Boolean))).sort().map((value) => ({ value, label: getMonthLabel2(value) }));
      const yearOptions = Array.from(new Set(rows.map((row) => row.dataset.filterYear || "").filter(Boolean))).sort((left, right) => Number(right) - Number(left)).map((value) => ({ value, label: value }));
      state.monthFilter = setSelectOptions2(refs.monthFilter, monthOptions, "Все месяцы", state.monthFilter);
      state.yearFilter = setSelectOptions2(refs.yearFilter, yearOptions, "Все годы", state.yearFilter);
    }
    function applyLocalFilters() {
      const refs = getContentRefs();
      const rows = Array.from(contentHost.querySelectorAll('[data-role="user-survey-row"]'));
      if (!refs.tableBody || rows.length === 0) {
        return;
      }
      let visibleCount = 0;
      rows.forEach((row) => {
        const rowMonth = row.dataset.filterMonth || "";
        const rowYear = row.dataset.filterYear || "";
        const matchesMonth = !state.monthFilter || rowMonth === state.monthFilter;
        const matchesYear = !state.yearFilter || rowYear === state.yearFilter;
        const visible = matchesMonth && matchesYear;
        row.hidden = !visible;
        if (visible) {
          visibleCount += 1;
        }
      });
      const serverEmptyRow = refs.tableBody.querySelector('[data-role="user-survey-empty-row"]');
      if (serverEmptyRow && rows.length > 0) {
        serverEmptyRow.hidden = visibleCount > 0;
      }
      ensureFilteredEmptyRow(refs.tableBody, visibleCount > 0);
    }
    return {
      populateDateFilters,
      applyLocalFilters
    };
  }

  // Web/wwwroot/js/features/survey/user-survey-row-tooltip.js
  function createSurveyUserRowTooltip(options = {}) {
    return window.AppUi.createRowTooltip({
      ...options
    });
  }

  // Web/wwwroot/js/features/survey/user-survey-snapshot-loader.js
  function buildSnapshotUrl({ tab, userId, page, searchTerm, signedOnly, filterQuery }) {
    if (tab === "help") {
      return "/help";
    }
    if (tab === "active") {
      return `/survey?page=${page}&searchTerm=${encodeURIComponent(searchTerm || "")}`;
    }
    const params = new URLSearchParams(filterQuery ?? window.location.search);
    ["page", "searchTerm", "signedOnly"].forEach((key) => params.delete(key));
    params.set("page", String(page));
    params.set("searchTerm", searchTerm || "");
    params.set("signedOnly", signedOnly ? "true" : "false");
    return `/archive/${userId}?${params.toString()}`;
  }
  function getSnapshotLoadError(tab) {
    return tab === "help" ? "Ошибка загрузки справки" : "Ошибка загрузки данных анкет";
  }
  function getSnapshotParseError(tab) {
    return tab === "help" ? "Не удалось построить содержимое справки" : "Не удалось построить содержимое страницы анкет";
  }
  async function fetchSurveyUserSnapshot({ tab, userId, page, searchTerm, signedOnly, filterQuery }) {
    const response = await fetch(buildSnapshotUrl({ tab, userId, page, searchTerm, signedOnly, filterQuery }), {
      headers: {
        "X-Requested-With": "XMLHttpRequest"
      }
    });
    if (!response.ok) {
      throw new Error(getSnapshotLoadError(tab));
    }
    const snapshot = createSnapshotFromHtml(await response.text());
    if (!snapshot) {
      throw new Error(getSnapshotParseError(tab));
    }
    return snapshot;
  }

  // Web/wwwroot/js/features/survey/user-survey-list.js
  function readSurveyUserBootstrapData(root = document) {
    const bootstrapElement = root.querySelector("#survey-user-list-bootstrap") || root.querySelector("#user-archive-bootstrap") || document.getElementById("survey-user-list-bootstrap") || document.getElementById("user-archive-bootstrap");
    if (!bootstrapElement?.textContent) {
      return null;
    }
    try {
      return JSON.parse(bootstrapElement.textContent.trim());
    } catch (error) {
      console.error("Не удалось прочитать bootstrap-данные user survey:", error);
      return null;
    }
  }
  function mountSurveyUserListPage(page, bindSurveyUserListPage2) {
    const bootstrapData = readSurveyUserBootstrapData(page);
    return bootstrapData ? bindSurveyUserListPage2(bootstrapData, page) : null;
  }
  function renderSurveyUserChrome(initialData) {
    const chromeContext = typeof window.readAppChromeContext === "function" ? window.readAppChromeContext() : null;
    const headerHost = document.getElementById("chrome-header");
    const footerHost = document.getElementById("chrome-footer");
    const props = {
      userRole: chromeContext?.userRole || initialData?.userRole,
      displayName: chromeContext?.displayName || initialData?.displayName,
      userName: chromeContext?.userName || initialData?.userName,
      organizationName: chromeContext?.organizationName || initialData?.organizationName
    };
    if (headerHost && typeof window.mountHeader === "function") {
      window.mountHeader(headerHost, props);
    }
    if (footerHost && typeof window.mountFooter === "function") {
      window.mountFooter(footerHost);
    }
  }
  function createSurveyUserListInteractionController({
    contentHost,
    state,
    rowTooltip,
    localFilters,
    openSurveyById,
    handleTabChange,
    loadTabSnapshot
  } = {}) {
    if (!contentHost) {
      return { destroy: () => {
      } };
    }
    const tabActions = [
      ["tab-active", "active"],
      ["tab-help", "help"],
      ["tab-archived", "archived"]
    ];
    function getEventTarget(event) {
      return event.target instanceof Element ? event.target : null;
    }
    function belongsToPage(element) {
      return Boolean(element && contentHost.contains(element));
    }
    function readPositiveNumber(element, name) {
      const value = Number(element?.dataset?.[name] || 0);
      return Number.isFinite(value) && value > 0 ? value : 0;
    }
    function getClickedTab(target) {
      for (const [role, tab] of tabActions) {
        const button = target.closest(`[data-role="${role}"]`);
        if (belongsToPage(button)) {
          return tab;
        }
      }
      return null;
    }
    function handleClick(event) {
      const target = getEventTarget(event);
      if (!target) {
        return;
      }
      const tab = getClickedTab(target);
      if (tab) {
        event.preventDefault();
        handleTabChange?.(tab);
        return;
      }
      const actionButton = target.closest('[data-role="action"]');
      if (belongsToPage(actionButton)) {
        const surveyId = readPositiveNumber(actionButton, "surveyId");
        if (surveyId) {
          openSurveyById?.(surveyId);
        }
        return;
      }
      const actionableRow = target.closest('[data-role="user-survey-row"][data-row-action]');
      if (belongsToPage(actionableRow) && !target.closest("button, a, input, select, textarea")) {
        const surveyId = readPositiveNumber(actionableRow, "surveyId");
        if (surveyId) {
          rowTooltip?.hide?.();
          openSurveyById?.(surveyId);
        }
        return;
      }
      const paginationButton = target.closest('[data-role="pagination-page"]');
      if (belongsToPage(paginationButton)) {
        const targetPage = readPositiveNumber(paginationButton, "page");
        if (!targetPage || targetPage === state?.currentSnapshot?.currentPage) {
          return;
        }
        event.preventDefault();
        loadTabSnapshot?.(state.activeTab, {
          page: targetPage,
          searchTerm: state.currentSnapshot.searchTerm,
          signedOnly: state.currentSnapshot.signedOnly,
          scrollToTableStart: true
        });
      }
    }
    function handleDoubleClick(event) {
      const target = getEventTarget(event);
      const row = target?.closest('[data-role="user-survey-row"]');
      if (!belongsToPage(row) || target.closest("button") || row.dataset.rowAction) {
        return;
      }
      const surveyId = readPositiveNumber(row, "surveyId");
      if (surveyId) {
        openSurveyById?.(surveyId);
      }
    }
    function handleMouseOver(event) {
      const target = getEventTarget(event);
      const row = target?.closest('[data-role="user-survey-row"][data-hover-label]');
      if (!belongsToPage(row) || rowTooltip?.isActiveRow?.(row)) {
        return;
      }
      rowTooltip?.show?.(row, event);
    }
    function handleMouseMove(event) {
      if (!rowTooltip?.hasActiveRow?.()) {
        return;
      }
      rowTooltip.move(event);
    }
    function handleMouseOut(event) {
      if (!rowTooltip?.hasActiveRow?.() || rowTooltip.activeRowContains?.(event.relatedTarget)) {
        return;
      }
      rowTooltip.hide();
    }
    function handleSubmit(event) {
      const target = getEventTarget(event);
      const searchForm = target?.closest('[data-role="search-form"]');
      if (!belongsToPage(searchForm)) {
        return;
      }
      event.preventDefault();
      const searchInput = searchForm.querySelector('[data-role="search-input"]');
      const signedInput = searchForm.querySelector('[data-role="signed-filter-input"]');
      loadTabSnapshot?.(state.activeTab, {
        page: 1,
        searchTerm: searchInput?.value?.trim() || "",
        signedOnly: Boolean(signedInput?.checked)
      });
    }
    function handleChange(event) {
      const target = getEventTarget(event);
      if (!target) {
        return;
      }
      const monthFilter = target.closest('[data-role="month-filter"]');
      if (belongsToPage(monthFilter)) {
        state.monthFilter = monthFilter.value;
        localFilters?.applyLocalFilters?.();
        return;
      }
      const yearFilter = target.closest('[data-role="year-filter"]');
      if (belongsToPage(yearFilter)) {
        state.yearFilter = yearFilter.value;
        localFilters?.applyLocalFilters?.();
        return;
      }
      const signedInput = target.closest('[data-role="signed-filter-input"]');
      if (belongsToPage(signedInput)) {
        loadTabSnapshot?.("archived", {
          page: 1,
          searchTerm: state.currentSnapshot.searchTerm,
          signedOnly: signedInput.checked
        });
      }
    }
    const listeners = [
      ["click", handleClick],
      ["dblclick", handleDoubleClick],
      ["mouseover", handleMouseOver],
      ["mousemove", handleMouseMove],
      ["mouseout", handleMouseOut],
      ["submit", handleSubmit],
      ["change", handleChange]
    ];
    listeners.forEach(([type, handler]) => contentHost.addEventListener(type, handler));
    return {
      destroy() {
        listeners.forEach(([type, handler]) => {
          contentHost.removeEventListener(type, handler);
        });
      }
    };
  }
  function createSurveyUserHistoryController({ onTabChange } = {}) {
    function sync(tab, mode) {
      const entry = buildSurveyUserHistoryEntry(tab);
      if (!entry) {
        return;
      }
      const currentPath = normalizeSurveyUserPathname(window.location.pathname);
      const shouldKeepCurrentQuery = tab === "archived" && currentPath === entry.url && window.location.search;
      const entryUrl = shouldKeepCurrentQuery ? `${entry.url}${window.location.search}` : entry.url;
      const nextState = { tab: entry.tab };
      if (mode === "replace") {
        window.history.replaceState(nextState, "", entryUrl);
        return;
      }
      if (currentPath === entry.url && window.location.search === (shouldKeepCurrentQuery ? window.location.search : "") && window.history.state?.tab === nextState.tab) {
        return;
      }
      window.history.pushState(nextState, "", entryUrl);
    }
    function pushArchiveFilterQuery(queryString) {
      const normalizedQuery = String(queryString || "").replace(/^\?/, "").trim();
      const nextHistoryUrl = normalizedQuery ? `/archive?${normalizedQuery}` : "/archive";
      const currentUrl = `${normalizeSurveyUserPathname(window.location.pathname)}${window.location.search}`;
      if (currentUrl === nextHistoryUrl && window.history.state?.tab === "archived") {
        window.history.replaceState({ tab: "archived" }, "", nextHistoryUrl);
      } else {
        window.history.pushState({ tab: "archived" }, "", nextHistoryUrl);
      }
      return normalizedQuery;
    }
    function handlePopState() {
      const entry = window.history.state?.tab ? buildSurveyUserHistoryEntry(window.history.state.tab) : getSurveyUserHistoryEntryFromLocation(window.location.pathname);
      if (!entry) {
        return;
      }
      onTabChange?.(entry.tab, { historyMode: "none" });
    }
    function mount() {
      window.addEventListener("popstate", handlePopState);
    }
    function destroy() {
      window.removeEventListener("popstate", handlePopState);
    }
    return {
      sync,
      pushArchiveFilterQuery,
      mount,
      destroy
    };
  }
  function createSurveyUserListModalController({
    state,
    initialData,
    setError,
    isDisposed,
    onBackToList,
    onSurveySubmitted
  }) {
    const modalState = {
      fillCleanup: null,
      answersCleanup: null,
      prefetchedHtml: null,
      openRequestId: 0
    };
    function cleanup(kind) {
      if (kind === "fill" && typeof modalState.fillCleanup === "function") {
        modalState.fillCleanup();
        modalState.fillCleanup = null;
      }
      if (kind === "answers" && typeof modalState.answersCleanup === "function") {
        modalState.answersCleanup();
        modalState.answersCleanup = null;
      }
    }
    function getModalConfig() {
      if (state.currentView === "survey-fill") {
        return {
          kind: "fill",
          hostSelector: '[data-role="fill-modal-host"]',
          title: "Заполнение анкеты",
          mountPage: window.mountSurveyFillPage,
          extraOptions: {
            onBack: onBackToList,
            onSubmitted: () => onSurveySubmitted?.(state.currentSurvey)
          }
        };
      }
      if (state.currentView === "check-answers") {
        return {
          kind: "answers",
          hostSelector: '[data-role="answers-modal-host"]',
          title: "Просмотр анкеты",
          mountPage: window.mountCheckAnswersPage,
          extraOptions: {}
        };
      }
      return null;
    }
    function render() {
      cleanup("fill");
      cleanup("answers");
      const config = getModalConfig();
      const modalHost = config ? document.querySelector(config.hostSelector) : null;
      if (!config || !state.currentSurvey || !modalHost || typeof config.mountPage !== "function") {
        return;
      }
      const initialHtml = modalState.prefetchedHtml;
      modalState.prefetchedHtml = null;
      modalState[`${config.kind}Cleanup`] = mountSurveyUserModal(modalHost, {
        title: config.title,
        onClose: onBackToList,
        mountBody: (modalBodyHost, modalFooterHost) => config.mountPage(modalBodyHost, {
          survey: state.currentSurvey,
          organizationId: initialData.userOrganizationId,
          initialHtml,
          footerHost: modalFooterHost,
          ...config.extraOptions
        })
      });
    }
    async function open(survey, activeTab) {
      if (!survey) {
        return;
      }
      const surveyId = getSurveyId(survey);
      const targetView = activeTab === "active" ? "survey-fill" : "check-answers";
      const requestId = modalState.openRequestId + 1;
      modalState.openRequestId = requestId;
      try {
        const prefetchedHtml = targetView === "survey-fill" ? await window.fetchSurveyFillContentHtml?.(surveyId, initialData.userOrganizationId) : await window.fetchSurveyAnswersContentHtml?.(surveyId, initialData.userOrganizationId);
        if (isDisposed() || modalState.openRequestId !== requestId) {
          return;
        }
        modalState.prefetchedHtml = typeof prefetchedHtml === "string" ? prefetchedHtml : null;
        state.currentSurvey = survey;
        state.currentView = targetView;
        render();
      } catch (error) {
        if (isDisposed() || modalState.openRequestId !== requestId) {
          return;
        }
        modalState.prefetchedHtml = null;
        setError(error?.message || "Не удалось открыть анкету");
      }
    }
    function closeToList() {
      modalState.openRequestId += 1;
      modalState.prefetchedHtml = null;
      state.currentView = "survey-list";
      state.currentSurvey = null;
      render();
    }
    function destroy() {
      modalState.openRequestId += 1;
      cleanup("fill");
      cleanup("answers");
    }
    return {
      render,
      open,
      closeToList,
      destroy
    };
  }
  function registerSurveyUserListPage(bindSurveyUserListPage2) {
    if (window.AppPageLifecycle?.register) {
      window.AppPageLifecycle.register(
        "survey-user-list",
        '[data-page="user-surveys"]',
        (page2) => mountSurveyUserListPage(page2, bindSurveyUserListPage2)
      );
      return;
    }
    const page = document.querySelector('[data-page="user-surveys"]');
    if (page) {
      mountSurveyUserListPage(page, bindSurveyUserListPage2);
    }
  }
  window.bindSurveyUserListPage = function bindSurveyUserListPage(initialData, pageRoot = null) {
    window.__surveyUserListController?.destroy?.();
    const contentHost = pageRoot || document.getElementById("default_content");
    const emptyTemplate = contentHost?.querySelector("#survey-user-empty-template");
    if (!contentHost) {
      return null;
    }
    let disposed = false;
    function remountPageEnhancements() {
      if (!(contentHost instanceof Element)) {
        return;
      }
      window.SurveysPage?.destroy?.(contentHost);
      window.SurveyFilters?.destroy?.(contentHost);
      window.SurveyFilters?.mount?.(contentHost);
    }
    const initialSnapshot = createSnapshotFromHost(contentHost);
    if (!initialSnapshot) {
      return;
    }
    const tabTemplateElements = {
      active: contentHost.querySelector("#survey-user-active-content-template") || document.getElementById("survey-user-active-content-template"),
      archived: contentHost.querySelector("#survey-user-archived-content-template") || document.getElementById("survey-user-archived-content-template"),
      help: contentHost.querySelector("#survey-user-help-content-template") || document.getElementById("survey-user-help-content-template")
    };
    const state = {
      activeTab: initialSnapshot.activeTab,
      currentView: "survey-list",
      currentSurvey: null,
      currentSnapshot: initialSnapshot,
      loading: false,
      activeCount: readSurveyUserActiveCountFromDom(contentHost) ?? readSurveyUserActiveCountFromSnapshot(initialSnapshot) ?? 0,
      monthFilter: "",
      yearFilter: "",
      tabSnapshots: {
        active: initialSnapshot.activeTab === "active" ? initialSnapshot : null,
        archived: initialSnapshot.activeTab === "archived" ? initialSnapshot : null,
        help: initialSnapshot.activeTab === "help" ? initialSnapshot : null
      }
    };
    renderSurveyUserChrome(initialData);
    let refreshPromise = null;
    const rowTooltip = createSurveyUserRowTooltip();
    function getContentRoot() {
      return contentHost.querySelector('[data-role="survey-user-content"]');
    }
    function getCachedTabSnapshot(tab) {
      if (state.tabSnapshots[tab]) {
        return state.tabSnapshots[tab];
      }
      const snapshot = createSnapshotFromTemplateElement(tabTemplateElements[tab]);
      if (snapshot) {
        state.tabSnapshots[tab] = snapshot;
      }
      return snapshot;
    }
    function updateActiveCountFromSnapshot(snapshot) {
      const nextCount = readSurveyUserActiveCountFromSnapshot(snapshot);
      if (nextCount !== null) {
        state.activeCount = nextCount;
      }
    }
    function syncActiveCountBadge() {
      syncSurveyUserActiveCountBadge(getContentRoot(), state.activeCount);
    }
    function getContentRefs() {
      const root = getContentRoot();
      return {
        root,
        searchForm: root?.querySelector('[data-role="search-form"]'),
        searchInput: root?.querySelector('[data-role="search-input"]'),
        monthFilter: root?.querySelector('[data-role="month-filter"]'),
        yearFilter: root?.querySelector('[data-role="year-filter"]'),
        signedInput: root?.querySelector('[data-role="signed-filter-input"]'),
        loading: root?.querySelector('[data-role="loading"]'),
        tableSection: root?.querySelector('[data-role="table-section"]'),
        tableBody: root?.querySelector('[data-role="survey-table-body"]'),
        pagination: root?.querySelector('[data-role="pagination"]'),
        errorWrap: root?.querySelector('[data-role="error"]'),
        errorText: root?.querySelector('[data-role="error-text"]')
      };
    }
    function scrollToTableSection() {
      const refs = getContentRefs();
      const target = refs.tableSection?.querySelector("table") || refs.tableSection;
      if (!target) {
        return;
      }
      target.scrollIntoView({
        block: "start",
        behavior: "auto"
      });
    }
    function setLoading(isLoading) {
      state.loading = isLoading;
      const refs = getContentRefs();
      refs.loading?.classList.toggle("u-hidden", !isLoading);
      if (refs.tableSection) {
        refs.tableSection.classList.toggle("u-hidden", isLoading);
      }
    }
    function setError(message) {
      const refs = getContentRefs();
      refs.errorText && (refs.errorText.textContent = "");
      refs.errorWrap?.classList.add("u-hidden");
      const rawMessage = String(message || "").trim();
      if (!rawMessage) {
        return;
      }
      const safeMessage = typeof window.normalizeClientErrorMessage === "function" ? window.normalizeClientErrorMessage(rawMessage) : rawMessage;
      window.AppUi.notify(safeMessage, "error", { title: "Ошибка" });
    }
    const localFilters = createSurveyUserLocalFilters({
      contentHost,
      emptyTemplate,
      state,
      getContentRefs,
      getMonthLabel,
      setSelectOptions
    });
    const modals = createSurveyUserListModalController({
      state,
      initialData,
      setError,
      isDisposed: () => disposed,
      onBackToList: handleBackToList,
      onSurveySubmitted: handleSurveySubmitted
    });
    const historyController = createSurveyUserHistoryController({
      onTabChange: (tab, options) => handleTabChange(tab, null, options)
    });
    let interactionController = null;
    function applySnapshot(snapshot, options = {}, { replaceContent = false } = {}) {
      if (!snapshot || replaceContent && !snapshot.template) {
        return;
      }
      if (replaceContent) {
        rowTooltip.hide();
        contentHost.replaceChildren(snapshot.template.content.cloneNode(true));
        state.currentSnapshot = createSnapshotFromHost(contentHost) || snapshot;
      } else {
        state.currentSnapshot = snapshot;
      }
      const currentSnapshot = state.currentSnapshot;
      state.activeTab = currentSnapshot.activeTab;
      state.tabSnapshots[state.activeTab] = currentSnapshot;
      if (state.activeTab === "active") {
        updateActiveCountFromSnapshot(currentSnapshot);
      }
      if (!options.preserveFilters) {
        state.monthFilter = "";
        state.yearFilter = "";
      }
      setLoading(false);
      setError("");
      localFilters.populateDateFilters();
      localFilters.applyLocalFilters();
      remountPageEnhancements();
      syncActiveCountBadge();
      modals.render();
    }
    function mountSnapshot(snapshot, options = {}) {
      applySnapshot(snapshot, options, { replaceContent: true });
    }
    function hydrateCurrentSnapshot(snapshot, options = {}) {
      applySnapshot(snapshot, options);
    }
    async function fetchSnapshot(tab, page, searchTerm, signedOnly, filterQuery = null) {
      return fetchSurveyUserSnapshot({
        tab,
        userId: initialData.userId,
        page,
        searchTerm,
        signedOnly,
        filterQuery
      });
    }
    async function loadTabSnapshot(tab, options = {}) {
      const currentSnapshot = state.tabSnapshots[tab];
      const page = options.page ?? currentSnapshot?.currentPage ?? 1;
      const searchTerm = options.searchTerm ?? currentSnapshot?.searchTerm ?? "";
      const signedOnly = tab === "archived" ? Boolean(options.signedOnly ?? currentSnapshot?.signedOnly) : false;
      if (options.showLoading !== false && state.activeTab === tab) {
        setError("");
        setLoading(true);
      }
      try {
        const snapshot = await fetchSnapshot(tab, page, searchTerm, signedOnly, options.filterQuery ?? null);
        if (disposed) {
          return null;
        }
        state.tabSnapshots[tab] = snapshot;
        if (tab === "active") {
          updateActiveCountFromSnapshot(snapshot);
          syncActiveCountBadge();
        }
        if (options.applyToCurrent !== false && state.activeTab === tab) {
          mountSnapshot(snapshot, { preserveFilters: options.preserveFilters === true });
          if (options.scrollToTableStart === true) {
            scrollToTableSection();
          }
        }
        return snapshot;
      } catch (error) {
        if (disposed) {
          return null;
        }
        if (state.activeTab === tab) {
          setLoading(false);
          setError(error?.message || "Ошибка загрузки данных анкет");
        } else {
          console.error("Ошибка фонового обновления списка анкет:", error);
        }
        return null;
      }
    }
    function openSurveyById(surveyId) {
      const survey = state.currentSnapshot.surveys.find((item) => getSurveyId(item) === surveyId);
      if (!survey) {
        return;
      }
      modals.open(survey, state.activeTab);
    }
    function handleBackToList() {
      modals.closeToList();
    }
    async function handleSurveySubmitted() {
      handleBackToList();
      await refreshAllSnapshots({ preserveFilters: true });
    }
    async function refreshAllSnapshots(options = {}) {
      if (refreshPromise) {
        return refreshPromise;
      }
      const activeSnapshot = state.tabSnapshots.active;
      const archivedSnapshot = state.tabSnapshots.archived;
      refreshPromise = Promise.all([
        loadTabSnapshot("active", {
          page: activeSnapshot?.currentPage ?? 1,
          searchTerm: activeSnapshot?.searchTerm ?? "",
          applyToCurrent: false,
          showLoading: state.activeTab === "active"
        }),
        loadTabSnapshot("archived", {
          page: archivedSnapshot?.currentPage ?? 1,
          searchTerm: archivedSnapshot?.searchTerm ?? "",
          signedOnly: archivedSnapshot?.signedOnly ?? false,
          applyToCurrent: false,
          showLoading: state.activeTab === "archived"
        })
      ]).finally(() => {
        refreshPromise = null;
      });
      const [nextActiveSnapshot, nextArchivedSnapshot] = await refreshPromise;
      if (disposed) {
        return null;
      }
      const currentSnapshot = state.activeTab === "archived" ? nextArchivedSnapshot : state.activeTab === "active" ? nextActiveSnapshot : state.tabSnapshots.help;
      if (currentSnapshot) {
        mountSnapshot(currentSnapshot, { preserveFilters: options.preserveFilters === true });
      }
      return {
        active: nextActiveSnapshot,
        archived: nextArchivedSnapshot
      };
    }
    function handleTabChange(tab, _unused = null, options = {}) {
      options = options || {};
      const normalizedTab = tab === "archived_surveys_for_user" ? "archived" : tab;
      if (normalizedTab !== "active" && normalizedTab !== "archived" && normalizedTab !== "help") {
        return;
      }
      state.activeTab = normalizedTab;
      state.currentView = "survey-list";
      state.currentSurvey = null;
      state.monthFilter = "";
      state.yearFilter = "";
      if (options.historyMode !== "none") {
        historyController.sync(normalizedTab, options.historyMode || "push");
      }
      const cachedSnapshot = getCachedTabSnapshot(normalizedTab);
      if (cachedSnapshot) {
        mountSnapshot(cachedSnapshot);
        return;
      }
      loadTabSnapshot(normalizedTab, {
        page: 1,
        searchTerm: "",
        signedOnly: false,
        applyToCurrent: true
      });
    }
    interactionController = createSurveyUserListInteractionController({
      contentHost,
      state,
      rowTooltip,
      localFilters,
      openSurveyById,
      handleTabChange,
      loadTabSnapshot
    });
    historyController.sync(state.activeTab, "replace");
    hydrateCurrentSnapshot(initialSnapshot);
    const refreshSurveyUserPageData = function refreshSurveyUserPageData2(options = {}) {
      return refreshAllSnapshots({
        preserveFilters: options.preserveFilters !== false
      });
    };
    window.refreshSurveyUserPageData = refreshSurveyUserPageData;
    const refreshSurveyUserArchiveFilters = function refreshSurveyUserArchiveFilters2(queryString, options = {}) {
      if (state.activeTab !== "archived") {
        return;
      }
      const normalizedQuery = historyController.pushArchiveFilterQuery(queryString);
      loadTabSnapshot("archived", {
        page: 1,
        searchTerm: state.currentSnapshot.searchTerm,
        signedOnly: state.currentSnapshot.signedOnly,
        filterQuery: normalizedQuery,
        preserveFilters: true,
        scrollToTableStart: Boolean(options.scrollTargetSelector)
      });
    };
    window.refreshSurveyUserArchiveFilters = refreshSurveyUserArchiveFilters;
    historyController.mount();
    const destroy = () => {
      if (disposed) {
        return;
      }
      disposed = true;
      modals.destroy();
      rowTooltip.destroy();
      interactionController?.destroy?.();
      historyController.destroy();
      window.SurveysPage?.destroy?.(contentHost);
      window.SurveyFilters?.destroy?.(contentHost);
      if (window.refreshSurveyUserPageData === refreshSurveyUserPageData) {
        delete window.refreshSurveyUserPageData;
      }
      if (window.refreshSurveyUserArchiveFilters === refreshSurveyUserArchiveFilters) {
        delete window.refreshSurveyUserArchiveFilters;
      }
      if (window.__surveyUserListController?.destroy === destroy) {
        delete window.__surveyUserListController;
      }
    };
    window.__surveyUserListController = { destroy };
    return destroy;
  };
  registerSurveyUserListPage(window.bindSurveyUserListPage);
})();
//# sourceMappingURL=survey-user-app.js.map
