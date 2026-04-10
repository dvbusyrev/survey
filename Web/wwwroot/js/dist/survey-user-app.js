(() => {
  // Web/wwwroot/js/ui/app-header.js
  function renderHeader(host, { userRole, displayName, userName, organizationName }) {
    const rawDisplayName = displayName && String(displayName).trim() ? String(displayName).trim() : userRole === "admin" ? "Администратор" : "Пользователь";
    const displayNameParts = rawDisplayName.split(":").map((part) => part.trim()).filter(Boolean);
    const normalizedUserName = userName && String(userName).trim() ? String(userName).trim() : displayNameParts.length > 1 ? displayNameParts.slice(1).join(": ").trim() : rawDisplayName;
    const normalizedOrganizationName = organizationName && String(organizationName).trim() ? String(organizationName).trim() : displayNameParts[0] || "Пользователь";
    const headerTopLine = userRole === "admin" ? "Администрирование" : normalizedOrganizationName;
    const normalizedDisplayName = userRole === "admin" ? normalizedUserName || "Администратор" : normalizedUserName || rawDisplayName;
    const template = document.getElementById("header-template");
    if (!host || !template?.content?.firstElementChild) {
      return null;
    }
    host.innerHTML = "";
    const header = template.content.firstElementChild.cloneNode(true);
    const modeLabel = header.querySelector(".header-mode-label");
    const role = header.querySelector("#role");
    const logoutButton = header.querySelector(".logout-button");
    if (modeLabel) {
      modeLabel.textContent = headerTopLine;
    }
    if (role) {
      role.textContent = normalizedDisplayName;
      role.setAttribute("title", normalizedDisplayName);
    }
    if (logoutButton) {
      logoutButton.addEventListener("click", () => {
        fetch("/auth/logout", { method: "POST" }).then((response) => {
          if (response.ok) {
            window.location.href = "/";
          } else {
            console.error("Ошибка при выходе");
          }
        }).catch((error) => console.error("Ошибка сети:", error));
      });
    }
    host.appendChild(header);
    return () => {
      host.innerHTML = "";
    };
  }
  window.mountHeader = function mountHeader(host, props) {
    return renderHeader(host, props || {});
  };

  // Web/wwwroot/js/ui/app-navigation.js
  function renderNavigation(host, { openTab, activeTab, userRole, userId }) {
    const isAdmin = userRole === "admin";
    const isSurveySectionActive = isAdmin ? ["get_surveys", "add_survey", "list_answers_users", "archived_surveys"].includes(activeTab) : ["active", "archived", "answers_tab", "archived_surveys_for_user"].includes(activeTab);
    const isOrganizationSectionActive = ["get_organization", "add_organization", "archive_list_organizations"].includes(activeTab);
    const navigate = (tab) => {
      if (tab === "add_user") {
        const tryOpenAddUserModal = () => {
          if (typeof window.openAddUserModal === "function" && document.getElementById("addUserModal")) {
            window.openAddUserModal();
            return true;
          }
          return false;
        };
        if (tryOpenAddUserModal()) {
          return;
        }
        if (typeof openTab === "function") {
          openTab("get_users");
          let attempts = 0;
          const timer = window.setInterval(() => {
            attempts += 1;
            if (tryOpenAddUserModal() || attempts >= 30) {
              window.clearInterval(timer);
            }
          }, 200);
          return;
        }
        window.location.href = "/users";
        return;
      }
      if (tab === "add_organization") {
        const tryOpenAddOrganizationModal = () => {
          if (typeof window.openAddOrganizationModal === "function" && document.getElementById("addOrganizationModal")) {
            window.openAddOrganizationModal();
            return true;
          }
          return false;
        };
        if (tryOpenAddOrganizationModal()) {
          return;
        }
        if (typeof openTab === "function") {
          openTab("get_organization");
          let attempts = 0;
          const timer = window.setInterval(() => {
            attempts += 1;
            if (tryOpenAddOrganizationModal() || attempts >= 30) {
              window.clearInterval(timer);
            }
          }, 200);
          return;
        }
        window.location.href = "/organizations/create";
        return;
      }
      if (typeof openTab === "function") {
        openTab(tab);
        return;
      }
      if (tab === "help") {
        window.location.href = "/help";
        return;
      }
      if ((tab === "active" || tab === "answers_tab") && userId) {
        window.location.href = "/my-surveys";
        return;
      }
      if ((tab === "archived" || tab === "archived_surveys_for_user") && userId) {
        window.location.href = "/my-surveys/archive";
        return;
      }
      const routes = {
        get_surveys: "/surveys",
        add_survey: "/surveys/create",
        list_answers_users: "/surveys/answers",
        archived_surveys: "/surveys/archive",
        open_statistics: "/statistics",
        get_users: "/users",
        get_organization: "/organizations",
        archive_list_organizations: "/organizations/archive",
        reports: "/reports",
        email: "/mail-settings",
        get_logs: "/logs"
      };
      if (routes[tab]) {
        window.location.href = routes[tab];
        return;
      }
      if (tab === "monthly_summary_report") {
        window.location.href = "/reports";
        return;
      }
      if (tab.startsWith("quarterly_report_q")) {
        window.location.href = "/reports";
      }
    };
    const templateId = isAdmin ? "nav-template-admin" : "nav-template-user";
    const template = document.getElementById(templateId);
    if (!host || !template?.content?.firstElementChild) {
      return null;
    }
    host.innerHTML = "";
    const nav = template.content.firstElementChild.cloneNode(true);
    host.appendChild(nav);
    const closeSubmenus = () => {
      nav.querySelectorAll(".nav-item.has-submenu.submenu-open").forEach((item) => {
        item.classList.remove("submenu-open");
      });
    };
    nav.querySelectorAll(".nav-item").forEach((item) => {
      const tab = item.dataset.tab || "";
      const navClass = item.dataset.navClass || "";
      const isActive = navClass === "surveys" ? isSurveySectionActive : navClass === "organizations" ? isOrganizationSectionActive : tab === activeTab;
      item.classList.toggle("active", isActive);
    });
    nav.querySelectorAll(".submenu-item").forEach((subItem) => {
      subItem.classList.toggle("active", (subItem.dataset.tab || "") === activeTab);
    });
    nav.querySelectorAll(".nav-item.has-submenu").forEach((item) => {
      const onEnter = () => item.classList.add("submenu-open");
      const onLeave = () => item.classList.remove("submenu-open");
      item.addEventListener("mouseenter", onEnter);
      item.addEventListener("mouseleave", onLeave);
    });
    const navLeaveHandler = () => closeSubmenus();
    nav.addEventListener("mouseleave", navLeaveHandler);
    nav.querySelectorAll(".nav-link").forEach((link) => {
      link.addEventListener("click", (event) => {
        event.preventDefault();
        const item = event.currentTarget.closest(".nav-item");
        if (!item) {
          return;
        }
        if (item.classList.contains("has-submenu")) {
          const willOpen = !item.classList.contains("submenu-open");
          closeSubmenus();
          if (willOpen) {
            item.classList.add("submenu-open");
          }
          return;
        }
        closeSubmenus();
        navigate(item.dataset.tab || "");
      });
    });
    nav.querySelectorAll(".submenu-link").forEach((link) => {
      link.addEventListener("click", (event) => {
        event.preventDefault();
        closeSubmenus();
        const item = event.currentTarget.closest(".submenu-item");
        navigate(item?.dataset?.tab || "");
      });
    });
    const onPointerDown = (event) => {
      if (!event.target.closest(".admin-nav")) {
        closeSubmenus();
      }
    };
    document.addEventListener("pointerdown", onPointerDown);
    return () => {
      document.removeEventListener("pointerdown", onPointerDown);
      nav.removeEventListener("mouseleave", navLeaveHandler);
      host.innerHTML = "";
    };
  }
  window.mountNavigation = function mountNavigation(host, props) {
    return renderNavigation(host, props || {});
  };

  // Web/wwwroot/js/ui/app-footer.js
  function renderFooter(host) {
    const template = document.getElementById("footer-template");
    if (!host || !template?.content?.firstElementChild) {
      return null;
    }
    host.innerHTML = "";
    host.appendChild(template.content.firstElementChild.cloneNode(true));
    return () => {
      host.innerHTML = "";
    };
  }
  window.mountFooter = function mountFooter(host) {
    return renderFooter(host);
  };

  // Web/wwwroot/js/features/survey/user-survey-flow.js
  window.populateMonthOptions = function() {
    const select = document.getElementById("filterOrganization");
    const cards = document.querySelectorAll(".survey-card");
    const months = /* @__PURE__ */ new Set();
    cards.forEach((card) => {
      const dateElement = card.querySelector(".dates");
      const dateText = dateElement.textContent.trim();
      const match = dateText.match(/(\d{2})\.(\d{2})\.(\d{4})/);
      const month = match[2];
      months.add(month);
    });
    const currentValue = select.value;
    select.innerHTML = "";
    const defaultMonthOption = document.createElement("option");
    defaultMonthOption.value = "";
    defaultMonthOption.textContent = "За все месяцы";
    select.appendChild(defaultMonthOption);
    Array.from(months).sort().forEach((month) => {
      const option = document.createElement("option");
      option.value = month;
      option.textContent = getMonthName(month);
      select.appendChild(option);
    });
    select.value = currentValue;
  };
  window.populateYearOptions = function() {
    const select = document.getElementById("filterSurvey");
    const cards = document.querySelectorAll(".survey-card");
    const years = /* @__PURE__ */ new Set();
    cards.forEach((card) => {
      const dateElement = card.querySelector(".dates");
      const dateText = dateElement.textContent.trim();
      const match = dateText.match(/(\d{2})\.(\d{2})\.(\d{4})/);
      const year = match[3];
      years.add(year);
    });
    const currentValue = select.value;
    select.innerHTML = "";
    const defaultYearOption = document.createElement("option");
    defaultYearOption.value = "";
    defaultYearOption.textContent = "По всем годам";
    select.appendChild(defaultYearOption);
    Array.from(years).sort().forEach((year) => {
      const option = document.createElement("option");
      option.value = year;
      option.textContent = year;
      select.appendChild(option);
    });
    select.value = currentValue;
  };
  window.filterByDate = function() {
    const monthSelect = document.getElementById("filterOrganization");
    const yearSelect = document.getElementById("filterSurvey");
    const month = monthSelect.value;
    const year = yearSelect.value;
    const cards = document.querySelectorAll(".survey-card");
    let visibleCount = 0;
    cards.forEach((card) => {
      const dateElement = card.querySelector(".dates");
      if (!dateElement) {
        card.style.display = "none";
        return;
      }
      const dateText = dateElement.textContent.trim();
      const match = dateText.match(/(\d{2})\.(\d{2})\.(\d{4})/);
      if (!match) {
        card.style.display = "none";
        return;
      }
      const rowDay = match[1];
      const rowMonth = match[2];
      const rowYear = match[3];
      const matchMonth = !month || rowMonth === month;
      const matchYear = !year || rowYear === year;
      if (matchMonth && matchYear) {
        card.style.display = "";
        visibleCount++;
      } else {
        card.style.display = "none";
      }
    });
    const noSurveysElement = document.querySelector(".no-surveys");
    if (noSurveysElement) {
      noSurveysElement.style.display = visibleCount === 0 ? "" : "none";
    }
  };
  function getMonthName(monthNum) {
    const months = {
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
    return months[monthNum] || monthNum;
  }
  window.addEventListener("load", function() {
    window.populateMonthOptions();
    window.populateYearOptions();
    window.filterByDate();
  });
  var CADESCOM_CONTAINER_STORE = 100;
  var CAPICOM_STORE_OPEN_READ_ONLY = 0;
  var CADESCOM_CADES_BES = 1;
  var cadesPluginLoadPromise = null;
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
    if (typeof window.cadesplugin !== "undefined") {
      await window.cadesplugin;
      return window.cadesplugin;
    }
    if (!cadesPluginLoadPromise) {
      cadesPluginLoadPromise = loadScriptOnce("/js/cadesplugin_api.js").then(async () => {
        if (typeof window.cadesplugin === "undefined") {
          throw new Error("CAdESCOM плагин не загружен! Установите КриптоПРО ЭЦП Browser plug-in.");
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
    try {
      await ensureCadesPluginLoaded();
      console.log("1. Плагин обнаружен, версия:", await cadesplugin.version);
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
    if (!response.ok) throw new Error("Ошибка получения данных");
    return await response.text();
  }
  async function showCertificateSelectionDialog(certificates) {
    return new Promise((resolve) => {
      const modal = document.createElement("div");
      modal.className = "csp-modal";
      const content = document.createElement("div");
      content.className = "csp-modal-content";
      const title = document.createElement("h3");
      title.textContent = "Выберите сертификат для подписи";
      content.appendChild(title);
      const body = document.createElement("div");
      body.className = "csp-modal-body";
      const listContainer = document.createElement("div");
      listContainer.className = "cert-list-container";
      const certList = document.createElement("div");
      certList.className = "cert-list";
      certificates.forEach((cert) => {
        const certItem = document.createElement("div");
        certItem.className = "cert-item";
        certItem.dataset.index = String(cert.index);
        const subject = document.createElement("div");
        subject.className = "cert-subject";
        subject.textContent = cert.subject;
        const details = document.createElement("div");
        details.className = "cert-details";
        const issuerRow = document.createElement("div");
        const issuerLabel = document.createElement("strong");
        issuerLabel.textContent = "Издатель:";
        issuerRow.appendChild(issuerLabel);
        issuerRow.appendChild(document.createTextNode(` ${cert.issuer}`));
        const validityRow = document.createElement("div");
        const validityLabel = document.createElement("strong");
        validityLabel.textContent = "Действителен:";
        validityRow.appendChild(validityLabel);
        validityRow.appendChild(
          document.createTextNode(
            ` ${new Date(cert.validFrom).toLocaleDateString()} - ${new Date(cert.validTo).toLocaleDateString()}`
          )
        );
        const thumbprintRow = document.createElement("div");
        const thumbprintLabel = document.createElement("strong");
        thumbprintLabel.textContent = "Отпечаток:";
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
      const footer = document.createElement("div");
      footer.className = "csp-modal-footer";
      const cancelButton = document.createElement("button");
      cancelButton.className = "csp-btn csp-btn-secondary";
      cancelButton.id = "cert-cancel";
      cancelButton.textContent = "Отмена";
      footer.appendChild(cancelButton);
      content.appendChild(footer);
      modal.appendChild(content);
      modal.querySelectorAll(".cert-item").forEach((item) => {
        item.addEventListener("click", () => {
          const index = parseInt(item.getAttribute("data-index"));
          const selectedCert = certificates.find((c) => c.index === index);
          document.body.removeChild(modal);
          resolve(selectedCert);
        });
        item.addEventListener("mouseenter", () => {
          item.style.backgroundColor = "#f0f7ff";
        });
        item.addEventListener("mouseleave", () => {
          item.style.backgroundColor = "";
        });
      });
      modal.querySelector("#cert-cancel").addEventListener("click", () => {
        document.body.removeChild(modal);
        resolve(null);
      });
      document.body.appendChild(modal);
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
      await signedData.propset_Content(data);
      return await signedData.SignCades(signer, CADESCOM_CADES_BES);
    } catch (error) {
      console.error("Ошибка при создании подписи:", error);
      throw error;
    }
  }
  async function sendSignatureToServer(id, organization_id, signature) {
    const response = await fetch(`/signatures/${id}/${organization_id}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ signature })
    });
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || "Ошибка сервера");
    }
  }
  function showCSPInstallInstructions() {
    const modal = document.createElement("div");
    modal.className = "csp-modal";
    const content = document.createElement("div");
    content.className = "csp-modal-content";
    const title = document.createElement("h3");
    title.textContent = "Требуется установка КриптоПРО";
    const body = document.createElement("div");
    body.className = "csp-modal-body";
    const intro = document.createElement("p");
    intro.textContent = "Для подписи документов необходимо:";
    const steps = document.createElement("ol");
    const step1 = document.createElement("li");
    const link1 = document.createElement("a");
    link1.href = "https://www.cryptopro.ru/products/cades/plugin";
    link1.target = "_blank";
    link1.textContent = "КриптоПРО ЭЦП Browser plug-in";
    step1.appendChild(document.createTextNode("Установить "));
    step1.appendChild(link1);
    const step2 = document.createElement("li");
    const link2 = document.createElement("a");
    link2.href = "https://www.cryptopro.ru/products/csp";
    link2.target = "_blank";
    link2.textContent = "КриптоПРО CSP";
    step2.appendChild(document.createTextNode("Установить "));
    step2.appendChild(link2);
    step2.appendChild(document.createTextNode(" (версия 4.0+)"));
    const step3 = document.createElement("li");
    step3.textContent = "Обновить страницу после установки";
    steps.appendChild(step1);
    steps.appendChild(step2);
    steps.appendChild(step3);
    body.appendChild(intro);
    body.appendChild(steps);
    const footer = document.createElement("div");
    footer.className = "csp-modal-footer";
    const closeButton = document.createElement("button");
    closeButton.className = "csp-modal-close";
    closeButton.textContent = "Закрыть";
    footer.appendChild(closeButton);
    content.appendChild(title);
    content.appendChild(body);
    content.appendChild(footer);
    modal.appendChild(content);
    modal.querySelector(".csp-modal-close").addEventListener("click", () => {
      document.body.removeChild(modal);
    });
    document.body.appendChild(modal);
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
    const notification = document.createElement("div");
    notification.className = "csp-notification success";
    const icon = document.createElement("span");
    icon.className = "csp-notification-icon";
    icon.textContent = "✓";
    const text = document.createElement("span");
    text.className = "csp-notification-text";
    text.textContent = "Документ успешно подписан";
    notification.appendChild(icon);
    notification.appendChild(text);
    document.body.appendChild(notification);
    setTimeout(() => {
      notification.classList.add("fade-out");
      setTimeout(() => notification.remove(), 300);
    }, 5e3);
  }
  function showError(message) {
    const notification = document.createElement("div");
    notification.className = "csp-notification error";
    const icon = document.createElement("span");
    icon.className = "csp-notification-icon";
    icon.textContent = "!";
    const text = document.createElement("span");
    text.className = "csp-notification-text";
    text.textContent = message;
    notification.appendChild(icon);
    notification.appendChild(text);
    document.body.appendChild(notification);
    setTimeout(() => {
      notification.classList.add("fade-out");
      setTimeout(() => notification.remove(), 300);
    }, 5e3);
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
      if (typeof checkAnswersCleanup === "function") {
        checkAnswersCleanup();
        checkAnswersCleanup = null;
      }
      host.innerHTML = "";
      if (state.loading) {
        const loadingNode = document.createElement("div");
        loadingNode.className = "loading";
        loadingNode.textContent = "Загрузка анкеты...";
        host.appendChild(loadingNode);
        return;
      }
      if (state.error) {
        const errorNode2 = document.createElement("div");
        errorNode2.className = "error-message";
        errorNode2.textContent = state.error;
        host.appendChild(errorNode2);
        return;
      }
      if (state.submissionState.isSubmitted && !state.submissionState.showResults) {
        const successTemplate = document.getElementById("survey-user-fill-success-template");
        if (successTemplate?.content?.firstElementChild) {
          host.appendChild(successTemplate.content.firstElementChild.cloneNode(true));
        }
        return;
      }
      if (state.submissionState.showResults && state.submissionState.resultsData) {
        const checkContainer = document.createElement("div");
        host.appendChild(checkContainer);
        if (typeof window.mountCheckAnswersView === "function") {
          checkAnswersCleanup = window.mountCheckAnswersView(checkContainer, {
            data: state.submissionState.resultsData,
            userRole,
            onBack
          });
        }
        return;
      }
      const fillTemplate = document.getElementById("survey-user-fill-template");
      const questionTemplate = document.getElementById("survey-user-fill-question-template");
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
        titleNode.textContent = survey.name_survey || "";
      }
      if (descriptionNode) {
        descriptionNode.textContent = survey.description || "Анкета без описания";
      }
      if (errorNode) {
        errorNode.style.display = state.error ? "" : "none";
        errorNode.textContent = state.error || "";
      }
      state.questions.forEach((question) => {
        const questionNode = questionTemplate.content.firstElementChild.cloneNode(true);
        const questionTextNode = questionNode.querySelector('[data-role="question-text"]');
        const ratingsHost = questionNode.querySelector('[data-role="rating-buttons"]');
        const commentWrap = questionNode.querySelector('[data-role="comment-wrap"]');
        const textarea = questionNode.querySelector("textarea");
        const answer = state.answers[question.id] || {};
        if (questionTextNode) {
          questionTextNode.textContent = question.text;
        }
        for (let rating = 1; rating <= 5; rating += 1) {
          const ratingButton = document.createElement("button");
          ratingButton.type = "button";
          ratingButton.className = `btn_crit ${answer.rating === rating ? "active" : ""}`;
          ratingButton.textContent = String(rating);
          ratingButton.addEventListener("click", () => {
            setError(null);
            state.answers = {
              ...state.answers,
              [question.id]: {
                rating,
                comment: rating < 5 ? state.answers[question.id]?.comment || "" : ""
              }
            };
            rerender();
          });
          ratingsHost?.appendChild(ratingButton);
        }
        const showComment = answer.rating > 0 && answer.rating < 5;
        if (commentWrap) {
          commentWrap.style.display = showComment ? "" : "none";
        }
        if (textarea) {
          textarea.value = answer.comment || "";
          textarea.addEventListener("input", (event) => {
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
      submitButton?.addEventListener("click", async () => {
        try {
          setError(null);
          const answersArray = Object.entries(state.answers).map(([questionId, answer]) => ({
            question_id: questionId,
            question_text: state.questions.find((q) => String(q.id) === String(questionId))?.text || "",
            rating: answer.rating,
            comment: answer.comment || ""
          }));
          const response = await fetch("/answers/create", {
            method: "POST",
            headers: {
              "Content-Type": "application/json",
              "X-Requested-With": "XMLHttpRequest"
            },
            body: JSON.stringify({
              organization_id: organizationId,
              id_survey: survey.id_survey,
              answers: answersArray
            })
          });
          if (!response.ok) {
            let errorMessage = "Ошибка при отправке ответов";
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
          }, 2e3);
        } catch (err) {
          setError(err.message);
          rerender();
        }
      });
      cancelButton?.addEventListener("click", () => onBack?.());
      host.appendChild(fillNode);
    };
    const loadQuestions = async () => {
      try {
        const response = await fetch(`/surveys/${survey.id_survey}/organizations/${survey.organization_id}/questions`);
        if (!response.ok) {
          throw new Error("Не удалось загрузить вопросы анкеты");
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
      if (typeof checkAnswersCleanup === "function") {
        checkAnswersCleanup();
      }
      host.innerHTML = "";
    };
  };
  window.mountCheckAnswersView = function mountCheckAnswersView(host, { data }) {
    const template = document.getElementById("survey-user-checkanswers-template");
    if (!host || !template?.content?.firstElementChild) {
      return null;
    }
    host.innerHTML = "";
    const viewNode = template.content.firstElementChild.cloneNode(true);
    const tbody = viewNode.querySelector('[data-role="answers-body"]');
    const signBtn = viewNode.querySelector('[data-role="sign-btn"]');
    const pdfBtn = viewNode.querySelector('[data-role="pdf-btn"]');
    const archiveBtn = viewNode.querySelector('[data-role="archive-btn"]');
    (data?.Answers || []).forEach((answer) => {
      const row = document.createElement("tr");
      const questionCell = document.createElement("td");
      questionCell.textContent = answer.question_text || "";
      const ratingCell = document.createElement("td");
      ratingCell.textContent = String(answer.rating ?? "");
      const commentCell = document.createElement("td");
      commentCell.textContent = answer.comment || "";
      row.appendChild(questionCell);
      row.appendChild(ratingCell);
      row.appendChild(commentCell);
      tbody?.appendChild(row);
    });
    signBtn?.addEventListener("click", () => CSP(data?.Survey?.id_survey, data?.IdOrganization));
    pdfBtn?.addEventListener("click", () => createPdfReport(data?.Survey?.id_survey, data?.IdOrganization));
    archiveBtn?.addEventListener("click", () => downloadSignedArchive(data?.Survey?.id_survey, data?.IdOrganization));
    host.appendChild(viewNode);
    return () => {
      host.innerHTML = "";
    };
  };
  window.createPdfReport = async function(surveyId, organizationId) {
    try {
      const response = await fetch(`/answers/${surveyId}/${organizationId}/pdf`);
      if (!response.ok) throw new Error("Ошибка создания PDF");
      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `Анкета_${surveyId}_${(/* @__PURE__ */ new Date()).toISOString().slice(0, 10)}.pdf`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      window.URL.revokeObjectURL(url);
    } catch (error) {
      console.error("Ошибка при создании PDF:", error);
      showError("Не удалось создать PDF файл");
    }
  };
  window.downloadSignedArchive = async function(surveyId, organizationId) {
    try {
      const loadingIndicator = document.createElement("div");
      loadingIndicator.className = "loading-overlay";
      const loadingContent = document.createElement("div");
      loadingContent.className = "loading-content";
      const spinner = document.createElement("div");
      spinner.className = "loading-spinner";
      const label = document.createElement("p");
      label.textContent = "Подготовка архива...";
      loadingContent.appendChild(spinner);
      loadingContent.appendChild(label);
      loadingIndicator.appendChild(loadingContent);
      document.body.appendChild(loadingIndicator);
      const response = await fetch(`/answers/${surveyId}/${organizationId}/signed-archive`);
      if (!response.ok) {
        const errorData = await response.json().catch(() => null);
        const errorMessage = errorData?.error || "Ошибка загрузки архива";
        throw new Error(errorMessage);
      }
      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `Анкета_с_подписью_${surveyId}.zip`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      window.URL.revokeObjectURL(url);
    } catch (error) {
      console.error("Ошибка при загрузке архива:", error);
      const errorMessage = error.message || "Не удалось загрузить архив с подписью";
      showError(errorMessage);
      if (error.details) {
        console.error("Детали ошибки:", error.details);
      }
    } finally {
      const overlay = document.querySelector(".loading-overlay");
      if (overlay) {
        document.body.removeChild(overlay);
      }
    }
  };
  window.mountCheckAnswersPage = function mountCheckAnswersPage(host, { survey, organizationId, userRole, onBack }) {
    if (!host) {
      return null;
    }
    let destroyed = false;
    const data = {
      loading: true,
      error: null,
      surveyName: survey.name_survey || "",
      answers: [],
      csp: survey.csp || null
    };
    const render = () => {
      if (destroyed) {
        return;
      }
      host.innerHTML = "";
      if (data.loading) {
        const loadingNode = document.createElement("div");
        loadingNode.className = "loading-container";
        const p = document.createElement("p");
        p.textContent = "Загрузка данных анкеты...";
        loadingNode.appendChild(p);
        host.appendChild(loadingNode);
        return;
      }
      if (data.error) {
        const errorNode = document.createElement("div");
        errorNode.className = "error-container";
        const p = document.createElement("p");
        p.textContent = data.error;
        errorNode.appendChild(p);
        host.appendChild(errorNode);
        return;
      }
      const template = document.getElementById("survey-user-checkanswers-page-template");
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
        surveyName.textContent = data.surveyName || "";
      }
      if (signatureInfo && signatureStatus) {
        signatureInfo.style.display = data.csp ? "" : "none";
        signatureStatus.textContent = data.csp ? "подписано" : "не подписано";
        signatureStatus.classList.toggle("signed", Boolean(data.csp));
        signatureStatus.classList.toggle("not-signed", !data.csp);
      }
      if ((data.answers || []).length === 0) {
        if (emptyMessage) {
          emptyMessage.style.display = "";
        }
        if (answersContent) {
          answersContent.style.display = "none";
        }
      } else {
        if (emptyMessage) {
          emptyMessage.style.display = "none";
        }
        (data.answers || []).forEach((group) => {
          const block = document.createElement("div");
          block.className = "answer-block";
          const date = document.createElement("div");
          date.className = "answer-date";
          const calendar = document.createElement("span");
          calendar.className = "calendar-icon";
          calendar.textContent = "📅";
          date.appendChild(calendar);
          date.appendChild(document.createTextNode(` ${group.date || "Дата не указана"}`));
          const table = document.createElement("table");
          table.className = "answers-table";
          const thead = document.createElement("thead");
          const headerRow = document.createElement("tr");
          const thQuestion = document.createElement("th");
          thQuestion.textContent = "Вопрос";
          const thRating = document.createElement("th");
          thRating.textContent = "Оценка";
          const thComment = document.createElement("th");
          thComment.textContent = "Комментарий";
          headerRow.appendChild(thQuestion);
          headerRow.appendChild(thRating);
          headerRow.appendChild(thComment);
          thead.appendChild(headerRow);
          const tbody = document.createElement("tbody");
          (group.answers || []).forEach((answer) => {
            const row = document.createElement("tr");
            const q = document.createElement("td");
            q.setAttribute("data-label", "Вопрос");
            q.textContent = answer.question_text || "";
            const r = document.createElement("td");
            r.setAttribute("data-label", "Оценка");
            r.className = "rating-cell";
            const badge = document.createElement("span");
            badge.className = "rating-badge";
            badge.textContent = String(answer.rating ?? "");
            r.appendChild(badge);
            const c = document.createElement("td");
            c.setAttribute("data-label", "Комментарий");
            c.textContent = answer.comment || "";
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
      pdfBtn?.addEventListener("click", () => createPdfReport(survey.id_survey, organizationId));
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
          throw new Error(result?.error || "Неверный формат ответа");
        }
        data.loading = false;
        data.error = null;
        data.surveyName = result.survey?.name || survey.name_survey || "";
        data.answers = result.answers || [];
        data.csp = result.survey?.csp || null;
        render();
      } catch (error) {
        console.error("Ошибка:", error);
        data.loading = false;
        data.error = error.message;
        data.surveyName = survey.name_survey || "";
        data.answers = [];
        data.csp = null;
        render();
      }
    };
    render();
    fetchSurveyAnswers();
    return () => {
      destroyed = true;
      host.innerHTML = "";
    };
  };

  // Web/wwwroot/js/features/survey/user-survey-list.js
  var mountSurveyFillPage2 = window.mountSurveyFillPage;
  var mountCheckAnswersPage2 = window.mountCheckAnswersPage;
  function normalizeSurveyUserPathname(pathname) {
    if (!pathname) {
      return "/";
    }
    return pathname.length > 1 && pathname.endsWith("/") ? pathname.slice(0, -1) : pathname;
  }
  function buildSurveyUserHistoryEntry(tab) {
    switch (tab) {
      case "active":
        return { tab: "active", url: "/my-surveys" };
      case "archived":
      case "archived_surveys_for_user":
        return { tab: "archived", url: "/my-surveys/archive" };
      case "help":
        return { tab: "help", url: "/help" };
      default:
        return null;
    }
  }
  function getSurveyUserHistoryEntryFromLocation(pathname) {
    const normalizedPath = normalizeSurveyUserPathname(pathname);
    if (normalizedPath === "/my-surveys") {
      return buildSurveyUserHistoryEntry("active");
    }
    if (normalizedPath === "/my-surveys/archive") {
      return buildSurveyUserHistoryEntry("archived");
    }
    if (normalizedPath === "/help") {
      return buildSurveyUserHistoryEntry("help");
    }
    return null;
  }
  function formatDate(dateString) {
    if (!dateString) {
      return "Не указано";
    }
    try {
      const date = new Date(dateString);
      return Number.isNaN(date.getTime()) ? "Не указано" : date.toLocaleDateString("ru-RU");
    } catch {
      return "Не указано";
    }
  }
  function computeTimeLeft(dateClose) {
    if (!dateClose) {
      return "завершено";
    }
    const now = /* @__PURE__ */ new Date();
    const closeDate = new Date(dateClose);
    const diff = closeDate - now;
    if (diff <= 0) {
      return "завершено";
    }
    const days = Math.floor(diff / (1e3 * 60 * 60 * 24));
    const hours = Math.floor(diff % (1e3 * 60 * 60 * 24) / (1e3 * 60 * 60));
    return `${days}д ${hours}ч`;
  }
  window.renderSurveyUserList = function(initialData) {
    const root = document.getElementById("root");
    const pageTemplate = document.getElementById("survey-user-page-template");
    const cardTemplate = document.getElementById("survey-user-card-template");
    const emptyTemplate = document.getElementById("survey-user-empty-template");
    if (!root || !pageTemplate?.content?.firstElementChild || !cardTemplate?.content?.firstElementChild) {
      return;
    }
    const initialTab = initialData.initialTab === "archived" ? "archived" : "active";
    const initialHistory = getSurveyUserHistoryEntryFromLocation(window.location.pathname) || buildSurveyUserHistoryEntry(initialTab) || buildSurveyUserHistoryEntry("active");
    const state = {
      activeTab: initialHistory?.tab || initialTab,
      surveys: initialData.initialSurveys || [],
      currentPage: initialData.initialPage || 1,
      totalPages: initialData.initialTotalPages || 1,
      totalCount: initialData.initialTotalCount || 0,
      activeCount: initialTab === "active" ? initialData.initialTotalCount || 0 : 0,
      archivedCount: initialTab === "archived" ? initialData.initialTotalCount || 0 : 0,
      searchTerm: initialData.initialSearchTerm || "",
      dateFilter: "",
      filterSigned: false,
      loading: false,
      error: null,
      currentView: "survey-list",
      currentSurvey: null
    };
    root.innerHTML = "";
    const page = pageTemplate.content.firstElementChild.cloneNode(true);
    root.appendChild(page);
    const refs = {
      title: page.querySelector('[data-role="title"]'),
      subtitle: page.querySelector('[data-role="subtitle"]'),
      tabActive: page.querySelector('[data-role="tab-active"]'),
      tabArchived: page.querySelector('[data-role="tab-archived"]'),
      activeCount: page.querySelector('[data-role="active-count"]'),
      errorWrap: page.querySelector('[data-role="error"]'),
      errorText: page.querySelector('[data-role="error-text"]'),
      searchForm: page.querySelector('[data-role="search-form"]'),
      searchInput: page.querySelector('[data-role="search-input"]'),
      monthFilter: page.querySelector('[data-role="month-filter"]'),
      yearFilter: page.querySelector('[data-role="year-filter"]'),
      signedWrap: page.querySelector('[data-role="signed-filter-wrap"]'),
      signedInput: page.querySelector('[data-role="signed-filter-input"]'),
      loading: page.querySelector('[data-role="loading"]'),
      grid: page.querySelector('[data-role="survey-grid"]'),
      pagination: page.querySelector('[data-role="pagination"]'),
      prevPage: page.querySelector('[data-role="prev-page"]'),
      nextPage: page.querySelector('[data-role="next-page"]'),
      pageLabel: page.querySelector('[data-role="page-label"]'),
      fillModalHost: page.querySelector('[data-role="fill-modal-host"]'),
      answersModalHost: page.querySelector('[data-role="answers-modal-host"]')
    };
    const modalState = {
      fillCleanup: null,
      answersCleanup: null
    };
    function renderChrome() {
      const headerHost = page.querySelector('[data-component="header"]');
      const navHost = page.querySelector('[data-component="navigation"]');
      const footerHost = page.querySelector('[data-component="footer"]');
      if (headerHost && typeof window.mountHeader === "function") {
        window.mountHeader(headerHost, {
          userRole: initialData.userRole,
          displayName: initialData.displayName,
          userName: initialData.userName,
          organizationName: initialData.organizationName
        });
      }
      if (navHost && typeof window.mountNavigation === "function") {
        window.mountNavigation(navHost, {
          openTab: handleTabChange,
          activeTab: state.activeTab,
          userRole: initialData.userRole,
          userId: initialData.userId
        });
      }
      if (footerHost && typeof window.mountFooter === "function") {
        window.mountFooter(footerHost);
      }
    }
    function cleanupModal(kind) {
      if (kind === "fill" && typeof modalState.fillCleanup === "function") {
        modalState.fillCleanup();
        modalState.fillCleanup = null;
      }
      if (kind === "answers" && typeof modalState.answersCleanup === "function") {
        modalState.answersCleanup();
        modalState.answersCleanup = null;
      }
    }
    function mountModal(host, { title, className, onClose, mountBody }) {
      const template = document.getElementById("survey-user-modal-template");
      if (!host || !template?.content?.firstElementChild) {
        return null;
      }
      host.innerHTML = "";
      const modalNode = template.content.firstElementChild.cloneNode(true);
      if (className) {
        modalNode.classList.add(...String(className).split(" ").filter(Boolean));
      }
      const titleWrap = modalNode.querySelector('[data-role="title-wrap"]');
      const titleNode = modalNode.querySelector('[data-role="title"]');
      const modalContent = modalNode.querySelector(".modal-content");
      const closeButton = modalNode.querySelector('[data-role="close-btn"]');
      const bodyHost = modalNode.querySelector('[data-role="body"]');
      if (title && titleWrap && titleNode) {
        titleWrap.style.display = "";
        titleNode.textContent = title;
      }
      const handleEscape = (event) => {
        if (event.key === "Escape") {
          onClose?.();
        }
      };
      modalNode.addEventListener("click", () => onClose?.());
      modalContent?.addEventListener("click", (event) => event.stopPropagation());
      closeButton?.addEventListener("click", () => onClose?.());
      host.appendChild(modalNode);
      document.body.classList.add("modal-open");
      document.addEventListener("keydown", handleEscape);
      const bodyCleanup = typeof mountBody === "function" && bodyHost ? mountBody(bodyHost) : null;
      return () => {
        if (typeof bodyCleanup === "function") {
          bodyCleanup();
        }
        document.body.classList.remove("modal-open");
        document.removeEventListener("keydown", handleEscape);
        host.innerHTML = "";
      };
    }
    function syncHistory(tab, mode) {
      const entry = buildSurveyUserHistoryEntry(tab);
      if (!entry) {
        return;
      }
      const nextState = { tab: entry.tab };
      if (mode === "replace") {
        window.history.replaceState(nextState, "", entry.url);
        return;
      }
      const currentPath = normalizeSurveyUserPathname(window.location.pathname);
      if (currentPath === entry.url && window.history.state?.tab === nextState.tab) {
        return;
      }
      window.history.pushState(nextState, "", entry.url);
    }
    function filteredSurveys() {
      let result = state.surveys;
      if (state.dateFilter) {
        const filterDate = new Date(state.dateFilter);
        if (!Number.isNaN(filterDate.getTime())) {
          result = result.filter((survey) => {
            if (state.activeTab === "active") {
              const startDate = new Date(survey.date_open);
              const endDate = new Date(survey.date_close);
              endDate.setHours(23, 59, 59, 999);
              return filterDate >= startDate && filterDate <= endDate;
            }
            return survey.completion_date ? new Date(survey.completion_date).toDateString() === filterDate.toDateString() : false;
          });
        }
      }
      if (state.activeTab === "archived" && state.filterSigned) {
        result = result.filter((survey) => survey.csp);
      }
      return result;
    }
    function renderModals() {
      cleanupModal("fill");
      cleanupModal("answers");
      if (state.currentView === "survey-fill" && state.currentSurvey && refs.fillModalHost) {
        modalState.fillCleanup = mountModal(refs.fillModalHost, {
          title: "Активная анкета",
          onClose: handleBackToList,
          mountBody: (modalBodyHost) => typeof mountSurveyFillPage2 === "function" ? mountSurveyFillPage2(modalBodyHost, {
            survey: state.currentSurvey,
            organizationId: initialData.userOrganizationId,
            userRole: initialData.userRole,
            onBack: handleBackToList
          }) : null
        });
      }
      if (state.currentView === "check-answers" && state.currentSurvey && refs.answersModalHost) {
        modalState.answersCleanup = mountModal(refs.answersModalHost, {
          title: "Ответы на анкету",
          onClose: handleBackToList,
          mountBody: (modalBodyHost) => typeof mountCheckAnswersPage2 === "function" ? mountCheckAnswersPage2(modalBodyHost, {
            survey: state.currentSurvey,
            organizationId: initialData.userOrganizationId,
            userRole: initialData.userRole,
            onBack: handleBackToList
          }) : null
        });
      }
    }
    function renderCards() {
      refs.grid.innerHTML = "";
      const list = filteredSurveys();
      if (list.length === 0) {
        if (emptyTemplate?.content?.firstElementChild) {
          refs.grid.appendChild(emptyTemplate.content.firstElementChild.cloneNode(true));
        }
        return;
      }
      list.forEach((survey) => {
        const card = cardTemplate.content.firstElementChild.cloneNode(true);
        card.querySelector('[data-role="name"]').textContent = survey.name_survey || "Без названия";
        card.querySelector('[data-role="description"]').textContent = survey.description || "Нет описания";
        card.querySelector('[data-role="period"]').textContent = `${formatDate(survey.date_open)} - ${formatDate(survey.date_close)}`;
        const completionInfo = card.querySelector('[data-role="completion-info"]');
        const activeInfo = card.querySelector('[data-role="active-info"]');
        const signature = card.querySelector('[data-role="signature"]');
        const signatureStatus = card.querySelector('[data-role="signature-status"]');
        const status = card.querySelector('[data-role="status"]');
        if (state.activeTab === "archived") {
          completionInfo.style.display = "";
          activeInfo.style.display = "none";
          completionInfo.querySelector('[data-role="completion-text"]').textContent = `Заполнено: ${formatDate(survey.completion_date)}`;
          signature.style.display = "";
          signatureStatus.textContent = survey.csp ? "подписано" : "не подписано";
          signatureStatus.classList.toggle("signed", Boolean(survey.csp));
          signatureStatus.classList.toggle("not-signed", !survey.csp);
          status.textContent = "Завершена";
          status.className = "status archived";
        } else {
          completionInfo.style.display = "none";
          activeInfo.style.display = "";
          activeInfo.querySelector(".time-left").textContent = computeTimeLeft(survey.date_close);
          signature.style.display = "none";
          status.textContent = "Активна";
          status.className = "status active";
        }
        card.querySelector('[data-role="card-click"]').addEventListener("click", () => {
          state.currentSurvey = survey;
          state.currentView = state.activeTab === "active" ? "survey-fill" : "check-answers";
          renderModals();
        });
        refs.grid.appendChild(card);
      });
    }
    function render() {
      refs.title.textContent = state.activeTab === "active" ? "Доступные анкеты" : "Пройденные анкеты";
      refs.subtitle.textContent = state.activeTab === "active" ? "Ниже вы можете ознакомиться с доступными вам анкетами" : "Ниже вы можете ознакомиться с пройденными вами анкетами";
      refs.tabActive.classList.toggle("active-tab", state.activeTab === "active");
      refs.tabArchived.classList.toggle("active-tab", state.activeTab === "archived");
      refs.activeCount.textContent = String(state.activeCount);
      refs.errorWrap.style.display = state.error ? "flex" : "none";
      refs.errorText.textContent = state.error || "";
      refs.searchInput.value = state.searchTerm;
      refs.signedWrap.style.display = state.activeTab === "archived" ? "" : "none";
      refs.signedInput.checked = state.filterSigned;
      refs.loading.style.display = state.loading ? "" : "none";
      refs.grid.style.display = state.loading ? "none" : "";
      const showPagination = state.activeTab === "active" && filteredSurveys().length > 0;
      refs.pagination.style.display = showPagination ? "flex" : "none";
      refs.prevPage.disabled = state.currentPage <= 1;
      refs.nextPage.disabled = state.currentPage >= state.totalPages;
      refs.pageLabel.textContent = `Страница ${state.currentPage} из ${state.totalPages}`;
      renderCards();
      renderModals();
    }
    async function loadCounts() {
      try {
        const activeResponse = await fetch("/my-surveys?page=1&searchTerm=", {
          headers: { "X-Requested-With": "XMLHttpRequest" }
        });
        if (activeResponse.ok) {
          const activeData = await activeResponse.json();
          state.activeCount = activeData.totalCount || activeData.accessibleSurveys?.length || 0;
        }
        const archiveResponse = await fetch(`/my-surveys/archive/${initialData.userId}?searchTerm=`, {
          headers: { "X-Requested-With": "XMLHttpRequest" }
        });
        if (archiveResponse.ok) {
          const archiveData = await archiveResponse.json();
          state.archivedCount = archiveData.totalCount || archiveData.accessibleSurveys?.length || 0;
        }
      } catch (error) {
        console.error("Ошибка загрузки счетчиков:", error);
      }
    }
    async function loadSurveyData(tab, pageNumber, search, signedOnly, date) {
      state.loading = true;
      state.error = null;
      render();
      try {
        const endpoint = tab === "active" ? `/my-surveys?page=${pageNumber}&searchTerm=${search}&date=${date}` : `/my-surveys/archive/${initialData.userId}?searchTerm=${search}&signedOnly=${signedOnly}&date=${date}`;
        const response = await fetch(endpoint, { headers: { "X-Requested-With": "XMLHttpRequest" } });
        if (!response.ok) {
          throw new Error("Ошибка загрузки данных анкет");
        }
        const data = await response.json();
        state.surveys = data.accessibleSurveys || [];
        if (tab === "active") {
          state.currentPage = data.currentPage || 1;
          state.totalPages = data.totalPages || 1;
          state.activeCount = data.totalCount || state.activeCount;
        } else {
          state.currentPage = 1;
          state.totalPages = 1;
          state.archivedCount = data.totalCount || state.archivedCount;
        }
      } catch (error) {
        state.error = error.message || "Ошибка загрузки";
      } finally {
        state.loading = false;
        render();
      }
    }
    function handleTabChange(tab, options = {}) {
      if (tab === "help") {
        window.location.href = "/help";
        return;
      }
      const normalized = tab === "archived_surveys_for_user" ? "archived" : tab;
      if (normalized !== "active" && normalized !== "archived") {
        return;
      }
      state.activeTab = normalized;
      state.currentPage = 1;
      state.currentView = "survey-list";
      state.currentSurvey = null;
      if (options.historyMode !== "none") {
        syncHistory(normalized, options.historyMode || "push");
      }
      loadSurveyData(state.activeTab, 1, state.searchTerm, state.filterSigned, state.dateFilter);
    }
    function handleBackToList() {
      state.currentView = "survey-list";
      state.currentSurvey = null;
      renderModals();
    }
    refs.tabActive.addEventListener("click", () => handleTabChange("active"));
    refs.tabArchived.addEventListener("click", () => handleTabChange("archived"));
    refs.searchForm.addEventListener("submit", (event) => {
      event.preventDefault();
      loadSurveyData(state.activeTab, 1, state.searchTerm, state.filterSigned, state.dateFilter);
    });
    refs.searchInput.addEventListener("input", (event) => {
      state.searchTerm = event.target.value;
    });
    refs.signedInput.addEventListener("change", (event) => {
      state.filterSigned = event.target.checked;
      if (state.activeTab === "archived") {
        loadSurveyData("archived", 1, state.searchTerm, state.filterSigned, state.dateFilter);
      }
    });
    refs.monthFilter.addEventListener("click", () => window.populateMonthOptions && window.populateMonthOptions());
    refs.monthFilter.addEventListener("change", () => {
      state.dateFilter = refs.monthFilter.value ? `${(/* @__PURE__ */ new Date()).getFullYear()}-${refs.monthFilter.value}-01` : "";
      loadSurveyData(state.activeTab, state.currentPage, state.searchTerm, state.filterSigned, state.dateFilter);
    });
    refs.yearFilter.addEventListener("click", () => window.populateYearOptions && window.populateYearOptions());
    refs.yearFilter.addEventListener("change", () => window.filterByDate && window.filterByDate());
    refs.prevPage.addEventListener("click", () => {
      if (state.currentPage > 1) {
        loadSurveyData(state.activeTab, state.currentPage - 1, state.searchTerm, state.filterSigned, state.dateFilter);
      }
    });
    refs.nextPage.addEventListener("click", () => {
      if (state.currentPage < state.totalPages) {
        loadSurveyData(state.activeTab, state.currentPage + 1, state.searchTerm, state.filterSigned, state.dateFilter);
      }
    });
    window.addEventListener("popstate", () => {
      const entry = window.history.state?.tab ? buildSurveyUserHistoryEntry(window.history.state.tab) : getSurveyUserHistoryEntryFromLocation(window.location.pathname);
      if (!entry) {
        return;
      }
      handleTabChange(entry.tab, { historyMode: "none" });
    });
    syncHistory(state.activeTab, "replace");
    renderChrome();
    render();
    loadCounts().then(render);
    loadSurveyData(state.activeTab, state.currentPage, state.searchTerm, state.filterSigned, state.dateFilter);
  };
  function getSurveyUserBootstrapData() {
    const bootstrapElement = document.getElementById("survey-user-list-bootstrap") || document.getElementById("user-archive-bootstrap");
    if (!bootstrapElement?.content?.textContent) {
      return null;
    }
    try {
      return JSON.parse(bootstrapElement.content.textContent.trim());
    } catch (error) {
      console.error("Не удалось прочитать bootstrap-данные user survey:", error);
      return null;
    }
  }
  var surveyUserBootstrapData = getSurveyUserBootstrapData();
  if (document.getElementById("root") && surveyUserBootstrapData) {
    window.renderSurveyUserList(surveyUserBootstrapData);
  }
})();
//# sourceMappingURL=survey-user-app.js.map
