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
    header.classList.toggle("app-header--client", !isAdmin);
    const modeLabel = header.querySelector(".header-mode-label");
    const role = header.querySelector(".header-user-name");
    const logoutButton = header.querySelector(".logout-button");
    const menuToggle = header.querySelector(".header-menu-toggle");
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
    if (menuToggle && !isAdmin) {
      menuToggle.hidden = true;
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
    function hasNavigationHost() {
      return Boolean(document.getElementById("chrome-navigation"));
    }
    function syncMobileNavigationToggleButtons() {
      const isOpen = isMobileNavigationOpen();
      const isCompact = isMobileNavigationViewport();
      const hasNavigation = hasNavigationHost();
      document.querySelectorAll(".header-menu-toggle").forEach((button) => {
        button.setAttribute("aria-expanded", isOpen ? "true" : "false");
        button.setAttribute("aria-label", isOpen ? "Закрыть навигацию" : "Открыть навигацию");
        button.hidden = !hasNavigation || !isCompact;
      });
    }
    function setMobileNavigationOpen(nextOpen) {
      const shouldOpen = Boolean(nextOpen) && hasNavigationHost() && isMobileNavigationViewport();
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
    window.isAppMobileNavigationViewport = isMobileNavigationViewport;
    function renderNavigation(host, { openTab, activeTab, userRole, userId }) {
      const isAdmin = userRole === "admin";
      const isModifiedNavigationEvent = (event) => event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey;
      const isSurveySectionActive = isAdmin ? ["get_surveys", "add_survey", "list_answers_users", "archived_surveys"].includes(activeTab) : ["active", "archived", "answers_tab", "archived_surveys_for_user"].includes(activeTab);
      const isOrganizationSectionActive = ["get_organization", "organization_surveys", "add_organization", "archive_list_organizations"].includes(activeTab);
      const isEmailSectionActive = ["email", "email_new"].includes(activeTab);
      const isSettingsSectionActive = ["email_settings", "theme_settings", "survey_auto_creation"].includes(activeTab);
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
          window.AppScrollState?.prepareNavigation({ carry: true });
          window.location.href = "/help";
          return;
        }
        if (tab === "download_logs") {
          window.location.href = "/logs/export";
          return;
        }
        if ((tab === "active" || tab === "answers_tab") && userId) {
          window.AppScrollState?.prepareNavigation({ carry: true });
          window.location.href = "/survey";
          return;
        }
        if ((tab === "archived" || tab === "archived_surveys_for_user") && userId) {
          window.AppScrollState?.prepareNavigation({ carry: true });
          window.location.href = "/archive";
          return;
        }
        const routes = {
          get_surveys: "/survey",
          add_survey: "/survey/create",
          list_answers_users: "/survey/answer",
          archived_surveys: "/survey/archive",
          open_statistics: "/statistics",
          get_users: "/users",
          archived_users: "/users/archive",
          get_organization: "/organizations",
          organization_surveys: "/organizations/survey",
          archive_list_organizations: "/organizations/archive",
          reports: "/reports",
          survey_auto_creation: "/settings/survey-creation",
          theme_settings: "/settings/theme",
          email: "/email",
          email_new: "/email",
          email_settings: "/settings/email",
          get_logs: "/logs"
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

  // Web/wwwroot/js/features/admin/admin-inline-page-loader.js
  var DETACHED_CONTENT_HOST_ID = "admin-inline-detached-content";
  var loadedStylesheetUrls = /* @__PURE__ */ new Set();
  var loadedScriptUrls = /* @__PURE__ */ new Set();
  var loadedAssetsPrimed = false;
  function parseHtmlDocument(html) {
    const parser = new DOMParser();
    return parser.parseFromString(html || "", "text/html");
  }
  function normalizeAssetUrl(url) {
    if (!url) {
      return "";
    }
    try {
      return new URL(url, window.location.origin).href;
    } catch (error) {
      return "";
    }
  }
  function primeLoadedAssets() {
    if (loadedAssetsPrimed) {
      return;
    }
    document.querySelectorAll('link[rel="stylesheet"][href]').forEach((link) => {
      const href = normalizeAssetUrl(link.href);
      if (href) {
        loadedStylesheetUrls.add(href);
      }
    });
    document.querySelectorAll("script[src]").forEach((script) => {
      const src = normalizeAssetUrl(script.src);
      if (src) {
        loadedScriptUrls.add(src);
      }
    });
    loadedAssetsPrimed = true;
  }
  function isThemeStylesheetUrl(href) {
    try {
      return new URL(href, window.location.origin).pathname.endsWith("/css/shared/app-theme.css");
    } catch (error) {
      return false;
    }
  }
  function getThemeStylesheetAnchor() {
    return Array.from(document.querySelectorAll('link[rel="stylesheet"][href]')).find((link) => isThemeStylesheetUrl(link.getAttribute("href") || link.href)) || document.getElementById("app-theme-inline");
  }
  function normalizeThemeStylesheetOrder() {
    const themeAnchor = getThemeStylesheetAnchor();
    const head = themeAnchor?.parentNode;
    if (!head) {
      return;
    }
    const children = Array.from(head.children);
    const themeIndex = children.indexOf(themeAnchor);
    if (themeIndex < 0) {
      return;
    }
    children.slice(themeIndex + 1).forEach((node) => {
      if (node.tagName === "LINK" && node.getAttribute("rel") === "stylesheet" && !isThemeStylesheetUrl(node.getAttribute("href") || node.href)) {
        head.insertBefore(node, themeAnchor);
      }
    });
  }
  function insertStylesheetBeforeTheme(link) {
    normalizeThemeStylesheetOrder();
    const themeAnchor = getThemeStylesheetAnchor();
    if (themeAnchor?.parentNode) {
      themeAnchor.parentNode.insertBefore(link, themeAnchor);
      return;
    }
    document.head.appendChild(link);
  }
  function loadStylesheetsFromDocument(parsedDocument) {
    primeLoadedAssets();
    normalizeThemeStylesheetOrder();
    parsedDocument.querySelectorAll('link[rel="stylesheet"][href]').forEach((sourceLink) => {
      const href = normalizeAssetUrl(sourceLink.getAttribute("href"));
      if (!href || loadedStylesheetUrls.has(href)) {
        return;
      }
      loadedStylesheetUrls.add(href);
      const link = document.createElement("link");
      link.rel = "stylesheet";
      link.href = href;
      if (sourceLink.media) {
        link.media = sourceLink.media;
      }
      insertStylesheetBeforeTheme(link);
    });
  }
  function loadScriptAsset(src) {
    return new Promise((resolve, reject) => {
      const script = document.createElement("script");
      script.src = src;
      script.async = false;
      script.onload = () => resolve();
      script.onerror = () => reject(new Error(`Не удалось загрузить скрипт: ${src}`));
      document.body.appendChild(script);
    });
  }
  async function loadScriptsFromDocument(parsedDocument) {
    primeLoadedAssets();
    let loadedAnyScript = false;
    const scriptSources = Array.from(parsedDocument.querySelectorAll("script[src]")).map((script) => normalizeAssetUrl(script.getAttribute("src"))).filter(Boolean).filter((src, index, list) => list.indexOf(src) === index);
    for (const src of scriptSources) {
      if (loadedScriptUrls.has(src)) {
        continue;
      }
      loadedScriptUrls.add(src);
      try {
        await loadScriptAsset(src);
        loadedAnyScript = true;
      } catch (error) {
        loadedScriptUrls.delete(src);
        throw error;
      }
    }
    return loadedAnyScript;
  }
  function shouldSkipFetchedNode(node) {
    if (!node) {
      return true;
    }
    if (node.nodeType === Node.TEXT_NODE) {
      return !node.textContent.trim();
    }
    if (node.nodeType !== Node.ELEMENT_NODE) {
      return false;
    }
    const element = node;
    if (["SCRIPT", "LINK", "STYLE", "META", "TITLE"].includes(element.tagName)) {
      return true;
    }
    if ([
      "global-antiforgery-token",
      "layout-chrome-context",
      "chrome-context",
      "chrome-header",
      "chrome-navigation",
      "chrome-footer",
      "app-theme-background",
      "app-theme-effects-root",
      "app-theme-foreground-effects-root",
      "root",
      DETACHED_CONTENT_HOST_ID
    ].includes(element.id)) {
      return true;
    }
    if (element.tagName === "TEMPLATE" && ["nav-template-admin", "nav-template-user", "header-template", "footer-template", "admin-extension-modal-template", "admin-extension-modal-row-template", "admin-statistics-template"].includes(element.id)) {
      return true;
    }
    if (element.querySelector && element.querySelector("#content_admin")) {
      return true;
    }
    return false;
  }
  function getPrimaryRenderableNodes(sourceDocument) {
    const contentHost = sourceDocument.getElementById("content_admin");
    if (contentHost) {
      return Array.from(contentHost.childNodes);
    }
    const pageContent = sourceDocument.getElementById("default_content");
    return pageContent ? [pageContent] : Array.from(sourceDocument.body.childNodes);
  }
  function getDetachedRenderableNodes(sourceDocument) {
    const contentHost = sourceDocument.getElementById("content_admin");
    const pageContent = sourceDocument.getElementById("default_content");
    const primaryNode = contentHost || pageContent;
    if (!primaryNode) {
      return [];
    }
    const nodes = [];
    const seen = /* @__PURE__ */ new Set();
    const detachedSelectors = [
      ".modal",
      '[id$="Modal"]',
      "template",
      "#notification",
      "#loadingOverlay",
      "#survey-edit-selected-organization-names"
    ];
    const appendNode = (node) => {
      if (!node || seen.has(node) || node === primaryNode || shouldSkipFetchedNode(node)) {
        return;
      }
      if (primaryNode.contains?.(node)) {
        return;
      }
      if (node.nodeType === Node.ELEMENT_NODE && (node.querySelector?.("#content_admin") || node.querySelector?.("#default_content"))) {
        return;
      }
      seen.add(node);
      nodes.push(node);
    };
    Array.from(sourceDocument.body.childNodes).forEach(appendNode);
    sourceDocument.body.querySelectorAll(detachedSelectors.join(",")).forEach(appendNode);
    return nodes;
  }
  function buildFragmentFromNodes(nodes, cloneNodes = true) {
    const fragment = document.createDocumentFragment();
    (nodes || []).forEach((node) => {
      if (!shouldSkipFetchedNode(node)) {
        fragment.appendChild(cloneNodes ? node.cloneNode(true) : node);
      }
    });
    return fragment;
  }
  function buildRenderableFragment(parsedDocument) {
    return buildFragmentFromNodes(getPrimaryRenderableNodes(parsedDocument));
  }
  function ensureDetachedContentHost() {
    let host = document.getElementById(DETACHED_CONTENT_HOST_ID);
    if (host) {
      return host;
    }
    host = document.createElement("div");
    host.id = DETACHED_CONTENT_HOST_ID;
    document.body.appendChild(host);
    return host;
  }
  function syncDetachedContent(sourceDocument, cloneNodes = true) {
    const host = ensureDetachedContentHost();
    host.innerHTML = "";
    host.appendChild(buildFragmentFromNodes(getDetachedRenderableNodes(sourceDocument), cloneNodes));
  }
  function captureInitialDetachedContent() {
    if (!document.body) {
      return;
    }
    syncDetachedContent(document, false);
  }
  function hydrateFetchedContentState() {
    const selectedOrganizationNamesElement = document.getElementById("survey-edit-selected-organization-names");
    if (!selectedOrganizationNamesElement) {
      window.selectedOrganizationNames = [];
      return;
    }
    try {
      window.selectedOrganizationNames = JSON.parse(selectedOrganizationNamesElement.content.textContent.trim());
    } catch (error) {
      console.warn("Не удалось восстановить выбранные организации из шаблона.", error);
      window.selectedOrganizationNames = [];
    }
  }

  // Web/wwwroot/js/features/admin/admin-inline-history.js
  function normalizePathname(pathname) {
    if (!pathname) {
      return "/";
    }
    return pathname.length > 1 && pathname.endsWith("/") ? pathname.slice(0, -1) : pathname;
  }
  function normalizeLocationUrl(pathname, search = "") {
    const normalizedPath = normalizePathname(pathname);
    return `${normalizedPath}${search || ""}`;
  }
  function normalizeLogsHistoryId(value) {
    const rawValue = String(value || "").trim();
    if (!rawValue) {
      return null;
    }
    const normalizedValue = rawValue.startsWith("?") ? rawValue.slice(1) : rawValue;
    return normalizedValue.length > 0 ? normalizedValue : null;
  }
  function resolveQueryHistoryId(pathname, value, preserveCurrentWhenMissing = false) {
    if (value === void 0) {
      return preserveCurrentWhenMissing && normalizePathname(window.location.pathname) === normalizePathname(pathname) ? normalizeLogsHistoryId(window.location.search) : null;
    }
    return normalizeLogsHistoryId(value);
  }
  function buildQueryHistoryEntry(tab, pathname, value, options = {}) {
    const query = resolveQueryHistoryId(
      pathname,
      value,
      options.preserveCurrentWhenMissing === true
    );
    return {
      tab,
      id: query,
      url: query ? `${pathname}?${query}` : pathname
    };
  }
  function buildAdminHistoryEntry(tab, id = void 0, modalData = null) {
    const surveyId = id ?? modalData?.id_survey ?? null;
    const userId = id ?? modalData?.id_user ?? null;
    const organizationId = id ?? modalData?.id_organization ?? modalData?.organizationId ?? null;
    switch (tab) {
      case "get_surveys":
        return buildQueryHistoryEntry(tab, "/survey", id, { preserveCurrentWhenMissing: id === void 0 });
      case "list_answers_users":
        return buildQueryHistoryEntry(tab, "/survey/answer", id, { preserveCurrentWhenMissing: id === void 0 });
      case "archived_surveys":
        return buildQueryHistoryEntry(tab, "/survey/archive", id, { preserveCurrentWhenMissing: id === void 0 });
      case "get_survey_signatures":
        return surveyId ? { tab, id: surveyId, url: `/survey/${surveyId}/signatures` } : null;
      case "add_survey":
        return { tab, id: null, url: "/survey/create" };
      case "copy_survey":
        return surveyId ? { tab, id: surveyId, url: `/survey/${surveyId}/copy` } : null;
      case "update_survey":
        return surveyId ? { tab, id: surveyId, url: `/survey/${surveyId}/edit` } : null;
      case "update_archived_survey":
        return surveyId ? { tab, id: surveyId, url: `/survey/archive/${surveyId}/edit` } : null;
      case "open_statistics":
        return { tab, id: null, url: "/statistics" };
      case "get_users":
        return buildQueryHistoryEntry(tab, "/users", id, { preserveCurrentWhenMissing: id === void 0 });
      case "add_user":
        return { tab, id: null, url: "/users/create" };
      case "update_user":
        return userId ? { tab, id: userId, url: `/users/${userId}/edit` } : null;
      case "archived_users":
      case "archive_list_users":
        return buildQueryHistoryEntry("archived_users", "/users/archive", id, { preserveCurrentWhenMissing: id === void 0 });
      case "get_organization":
        return buildQueryHistoryEntry(tab, "/organizations", id, { preserveCurrentWhenMissing: id === void 0 });
      case "organization_surveys":
        return { tab, id: null, url: "/organizations/survey" };
      case "add_organization":
        return { tab, id: null, url: "/organizations/create" };
      case "update_organization":
        return organizationId ? { tab, id: organizationId, url: `/organizations/${organizationId}/edit` } : null;
      case "archive_list_organizations":
        return buildQueryHistoryEntry(tab, "/organizations/archive", id, { preserveCurrentWhenMissing: id === void 0 });
      case "reports":
        return { tab, id: null, url: "/reports" };
      case "survey_auto_creation":
        return { tab, id: null, url: "/settings/survey-creation" };
      case "theme_settings":
        return { tab, id: null, url: "/settings/theme" };
      case "get_logs":
        return buildQueryHistoryEntry(tab, "/logs", id, { preserveCurrentWhenMissing: id === void 0 });
      case "email":
      case "email_new":
        return { tab: tab === "email" ? "email_new" : tab, id: null, url: "/email" };
      case "email_settings":
        return { tab, id: null, url: "/settings/email" };
      case "help":
        return { tab, id: null, url: "/help" };
      default:
        return null;
    }
  }
  function getAdminHistoryEntryFromLocation(pathname, search = "") {
    const normalizedPath = normalizePathname(pathname);
    if (normalizedPath === "/survey" || normalizedPath === "/surveys") {
      return buildAdminHistoryEntry("get_surveys", search || "");
    }
    if (normalizedPath === "/survey/answer" || normalizedPath === "/surveys/answers") {
      return buildAdminHistoryEntry("list_answers_users", search || "");
    }
    if (normalizedPath === "/survey/archive" || normalizedPath === "/surveys/archive") {
      return buildAdminHistoryEntry("archived_surveys", search || "");
    }
    if (normalizedPath === "/survey/create" || normalizedPath === "/surveys/create") {
      return buildAdminHistoryEntry("add_survey");
    }
    if (normalizedPath === "/statistics") {
      return buildAdminHistoryEntry("open_statistics");
    }
    if (normalizedPath === "/users") {
      return buildAdminHistoryEntry("get_users", search || "");
    }
    if (normalizedPath === "/users/create") {
      return buildAdminHistoryEntry("add_user");
    }
    if (normalizedPath === "/users/archive") {
      return buildAdminHistoryEntry("archived_users", search || "");
    }
    if (normalizedPath === "/organizations") {
      return buildAdminHistoryEntry("get_organization", search || "");
    }
    if (normalizedPath === "/organizations/survey" || normalizedPath === "/organizations/surveys") {
      return buildAdminHistoryEntry("organization_surveys");
    }
    if (normalizedPath === "/organizations/create") {
      return buildAdminHistoryEntry("add_organization");
    }
    if (normalizedPath === "/organizations/archive") {
      return buildAdminHistoryEntry("archive_list_organizations", search || "");
    }
    if (normalizedPath === "/reports") {
      return buildAdminHistoryEntry("reports");
    }
    if (normalizedPath === "/settings/survey-creation" || normalizedPath === "/survey-auto-creation") {
      return buildAdminHistoryEntry("survey_auto_creation");
    }
    if (normalizedPath === "/settings/theme" || normalizedPath === "/theme/configuration" || normalizedPath === "/theme-settings") {
      return buildAdminHistoryEntry("theme_settings");
    }
    if (normalizedPath === "/logs" || normalizedPath === "/event-log") {
      return buildAdminHistoryEntry("get_logs", search || "");
    }
    if (normalizedPath === "/email" || normalizedPath === "/mail" || normalizedPath === "/mail/new") {
      return buildAdminHistoryEntry("email_new");
    }
    if (normalizedPath === "/settings/email" || normalizedPath === "/mail/configuration" || normalizedPath === "/mail-settings") {
      return buildAdminHistoryEntry("email_settings");
    }
    if (normalizedPath === "/help") {
      return buildAdminHistoryEntry("help");
    }
    let match = normalizedPath.match(/^\/survey\/(\d+)\/signatures$/) || normalizedPath.match(/^\/surveys\/(\d+)\/signatures$/);
    if (match) {
      return buildAdminHistoryEntry("get_survey_signatures", Number(match[1]));
    }
    match = normalizedPath.match(/^\/survey\/archive\/(\d+)\/edit$/) || normalizedPath.match(/^\/surveys\/archive\/(\d+)\/edit$/);
    if (match) {
      return buildAdminHistoryEntry("update_archived_survey", Number(match[1]));
    }
    match = normalizedPath.match(/^\/survey\/(\d+)\/edit$/) || normalizedPath.match(/^\/surveys\/(\d+)\/edit$/);
    if (match) {
      return buildAdminHistoryEntry("update_survey", Number(match[1]));
    }
    match = normalizedPath.match(/^\/survey\/(\d+)\/copy$/) || normalizedPath.match(/^\/surveys\/(\d+)\/copy$/);
    if (match) {
      return buildAdminHistoryEntry("copy_survey", Number(match[1]));
    }
    match = normalizedPath.match(/^\/users\/(\d+)\/edit$/);
    if (match) {
      return buildAdminHistoryEntry("update_user", Number(match[1]));
    }
    match = normalizedPath.match(/^\/organizations\/(\d+)\/edit$/);
    if (match) {
      return buildAdminHistoryEntry("update_organization", Number(match[1]));
    }
    return null;
  }

  // Web/wwwroot/js/features/admin/admin-inline-modal-renderer.js
  function createClosedAdminModalState() {
    return {
      isOpen: false,
      content: "",
      data: null,
      message: null,
      isSuccess: false
    };
  }
  function appendDialog(root, header, body, footer) {
    root.appendChild(header);
    root.appendChild(body);
    root.appendChild(footer);
  }
  function createDialogSection(className, text) {
    const section = document.createElement("div");
    section.className = className;
    if (text) {
      section.textContent = text;
    }
    return section;
  }
  function createAdminModalRenderer({
    pageContainer,
    getExtensionModalMount,
    onClose,
    onCopySurvey,
    onUpdateSurvey,
    onDeleteSurvey,
    onCreateMonthlyReport,
    onCreateMonthlySummaryReport,
    onCreateQuarterlyReport
  }) {
    let cleanup = null;
    let modalNode = document.getElementById("admin-inline-modal-host");
    if (modalNode) {
      modalNode.remove();
    }
    modalNode = document.createElement("div");
    modalNode.id = "admin-inline-modal-host";
    const modalContent = document.createElement("div");
    modalContent.className = "modal-content";
    const modalClose = document.createElement("span");
    modalClose.className = "modal-close";
    const modalIcon = document.createElement("i");
    modalIcon.className = "fas fa-xmark";
    const bodyHost = document.createElement("div");
    bodyHost.className = "modal-body";
    modalClose.appendChild(modalIcon);
    modalContent.appendChild(modalClose);
    modalContent.appendChild(bodyHost);
    modalNode.appendChild(modalContent);
    pageContainer.appendChild(modalNode);
    function syncPageState() {
      window.syncSiteModalBodyState?.();
    }
    function reveal() {
      modalNode.classList.add("modal--visible");
      modalNode.setAttribute("aria-hidden", "false");
      syncPageState();
    }
    function renderReport(modalState) {
      const root = document.createElement("div");
      const title = document.createElement("h2");
      title.className = "modal-title";
      title.textContent = "Создать отчёт";
      root.appendChild(title);
      const actions = document.createElement("div");
      actions.style.display = "flex";
      actions.style.gap = "10px";
      actions.style.justifyContent = "space-between";
      actions.style.marginTop = "1.5rem";
      const month = document.createElement("div");
      month.className = "submenu2-container";
      month.style.flex = "1";
      const monthButton = document.createElement("button");
      monthButton.style.width = "100%";
      monthButton.textContent = "Отчёт за месяц";
      const monthMenu = document.createElement("div");
      monthMenu.className = "submenu2";
      const bySurvey = document.createElement("div");
      bySurvey.textContent = "По выбранной анкете";
      bySurvey.addEventListener("click", () => onCreateMonthlyReport(modalState.data?.id_survey));
      const allSurveys = document.createElement("div");
      allSurveys.textContent = "По всем анкетам";
      allSurveys.addEventListener("click", () => onCreateMonthlySummaryReport());
      monthMenu.appendChild(bySurvey);
      monthMenu.appendChild(allSurveys);
      month.appendChild(monthButton);
      month.appendChild(monthMenu);
      const quarter = document.createElement("div");
      quarter.className = "submenu2-container";
      quarter.style.flex = "1";
      const quarterButton = document.createElement("button");
      quarterButton.style.width = "100%";
      quarterButton.textContent = "Отчёт за квартал";
      const quarterMenu = document.createElement("div");
      quarterMenu.className = "submenu2";
      [1, 2, 3, 4].forEach((quarterNumber) => {
        const item = document.createElement("div");
        item.textContent = `${quarterNumber} квартал`;
        item.addEventListener("click", () => onCreateQuarterlyReport(quarterNumber));
        quarterMenu.appendChild(item);
      });
      quarter.appendChild(quarterButton);
      quarter.appendChild(quarterMenu);
      actions.appendChild(month);
      actions.appendChild(quarter);
      root.appendChild(actions);
      bodyHost.appendChild(root);
    }
    function renderSurveyAction(modalState) {
      const isCopy = modalState.content === "copy";
      const isUpdate = modalState.content === "update";
      const titleText = isCopy ? "Копирование анкеты" : isUpdate ? "Редактирование анкеты" : "Удаление анкеты";
      const messageText = isCopy ? `Вы уверены, что хотите создать копию анкеты "${modalState.data?.name_survey}"?` : isUpdate ? `Вы переходите к редактированию анкеты "${modalState.data?.name_survey}".` : `Вы уверены, что хотите удалить анкету "${modalState.data?.name_survey}"?`;
      const okText = isCopy ? "Копировать" : isUpdate ? "Продолжить" : "Удалить";
      const onConfirm = isCopy ? onCopySurvey : isUpdate ? onUpdateSurvey : onDeleteSurvey;
      const root = document.createElement("div");
      const header = createDialogSection("modal-header", "");
      const title = document.createElement("h2");
      title.className = "h2_modal";
      title.textContent = titleText;
      header.replaceChildren(title);
      const body = createDialogSection("modal-body");
      const message = createDialogSection("modal-message", messageText);
      body.appendChild(message);
      const footer = document.createElement("div");
      footer.className = "modal-footer";
      const cancel = document.createElement("button");
      cancel.className = "modal_btn modal_btn-secondary";
      cancel.textContent = "Отмена";
      cancel.addEventListener("click", onClose);
      const confirm = document.createElement("button");
      confirm.className = "modal_btn modal_btn-primary";
      confirm.textContent = okText;
      confirm.addEventListener("click", onConfirm);
      footer.appendChild(cancel);
      footer.appendChild(confirm);
      appendDialog(root, header, body, footer);
      bodyHost.appendChild(root);
    }
    function renderMessage(modalState) {
      const root = document.createElement("div");
      const header = createDialogSection("modal-header", "");
      const title = document.createElement("h2");
      title.className = "h2_modal";
      title.textContent = modalState.isSuccess ? "Успешно" : "Ошибка";
      header.replaceChildren(title);
      const body = document.createElement("div");
      body.className = "modal-body";
      const message = createDialogSection(
        `modal-message ${modalState.isSuccess ? "success-message" : "error-message"}`,
        modalState.message || ""
      );
      body.appendChild(message);
      const footer = document.createElement("div");
      footer.className = "modal-footer";
      const confirm = document.createElement("button");
      confirm.className = "modal_btn modal_btn-primary";
      confirm.textContent = "OK";
      confirm.addEventListener("click", onClose);
      footer.appendChild(confirm);
      appendDialog(root, header, body, footer);
      bodyHost.appendChild(root);
    }
    function render(modalState) {
      modalNode.className = "modal";
      modalNode.setAttribute("aria-hidden", "true");
      if (typeof cleanup === "function") {
        cleanup();
        cleanup = null;
      }
      bodyHost.replaceChildren();
      if (!modalState.isOpen) {
        syncPageState();
        return;
      }
      if (modalState.content === "extend") {
        const mountExtensionModal = getExtensionModalMount();
        if (typeof mountExtensionModal === "function") {
          cleanup = mountExtensionModal(bodyHost, { survey: modalState.data, onClose }) || null;
        } else {
          const message = document.createElement("div");
          message.textContent = "Модуль продления не загружен.";
          bodyHost.appendChild(message);
        }
      } else if (modalState.content === "report") {
        renderReport(modalState);
      } else if (["copy", "update", "delete"].includes(modalState.content)) {
        renderSurveyAction(modalState);
      } else if (modalState.content === "message") {
        renderMessage(modalState);
      } else {
        return;
      }
      reveal();
    }
    modalClose.addEventListener("click", onClose);
    return {
      render,
      destroy() {
        if (typeof cleanup === "function") {
          cleanup();
        }
        modalNode.remove();
      }
    };
  }

  // Web/wwwroot/js/features/admin/admin-organization-actions.js
  function requireOrganizationId(organizationId) {
    if (!organizationId) {
      throw new Error("ID организации не указан.");
    }
  }
  function createAdminOrganizationActions({
    fetchPage,
    getActiveTab,
    getModalData,
    getRequestVerificationToken,
    openModalWhenReady,
    setActiveTab
  }) {
    async function removeCurrentOrganization() {
      const modalData = getModalData();
      const organizationId = modalData?.id_organization ?? modalData?.organizationId;
      const response = await fetch(`/organizations/${organizationId}/delete`, {
        method: "POST",
        cache: "no-store",
        headers: {
          "X-Admin-Inline-Request": "true",
          RequestVerificationToken: getRequestVerificationToken()
        }
      });
      if (!response.ok) {
        throw new Error(await response.text() || "Ошибка при удалении организации.");
      }
      await fetchPage("/organizations");
      setActiveTab("get_organization");
    }
    return {
      async add() {
        const modalIsReady = getActiveTab() === "get_organization" && document.getElementById("addOrganizationModal");
        if (!modalIsReady) {
          await fetchPage("/organizations");
        }
        setActiveTab("get_organization");
        openModalWhenReady("addOrganizationModal", window.openAddOrganizationModal);
      },
      async edit(organizationId) {
        requireOrganizationId(organizationId);
        await fetchPage(`/organizations/${organizationId}/edit`);
        setActiveTab("update_organization");
      },
      removeCurrentOrganization
    };
  }

  // Web/wwwroot/js/features/admin/admin-survey-actions.js
  function requireSurveyId(surveyId) {
    if (!surveyId) {
      throw new Error("ID анкеты не указан.");
    }
  }
  function createAdminSurveyActions({
    fetchPage,
    getActiveTab,
    getModalData,
    getRequestVerificationToken,
    notify,
    openModalWhenReady,
    setActiveTab
  }) {
    async function removeCurrentSurvey() {
      const surveyId = getModalData()?.id_survey;
      const response = await fetch(`/survey/${surveyId}/delete`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          RequestVerificationToken: getRequestVerificationToken()
        },
        body: JSON.stringify({ surveyId })
      });
      const result = await response.json();
      if (!response.ok) {
        throw new Error(result.message || "Ошибка при удалении анкеты.");
      }
      await fetchPage("/survey");
      notify(result.message, "success");
      setActiveTab("get_surveys");
      return result;
    }
    return {
      async add() {
        const editorIsReady = getActiveTab() === "get_surveys" && document.getElementById("surveyEditorModal") && !document.getElementById("surveyId");
        if (!editorIsReady) {
          await fetchPage("/survey");
        }
        setActiveTab("get_surveys");
        openModalWhenReady("surveyEditorModal", window.openAddSurveyModal);
      },
      async copy(surveyId) {
        requireSurveyId(surveyId);
        await fetchPage("/survey");
        setActiveTab("get_surveys");
        openModalWhenReady(
          "surveyEditorModal",
          () => window.openCopySurveyModalById?.(surveyId, { skipListRefresh: true })
        );
      },
      async edit(surveyId, { archived = false } = {}) {
        requireSurveyId(surveyId);
        const endpoint = archived ? `/survey/archive/${surveyId}/edit` : `/survey/${surveyId}/edit`;
        await fetchPage(endpoint);
        setActiveTab(archived ? "archived_surveys" : "get_surveys");
        openModalWhenReady("surveyEditorModal", window.openEditSurveyModal);
      },
      removeCurrentSurvey
    };
  }

  // Web/wwwroot/js/features/admin/admin-inline-tab-registry.js
  var listPageDefinitions = Object.freeze({
    get_surveys: { pathname: "/survey" },
    list_answers_users: { pathname: "/survey/answer" },
    archived_surveys: { pathname: "/survey/archive" },
    get_logs: { pathname: "/logs" },
    get_users: { pathname: "/users" },
    get_organization: { pathname: "/organizations" },
    archive_list_organizations: { pathname: "/organizations/archive" },
    archived_users: { pathname: "/users/archive", activeTab: "archived_users" },
    archive_list_users: { pathname: "/users/archive", activeTab: "archived_users" }
  });
  var staticPageDefinitions = Object.freeze({
    open_statistics: { pathname: "/statistics" },
    organization_surveys: { pathname: "/organizations/survey" },
    help: { pathname: "/help" },
    reports: { pathname: "/reports" },
    survey_auto_creation: { pathname: "/settings/survey-creation" },
    theme_settings: { pathname: "/settings/theme" },
    email: { pathname: "/email", activeTab: "email_new" },
    email_new: { pathname: "/email", activeTab: "email_new" },
    email_settings: { pathname: "/settings/email" }
  });
  var entityPageDefinitions = Object.freeze({
    get_survey_signatures: {
      pathname: (id) => `/survey/${id}/signatures`,
      missingIdMessage: "ID анкеты не указан."
    }
  });
  function hasIdentifier(value) {
    return value !== null && value !== void 0 && value !== "";
  }
  function resolveAdminTabPageRequest(tab, id, buildListRequestUrl) {
    const listDefinition = listPageDefinitions[tab];
    if (listDefinition) {
      return {
        url: buildListRequestUrl(listDefinition.pathname, id),
        activeTab: listDefinition.activeTab || tab
      };
    }
    const staticDefinition = staticPageDefinitions[tab];
    if (staticDefinition) {
      return {
        url: staticDefinition.pathname,
        activeTab: staticDefinition.activeTab || tab
      };
    }
    const entityDefinition = entityPageDefinitions[tab];
    if (!entityDefinition) {
      return null;
    }
    if (!hasIdentifier(id)) {
      throw new Error(entityDefinition.missingIdMessage);
    }
    return {
      url: entityDefinition.pathname(id),
      activeTab: entityDefinition.activeTab || tab
    };
  }

  // Web/wwwroot/js/features/admin/admin-user-actions.js
  function requireUserId(userId) {
    if (!userId) {
      throw new Error("ID пользователя не указан.");
    }
  }
  function createAdminUserActions({
    fetchPage,
    getActiveTab,
    getModalData,
    getRequestVerificationToken,
    notify,
    openModalWhenReady,
    setActiveTab
  }) {
    async function removeCurrentUser() {
      const userId = getModalData()?.id_user;
      const response = await fetch(`/users/${userId}/delete`, {
        method: "POST",
        headers: {
          RequestVerificationToken: getRequestVerificationToken()
        }
      });
      const message = await response.text();
      if (!response.ok) {
        throw new Error(message || "Ошибка при удалении пользователя.");
      }
      await fetchPage("/users");
      notify(message, "success");
      setActiveTab("get_users");
      return message;
    }
    return {
      async add() {
        const modalIsReady = getActiveTab() === "get_users" && document.getElementById("addUserModal");
        if (!modalIsReady) {
          await fetchPage("/users");
        }
        setActiveTab("get_users");
        openModalWhenReady("addUserModal", window.openAddUserModal);
      },
      async edit(userId) {
        requireUserId(userId);
        await fetchPage(`/users/${userId}/edit`);
        setActiveTab("update_user");
      },
      removeCurrentUser
    };
  }

  // Web/wwwroot/js/features/admin/admin-inline-core.js
  (() => {
    function getRequestVerificationToken() {
      return window.AppHttp?.getAntiforgeryToken() || "";
    }
    function createContentWrapper() {
      const wrapper = document.createElement("div");
      wrapper.className = "content-wrapper";
      return wrapper;
    }
    const rootElement = document.getElementById("root");
    const existingHeaderHost = document.getElementById("chrome-header");
    const existingNavHost = document.getElementById("chrome-navigation");
    const existingContentAdmin = document.getElementById("content_admin");
    const existingFooterHost = document.getElementById("chrome-footer");
    const layoutContextNode = document.getElementById("layout-chrome-context");
    const hasExistingShell = Boolean(existingHeaderHost && existingNavHost && existingContentAdmin && existingFooterHost);
    if (!rootElement && !hasExistingShell) {
      return;
    }
    captureInitialDetachedContent();
    const initialData = {
      userRole: layoutContextNode?.dataset?.userRole || "",
      userId: Number(layoutContextNode?.dataset?.userId || 0),
      displayName: layoutContextNode?.dataset?.displayName || "",
      userName: layoutContextNode?.dataset?.userName || "",
      organizationName: layoutContextNode?.dataset?.organizationName || "",
      ...window.__adminBootstrap || {}
    };
    const initialHistoryEntry = getAdminHistoryEntryFromLocation(window.location.pathname, window.location.search) || buildAdminHistoryEntry("get_surveys");
    const userRole = initialData.userRole || "";
    const hasAccess = Boolean(userRole);
    const getExtensionModalMount = () => window.AdminInlineAppPages?.mountExtensionModal || null;
    if (!hasAccess) {
      if (!rootElement) {
        return;
      }
      rootElement.innerHTML = "";
      const denied = document.createElement("div");
      denied.className = "access-denied";
      const h2 = document.createElement("h2");
      h2.textContent = "Доступ запрещён";
      const p = document.createElement("p");
      p.textContent = "У вас нет прав для просмотра этой страницы.";
      const br = document.createElement("br");
      const a = document.createElement("a");
      a.href = "/";
      a.className = "btn";
      a.textContent = "Вернуться на страницу авторизации";
      denied.appendChild(h2);
      denied.appendChild(p);
      denied.appendChild(br);
      denied.appendChild(a);
      rootElement.appendChild(denied);
      return;
    }
    const state = {
      activeTab: initialHistoryEntry?.tab || "get_surveys",
      loading: false,
      showLoader: false,
      modal: createClosedAdminModalState()
    };
    let contentCleanup = null;
    let headerCleanup = null;
    let navCleanup = null;
    let footerCleanup = null;
    let loaderTimer = null;
    let initTogglesTimer = null;
    let initEditTimer = null;
    let contentLifecycleScope = null;
    let pageContainer = rootElement ? document.createElement("div") : existingHeaderHost.closest(".page-container") || document.body;
    let headerHost = existingHeaderHost;
    let navHost = existingNavHost;
    let contentAdmin = existingContentAdmin;
    let footerHost = existingFooterHost;
    if (rootElement) {
      rootElement.innerHTML = "";
      pageContainer.className = "page-container";
      headerHost = document.createElement("div");
      const adminContainer = document.createElement("div");
      adminContainer.className = "admin-container";
      navHost = document.createElement("div");
      contentAdmin = document.createElement("div");
      contentAdmin.id = "content_admin";
      footerHost = document.createElement("div");
      adminContainer.appendChild(navHost);
      adminContainer.appendChild(contentAdmin);
      pageContainer.appendChild(headerHost);
      pageContainer.appendChild(adminContainer);
      pageContainer.appendChild(footerHost);
      rootElement.appendChild(pageContainer);
    }
    const adminModalRenderer = createAdminModalRenderer({
      pageContainer,
      getExtensionModalMount,
      onClose: () => closeModal(),
      onCopySurvey: () => handleCopySurvey(),
      onUpdateSurvey: () => handleUpdateSurvey(),
      onDeleteSurvey: () => handleDeleteSurvey(),
      onCreateMonthlyReport: (surveyId) => createMonthlyReport(surveyId),
      onCreateMonthlySummaryReport: () => createMonthlySummaryReport(),
      onCreateQuarterlyReport: (quarter) => createQuarterlyReport(quarter)
    });
    const syncBrowserHistory = (historyEntry, mode = "push") => {
      if (!historyEntry) {
        return;
      }
      const nextState = {
        tab: historyEntry.tab,
        id: historyEntry.id ?? null
      };
      const currentUrl = normalizeLocationUrl(window.location.pathname, window.location.search);
      if (mode === "replace") {
        window.history.replaceState(nextState, "", historyEntry.url);
        return;
      }
      if (currentUrl === historyEntry.url && window.history.state?.tab === nextState.tab && (window.history.state?.id ?? null) === nextState.id) {
        return;
      }
      window.history.pushState(nextState, "", historyEntry.url);
    };
    const remountNavigation = () => {
      if (typeof navCleanup === "function") {
        navCleanup();
      }
      navCleanup = typeof window.mountNavigation === "function" ? window.mountNavigation(navHost, {
        openTab,
        activeTab: state.activeTab,
        userRole: initialData.userRole,
        userId: initialData.userId
      }) : null;
    };
    const remountChrome = () => {
      if (typeof headerCleanup === "function") {
        headerCleanup();
      }
      if (typeof footerCleanup === "function") {
        footerCleanup();
      }
      headerCleanup = typeof window.mountHeader === "function" ? window.mountHeader(headerHost, {
        userRole: initialData.userRole,
        displayName: initialData.displayName,
        userName: initialData.userName,
        organizationName: initialData.organizationName
      }) : null;
      remountNavigation();
      footerCleanup = typeof window.mountFooter === "function" ? window.mountFooter(footerHost) : null;
    };
    const setLoading = (isLoading) => {
      state.loading = isLoading;
      if (loaderTimer) {
        window.clearTimeout(loaderTimer);
        loaderTimer = null;
      }
      state.showLoader = false;
      renderLoader();
    };
    const renderLoader = () => {
      const existing = contentAdmin.querySelector(".loading-overlay");
      if (existing) {
        existing.remove();
      }
    };
    const closeModal = () => {
      state.modal = createClosedAdminModalState();
      renderModal();
    };
    const setModal = (nextModal) => {
      state.modal = nextModal;
      renderModal();
    };
    const schedulePostContentHooks = () => {
      const mountedPage = contentAdmin.querySelector(".app-page[data-page]")?.dataset.page || "";
      const schedule = (callback) => {
        if (contentLifecycleScope) {
          contentLifecycleScope.timeout(callback, 0);
          return;
        }
        window.setTimeout(callback, 0);
      };
      if (initTogglesTimer) {
        window.clearTimeout(initTogglesTimer);
      }
      const initializePasswordToggles = () => {
        if (window.initPasswordToggles) {
          window.initPasswordToggles(document);
        }
      };
      if (contentLifecycleScope) {
        contentLifecycleScope.timeout(initializePasswordToggles, 0);
      } else {
        initTogglesTimer = window.setTimeout(initializePasswordToggles, 0);
      }
      if (initEditTimer) {
        window.clearTimeout(initEditTimer);
        initEditTimer = null;
      }
      if (state.activeTab === "update_survey") {
        const initializeSurveyEdit = () => {
          if (typeof window.surveyEditInit === "function") {
            window.surveyEditInit();
          }
        };
        if (contentLifecycleScope) {
          contentLifecycleScope.timeout(initializeSurveyEdit, 0);
        } else {
          initEditTimer = window.setTimeout(initializeSurveyEdit, 0);
        }
      }
      if (mountedPage === "answers-statistics") {
        schedule(() => {
          if (typeof window.initAnswerStatisticsPage === "function") {
            window.initAnswerStatisticsPage();
          }
        });
      }
      if (mountedPage === "mail-settings-page" || mountedPage === "mail-compose") {
        schedule(() => {
          if (typeof window.initEmailSettingsPage === "function") {
            window.initEmailSettingsPage();
          }
        });
      }
    };
    const setContentMount = (mountFn) => {
      if (contentAdmin.querySelector('.app-page[data-page="theme-settings-page"]') && typeof window.teardownThemeSettingsPage === "function") {
        window.teardownThemeSettingsPage();
      }
      if (typeof contentCleanup === "function") {
        contentCleanup();
        contentCleanup = null;
      }
      window.AppPageLifecycle?.unmount(contentAdmin);
      contentLifecycleScope?.dispose();
      contentLifecycleScope = window.AppPageLifecycle?.createScope?.() || null;
      contentAdmin.innerHTML = "";
      const wrapper = createContentWrapper();
      contentAdmin.appendChild(wrapper);
      if (typeof mountFn === "function") {
        contentCleanup = mountFn(wrapper) || null;
      }
      window.AppPageLifecycle?.mount(contentAdmin);
      schedulePostContentHooks();
      renderLoader();
    };
    const setHtmlContent = (parsedDocument) => {
      const fragment = buildRenderableFragment(parsedDocument);
      setContentMount((host) => {
        host.appendChild(fragment);
        return null;
      });
      syncDetachedContent(parsedDocument);
      hydrateFetchedContentState();
    };
    const fetchHtmlPage = async (endpoint, options) => {
      const response = await fetch(endpoint, {
        ...options,
        cache: "no-store",
        headers: {
          ...options?.headers || {},
          "X-Admin-Inline-Request": "true"
        }
      });
      if (!response.ok) {
        throw new Error(
          window.getResponseErrorMessage ? window.getResponseErrorMessage(response, "Ошибка загрузки") : `Ошибка загрузки: ${response.status}`
        );
      }
      const html = await response.text();
      const parsedDocument = parseHtmlDocument(html);
      const nextChromeContext = typeof window.syncAdminChromeContextFromDocument === "function" ? window.syncAdminChromeContextFromDocument(parsedDocument) : null;
      if (nextChromeContext && typeof nextChromeContext === "object") {
        Object.assign(initialData, nextChromeContext);
      }
      loadStylesheetsFromDocument(parsedDocument);
      setHtmlContent(parsedDocument);
      const loadedAnyScript = await loadScriptsFromDocument(parsedDocument);
      if (loadedAnyScript) {
        schedulePostContentHooks();
      }
      return response;
    };
    const renderModal = () => adminModalRenderer.render(state.modal);
    const setActiveTabAndRefreshNav = (tab) => {
      state.activeTab = tab;
      remountNavigation();
    };
    const tryOpenModal = (modalId, openHandler) => {
      const modal = document.getElementById(modalId);
      if (!modal || typeof openHandler !== "function") {
        return false;
      }
      openHandler();
      return true;
    };
    const openModalWhenReady = (modalId, openHandler) => {
      if (tryOpenModal(modalId, openHandler)) {
        return;
      }
      let attempts = 0;
      const timer = window.setInterval(() => {
        attempts += 1;
        if (tryOpenModal(modalId, openHandler) || attempts >= 20) {
          window.clearInterval(timer);
        }
      }, 50);
    };
    const actionDependencies = {
      fetchPage: fetchHtmlPage,
      getActiveTab: () => state.activeTab,
      getModalData: () => state.modal.data,
      getRequestVerificationToken,
      openModalWhenReady,
      setActiveTab: setActiveTabAndRefreshNav
    };
    const surveyActions = createAdminSurveyActions({
      ...actionDependencies,
      notify: (...args) => window.AppUi?.notify?.(...args)
    });
    const userActions = createAdminUserActions({
      ...actionDependencies,
      notify: (...args) => window.AppUi?.notify?.(...args)
    });
    const organizationActions = createAdminOrganizationActions(actionDependencies);
    function scrollToSelector(selector) {
      if (!selector) {
        return false;
      }
      const target = document.querySelector(selector);
      if (!target) {
        return false;
      }
      target.scrollIntoView({
        block: "start",
        behavior: "auto"
      });
      return true;
    }
    function buildListRequestUrl(pathname, queryId = null) {
      const normalizedPath = normalizePathname(pathname);
      const normalizedQuery = normalizeLogsHistoryId(queryId);
      return normalizedQuery ? `${normalizedPath}?${normalizedQuery}` : normalizedPath;
    }
    const openTab = async (tab, id = void 0, options = {}) => {
      const historyMode = options.historyMode ?? "push";
      const force = options.force === true;
      const scrollMode = options.scrollMode ?? "restore";
      const scrollTargetSelector = String(options.scrollTargetSelector || "").trim();
      const historyEntry = buildAdminHistoryEntry(tab, id, state.modal.data);
      const resolvedId = historyEntry?.id ?? id ?? null;
      if (!force && state.activeTab === tab && resolvedId === (window.history.state?.id ?? null)) {
        return;
      }
      if (scrollMode === "carry") {
        window.AppScrollState?.prepareNavigation({ carry: true });
      } else {
        window.AppScrollState?.saveCurrentPosition();
      }
      const initialPageRequest = tab === "get_surveys" ? resolveAdminTabPageRequest(tab, resolvedId, buildListRequestUrl) : null;
      if (initialPageRequest) {
        await fetchHtmlPage(initialPageRequest.url);
        setActiveTabAndRefreshNav(initialPageRequest.activeTab);
        if (historyMode !== "none") {
          syncBrowserHistory(historyEntry, historyMode);
        }
        if (!scrollToSelector(scrollTargetSelector)) {
          window.AppScrollState?.restoreCurrentPosition({ preferCarry: scrollMode === "carry" });
        }
        return;
      }
      setLoading(true);
      try {
        const pageRequest = resolveAdminTabPageRequest(tab, resolvedId, buildListRequestUrl);
        if (pageRequest) {
          await fetchHtmlPage(pageRequest.url);
          setActiveTabAndRefreshNav(pageRequest.activeTab);
        } else {
          switch (tab) {
            case "add_survey":
              await surveyActions.add();
              break;
            case "download_logs": {
              const response = await fetch("/logs/export");
              if (!response.ok) {
                throw new Error(window.getResponseErrorMessage ? window.getResponseErrorMessage(response, "Ошибка выгрузки логов") : `Ошибка выгрузки логов: ${response.status}`);
              }
              const blob = await response.blob();
              const downloadUrl = window.URL.createObjectURL(blob);
              const link = document.createElement("a");
              link.href = downloadUrl;
              link.download = "logs.txt";
              document.body.appendChild(link);
              link.click();
              link.remove();
              window.URL.revokeObjectURL(downloadUrl);
              break;
            }
            case "copy_survey":
              await surveyActions.copy(resolvedId);
              break;
            case "update_survey":
              await surveyActions.edit(resolvedId);
              break;
            case "update_archived_survey":
              await surveyActions.edit(resolvedId, { archived: true });
              break;
            case "delete_survey":
              await surveyActions.removeCurrentSurvey();
              break;
            case "add_user":
              await userActions.add();
              break;
            case "update_user":
              await userActions.edit(resolvedId);
              break;
            case "delete_user":
              await userActions.removeCurrentUser();
              break;
            case "add_organization":
              await organizationActions.add();
              break;
            case "update_organization":
              await organizationActions.edit(resolvedId);
              break;
            case "delete_organization":
              await organizationActions.removeCurrentOrganization();
              break;
            case "monthly_summary_report":
              createMonthlySummaryReport();
              await fetchHtmlPage("/reports");
              setActiveTabAndRefreshNav("reports");
              break;
            case "quarterly_report_q1":
            case "quarterly_report_q2":
            case "quarterly_report_q3":
            case "quarterly_report_q4":
              createQuarterlyReport(Number(tab.slice(-1)));
              await fetchHtmlPage("/reports");
              setActiveTabAndRefreshNav("reports");
              break;
            default:
              console.warn(`Вкладка ${tab} не обработана.`);
              break;
          }
        }
        if (historyMode !== "none") {
          const nextHistory = ["delete_survey"].includes(tab) ? buildAdminHistoryEntry("get_surveys") : ["add_survey", "update_survey"].includes(tab) ? buildAdminHistoryEntry("get_surveys") : ["update_archived_survey"].includes(tab) ? buildAdminHistoryEntry("archived_surveys") : ["add_user"].includes(tab) ? buildAdminHistoryEntry("get_users") : ["add_organization"].includes(tab) ? buildAdminHistoryEntry("get_organization") : ["delete_user"].includes(tab) ? buildAdminHistoryEntry("get_users") : ["delete_organization"].includes(tab) ? buildAdminHistoryEntry("get_organization") : ["monthly_summary_report", "quarterly_report_q1", "quarterly_report_q2", "quarterly_report_q3", "quarterly_report_q4"].includes(tab) ? buildAdminHistoryEntry("reports") : historyEntry;
          syncBrowserHistory(nextHistory, ["delete_survey", "delete_user", "delete_organization"].includes(tab) ? "replace" : historyMode);
        }
        if (tab !== "download_logs") {
          if (!scrollToSelector(scrollTargetSelector)) {
            window.AppScrollState?.restoreCurrentPosition({ preferCarry: scrollMode === "carry" });
          }
        }
      } catch (error) {
        console.error("Ошибка переключения вкладки:", error);
        window.AppUi?.notify?.(error.message || "Произошла ошибка загрузки.", "error");
      } finally {
        setLoading(false);
      }
    };
    const handleCopySurvey = async () => {
      closeModal();
      await openTab("copy_survey");
    };
    const handleUpdateSurvey = async () => {
      closeModal();
      await openTab("update_survey");
    };
    const handleDeleteSurvey = async () => {
      try {
        setLoading(true);
        await surveyActions.removeCurrentSurvey();
      } catch (error) {
        console.error("Ошибка при удалении анкеты:", error);
        window.AppUi?.notify?.(error.message || "Не удалось удалить анкету.", "error");
      } finally {
        setLoading(false);
      }
    };
    remountChrome();
    renderLoader();
    renderModal();
    window.handleTabClick = (tabName, options = {}) => {
      const resolvedOptions = options && typeof options === "object" ? options : {};
      return openTab(tabName, null, { scrollMode: "carry", ...resolvedOptions });
    };
    window.refreshAdminTab = (tabName, id = void 0, options = {}) => {
      const resolvedOptions = options && typeof options === "object" ? options : {};
      return openTab(tabName, id, { force: true, scrollMode: "restore", ...resolvedOptions });
    };
    document.addEventListener("click", (event) => {
      if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
        return;
      }
      const navLink = event.target.closest(".admin-nav .nav-link, .admin-nav .submenu-link");
      if (!navLink) {
        return;
      }
      const tabHolder = navLink.closest(".submenu-item, .nav-item");
      const tabName = tabHolder?.dataset?.tab || "";
      const ownerNavItem = navLink.closest(".nav-item.has-submenu");
      const ownerTab = ownerNavItem?.dataset?.tab || "";
      if (!tabName) {
        return;
      }
      const closeOpenSubmenus = () => {
        document.querySelectorAll(".admin-nav .nav-item.has-submenu.submenu-open").forEach((item) => {
          item.classList.remove("submenu-open");
        });
      };
      const suppressSubmenus = () => {
        if (typeof window.suppressNavigationSubmenus === "function") {
          window.suppressNavigationSubmenus(document, ownerTab);
          return;
        }
        closeOpenSubmenus();
      };
      const releaseSubmenuSuppression = () => {
        if (typeof window.releaseNavigationSubmenuSuppression === "function") {
          window.releaseNavigationSubmenuSuppression();
        }
      };
      const isDirectNavDisabled = tabHolder?.classList?.contains("nav-item") && tabHolder.classList.contains("has-submenu") && tabHolder.dataset.disableDirectNav === "true";
      const isMobileNavigationViewport = typeof window.isAppMobileNavigationViewport === "function" ? window.isAppMobileNavigationViewport() : typeof window.matchMedia === "function" ? window.matchMedia("(max-width: 900px)").matches || document.body.classList.contains("compact-nav-mode") : window.innerWidth <= 900 || document.body.classList.contains("compact-nav-mode");
      const isMobileSubmenuToggle = isMobileNavigationViewport && tabHolder?.classList?.contains("nav-item") && tabHolder.classList.contains("has-submenu");
      if (isDirectNavDisabled || isMobileSubmenuToggle) {
        releaseSubmenuSuppression();
        const shouldOpen = !tabHolder.classList.contains("submenu-open");
        closeOpenSubmenus();
        event.preventDefault();
        event.stopPropagation();
        if (shouldOpen) {
          tabHolder.classList.add("submenu-open");
        }
        return;
      }
      suppressSubmenus();
      event.preventDefault();
      event.stopPropagation();
      if (isMobileNavigationViewport && typeof window.closeMobileNavigation === "function") {
        window.closeMobileNavigation();
      }
      openTab(tabName, null, { scrollMode: "carry" });
    }, true);
    document.addEventListener("click", (event) => {
      if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
        return;
      }
      const link = event.target.closest("a[href]");
      if (!link || link.target || link.hasAttribute("download")) {
        return;
      }
      let targetUrl;
      try {
        targetUrl = new URL(link.href, window.location.href);
      } catch (error) {
        return;
      }
      if (targetUrl.origin !== window.location.origin) {
        return;
      }
      const nextHistoryEntry = getAdminHistoryEntryFromLocation(targetUrl.pathname, targetUrl.search);
      if (!nextHistoryEntry) {
        return;
      }
      event.preventDefault();
      const scrollTargetSelector = link.dataset.scrollTargetSelector || "";
      openTab(nextHistoryEntry.tab, nextHistoryEntry.id, {
        scrollMode: scrollTargetSelector ? "restore" : "carry",
        scrollTargetSelector
      });
    });
    syncBrowserHistory(initialHistoryEntry, "replace");
    window.addEventListener("popstate", () => {
      const nextHistoryEntry = window.history.state?.tab ? buildAdminHistoryEntry(window.history.state.tab, window.history.state.id) : getAdminHistoryEntryFromLocation(window.location.pathname, window.location.search);
      if (nextHistoryEntry) {
        openTab(nextHistoryEntry.tab, nextHistoryEntry.id, {
          historyMode: "none",
          force: true,
          scrollMode: "restore"
        });
      }
    });
    if (!rootElement) {
      hydrateFetchedContentState();
      schedulePostContentHooks();
      window.setTimeout(() => {
        if (initialHistoryEntry?.tab === "add_survey" || initialHistoryEntry?.tab === "update_survey") {
          setActiveTabAndRefreshNav("get_surveys");
          syncBrowserHistory(buildAdminHistoryEntry("get_surveys"), "replace");
        } else if (initialHistoryEntry?.tab === "update_archived_survey") {
          setActiveTabAndRefreshNav("archived_surveys");
          syncBrowserHistory(buildAdminHistoryEntry("archived_surveys"), "replace");
        } else if (initialHistoryEntry?.tab === "add_user") {
          setActiveTabAndRefreshNav("get_users");
          syncBrowserHistory(buildAdminHistoryEntry("get_users"), "replace");
        } else if (initialHistoryEntry?.tab === "add_organization") {
          setActiveTabAndRefreshNav("get_organization");
          syncBrowserHistory(buildAdminHistoryEntry("get_organization"), "replace");
        }
        remountChrome();
      }, 0);
      return;
    }
    if (initialHistoryEntry?.tab && initialHistoryEntry.tab !== "get_surveys") {
      window.setTimeout(() => {
        openTab(initialHistoryEntry.tab, initialHistoryEntry.id, {
          historyMode: "replace",
          force: true,
          scrollMode: "restore"
        });
      }, 0);
      return;
    }
    openTab("get_surveys", initialHistoryEntry?.id ?? null, { historyMode: "replace", force: true, scrollMode: "restore" });
  })();
})();
//# sourceMappingURL=admin-inline-app.js.map
