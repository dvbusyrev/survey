(() => {
  // Web/wwwroot/js/ui/app-header.js
  function readChromeContextNode(contextNode) {
    if (!contextNode?.dataset) {
      return null;
    }
    return {
      userRole: contextNode.dataset.userRole || "",
      userId: Number(contextNode.dataset.userId || 0),
      displayName: contextNode.dataset.displayName || "",
      userName: contextNode.dataset.userName || "",
      organizationName: contextNode.dataset.organizationName || ""
    };
  }
  function renderHeader(host, { userRole, displayName, userName, organizationName }) {
    const normalizedUserRole = String(userRole || "").trim().toLowerCase();
    const isAdmin = normalizedUserRole === "admin" || normalizedUserRole === "administrator" || normalizedUserRole === "администратор";
    const rawDisplayName = displayName && String(displayName).trim() ? String(displayName).trim() : isAdmin ? "Администратор" : "Клиент";
    const displayNameParts = rawDisplayName.split(":").map((part) => part.trim()).filter(Boolean);
    const normalizedUserName = userName && String(userName).trim() ? String(userName).trim() : displayNameParts.length > 1 ? displayNameParts.slice(1).join(": ").trim() : rawDisplayName;
    const normalizedOrganizationName = organizationName && String(organizationName).trim() ? String(organizationName).trim() : displayNameParts[0] || "Клиент";
    const headerTopLine = normalizedOrganizationName;
    const normalizedDisplayName = isAdmin ? normalizedUserName || "Администратор" : normalizedOrganizationName;
    const template = document.getElementById("header-template");
    if (!host || !template?.content?.firstElementChild) {
      return null;
    }
    host.innerHTML = "";
    const header = template.content.firstElementChild.cloneNode(true);
    const modeLabel = header.querySelector(".header-mode-label");
    const role = header.querySelector(".header-user-name");
    const logoutButton = header.querySelector(".logout-button");
    if (modeLabel) {
      modeLabel.textContent = isAdmin ? headerTopLine : "";
      modeLabel.hidden = !isAdmin;
    }
    if (role) {
      role.textContent = normalizedDisplayName;
      role.setAttribute("title", normalizedDisplayName);
      role.hidden = false;
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
  window.readAppChromeContext = function readAppChromeContext() {
    return readChromeContextNode(document.getElementById("layout-chrome-context")) || readChromeContextNode(document.getElementById("chrome-context")) || null;
  };

  // Web/wwwroot/js/ui/app-navigation.js
  (() => {
    if (window.__appNavigationLoaded) {
      return;
    }
    window.__appNavigationLoaded = true;
    const NAV_SUBMENU_SUPPRESS_STORAGE_KEY = "app-nav-submenu-suppressed";
    const MOBILE_NAV_OPEN_CLASS = "mobile-nav-open";
    const COMPACT_NAVIGATION_CLASS = "compact-nav-mode";
    const NAVIGATION_LAYOUT_SYNC_CLASS = "nav-layout-sync";
    const MOBILE_NAV_MEDIA_QUERY = "(max-width: 900px)";
    const COMPACT_NAVIGATION_BREAKPOINT_PX = 1220;
    let navigationLayoutFrameId = 0;
    let navigationLayoutSyncFrameId = 0;
    let visualViewportResizeHandler = null;
    function isMobileNavigationViewport() {
      return typeof window.matchMedia === "function" ? window.matchMedia(MOBILE_NAV_MEDIA_QUERY).matches || document.body.classList.contains(COMPACT_NAVIGATION_CLASS) : window.innerWidth <= 900;
    }
    function isMobileNavigationOpen() {
      return document.body.classList.contains(MOBILE_NAV_OPEN_CLASS);
    }
    function syncMobileNavigationToggleButtons() {
      const isOpen = isMobileNavigationOpen();
      const isCompact = isMobileNavigationViewport();
      document.querySelectorAll(".header-menu-toggle").forEach((button) => {
        button.setAttribute("aria-expanded", isOpen ? "true" : "false");
        button.setAttribute("aria-label", isOpen ? "Закрыть навигацию" : "Открыть навигацию");
        button.hidden = !isCompact;
      });
    }
    function setMobileNavigationOpen(nextOpen) {
      const shouldOpen = Boolean(nextOpen) && isMobileNavigationViewport();
      document.body.classList.toggle(MOBILE_NAV_OPEN_CLASS, shouldOpen);
      syncMobileNavigationToggleButtons();
    }
    function closeMobileNavigation() {
      setMobileNavigationOpen(false);
    }
    function toggleMobileNavigation() {
      setMobileNavigationOpen(!isMobileNavigationOpen());
    }
    function getViewportWidth() {
      if (window.visualViewport?.width) {
        return window.visualViewport.width;
      }
      return window.innerWidth || document.documentElement.clientWidth || 0;
    }
    function measureCompactNavigationNeed() {
      return getViewportWidth() <= COMPACT_NAVIGATION_BREAKPOINT_PX;
    }
    function syncNavigationLayoutWithoutAnimation() {
      if (!document.body) {
        return;
      }
      document.body.classList.add(NAVIGATION_LAYOUT_SYNC_CLASS);
      if (navigationLayoutSyncFrameId) {
        window.cancelAnimationFrame(navigationLayoutSyncFrameId);
      }
      navigationLayoutSyncFrameId = window.requestAnimationFrame(() => {
        navigationLayoutSyncFrameId = window.requestAnimationFrame(() => {
          navigationLayoutSyncFrameId = 0;
          document.body?.classList.remove(NAVIGATION_LAYOUT_SYNC_CLASS);
        });
      });
    }
    function evaluateNavigationLayout() {
      if (!document.body) {
        return;
      }
      const wasCompact = document.body.classList.contains(COMPACT_NAVIGATION_CLASS);
      const isNarrowViewport = typeof window.matchMedia === "function" ? window.matchMedia(MOBILE_NAV_MEDIA_QUERY).matches : window.innerWidth <= 900;
      if (isNarrowViewport) {
        if (wasCompact) {
          syncNavigationLayoutWithoutAnimation();
        }
        document.body.classList.remove(COMPACT_NAVIGATION_CLASS);
        syncMobileNavigationToggleButtons();
        return;
      }
      if (wasCompact) {
        document.body.classList.remove(COMPACT_NAVIGATION_CLASS);
      }
      const shouldCompact = measureCompactNavigationNeed();
      if (shouldCompact !== wasCompact) {
        syncNavigationLayoutWithoutAnimation();
      }
      document.body.classList.toggle(COMPACT_NAVIGATION_CLASS, shouldCompact);
      if (!shouldCompact && isMobileNavigationOpen()) {
        closeMobileNavigation();
      }
      syncMobileNavigationToggleButtons();
    }
    function queueNavigationLayoutEvaluation() {
      if (navigationLayoutFrameId) {
        window.cancelAnimationFrame(navigationLayoutFrameId);
      }
      navigationLayoutFrameId = window.requestAnimationFrame(() => {
        navigationLayoutFrameId = 0;
        evaluateNavigationLayout();
      });
    }
    function attachViewportObservers(onResize) {
      if (!window.visualViewport) {
        return;
      }
      visualViewportResizeHandler = () => {
        onResize();
      };
      window.visualViewport.addEventListener("resize", visualViewportResizeHandler);
      window.visualViewport.addEventListener("scroll", visualViewportResizeHandler);
    }
    function getNavigationSuppressedTab() {
      try {
        return window.sessionStorage.getItem(NAV_SUBMENU_SUPPRESS_STORAGE_KEY) || "";
      } catch (error) {
        return "";
      }
    }
    function isNavigationSubmenuSuppressed(tab) {
      const suppressedTab = getNavigationSuppressedTab();
      if (!suppressedTab) {
        return false;
      }
      if (!tab) {
        return true;
      }
      return suppressedTab === tab;
    }
    function setNavigationSubmenuSuppressed(tab) {
      try {
        if (tab) {
          window.sessionStorage.setItem(NAV_SUBMENU_SUPPRESS_STORAGE_KEY, String(tab));
          return;
        }
        window.sessionStorage.removeItem(NAV_SUBMENU_SUPPRESS_STORAGE_KEY);
      } catch (error) {
      }
    }
    function closeNavigationSubmenus(root) {
      const scope = root && typeof root.querySelectorAll === "function" ? root : document;
      scope.querySelectorAll(".nav-item.has-submenu.submenu-open").forEach((item) => {
        item.classList.remove("submenu-open");
      });
    }
    function suppressNavigationSubmenus(root, tab) {
      setNavigationSubmenuSuppressed(tab || "");
      closeNavigationSubmenus(root);
    }
    function releaseNavigationSubmenuSuppression() {
      setNavigationSubmenuSuppressed("");
    }
    window.isNavigationSubmenuSuppressed = isNavigationSubmenuSuppressed;
    window.suppressNavigationSubmenus = suppressNavigationSubmenus;
    window.releaseNavigationSubmenuSuppression = releaseNavigationSubmenuSuppression;
    window.closeMobileNavigation = closeMobileNavigation;
    window.toggleMobileNavigation = toggleMobileNavigation;
    window.queueNavigationLayoutEvaluation = queueNavigationLayoutEvaluation;
    function renderNavigation(host, { openTab, activeTab, userRole, userId }) {
      const isAdmin = userRole === "admin";
      const isModifiedNavigationEvent = (event) => event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey;
      const isSurveySectionActive = isAdmin ? ["get_surveys", "add_survey", "list_answers_users", "archived_surveys"].includes(activeTab) : ["active", "archived", "answers_tab", "archived_surveys_for_user"].includes(activeTab);
      const isOrganizationSectionActive = ["get_organization", "organization_surveys", "add_organization", "archive_list_organizations"].includes(activeTab);
      const isEmailSectionActive = ["email", "email_new"].includes(activeTab);
      const isSettingsSectionActive = ["email_settings", "survey_auto_creation"].includes(activeTab);
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
            openTab("get_users", null, { scrollMode: "carry" });
            let attempts = 0;
            const timer = window.setInterval(() => {
              attempts += 1;
              if (tryOpenAddUserModal() || attempts >= 30) {
                window.clearInterval(timer);
              }
            }, 200);
            return;
          }
          window.AppScrollState?.prepareNavigation({ carry: true });
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
            openTab("get_organization", null, { scrollMode: "carry" });
            let attempts = 0;
            const timer = window.setInterval(() => {
              attempts += 1;
              if (tryOpenAddOrganizationModal() || attempts >= 30) {
                window.clearInterval(timer);
              }
            }, 200);
            return;
          }
          window.AppScrollState?.prepareNavigation({ carry: true });
          window.location.href = "/organizations";
          return;
        }
        if (typeof openTab === "function") {
          openTab(tab, null, { scrollMode: "carry" });
          return;
        }
        if (tab === "help") {
          window.open("/help/download", "_blank");
          window.AppScrollState?.prepareNavigation({ carry: true });
          window.location.href = "/help";
          return;
        }
        if (tab === "download_logs") {
          window.location.href = "/event-log/export";
          return;
        }
        if ((tab === "active" || tab === "answers_tab") && userId) {
          window.AppScrollState?.prepareNavigation({ carry: true });
          window.location.href = "/my-surveys";
          return;
        }
        if ((tab === "archived" || tab === "archived_surveys_for_user") && userId) {
          window.AppScrollState?.prepareNavigation({ carry: true });
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
          archived_users: "/users/archive",
          get_organization: "/organizations",
          organization_surveys: "/organizations/surveys",
          archive_list_organizations: "/organizations/archive",
          reports: "/reports",
          survey_auto_creation: "/survey-auto-creation",
          email: "/mail",
          email_new: "/mail",
          email_settings: "/mail/configuration",
          get_logs: "/event-log"
        };
        if (routes[tab]) {
          window.AppScrollState?.prepareNavigation({ carry: true });
          window.location.href = routes[tab];
          return;
        }
        if (tab === "monthly_summary_report") {
          window.AppScrollState?.prepareNavigation({ carry: true });
          window.location.href = "/reports";
          return;
        }
        if (tab.startsWith("quarterly_report_q")) {
          window.AppScrollState?.prepareNavigation({ carry: true });
          window.location.href = "/reports";
        }
      };
      const templateId = isAdmin ? "nav-template-admin" : "nav-template-user";
      const template = document.getElementById(templateId);
      if (!host || !template?.content?.firstElementChild) {
        return null;
      }
      evaluateNavigationLayout();
      host.innerHTML = "";
      const nav = template.content.firstElementChild.cloneNode(true);
      host.appendChild(nav);
      syncMobileNavigationToggleButtons();
      const closeSubmenus = () => closeNavigationSubmenus(nav);
      const closeMobileNavIfNeeded = () => {
        if (isMobileNavigationViewport()) {
          closeMobileNavigation();
        }
      };
      nav.querySelectorAll(".nav-item").forEach((item) => {
        const tab = item.dataset.tab || "";
        const navClass = item.dataset.navClass || "";
        const isActive = navClass === "surveys" ? isSurveySectionActive : navClass === "organizations" ? isOrganizationSectionActive : navClass === "email" ? isEmailSectionActive : navClass === "settings" ? isSettingsSectionActive : tab === activeTab;
        item.classList.toggle("active", isActive);
      });
      nav.querySelectorAll(".submenu-item").forEach((subItem) => {
        subItem.classList.toggle("active", (subItem.dataset.tab || "") === activeTab);
      });
      nav.querySelectorAll(".nav-item.has-submenu").forEach((item) => {
        const itemTab = item.dataset.tab || "";
        const onEnter = () => {
          if (isMobileNavigationViewport()) {
            return;
          }
          if (isNavigationSubmenuSuppressed(itemTab)) {
            releaseNavigationSubmenuSuppression();
            item.classList.remove("submenu-open");
            return;
          }
          if (isNavigationSubmenuSuppressed()) {
            releaseNavigationSubmenuSuppression();
          }
          item.classList.add("submenu-open");
        };
        const onLeave = () => {
          if (isMobileNavigationViewport()) {
            return;
          }
          item.classList.remove("submenu-open");
        };
        item.addEventListener("mouseenter", onEnter);
        item.addEventListener("mouseleave", onLeave);
      });
      const navLeaveHandler = () => {
        closeSubmenus();
        releaseNavigationSubmenuSuppression();
      };
      nav.addEventListener("mouseleave", navLeaveHandler);
      nav.querySelectorAll(".nav-link").forEach((link) => {
        link.addEventListener("click", (event) => {
          if (isModifiedNavigationEvent(event)) {
            closeSubmenus();
            return;
          }
          event.preventDefault();
          const item = event.currentTarget.closest(".nav-item");
          if (!item) {
            return;
          }
          if (item.classList.contains("has-submenu") && item.dataset.disableDirectNav === "true") {
            releaseNavigationSubmenuSuppression();
            const shouldOpen = !item.classList.contains("submenu-open");
            closeSubmenus();
            if (shouldOpen) {
              item.classList.add("submenu-open");
            }
            return;
          }
          if (isMobileNavigationViewport() && item.classList.contains("has-submenu")) {
            const shouldOpen = !item.classList.contains("submenu-open");
            closeSubmenus();
            if (shouldOpen) {
              item.classList.add("submenu-open");
            }
            return;
          }
          suppressNavigationSubmenus(nav, item.classList.contains("has-submenu") ? item.dataset.tab || "" : "");
          closeMobileNavIfNeeded();
          navigate(item.dataset.tab || "");
        });
      });
      nav.querySelectorAll(".submenu-link").forEach((link) => {
        link.addEventListener("click", (event) => {
          if (isModifiedNavigationEvent(event)) {
            closeSubmenus();
            return;
          }
          event.preventDefault();
          const ownerItem = event.currentTarget.closest(".nav-item.has-submenu");
          suppressNavigationSubmenus(nav, ownerItem?.dataset?.tab || "");
          const item = event.currentTarget.closest(".submenu-item");
          closeMobileNavIfNeeded();
          navigate(item?.dataset?.tab || "");
        });
      });
      const menuToggleButton = document.querySelector(".header-menu-toggle");
      const menuToggleHandler = (event) => {
        if (!isMobileNavigationViewport()) {
          return;
        }
        event.preventDefault();
        toggleMobileNavigation();
      };
      if (menuToggleButton) {
        menuToggleButton.addEventListener("click", menuToggleHandler);
      }
      const navOverlayClickHandler = (event) => {
        if (!isMobileNavigationViewport() || event.target !== host) {
          return;
        }
        closeMobileNavigation();
      };
      host.addEventListener("click", navOverlayClickHandler);
      const onEscape = (event) => {
        if (event.key === "Escape") {
          closeMobileNavigation();
        }
      };
      document.addEventListener("keydown", onEscape);
      const onPointerDown = (event) => {
        if (isMobileNavigationViewport()) {
          return;
        }
        if (!event.target.closest(".admin-nav")) {
          closeSubmenus();
          releaseNavigationSubmenuSuppression();
        }
      };
      document.addEventListener("pointerdown", onPointerDown);
      const onResize = () => {
        if (!isMobileNavigationViewport()) {
          closeMobileNavigation();
          closeSubmenus();
        }
        syncMobileNavigationToggleButtons();
        queueNavigationLayoutEvaluation();
      };
      window.addEventListener("resize", onResize);
      attachViewportObservers(onResize);
      return () => {
        if (menuToggleButton) {
          menuToggleButton.removeEventListener("click", menuToggleHandler);
        }
        host.removeEventListener("click", navOverlayClickHandler);
        document.removeEventListener("keydown", onEscape);
        document.removeEventListener("pointerdown", onPointerDown);
        window.removeEventListener("resize", onResize);
        if (visualViewportResizeHandler && window.visualViewport) {
          window.visualViewport.removeEventListener("resize", visualViewportResizeHandler);
          window.visualViewport.removeEventListener("scroll", visualViewportResizeHandler);
          visualViewportResizeHandler = null;
        }
        nav.removeEventListener("mouseleave", navLeaveHandler);
        closeMobileNavigation();
        host.innerHTML = "";
      };
    }
    window.mountNavigation = function mountNavigation(host, props) {
      return renderNavigation(host, props || {});
    };
    window.addEventListener("load", () => {
      queueNavigationLayoutEvaluation();
    });
  })();

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
        message: "CryptoPro установлен, но браузер не смог создать объекты плагина. Проверьте версию КриптоПРО CSP, расширение и перезапустите браузер.",
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
  async function CSP(id, organizationId) {
    try {
      await ensureCadesPluginLoaded();
      await checkCSPAvailable();
      const dataToSign = await getDataForSignature(id, organizationId);
      const signature = await createDigitalSignature(dataToSign);
      await sendSignatureToServer(id, organizationId, signature, dataToSign);
      updateUISuccess();
      if (typeof window.refreshSurveyUserPageData === "function") {
        await window.refreshSurveyUserPageData({ preserveFilters: true });
      }
    } catch (error) {
      console.error("Ошибка в CSP:", error);
      const normalizedError = normalizeCryptoProError(error);
      showError(normalizedError.message);
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
  async function getDataForSignature(id, organizationId) {
    const response = await fetch(`/signatures/${id}/${organizationId}`);
    if (!response.ok) throw new Error("Ошибка получения данных");
    const contentType = String(response.headers.get("content-type") || "").toLowerCase();
    if (contentType.includes("application/json")) {
      return await response.json();
    }
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
  async function sendSignatureToServer(id, organizationId, signature, dataToSign) {
    const request = { signature };
    if (dataToSign && typeof dataToSign === "object") {
      request.signedContent = dataToSign.content || "";
      request.contentEncoding = dataToSign.contentEncoding || "utf8";
      request.detached = Boolean(dataToSign.detached);
    }
    const response = await fetch(`/signatures/${id}/${organizationId}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    });
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || "Ошибка сервера");
    }
  }
  function updateUISuccess() {
    applySignedState(document, true);
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
  window.createAnswerReport = function createAnswerReport(idSurvey, organizationId, type) {
    window.AppScrollState?.prepareNavigation({ carry: true });
    window.location.assign(`/answers/${idSurvey}/${organizationId}/report/${type}`);
  };
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
  function applySignedState(source, isSigned) {
    const page = getAnswersPageContainer(source);
    if (!page) {
      return;
    }
    page.dataset.isSigned = isSigned ? "true" : "false";
    const signatureInfo = page.querySelector('[data-role="signature-info"]');
    const signatureStatus = page.querySelector('[data-role="signature-status"]');
    if (signatureInfo) {
      signatureInfo.classList.toggle("u-hidden", !isSigned);
      signatureInfo.classList.toggle("is-hidden", !isSigned);
    }
    if (signatureStatus) {
      signatureStatus.textContent = isSigned ? "подписано" : "не подписано";
      signatureStatus.classList.toggle("signed", isSigned);
      signatureStatus.classList.toggle("not-signed", !isSigned);
    }
  }
  window.downloadAnswerDocument = function downloadAnswerDocument(surveyId, organizationId, triggerElement) {
    const page = getAnswersPageContainer(triggerElement);
    const isSigned = page?.dataset.isSigned === "true";
    if (isSigned) {
      return window.downloadSignedArchive(surveyId, organizationId);
    }
    return window.createPdfReport(surveyId, organizationId);
  };
  function showError(message) {
    if (typeof window.siteNotify === "function") {
      window.siteNotify(message, "error", { title: "Ошибка" });
      return;
    }
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
  function createHtmlFragment(html) {
    const range = document.createRange();
    range.selectNode(document.body);
    return range.createContextualFragment(html);
  }
  function renderHostError(host, message) {
    const errorNode = document.createElement("div");
    errorNode.className = "error-message";
    errorNode.textContent = message;
    host.replaceChildren(errorNode);
  }
  async function fetchModalContentHtml(url, fallbackMessage) {
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
  window.fetchSurveyFillContentHtml = function fetchSurveyFillContentHtml(surveyId, organizationId) {
    return fetchModalContentHtml(
      `/surveys/${surveyId}/organizations/${organizationId}/fill-content`,
      "Не удалось загрузить анкету"
    );
  };
  window.fetchSurveyAnswersContentHtml = function fetchSurveyAnswersContentHtml(surveyId, organizationId) {
    return fetchModalContentHtml(
      `/answers/${surveyId}/${organizationId}/content`,
      "Не удалось загрузить ответы по анкете"
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
        refs.errorBlock.classList.remove("u-hidden");
        return;
      }
      refs.errorText.textContent = "";
      refs.errorBlock.classList.add("u-hidden");
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
      questionElement.querySelectorAll('[data-role="rating-button"]').forEach((button) => {
        button.addEventListener("click", () => {
          error = null;
          const rating = Number(button.dataset.rating || 0);
          answers[questionId] = {
            ...answers[questionId],
            rating,
            comment: rating < 5 ? answers[questionId]?.comment || "" : ""
          };
          renderError();
          updateQuestionState(questionId, questionElement);
        });
      });
      const commentInput = questionElement.querySelector('[data-role="comment-input"]');
      commentInput?.addEventListener("input", (event) => {
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
          const questionText = questionNode?.querySelector('[data-role="question-title"]')?.textContent?.trim() || "";
          return {
            question_id: questionId,
            question_text: questionText,
            rating: answer.rating,
            comment: answer.comment || ""
          };
        });
        const response = await fetch("/answers/create", {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "X-Requested-With": "XMLHttpRequest"
          },
          body: JSON.stringify({
            id_survey: survey.id_survey,
            id_organization: organizationId,
            answers: payloadAnswers
          })
        });
        if (!response.ok) {
          const errorData = await response.json().catch(() => null);
          throw new Error(errorData?.error || "Ошибка при отправке ответов");
        }
        await response.json().catch(() => null);
        onSubmitted?.({
          survey,
          answers: payloadAnswers,
          organizationId
        });
      } catch (err) {
        error = err?.message || "Не удалось отправить ответы";
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
      refs.submitButton?.addEventListener("click", submitAnswers);
      refs.cancelButton?.addEventListener("click", () => onBack?.());
      getQuestionNodes().forEach(bindQuestion);
      renderError();
      renderSubmitState();
    }
    const loadFillContent = async () => {
      try {
        const html = typeof initialHtml === "string" ? initialHtml : await window.fetchSurveyFillContentHtml(survey.id_survey, organizationId);
        if (destroyed) {
          return;
        }
        host.replaceChildren(createHtmlFragment(html));
        bindPage();
      } catch (err) {
        if (destroyed) {
          return;
        }
        renderHostError(host, err?.message || "Не удалось загрузить анкету");
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
  window.mountCheckAnswersPage = function mountCheckAnswersPage(host, { survey, organizationId, userRole, onBack, initialHtml }) {
    if (!host) {
      return null;
    }
    let destroyed = false;
    function bindPage() {
      const page = host.querySelector('[data-role="survey-answers-page"]');
      const surveyId = Number(page?.dataset.surveyId || survey?.id_survey || survey?.idSurvey || survey?.Id || 0);
      const currentOrganizationId = Number(page?.dataset.organizationId || organizationId || 0);
      const downloadButton = host.querySelector('[data-role="download-btn"]');
      const signButton = host.querySelector('[data-role="sign-actions"] button');
      downloadButton?.addEventListener("click", (event) => {
        event.preventDefault();
        if (surveyId > 0 && currentOrganizationId > 0) {
          window.downloadAnswerDocument(surveyId, currentOrganizationId, downloadButton);
        }
      });
      signButton?.addEventListener("click", (event) => {
        event.preventDefault();
        if (surveyId > 0 && currentOrganizationId > 0) {
          CSP(surveyId, currentOrganizationId);
        }
      });
    }
    const loadAnswersContent = async () => {
      try {
        const html = typeof initialHtml === "string" ? initialHtml : await window.fetchSurveyAnswersContentHtml(survey.id_survey, organizationId);
        if (destroyed) {
          return;
        }
        host.replaceChildren(createHtmlFragment(html));
        bindPage();
      } catch (error) {
        console.error("Ошибка:", error);
        if (destroyed) {
          return;
        }
        renderHostError(host, error?.message || "Не удалось загрузить ответы по анкете");
      }
    };
    loadAnswersContent();
    return () => {
      destroyed = true;
      host.replaceChildren();
    };
  };

  // Web/wwwroot/js/features/survey/survey-admin-date-filter.js
  (function() {
    const existingController = window.__surveyAdminDateFilterController;
    if (existingController && typeof existingController.destroy === "function") {
      existingController.destroy();
    }
    const PAGE_SELECTOR = '.app-page[data-page="surveys-list"], .app-page[data-page="surveys-archive"], .app-page[data-page="answers-list"], .app-page[data-page="user-surveys"]';
    const FILTER_SELECTOR = '[data-role="survey-date-filter"]';
    const ORGANIZATION_FILTER_SELECTOR = '[data-role="survey-organization-filter"]';
    const SURVEY_NAME_FILTER_SELECTOR = '[data-role="survey-name-filter"]';
    const SURVEY_ROW_SELECTOR = "tr[data-survey-date-begin][data-survey-date-end]";
    const MONTH_NAMES = [
      "Январь",
      "Февраль",
      "Март",
      "Апрель",
      "Май",
      "Июнь",
      "Июль",
      "Август",
      "Сентябрь",
      "Октябрь",
      "Ноябрь",
      "Декабрь"
    ];
    const WEEKDAY_NAMES = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"];
    const instances = /* @__PURE__ */ new Map();
    const organizationInstances = /* @__PURE__ */ new Map();
    const surveyNameInstances = /* @__PURE__ */ new Map();
    const serverFilterConfigs = /* @__PURE__ */ new WeakMap();
    let observer = null;
    function pad(value) {
      return String(value).padStart(2, "0");
    }
    function toIso(date) {
      if (!(date instanceof Date) || Number.isNaN(date.getTime())) {
        return "";
      }
      return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
    }
    function parseIso(isoValue) {
      const match = String(isoValue || "").trim().match(/^(\d{4})-(\d{2})-(\d{2})$/);
      if (!match) {
        return null;
      }
      const year = Number.parseInt(match[1], 10);
      const month = Number.parseInt(match[2], 10);
      const day = Number.parseInt(match[3], 10);
      const date = new Date(year, month - 1, day);
      if (Number.isNaN(date.getTime()) || date.getFullYear() !== year || date.getMonth() !== month - 1 || date.getDate() !== day) {
        return null;
      }
      return date;
    }
    function shiftMonth(sourceDate, monthOffset) {
      const date = sourceDate instanceof Date ? new Date(sourceDate.getFullYear(), sourceDate.getMonth(), 1) : /* @__PURE__ */ new Date();
      date.setMonth(date.getMonth() + monthOffset);
      return new Date(date.getFullYear(), date.getMonth(), 1);
    }
    function getMonthBounds(year, monthIndex) {
      const startDate = new Date(year, monthIndex, 1);
      const endDate = new Date(year, monthIndex + 1, 0);
      return {
        start: toIso(startDate),
        end: toIso(endDate)
      };
    }
    function getYearBounds(year) {
      return {
        start: `${year}-01-01`,
        end: `${year}-12-31`
      };
    }
    function getDecadeStart(year) {
      return Math.floor(year / 10) * 10;
    }
    function getDisplayDate(isoValue) {
      if (window.AppDate?.toDisplay) {
        return window.AppDate.toDisplay(isoValue);
      }
      const date = parseIso(isoValue);
      if (!date) {
        return "";
      }
      return `${pad(date.getDate())}.${pad(date.getMonth() + 1)}.${date.getFullYear()}`;
    }
    function compareIso(left, right) {
      if (!left || !right) {
        return 0;
      }
      return left === right ? 0 : left > right ? 1 : -1;
    }
    function isIsoWithin(isoValue, startIso, endIso) {
      return Boolean(isoValue) && (!startIso || compareIso(isoValue, startIso) >= 0) && (!endIso || compareIso(isoValue, endIso) <= 0);
    }
    function getRangeDescription(startIso, endIso) {
      if (!startIso || !endIso) {
        return "";
      }
      return `${getDisplayDate(startIso)} - ${getDisplayDate(endIso)}`;
    }
    function getMonthDescription(year, monthIndex) {
      return `${MONTH_NAMES[monthIndex]} ${year}`;
    }
    function getYearDescription(year) {
      return `${year} год`;
    }
    function createElement(tagName, className, textContent) {
      const element = document.createElement(tagName);
      if (className) {
        element.className = className;
      }
      if (textContent !== void 0) {
        element.textContent = textContent;
      }
      return element;
    }
    function ensurePopoverHeader(root) {
      const popover = root.querySelector('[data-role="survey-date-filter-popover"]');
      const modeSwitch = root.querySelector('[data-role="survey-date-filter-mode-switch"]');
      if (!popover || !modeSwitch) {
        return;
      }
      let header = popover.querySelector(".survey-period-filter__header");
      if (!header) {
        header = createElement("div", "survey-period-filter__header");
        popover.insertBefore(header, modeSwitch);
        header.appendChild(modeSwitch);
      }
      if (!modeSwitch.querySelector('[data-role="survey-date-filter-mode"][data-mode="year"]')) {
        const yearModeButton = createElement("button", "survey-period-filter__mode-button", "По году");
        yearModeButton.type = "button";
        yearModeButton.dataset.role = "survey-date-filter-mode";
        yearModeButton.dataset.mode = "year";
        modeSwitch.insertBefore(yearModeButton, modeSwitch.firstChild);
      }
      if (!header.querySelector('[data-role="survey-date-filter-close"]')) {
        const closeButton = createElement("button", "survey-period-filter__close-button modal-close");
        closeButton.type = "button";
        closeButton.dataset.role = "survey-date-filter-close";
        closeButton.setAttribute("aria-label", "Закрыть фильтр");
        const closeIcon = createElement("i", "fas fa-xmark");
        closeIcon.setAttribute("aria-hidden", "true");
        closeButton.appendChild(closeIcon);
        header.appendChild(closeButton);
      }
      if (!popover.querySelector('[data-role="survey-date-filter-year-panel"]')) {
        const yearPanel = createElement("div", "survey-period-filter__panel is-hidden");
        yearPanel.dataset.role = "survey-date-filter-year-panel";
        const panelNav = createElement("div", "survey-period-filter__panel-nav");
        const prevButton = createElement("button", "survey-period-filter__nav-button");
        prevButton.type = "button";
        prevButton.dataset.role = "survey-date-filter-year-range-prev";
        prevButton.setAttribute("aria-label", "Предыдущие годы");
        prevButton.appendChild(createElement("i", "fas fa-chevron-left"));
        prevButton.firstChild?.setAttribute("aria-hidden", "true");
        const title = createElement("span", "survey-period-filter__panel-title");
        title.dataset.role = "survey-date-filter-year-range-label";
        const nextButton = createElement("button", "survey-period-filter__nav-button");
        nextButton.type = "button";
        nextButton.dataset.role = "survey-date-filter-year-range-next";
        nextButton.setAttribute("aria-label", "Следующие годы");
        nextButton.appendChild(createElement("i", "fas fa-chevron-right"));
        nextButton.firstChild?.setAttribute("aria-hidden", "true");
        panelNav.appendChild(prevButton);
        panelNav.appendChild(title);
        panelNav.appendChild(nextButton);
        const yearsContainer = createElement("div", "survey-period-filter__years");
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
    function cleanupDetachedInstances() {
      Array.from(instances.entries()).forEach(([root]) => {
        if (!document.contains(root)) {
          instances.delete(root);
        }
      });
      Array.from(organizationInstances.entries()).forEach(([root]) => {
        if (!document.contains(root)) {
          organizationInstances.delete(root);
        }
      });
      Array.from(surveyNameInstances.entries()).forEach(([root]) => {
        if (!document.contains(root)) {
          surveyNameInstances.delete(root);
        }
      });
    }
    function getPagesFromNode(node) {
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
    function getDataRowsFromPage(page) {
      return Array.from(page?.querySelectorAll(SURVEY_ROW_SELECTOR) || []);
    }
    function getDateInstanceForPage(page) {
      return Array.from(instances.values()).find((instance) => instance.page === page) || null;
    }
    function getOrganizationInstanceForPage(page) {
      return Array.from(organizationInstances.values()).find((instance) => instance.page === page) || null;
    }
    function getSurveyNameInstanceForPage(page) {
      return Array.from(surveyNameInstances.values()).find((instance) => instance.page === page) || null;
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
    function collectAvailableOrganizations(page) {
      return Array.from(new Set(
        getDataRowsFromPage(page).flatMap((row) => parseRowOrganizations(row)).filter(Boolean)
      )).sort((left, right) => left.localeCompare(right, "ru"));
    }
    function getRowSurveyName(row) {
      return String(row?.dataset?.surveyName || "").trim();
    }
    function collectAvailableSurveyNames(page) {
      return Array.from(new Set(
        getDataRowsFromPage(page).map((row) => getRowSurveyName(row)).filter(Boolean)
      )).sort((left, right) => left.localeCompare(right, "ru"));
    }
    function getPageItemLabel(page) {
      return page?.dataset?.filterItemLabel || "анкет";
    }
    function getPageDateSummary(page) {
      return page?.dataset?.filterDateSummary || "у которых дата начала и дата конца попадают";
    }
    function parseIntegerList(values) {
      if (!Array.isArray(values)) {
        return [];
      }
      return values.map((value) => Number.parseInt(String(value), 10)).filter((value, index, array) => Number.isInteger(value) && array.indexOf(value) === index);
    }
    function getServerFilterConfig(page) {
      if (!(page instanceof Element)) {
        return null;
      }
      if (serverFilterConfigs.has(page)) {
        return serverFilterConfigs.get(page);
      }
      const bootstrapNode = page.querySelector('script[data-role="server-filter-bootstrap"]');
      if (!bootstrapNode) {
        serverFilterConfigs.set(page, null);
        return null;
      }
      try {
        const parsed = JSON.parse(bootstrapNode.textContent || "{}");
        const config = {
          basePath: String(parsed?.BasePath || parsed?.basePath || "").trim(),
          enableDateFilter: Boolean(parsed?.EnableDateFilter ?? parsed?.enableDateFilter),
          enableOrganizationFilter: Boolean(parsed?.EnableOrganizationFilter ?? parsed?.enableOrganizationFilter),
          enableSurveyFilter: Boolean(parsed?.EnableSurveyFilter ?? parsed?.enableSurveyFilter),
          organizationOptions: Array.isArray(parsed?.OrganizationOptions ?? parsed?.organizationOptions) ? (parsed.OrganizationOptions ?? parsed.organizationOptions).map((option) => ({
            id: Number.parseInt(String(option?.Id ?? option?.id ?? ""), 10),
            name: String(option?.Name ?? option?.name ?? "").trim()
          })).filter((option) => Number.isInteger(option.id) && option.name) : [],
          selectedOrganizationIds: parseIntegerList(parsed?.SelectedOrganizationIds ?? parsed?.selectedOrganizationIds),
          surveyOptions: Array.isArray(parsed?.SurveyOptions ?? parsed?.surveyOptions) ? (parsed.SurveyOptions ?? parsed.surveyOptions).map((option) => ({
            id: Number.parseInt(String(option?.Id ?? option?.id ?? ""), 10),
            name: String(option?.Name ?? option?.name ?? "").trim()
          })).filter((option) => Number.isInteger(option.id) && option.name) : [],
          selectedSurveyIds: parseIntegerList(parsed?.SelectedSurveyIds ?? parsed?.selectedSurveyIds),
          year: Number.isInteger(parsed?.Year) ? parsed.Year : Number.parseInt(String(parsed?.Year ?? parsed?.year ?? ""), 10),
          month: String(parsed?.Month ?? parsed?.month ?? "").trim(),
          dateFrom: String(parsed?.DateFrom ?? parsed?.dateFrom ?? "").trim(),
          dateTo: String(parsed?.DateTo ?? parsed?.dateTo ?? "").trim()
        };
        if (!Number.isInteger(config.year)) {
          config.year = null;
        }
        serverFilterConfigs.set(page, config);
        return config;
      } catch (error) {
        serverFilterConfigs.set(page, null);
        return null;
      }
    }
    function isServerFilterPage(page) {
      const config = getServerFilterConfig(page);
      return Boolean(config?.basePath);
    }
    function getServerFilterTabName(page) {
      switch (page?.dataset?.page) {
        case "surveys-list":
          return "get_surveys";
        case "surveys-archive":
          return "archived_surveys";
        case "answers-list":
          return "list_answers_users";
        default:
          return "";
      }
    }
    function getSelectedOptionNames(options, selectedIds) {
      const selectedIdSet = new Set(parseIntegerList(selectedIds));
      return options.filter((option) => selectedIdSet.has(option.id)).map((option) => option.name).sort((left, right) => left.localeCompare(right, "ru"));
    }
    function buildServerFilterUrl(page) {
      const config = getServerFilterConfig(page);
      if (!config?.basePath) {
        return "";
      }
      const currentPath = normalizeCurrentPath(window.location.pathname);
      const basePath = normalizeCurrentPath(config.basePath);
      const params = currentPath === basePath ? new URLSearchParams(window.location.search) : new URLSearchParams();
      ["page", "organizationIds", "surveyIds", "year", "month", "dateFrom", "dateTo"].forEach((key) => {
        params.delete(key);
      });
      if (config.selectedOrganizationIds.length > 0) {
        params.set("organizationIds", config.selectedOrganizationIds.join(","));
      }
      if (config.selectedSurveyIds.length > 0) {
        params.set("surveyIds", config.selectedSurveyIds.join(","));
      }
      if (Number.isInteger(config.year)) {
        params.set("year", String(config.year));
      } else if (config.month) {
        params.set("month", config.month);
      } else {
        if (config.dateFrom) {
          params.set("dateFrom", config.dateFrom);
        }
        if (config.dateTo) {
          params.set("dateTo", config.dateTo);
        }
      }
      const queryString = params.toString();
      return queryString ? `${config.basePath}?${queryString}` : config.basePath;
    }
    function normalizeCurrentPath(pathname) {
      if (!pathname) {
        return "/";
      }
      return pathname.length > 1 && pathname.endsWith("/") ? pathname.slice(0, -1) : pathname;
    }
    function navigateServerFilterPage(page) {
      const url = buildServerFilterUrl(page);
      if (!url) {
        return;
      }
      const config = getServerFilterConfig(page);
      const queryIndex = url.indexOf("?");
      const queryString = queryIndex >= 0 ? url.slice(queryIndex + 1) : "";
      const tabName = getServerFilterTabName(page);
      const scrollTargetSelector = page?.dataset?.tableScrollTarget || "";
      if (typeof window.refreshAdminTab === "function" && tabName) {
        window.refreshAdminTab(tabName, queryString || null, {
          scrollTargetSelector
        });
        return;
      }
      window.location.assign(url);
    }
    function syncServerDateFilterState(instance) {
      const config = getServerFilterConfig(instance?.page);
      if (!config) {
        return;
      }
      config.year = null;
      config.month = "";
      config.dateFrom = "";
      config.dateTo = "";
      if (instance.state.activeFilterType === "year" && Number.isInteger(instance.state.activeYear)) {
        config.year = instance.state.activeYear;
        return;
      }
      if (instance.state.activeFilterType === "month" && instance.state.activeMonth) {
        config.month = `${instance.state.activeMonth.year}-${pad(instance.state.activeMonth.monthIndex + 1)}`;
        return;
      }
      if (instance.state.activeFilterType === "range" && instance.state.rangeStart && instance.state.rangeEnd) {
        config.dateFrom = instance.state.rangeStart;
        config.dateTo = instance.state.rangeEnd;
      }
    }
    function getInitialDateState(page, today) {
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
      const config = getServerFilterConfig(page);
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
    function shouldHideCountSummary(page) {
      return page?.dataset?.filterHideCountSummary === "true";
    }
    function getCombinedVisibleCount(rows) {
      return rows.filter((row) => !row.classList.contains("is-hidden-by-date") && !row.classList.contains("is-hidden-by-organization") && !row.classList.contains("is-hidden-by-survey-name")).length;
    }
    function syncEmptyRow(page, rows, visibleCount) {
      const emptyRow = page?.querySelector('[data-role="survey-filter-empty-row"]');
      if (emptyRow) {
        emptyRow.classList.toggle("is-hidden", rows.length === 0 || visibleCount > 0);
      }
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
    function getDataRows(instance) {
      return Array.from(instance.refs.tableBody?.querySelectorAll(SURVEY_ROW_SELECTOR) || []);
    }
    function getOrganizationFilterLabel(selectedOrganizations) {
      if (!Array.isArray(selectedOrganizations) || selectedOrganizations.length === 0) {
        return "Фильтр по организациям";
      }
      if (selectedOrganizations.length === 1) {
        return selectedOrganizations[0];
      }
      return `Организаций: ${selectedOrganizations.length}`;
    }
    function getSurveyNameFilterLabel(selectedSurveyNames) {
      if (!Array.isArray(selectedSurveyNames) || selectedSurveyNames.length === 0) {
        return "Фильтр по анкетам";
      }
      if (selectedSurveyNames.length === 1) {
        return selectedSurveyNames[0];
      }
      return `Анкет: ${selectedSurveyNames.length}`;
    }
    function updateFilterSummary(instance, visibleCount, totalCount) {
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
    function updateOrganizationFilterSummary(instance, visibleCount, totalCount) {
      const selectedOrganizations = instance.state.serverMode ? getSelectedOptionNames(instance.state.availableOrganizationOptions, instance.state.selectedOrganizationIds) : instance.state.selectedOrganizations;
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
    function updateSurveyNameFilterSummary(instance, visibleCount, totalCount) {
      const selectedSurveyNames = instance.state.serverMode ? getSelectedOptionNames(instance.state.availableSurveyOptions, instance.state.selectedSurveyIds) : instance.state.selectedSurveyNames;
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
    function updatePageSummaries(page, visibleCount, totalCount) {
      const dateInstance = getDateInstanceForPage(page);
      const organizationInstance = getOrganizationInstanceForPage(page);
      const surveyNameInstance = getSurveyNameInstanceForPage(page);
      if (dateInstance) {
        updateFilterSummary(dateInstance, visibleCount, totalCount);
      }
      if (organizationInstance) {
        updateOrganizationFilterSummary(organizationInstance, visibleCount, totalCount);
      }
      if (surveyNameInstance) {
        updateSurveyNameFilterSummary(surveyNameInstance, visibleCount, totalCount);
      }
    }
    function applyPageFilters(page) {
      const rows = getDataRowsFromPage(page);
      if (isServerFilterPage(page)) {
        updatePageSummaries(
          page,
          rows.length,
          Number.parseInt(String(page?.dataset?.totalCount || rows.length), 10) || rows.length
        );
        syncEmptyRow(page, rows, rows.length);
        return;
      }
      const totalCount = rows.length;
      const dateInstance = getDateInstanceForPage(page);
      const organizationInstance = getOrganizationInstanceForPage(page);
      const surveyNameInstance = getSurveyNameInstanceForPage(page);
      const bounds = dateInstance ? getActiveFilterBounds(dateInstance.state) : null;
      const selectedOrganizations = organizationInstance?.state?.selectedOrganizations || [];
      const selectedSurveyNames = surveyNameInstance?.state?.selectedSurveyNames || [];
      rows.forEach((row) => {
        const beginIso = row.dataset.surveyDateBegin || "";
        const endIso = row.dataset.surveyDateEnd || "";
        const matchesDate = !bounds || isIsoWithin(beginIso, bounds.start, bounds.end) && isIsoWithin(endIso, bounds.start, bounds.end);
        const rowOrganizations = parseRowOrganizations(row);
        const matchesOrganizations = selectedOrganizations.length === 0 || rowOrganizations.some((name) => selectedOrganizations.includes(name));
        const rowSurveyName = getRowSurveyName(row);
        const matchesSurveyName = selectedSurveyNames.length === 0 || selectedSurveyNames.includes(rowSurveyName);
        row.classList.remove("is-hidden");
        row.classList.toggle("is-hidden-by-date", !matchesDate);
        row.classList.toggle("is-hidden-by-organization", !matchesOrganizations);
        row.classList.toggle("is-hidden-by-survey-name", !matchesSurveyName);
      });
      const visibleCount = getCombinedVisibleCount(rows);
      syncEmptyRow(page, rows, visibleCount);
      updatePageSummaries(page, visibleCount, totalCount);
    }
    function applyFilter(instance) {
      applyPageFilters(instance.page);
    }
    function updateCheckboxListHeight(container) {
      const list = container?.querySelector(".app-checkbox-list");
      if (!list) {
        return;
      }
      const listTop = list.getBoundingClientRect().top;
      const availableHeight = Math.max(160, window.innerHeight - listTop - 24);
      list.style.setProperty("--app-checkbox-list-max-height", `${availableHeight}px`);
    }
    function scheduleCheckboxListHeightUpdate(container) {
      window.requestAnimationFrame(() => updateCheckboxListHeight(container));
    }
    function setPopoverOpen(instance, isOpen) {
      instance.state.isOpen = Boolean(isOpen);
      instance.refs.trigger.setAttribute("aria-expanded", instance.state.isOpen ? "true" : "false");
      instance.refs.popover.classList.toggle("is-hidden", !instance.state.isOpen);
      if (instance.state.isOpen) {
        scheduleCheckboxListHeightUpdate(instance.refs.popover);
      }
    }
    function closeAllPopovers(exceptRoot = null) {
      cleanupDetachedInstances();
      instances.forEach((instance, root) => {
        if (root === exceptRoot) {
          return;
        }
        setPopoverOpen(instance, false);
      });
      organizationInstances.forEach((instance, root) => {
        if (root === exceptRoot) {
          return;
        }
        setPopoverOpen(instance, false);
      });
      surveyNameInstances.forEach((instance, root) => {
        if (root === exceptRoot) {
          return;
        }
        setPopoverOpen(instance, false);
      });
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
        const yearButton = createElement("button", "survey-period-filter__year-button", String(year));
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
        const monthButton = createElement("button", "survey-period-filter__month-button", monthName);
        monthButton.type = "button";
        monthButton.dataset.role = "survey-date-filter-month";
        monthButton.dataset.monthIndex = String(monthIndex);
        const isSelected = state.activeFilterType === "month" && state.activeMonth && state.activeMonth.year === state.monthViewYear && state.activeMonth.monthIndex === monthIndex;
        monthButton.classList.toggle("is-selected", isSelected);
        refs.monthsContainer.appendChild(monthButton);
      });
    }
    function buildWeekdayRow() {
      const weekdaysRow = createElement("div", "survey-period-filter__weekday-row");
      WEEKDAY_NAMES.forEach((weekday) => {
        weekdaysRow.appendChild(createElement("span", "survey-period-filter__weekday", weekday));
      });
      return weekdaysRow;
    }
    function buildDayButton(instance, isoValue, displayState) {
      const dayButton = createElement("button", "survey-period-filter__day-button");
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
    function buildCalendarCard(instance, monthDate, displayState) {
      const card = createElement("div", "survey-period-filter__calendar-card");
      const title = createElement(
        "h4",
        "survey-period-filter__calendar-title",
        getMonthDescription(monthDate.getFullYear(), monthDate.getMonth())
      );
      const weekdaysRow = buildWeekdayRow();
      const daysGrid = createElement("div", "survey-period-filter__days-grid");
      const firstDayIndex = (new Date(monthDate.getFullYear(), monthDate.getMonth(), 1).getDay() + 6) % 7;
      const daysInMonth = new Date(monthDate.getFullYear(), monthDate.getMonth() + 1, 0).getDate();
      for (let index = 0; index < firstDayIndex; index += 1) {
        daysGrid.appendChild(createElement("span", "survey-period-filter__day-placeholder"));
      }
      for (let day = 1; day <= daysInMonth; day += 1) {
        const isoValue = toIso(new Date(monthDate.getFullYear(), monthDate.getMonth(), day));
        daysGrid.appendChild(buildDayButton(instance, isoValue, displayState));
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
      refs.calendars.appendChild(buildCalendarCard(instance, firstMonth, displayState));
      refs.calendars.appendChild(buildCalendarCard(instance, secondMonth, displayState));
      if (state.rangeStart && !state.rangeEnd) {
        if (refs.hint) {
          refs.hint.textContent = `Начало диапазона: ${getDisplayDate(state.rangeStart)}. Выберите конечную дату.`;
        }
        return;
      }
      if (state.activeFilterType === "range" && state.rangeStart && state.rangeEnd) {
        if (refs.hint) {
          refs.hint.textContent = shouldHideCountSummary(instance.page) ? "" : `Выбран диапазон: ${getRangeDescription(state.rangeStart, state.rangeEnd)}.`;
        }
        return;
      }
      if (refs.hint) {
        refs.hint.textContent = "Выберите начальную и конечную дату периода.";
      }
    }
    function renderOrganizationPanel(instance) {
      const { state, refs } = instance;
      refs.options.textContent = "";
      const hasOptions = state.serverMode ? state.availableOrganizationOptions.length > 0 : state.availableOrganizations.length > 0;
      if (!hasOptions) {
        refs.options.appendChild(
          createElement("p", "app-checkbox-empty", "Организации для фильтрации не найдены.")
        );
        return;
      }
      const options = state.serverMode ? state.availableOrganizationOptions : state.availableOrganizations;
      options.forEach((option) => {
        const organizationId = state.serverMode ? option.id : null;
        const organizationName = state.serverMode ? option.name : option;
        const optionLabel = createElement("label", "app-checkbox-option");
        const checkbox = createElement("input", "app-checkbox-input");
        const labelText = createElement("span", "app-checkbox-text", organizationName);
        const isSelected = state.serverMode ? state.selectedOrganizationIds.includes(organizationId) : state.selectedOrganizations.includes(organizationName);
        optionLabel.classList.toggle("is-selected", isSelected);
        checkbox.type = "checkbox";
        checkbox.dataset.role = "survey-organization-filter-option";
        checkbox.dataset.organizationName = organizationName;
        if (state.serverMode) {
          checkbox.dataset.organizationId = String(organizationId);
        }
        checkbox.checked = isSelected;
        optionLabel.appendChild(checkbox);
        optionLabel.appendChild(labelText);
        refs.options.appendChild(optionLabel);
      });
    }
    function renderSurveyNamePanel(instance) {
      const { state, refs } = instance;
      refs.options.textContent = "";
      const hasOptions = state.serverMode ? state.availableSurveyOptions.length > 0 : state.availableSurveyNames.length > 0;
      if (!hasOptions) {
        refs.options.appendChild(
          createElement("p", "app-checkbox-empty", "Анкеты для фильтрации не найдены.")
        );
        return;
      }
      const options = state.serverMode ? state.availableSurveyOptions : state.availableSurveyNames;
      options.forEach((option) => {
        const surveyId = state.serverMode ? option.id : null;
        const surveyName = state.serverMode ? option.name : option;
        const optionLabel = createElement("label", "app-checkbox-option");
        const checkbox = createElement("input", "app-checkbox-input");
        const labelText = createElement("span", "app-checkbox-text", surveyName);
        const isSelected = state.serverMode ? state.selectedSurveyIds.includes(surveyId) : state.selectedSurveyNames.includes(surveyName);
        optionLabel.classList.toggle("is-selected", isSelected);
        checkbox.type = "checkbox";
        checkbox.dataset.role = "survey-name-filter-option";
        checkbox.dataset.surveyName = surveyName;
        if (state.serverMode) {
          checkbox.dataset.surveyId = String(surveyId);
        }
        checkbox.checked = isSelected;
        optionLabel.appendChild(checkbox);
        optionLabel.appendChild(labelText);
        refs.options.appendChild(optionLabel);
      });
    }
    function render(instance) {
      renderModeSwitch(instance);
      renderYearPanel(instance);
      renderMonthPanel(instance);
      renderRangePanel(instance);
    }
    function clearFilter(instance) {
      instance.state.activeFilterType = "all";
      instance.state.activeYear = null;
      instance.state.activeMonth = null;
      instance.state.rangeStart = "";
      instance.state.rangeEnd = "";
      render(instance);
      if (isServerFilterPage(instance.page)) {
        syncServerDateFilterState(instance);
        navigateServerFilterPage(instance.page);
        return;
      }
      applyFilter(instance);
    }
    function applyYearFilter(instance, year) {
      const { state } = instance;
      const isSameYear = state.activeFilterType === "year" && state.activeYear === year;
      if (isSameYear) {
        clearFilter(instance);
        return;
      }
      state.activeFilterType = "year";
      state.activeYear = year;
      state.monthViewYear = year;
      state.yearViewStart = getDecadeStart(year);
      render(instance);
      if (isServerFilterPage(instance.page)) {
        syncServerDateFilterState(instance);
        navigateServerFilterPage(instance.page);
        return;
      }
      applyFilter(instance);
    }
    function applyMonthFilter(instance, monthIndex) {
      const { state } = instance;
      const isSameMonth = state.activeFilterType === "month" && state.activeMonth && state.activeMonth.year === state.monthViewYear && state.activeMonth.monthIndex === monthIndex;
      if (isSameMonth) {
        clearFilter(instance);
        return;
      }
      state.activeFilterType = "month";
      state.activeYear = null;
      state.activeMonth = {
        year: state.monthViewYear,
        monthIndex
      };
      render(instance);
      if (isServerFilterPage(instance.page)) {
        syncServerDateFilterState(instance);
        navigateServerFilterPage(instance.page);
        return;
      }
      applyFilter(instance);
    }
    function handleRangeSelection(instance, isoValue) {
      const { state } = instance;
      if (!state.rangeStart || state.rangeEnd) {
        state.rangeStart = isoValue;
        state.rangeEnd = "";
        state.activeFilterType = "all";
        render(instance);
        if (isServerFilterPage(instance.page)) {
          return;
        }
        applyFilter(instance);
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
      if (isServerFilterPage(instance.page)) {
        syncServerDateFilterState(instance);
        navigateServerFilterPage(instance.page);
        return;
      }
      applyFilter(instance);
    }
    function renderOrganization(instance) {
      renderOrganizationPanel(instance);
    }
    function renderSurveyName(instance) {
      renderSurveyNamePanel(instance);
    }
    function toggleOrganizationSelection(instance, organizationName, isSelected) {
      const normalizedName = String(organizationName || "").trim();
      if (!normalizedName) {
        return;
      }
      const nextSelectedOrganizations = new Set(instance.state.selectedOrganizations);
      if (isSelected) {
        nextSelectedOrganizations.add(normalizedName);
      } else {
        nextSelectedOrganizations.delete(normalizedName);
      }
      instance.state.selectedOrganizations = Array.from(nextSelectedOrganizations).sort((left, right) => left.localeCompare(right, "ru"));
      renderOrganization(instance);
      applyPageFilters(instance.page);
    }
    function toggleOrganizationIdSelection(instance, organizationId, isSelected) {
      if (!Number.isInteger(organizationId)) {
        return;
      }
      const nextSelectedOrganizationIds = new Set(instance.state.selectedOrganizationIds);
      if (isSelected) {
        nextSelectedOrganizationIds.add(organizationId);
      } else {
        nextSelectedOrganizationIds.delete(organizationId);
      }
      instance.state.selectedOrganizationIds = Array.from(nextSelectedOrganizationIds).sort((left, right) => left - right);
      const config = getServerFilterConfig(instance.page);
      if (config) {
        config.selectedOrganizationIds = [...instance.state.selectedOrganizationIds];
      }
      renderOrganization(instance);
      navigateServerFilterPage(instance.page);
    }
    function toggleSurveyNameSelection(instance, surveyName, isSelected) {
      const normalizedName = String(surveyName || "").trim();
      if (!normalizedName) {
        return;
      }
      const nextSelectedSurveyNames = new Set(instance.state.selectedSurveyNames);
      if (isSelected) {
        nextSelectedSurveyNames.add(normalizedName);
      } else {
        nextSelectedSurveyNames.delete(normalizedName);
      }
      instance.state.selectedSurveyNames = Array.from(nextSelectedSurveyNames).sort((left, right) => left.localeCompare(right, "ru"));
      renderSurveyName(instance);
      applyPageFilters(instance.page);
    }
    function toggleSurveyIdSelection(instance, surveyId, isSelected) {
      if (!Number.isInteger(surveyId)) {
        return;
      }
      const nextSelectedSurveyIds = new Set(instance.state.selectedSurveyIds);
      if (isSelected) {
        nextSelectedSurveyIds.add(surveyId);
      } else {
        nextSelectedSurveyIds.delete(surveyId);
      }
      instance.state.selectedSurveyIds = Array.from(nextSelectedSurveyIds).sort((left, right) => left - right);
      const config = getServerFilterConfig(instance.page);
      if (config) {
        config.selectedSurveyIds = [...instance.state.selectedSurveyIds];
      }
      renderSurveyName(instance);
      navigateServerFilterPage(instance.page);
    }
    function bindInstance(root) {
      if (!(root instanceof Element) || instances.has(root)) {
        return;
      }
      ensurePopoverHeader(root);
      const page = root.closest(PAGE_SELECTOR);
      const tableBody = page?.querySelector('[data-role="main-table"] tbody');
      if (!page || !tableBody) {
        return;
      }
      const today = /* @__PURE__ */ new Date();
      const instance = {
        root,
        page,
        state: getInitialDateState(page, today),
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
          clearButton: root.querySelector('[data-role="survey-date-filter-clear"]'),
          tableBody,
          emptyRow: page.querySelector('[data-role="survey-filter-empty-row"]')
        },
        handlers: {}
      };
      instance.handlers.click = function(event) {
        event.stopPropagation();
        const trigger = event.target.closest('[data-role="survey-date-filter-trigger"]');
        if (trigger && root.contains(trigger)) {
          event.preventDefault();
          const shouldOpen = !instance.state.isOpen;
          closeAllPopovers(shouldOpen ? root : null);
          setPopoverOpen(instance, shouldOpen);
          return;
        }
        const modeButton = event.target.closest('[data-role="survey-date-filter-mode"]');
        if (modeButton && root.contains(modeButton)) {
          event.preventDefault();
          instance.state.mode = ["year", "range"].includes(modeButton.dataset.mode) ? modeButton.dataset.mode : "month";
          render(instance);
          return;
        }
        if (event.target.closest('[data-role="survey-date-filter-year-range-prev"]')) {
          event.preventDefault();
          instance.state.yearViewStart -= 10;
          render(instance);
          return;
        }
        if (event.target.closest('[data-role="survey-date-filter-year-range-next"]')) {
          event.preventDefault();
          instance.state.yearViewStart += 10;
          render(instance);
          return;
        }
        if (event.target.closest('[data-role="survey-date-filter-year-prev"]')) {
          event.preventDefault();
          instance.state.monthViewYear -= 1;
          render(instance);
          return;
        }
        if (event.target.closest('[data-role="survey-date-filter-year-next"]')) {
          event.preventDefault();
          instance.state.monthViewYear += 1;
          render(instance);
          return;
        }
        if (event.target.closest('[data-role="survey-date-filter-range-prev"]')) {
          event.preventDefault();
          instance.state.rangeViewDate = shiftMonth(instance.state.rangeViewDate, -1);
          render(instance);
          return;
        }
        if (event.target.closest('[data-role="survey-date-filter-range-next"]')) {
          event.preventDefault();
          instance.state.rangeViewDate = shiftMonth(instance.state.rangeViewDate, 1);
          render(instance);
          return;
        }
        if (event.target.closest('[data-role="survey-date-filter-close"]')) {
          event.preventDefault();
          setPopoverOpen(instance, false);
          return;
        }
        const yearButton = event.target.closest('[data-role="survey-date-filter-year"]');
        if (yearButton && root.contains(yearButton)) {
          event.preventDefault();
          const selectedYear = Number.parseInt(yearButton.dataset.year || "", 10);
          if (Number.isInteger(selectedYear)) {
            applyYearFilter(instance, selectedYear);
          }
          return;
        }
        const monthButton = event.target.closest('[data-role="survey-date-filter-month"]');
        if (monthButton && root.contains(monthButton)) {
          event.preventDefault();
          const monthIndex = Number.parseInt(monthButton.dataset.monthIndex || "", 10);
          if (Number.isInteger(monthIndex) && monthIndex >= 0 && monthIndex < 12) {
            applyMonthFilter(instance, monthIndex);
          }
          return;
        }
        const dayButton = event.target.closest('[data-role="survey-date-filter-day"]');
        if (dayButton && root.contains(dayButton)) {
          event.preventDefault();
          const isoValue = dayButton.dataset.dateIso || "";
          if (parseIso(isoValue)) {
            handleRangeSelection(instance, isoValue);
          }
          return;
        }
        if (event.target.closest('[data-role="survey-date-filter-clear"]')) {
          event.preventDefault();
          clearFilter(instance);
        }
      };
      root.addEventListener("click", instance.handlers.click);
      instances.set(root, instance);
      render(instance);
      applyFilter(instance);
    }
    function bindOrganizationInstance(root) {
      if (!(root instanceof Element) || organizationInstances.has(root)) {
        return;
      }
      const page = root.closest(PAGE_SELECTOR);
      const tableBody = page?.querySelector('[data-role="main-table"] tbody');
      if (!page || !tableBody) {
        return;
      }
      const instance = {
        root,
        page,
        state: {
          isOpen: false,
          serverMode: isServerFilterPage(page),
          availableOrganizations: collectAvailableOrganizations(page),
          availableOrganizationOptions: getServerFilterConfig(page)?.organizationOptions || [],
          selectedOrganizations: [],
          selectedOrganizationIds: [...getServerFilterConfig(page)?.selectedOrganizationIds || []]
        },
        refs: {
          trigger: root.querySelector('[data-role="survey-organization-filter-trigger"]'),
          label: root.querySelector('[data-role="survey-organization-filter-label"]'),
          popover: root.querySelector('[data-role="survey-organization-filter-popover"]'),
          options: root.querySelector('[data-role="survey-organization-filter-options"]'),
          summary: root.querySelector('[data-role="survey-organization-filter-summary"]'),
          clearButton: root.querySelector('[data-role="survey-organization-filter-clear"]')
        },
        handlers: {}
      };
      instance.handlers.click = function(event) {
        event.stopPropagation();
        const trigger = event.target.closest('[data-role="survey-organization-filter-trigger"]');
        if (trigger && root.contains(trigger)) {
          event.preventDefault();
          const shouldOpen = !instance.state.isOpen;
          closeAllPopovers(shouldOpen ? root : null);
          setPopoverOpen(instance, shouldOpen);
          return;
        }
        if (event.target.closest('[data-role="survey-organization-filter-close"]')) {
          event.preventDefault();
          setPopoverOpen(instance, false);
          return;
        }
        if (event.target.closest('[data-role="survey-organization-filter-clear"]')) {
          event.preventDefault();
          if (instance.state.serverMode) {
            instance.state.selectedOrganizationIds = [];
            const config = getServerFilterConfig(instance.page);
            if (config) {
              config.selectedOrganizationIds = [];
            }
            renderOrganization(instance);
            navigateServerFilterPage(instance.page);
            return;
          }
          instance.state.selectedOrganizations = [];
          renderOrganization(instance);
          applyPageFilters(instance.page);
        }
      };
      instance.handlers.change = function(event) {
        const option = event.target.closest('[data-role="survey-organization-filter-option"]');
        if (!option || !root.contains(option)) {
          return;
        }
        if (instance.state.serverMode) {
          toggleOrganizationIdSelection(
            instance,
            Number.parseInt(option.dataset.organizationId || "", 10),
            Boolean(option.checked)
          );
          return;
        }
        toggleOrganizationSelection(instance, option.dataset.organizationName || "", Boolean(option.checked));
      };
      root.addEventListener("click", instance.handlers.click);
      root.addEventListener("change", instance.handlers.change);
      organizationInstances.set(root, instance);
      renderOrganization(instance);
      applyPageFilters(instance.page);
    }
    function bindSurveyNameInstance(root) {
      if (!(root instanceof Element) || surveyNameInstances.has(root)) {
        return;
      }
      const page = root.closest(PAGE_SELECTOR);
      const tableBody = page?.querySelector('[data-role="main-table"] tbody');
      if (!page || !tableBody) {
        return;
      }
      const instance = {
        root,
        page,
        state: {
          isOpen: false,
          serverMode: isServerFilterPage(page),
          availableSurveyNames: collectAvailableSurveyNames(page),
          availableSurveyOptions: getServerFilterConfig(page)?.surveyOptions || [],
          selectedSurveyNames: [],
          selectedSurveyIds: [...getServerFilterConfig(page)?.selectedSurveyIds || []]
        },
        refs: {
          trigger: root.querySelector('[data-role="survey-name-filter-trigger"]'),
          label: root.querySelector('[data-role="survey-name-filter-label"]'),
          popover: root.querySelector('[data-role="survey-name-filter-popover"]'),
          options: root.querySelector('[data-role="survey-name-filter-options"]'),
          summary: root.querySelector('[data-role="survey-name-filter-summary"]'),
          clearButton: root.querySelector('[data-role="survey-name-filter-clear"]')
        },
        handlers: {}
      };
      instance.handlers.click = function(event) {
        event.stopPropagation();
        const trigger = event.target.closest('[data-role="survey-name-filter-trigger"]');
        if (trigger && root.contains(trigger)) {
          event.preventDefault();
          const shouldOpen = !instance.state.isOpen;
          closeAllPopovers(shouldOpen ? root : null);
          setPopoverOpen(instance, shouldOpen);
          return;
        }
        if (event.target.closest('[data-role="survey-name-filter-close"]')) {
          event.preventDefault();
          setPopoverOpen(instance, false);
          return;
        }
        if (event.target.closest('[data-role="survey-name-filter-clear"]')) {
          event.preventDefault();
          if (instance.state.serverMode) {
            instance.state.selectedSurveyIds = [];
            const config = getServerFilterConfig(instance.page);
            if (config) {
              config.selectedSurveyIds = [];
            }
            renderSurveyName(instance);
            navigateServerFilterPage(instance.page);
            return;
          }
          instance.state.selectedSurveyNames = [];
          renderSurveyName(instance);
          applyPageFilters(instance.page);
        }
      };
      instance.handlers.change = function(event) {
        const option = event.target.closest('[data-role="survey-name-filter-option"]');
        if (!option || !root.contains(option)) {
          return;
        }
        if (instance.state.serverMode) {
          toggleSurveyIdSelection(
            instance,
            Number.parseInt(option.dataset.surveyId || "", 10),
            Boolean(option.checked)
          );
          return;
        }
        toggleSurveyNameSelection(instance, option.dataset.surveyName || "", Boolean(option.checked));
      };
      root.addEventListener("click", instance.handlers.click);
      root.addEventListener("change", instance.handlers.change);
      surveyNameInstances.set(root, instance);
      renderSurveyName(instance);
      applyPageFilters(instance.page);
    }
    function bindAvailablePages(root = document) {
      cleanupDetachedInstances();
      const pages = root === document ? Array.from(document.querySelectorAll(PAGE_SELECTOR)) : getPagesFromNode(root);
      pages.forEach((page) => {
        const dateFilterRoot = page.querySelector(FILTER_SELECTOR);
        if (dateFilterRoot) {
          bindInstance(dateFilterRoot);
        }
        const organizationFilterRoot = page.querySelector(ORGANIZATION_FILTER_SELECTOR);
        if (organizationFilterRoot) {
          bindOrganizationInstance(organizationFilterRoot);
        }
        const surveyNameFilterRoot = page.querySelector(SURVEY_NAME_FILTER_SELECTOR);
        if (surveyNameFilterRoot) {
          bindSurveyNameInstance(surveyNameFilterRoot);
        }
      });
    }
    function handleDocumentClick(event) {
      cleanupDetachedInstances();
      let clickedInsideFilter = false;
      instances.forEach((instance, root) => {
        if (root.contains(event.target)) {
          clickedInsideFilter = true;
        }
      });
      organizationInstances.forEach((instance, root) => {
        if (root.contains(event.target)) {
          clickedInsideFilter = true;
        }
      });
      surveyNameInstances.forEach((instance, root) => {
        if (root.contains(event.target)) {
          clickedInsideFilter = true;
        }
      });
      if (!clickedInsideFilter) {
        closeAllPopovers();
      }
    }
    function handleDocumentKeydown(event) {
      if (event.key === "Escape") {
        closeAllPopovers();
      }
    }
    function destroy() {
      instances.forEach((instance, root) => {
        if (instance.handlers?.click) {
          root.removeEventListener("click", instance.handlers.click);
        }
      });
      instances.clear();
      organizationInstances.forEach((instance, root) => {
        if (instance.handlers?.click) {
          root.removeEventListener("click", instance.handlers.click);
        }
        if (instance.handlers?.change) {
          root.removeEventListener("change", instance.handlers.change);
        }
      });
      organizationInstances.clear();
      surveyNameInstances.forEach((instance, root) => {
        if (instance.handlers?.click) {
          root.removeEventListener("click", instance.handlers.click);
        }
        if (instance.handlers?.change) {
          root.removeEventListener("change", instance.handlers.change);
        }
      });
      surveyNameInstances.clear();
      if (observer) {
        observer.disconnect();
        observer = null;
      }
      serverFilterConfigs.clear?.();
      document.removeEventListener("click", handleDocumentClick);
      document.removeEventListener("keydown", handleDocumentKeydown);
    }
    window.__surveyAdminDateFilterController = {
      destroy
    };
    document.addEventListener("click", handleDocumentClick);
    document.addEventListener("keydown", handleDocumentKeydown);
    if (typeof MutationObserver !== "undefined" && document.body) {
      observer = new MutationObserver((mutations) => {
        mutations.forEach((mutation) => {
          mutation.addedNodes.forEach((node) => {
            bindAvailablePages(node);
          });
        });
      });
      observer.observe(document.body, {
        childList: true,
        subtree: true
      });
    }
    if (document.readyState === "loading") {
      document.addEventListener("DOMContentLoaded", function() {
        bindAvailablePages(document);
      });
      return;
    }
    bindAvailablePages(document);
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
    const activeTab = contentRoot.dataset.activeTab === "archived" ? "archived" : "active";
    const currentPage = Number(contentRoot.dataset.currentPage || 1);
    const totalPages = Number(contentRoot.dataset.totalPages || 1);
    const totalCount = Number(contentRoot.dataset.totalCount || 0);
    const searchTerm = contentRoot.dataset.searchTerm || "";
    const signedOnly = contentRoot.dataset.signedOnly === "true";
    return {
      activeTab,
      currentPage: Number.isFinite(currentPage) && currentPage > 0 ? currentPage : 1,
      totalPages: Number.isFinite(totalPages) && totalPages > 0 ? totalPages : 1,
      totalCount: Number.isFinite(totalCount) && totalCount >= 0 ? totalCount : 0,
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
  function mountSurveyUserModal(host, { mountBody, onClose }) {
    const template = document.getElementById("survey-user-modal-template");
    if (!host || !template?.content?.firstElementChild) {
      return null;
    }
    host.replaceChildren();
    const modalNode = template.content.firstElementChild.cloneNode(true);
    const modalContent = modalNode.querySelector(".modal-content");
    const closeButton = modalNode.querySelector('[data-role="close-btn"]');
    const bodyHost = modalNode.querySelector('[data-role="body"]');
    const handleEscape = (event) => {
      if (event.key === "Escape") {
        onClose?.();
      }
    };
    modalNode.addEventListener("click", () => onClose?.());
    modalContent?.addEventListener("click", (event) => event.stopPropagation());
    closeButton?.addEventListener("click", () => onClose?.());
    const bodyCleanup = typeof mountBody === "function" && bodyHost ? mountBody(bodyHost) : null;
    host.appendChild(modalNode);
    modalNode.classList.add("modal--visible");
    modalNode.setAttribute("aria-hidden", "false");
    if (typeof window.syncSiteModalBodyState === "function") {
      window.syncSiteModalBodyState();
    } else {
      document.body.classList.add("modal-open");
    }
    document.addEventListener("keydown", handleEscape);
    return () => {
      if (typeof bodyCleanup === "function") {
        bodyCleanup();
      }
      document.removeEventListener("keydown", handleEscape);
      host.replaceChildren();
      if (typeof window.syncSiteModalBodyState === "function") {
        window.syncSiteModalBodyState();
      } else {
        document.body.classList.remove("modal-open");
      }
    };
  }

  // Web/wwwroot/js/features/survey/user-survey-list.js
  var mountSurveyFillPage2 = window.mountSurveyFillPage;
  var mountCheckAnswersPage2 = window.mountCheckAnswersPage;
  var fetchSurveyFillContentHtml2 = window.fetchSurveyFillContentHtml;
  var fetchSurveyAnswersContentHtml2 = window.fetchSurveyAnswersContentHtml;
  window.bindSurveyUserListPage = function bindSurveyUserListPage(initialData) {
    const contentHost = document.getElementById("default_content");
    const emptyTemplate = document.getElementById("survey-user-empty-template");
    if (!contentHost) {
      return;
    }
    const initialSnapshot = createSnapshotFromHost(contentHost);
    if (!initialSnapshot) {
      return;
    }
    const state = {
      activeTab: initialSnapshot.activeTab,
      currentView: "survey-list",
      currentSurvey: null,
      currentSnapshot: initialSnapshot,
      loading: false,
      monthFilter: "",
      yearFilter: "",
      tabSnapshots: {
        active: initialSnapshot.activeTab === "active" ? initialSnapshot : createSnapshotFromTemplateElement(document.getElementById("survey-user-active-content-template")),
        archived: initialSnapshot.activeTab === "archived" ? initialSnapshot : createSnapshotFromTemplateElement(document.getElementById("survey-user-archived-content-template"))
      }
    };
    const modalState = {
      fillCleanup: null,
      answersCleanup: null,
      prefetchedHtml: null,
      openRequestId: 0
    };
    let refreshPromise = null;
    function getContentRoot() {
      return contentHost.querySelector('[data-role="survey-user-content"]');
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
    function renderChrome() {
      const headerHost = document.getElementById("chrome-header");
      const navHost = document.getElementById("chrome-navigation");
      const footerHost = document.getElementById("chrome-footer");
      const chromeContext = typeof window.readAppChromeContext === "function" ? window.readAppChromeContext() : null;
      const chromeProps = {
        userRole: chromeContext?.userRole || initialData.userRole,
        displayName: chromeContext?.displayName || initialData.displayName,
        userName: chromeContext?.userName || initialData.userName,
        organizationName: chromeContext?.organizationName || initialData.organizationName
      };
      if (headerHost && typeof window.mountHeader === "function") {
        window.mountHeader(headerHost, chromeProps);
      }
      if (navHost && typeof window.mountNavigation === "function") {
        window.mountNavigation(navHost, {
          openTab: handleTabChange,
          activeTab: state.activeTab === "archived" ? "archived_surveys_for_user" : state.activeTab,
          userRole: chromeProps.userRole,
          userId: chromeContext?.userId || initialData.userId
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
    function renderModals() {
      cleanupModal("fill");
      cleanupModal("answers");
      const fillModalHost = document.querySelector('[data-role="fill-modal-host"]');
      const answersModalHost = document.querySelector('[data-role="answers-modal-host"]');
      if (state.currentView === "survey-fill" && state.currentSurvey && fillModalHost) {
        const initialHtml = modalState.prefetchedHtml;
        modalState.prefetchedHtml = null;
        modalState.fillCleanup = mountSurveyUserModal(fillModalHost, {
          onClose: handleBackToList,
          mountBody: (modalBodyHost) => typeof mountSurveyFillPage2 === "function" ? mountSurveyFillPage2(modalBodyHost, {
            survey: state.currentSurvey,
            organizationId: initialData.userOrganizationId,
            userRole: initialData.userRole,
            initialHtml,
            onBack: handleBackToList,
            onSubmitted: () => handleSurveySubmitted(state.currentSurvey)
          }) : null
        });
      }
      if (state.currentView === "check-answers" && state.currentSurvey && answersModalHost) {
        const initialHtml = modalState.prefetchedHtml;
        modalState.prefetchedHtml = null;
        modalState.answersCleanup = mountSurveyUserModal(answersModalHost, {
          onClose: handleBackToList,
          mountBody: (modalBodyHost) => typeof mountCheckAnswersPage2 === "function" ? mountCheckAnswersPage2(modalBodyHost, {
            survey: state.currentSurvey,
            organizationId: initialData.userOrganizationId,
            userRole: initialData.userRole,
            initialHtml,
            onBack: handleBackToList
          }) : null
        });
      }
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
    function setLoading(isLoading) {
      state.loading = isLoading;
      const refs = getContentRefs();
      refs.loading?.classList.toggle("u-hidden", !isLoading);
      if (refs.tableSection) {
        refs.tableSection.style.display = isLoading ? "none" : "";
      }
    }
    function setError(message) {
      const refs = getContentRefs();
      refs.errorWrap?.classList.toggle("u-hidden", !message);
      if (refs.errorText) {
        refs.errorText.textContent = message || "";
      }
    }
    function populateDateFilters() {
      const refs = getContentRefs();
      const rows = Array.from(contentHost.querySelectorAll('[data-role="user-survey-row"]'));
      const monthOptions = Array.from(new Set(rows.map((row) => row.dataset.filterMonth || "").filter(Boolean))).sort().map((value) => ({ value, label: getMonthLabel(value) }));
      const yearOptions = Array.from(new Set(rows.map((row) => row.dataset.filterYear || "").filter(Boolean))).sort((left, right) => Number(right) - Number(left)).map((value) => ({ value, label: value }));
      state.monthFilter = setSelectOptions(refs.monthFilter, monthOptions, "Все месяцы", state.monthFilter);
      state.yearFilter = setSelectOptions(refs.yearFilter, yearOptions, "Все годы", state.yearFilter);
    }
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
    function mountSnapshot(snapshot, options = {}) {
      if (!snapshot?.template) {
        return;
      }
      contentHost.replaceChildren(snapshot.template.content.cloneNode(true));
      state.currentSnapshot = createSnapshotFromHost(contentHost) || snapshot;
      state.activeTab = state.currentSnapshot.activeTab;
      state.tabSnapshots[state.activeTab] = state.currentSnapshot;
      if (!options.preserveFilters) {
        state.monthFilter = "";
        state.yearFilter = "";
      }
      setLoading(false);
      setError("");
      populateDateFilters();
      applyLocalFilters();
      renderChrome();
      renderModals();
    }
    async function fetchSnapshot(tab, page, searchTerm, signedOnly) {
      const endpoint = tab === "active" ? `/my-surveys?page=${page}&searchTerm=${encodeURIComponent(searchTerm || "")}` : `/my-surveys/archive/${initialData.userId}?page=${page}&searchTerm=${encodeURIComponent(searchTerm || "")}&signedOnly=${signedOnly ? "true" : "false"}`;
      const response = await fetch(endpoint, {
        headers: {
          "X-Requested-With": "XMLHttpRequest"
        }
      });
      if (!response.ok) {
        throw new Error("Ошибка загрузки данных анкет");
      }
      const html = await response.text();
      const snapshot = createSnapshotFromHtml(html);
      if (!snapshot) {
        throw new Error("Не удалось построить содержимое страницы анкет");
      }
      return snapshot;
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
        const snapshot = await fetchSnapshot(tab, page, searchTerm, signedOnly);
        state.tabSnapshots[tab] = snapshot;
        if (options.applyToCurrent !== false && state.activeTab === tab) {
          mountSnapshot(snapshot, { preserveFilters: options.preserveFilters === true });
          if (options.scrollToTableStart === true) {
            scrollToTableSection();
          }
        }
        return snapshot;
      } catch (error) {
        if (state.activeTab === tab) {
          setLoading(false);
          setError(error?.message || "Ошибка загрузки данных анкет");
        } else {
          console.error("Ошибка фонового обновления списка анкет:", error);
        }
        return null;
      }
    }
    async function openSurveyById(surveyId) {
      const survey = state.currentSnapshot.surveys.find((item) => getSurveyId(item) === surveyId);
      if (!survey) {
        return;
      }
      const targetView = state.activeTab === "active" ? "survey-fill" : "check-answers";
      const requestId = modalState.openRequestId + 1;
      modalState.openRequestId = requestId;
      try {
        const prefetchedHtml = targetView === "survey-fill" ? await fetchSurveyFillContentHtml2?.(surveyId, initialData.userOrganizationId) : await fetchSurveyAnswersContentHtml2?.(surveyId, initialData.userOrganizationId);
        if (modalState.openRequestId !== requestId) {
          return;
        }
        modalState.prefetchedHtml = typeof prefetchedHtml === "string" ? prefetchedHtml : null;
        state.currentSurvey = survey;
        state.currentView = targetView;
        renderModals();
      } catch (error) {
        if (modalState.openRequestId !== requestId) {
          return;
        }
        modalState.prefetchedHtml = null;
        setError(error?.message || "Не удалось открыть анкету");
      }
    }
    function handleBackToList() {
      modalState.openRequestId += 1;
      modalState.prefetchedHtml = null;
      state.currentView = "survey-list";
      state.currentSurvey = null;
      renderModals();
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
      const currentSnapshot = state.activeTab === "archived" ? nextArchivedSnapshot : nextActiveSnapshot;
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
      if (tab === "help") {
        window.open("/help/download", "_blank");
        window.location.href = "/help";
        return;
      }
      const normalizedTab = tab === "archived_surveys_for_user" ? "archived" : tab;
      if (normalizedTab !== "active" && normalizedTab !== "archived") {
        return;
      }
      state.activeTab = normalizedTab;
      state.currentView = "survey-list";
      state.currentSurvey = null;
      state.monthFilter = "";
      state.yearFilter = "";
      if (options.historyMode !== "none") {
        syncHistory(normalizedTab, options.historyMode || "push");
      }
      const cachedSnapshot = state.tabSnapshots[normalizedTab];
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
    function handleClick(event) {
      const tabActiveButton = event.target.closest('[data-role="tab-active"]');
      if (tabActiveButton && contentHost.contains(tabActiveButton)) {
        event.preventDefault();
        handleTabChange("active");
        return;
      }
      const tabArchivedButton = event.target.closest('[data-role="tab-archived"]');
      if (tabArchivedButton && contentHost.contains(tabArchivedButton)) {
        event.preventDefault();
        handleTabChange("archived");
        return;
      }
      const actionButton = event.target.closest('[data-role="action"]');
      if (actionButton && contentHost.contains(actionButton)) {
        const surveyId = Number(actionButton.dataset.surveyId || 0);
        if (Number.isFinite(surveyId) && surveyId > 0) {
          openSurveyById(surveyId);
        }
        return;
      }
      const paginationButton = event.target.closest('[data-role="pagination-page"]');
      if (paginationButton && contentHost.contains(paginationButton)) {
        const targetPage = Number(paginationButton.dataset.page || 0);
        if (!Number.isFinite(targetPage) || targetPage <= 0 || targetPage === state.currentSnapshot.currentPage) {
          return;
        }
        event.preventDefault();
        loadTabSnapshot(state.activeTab, {
          page: targetPage,
          searchTerm: state.currentSnapshot.searchTerm,
          signedOnly: state.currentSnapshot.signedOnly,
          scrollToTableStart: true
        });
        return;
      }
    }
    function handleDoubleClick(event) {
      const row = event.target.closest('[data-role="user-survey-row"]');
      if (!row || !contentHost.contains(row) || event.target.closest("button")) {
        return;
      }
      const surveyId = Number(row.dataset.surveyId || 0);
      if (Number.isFinite(surveyId) && surveyId > 0) {
        openSurveyById(surveyId);
      }
    }
    function handleSubmit(event) {
      const searchForm = event.target.closest('[data-role="search-form"]');
      if (!searchForm || !contentHost.contains(searchForm)) {
        return;
      }
      event.preventDefault();
      const searchInput = searchForm.querySelector('[data-role="search-input"]');
      const signedInput = searchForm.querySelector('[data-role="signed-filter-input"]');
      loadTabSnapshot(state.activeTab, {
        page: 1,
        searchTerm: searchInput?.value?.trim() || "",
        signedOnly: Boolean(signedInput?.checked)
      });
    }
    function handleChange(event) {
      const monthFilter = event.target.closest('[data-role="month-filter"]');
      if (monthFilter && contentHost.contains(monthFilter)) {
        state.monthFilter = monthFilter.value;
        applyLocalFilters();
        return;
      }
      const yearFilter = event.target.closest('[data-role="year-filter"]');
      if (yearFilter && contentHost.contains(yearFilter)) {
        state.yearFilter = yearFilter.value;
        applyLocalFilters();
        return;
      }
      const signedInput = event.target.closest('[data-role="signed-filter-input"]');
      if (signedInput && contentHost.contains(signedInput)) {
        loadTabSnapshot("archived", {
          page: 1,
          searchTerm: state.currentSnapshot.searchTerm,
          signedOnly: signedInput.checked
        });
      }
    }
    contentHost.addEventListener("click", handleClick);
    contentHost.addEventListener("dblclick", handleDoubleClick);
    contentHost.addEventListener("submit", handleSubmit);
    contentHost.addEventListener("change", handleChange);
    window.addEventListener("popstate", () => {
      const entry = window.history.state?.tab ? buildSurveyUserHistoryEntry(window.history.state.tab) : getSurveyUserHistoryEntryFromLocation(window.location.pathname);
      if (!entry) {
        return;
      }
      handleTabChange(entry.tab, { historyMode: "none" });
    });
    syncHistory(state.activeTab, "replace");
    mountSnapshot(initialSnapshot);
    window.refreshSurveyUserPageData = function refreshSurveyUserPageData(options = {}) {
      return refreshAllSnapshots({
        preserveFilters: options.preserveFilters !== false
      });
    };
  };
  function getSurveyUserBootstrapData() {
    const bootstrapElement = document.getElementById("survey-user-list-bootstrap") || document.getElementById("user-archive-bootstrap");
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
  var surveyUserBootstrapData = getSurveyUserBootstrapData();
  if (document.querySelector('[data-page="user-surveys"]') && surveyUserBootstrapData) {
    window.bindSurveyUserListPage(surveyUserBootstrapData);
  }
})();
//# sourceMappingURL=survey-user-app.js.map
