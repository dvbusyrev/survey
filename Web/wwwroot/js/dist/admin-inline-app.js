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

  // Web/wwwroot/js/features/admin/admin-inline-pages.js
  (() => {
    const adminInlineAppPages = window.AdminInlineAppPages || (window.AdminInlineAppPages = {});
    adminInlineAppPages.mountExtensionModal = function mountExtensionModal(host, options = {}) {
      if (!host) {
        return null;
      }
      const {
        survey,
        onClose,
        submitButton: externalSubmitButton = null,
        cancelButton: externalCancelButton = null
      } = options;
      const closeModal = typeof onClose === "function" ? onClose : () => {
      };
      const hasExternalActions = Boolean(externalSubmitButton || externalCancelButton);
      let disposed = false;
      let organizations = [];
      let loading = true;
      let error = "";
      let extension = { organizationIds: [], extendedUntil: "" };
      let isOrganizationPanelOpen = false;
      const today = window.AppDate?.todayIso?.() || (/* @__PURE__ */ new Date()).toISOString().split("T")[0];
      const minEndDate = (() => {
        const date = /* @__PURE__ */ new Date();
        date.setDate(date.getDate() + 1);
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, "0");
        const day = String(date.getDate()).padStart(2, "0");
        return `${year}-${month}-${day}`;
      })();
      const isFormValid = () => {
        return Boolean(
          extension.organizationIds.length > 0 && extension.extendedUntil && window.AppDate?.compare(extension.extendedUntil, today) > 0
        );
      };
      const handleChange = (field, value) => {
        extension = {
          ...extension,
          [field]: value
        };
        render();
      };
      const toggleOrganization = (organizationId, isSelected) => {
        const normalizedId = String(organizationId || "");
        if (!normalizedId) {
          return;
        }
        const currentIds = new Set(extension.organizationIds);
        if (isSelected) {
          currentIds.add(normalizedId);
        } else {
          currentIds.delete(normalizedId);
        }
        extension = {
          ...extension,
          organizationIds: Array.from(currentIds)
        };
        isOrganizationPanelOpen = true;
        render();
      };
      const closeOrganizationPanel = () => {
        if (!isOrganizationPanelOpen || disposed) {
          return;
        }
        isOrganizationPanelOpen = false;
        render();
      };
      const handleDocumentPointerDown = (event) => {
        if (!host.contains(event.target)) {
          closeOrganizationPanel();
        }
      };
      const handleDocumentKeyDown = (event) => {
        if (event.key === "Escape") {
          closeOrganizationPanel();
        }
      };
      const updateCheckboxListHeight = (container) => {
        const list = container?.querySelector(".app-checkbox-list");
        if (!list) {
          return;
        }
        const listTop = list.getBoundingClientRect().top;
        const availableHeight = Math.max(160, window.innerHeight - listTop - 24);
        list.style.setProperty("--app-checkbox-list-max-height", `${availableHeight}px`);
      };
      const scheduleCheckboxListHeightUpdate = (container) => {
        window.requestAnimationFrame(() => updateCheckboxListHeight(container));
      };
      const handleSubmit = async () => {
        if (extension.organizationIds.length === 0 || !extension.extendedUntil) {
          window.siteNotify?.("Пожалуйста, заполните все поля.", "error");
          return;
        }
        if ((window.AppDate?.compare(extension.extendedUntil, today) ?? -1) <= 0) {
          window.siteNotify?.("Дата конца должна быть в будущем.", "error");
          return;
        }
        try {
          const response = await fetch("/survey-extensions", {
            method: "POST",
            headers: {
              "Content-Type": "application/json",
              "RequestVerificationToken": document.querySelector('input[name="__RequestVerificationToken"]')?.value || ""
            },
            body: JSON.stringify({
              surveyId: survey?.id_survey,
              extensions: extension.organizationIds.map((organizationId) => ({
                organizationId: parseInt(organizationId, 10),
                extendedUntil: extension.extendedUntil
              }))
            })
          });
          const responseText = await response.text();
          let responseData = null;
          try {
            responseData = JSON.parse(responseText);
          } catch (parseError) {
            console.error("Не удалось разобрать ответ сервера:", parseError);
          }
          if (!response.ok || !responseData?.success) {
            const validationErrors = Array.isArray(responseData?.errors) ? responseData.errors.filter(Boolean).join("\n") : "";
            throw new Error(
              validationErrors || responseData?.error || responseData?.message || responseText || (window.getResponseErrorMessage ? window.getResponseErrorMessage(response, "Ошибка продления") : `Ошибка продления: ${response.status}`)
            );
          }
          closeModal();
          if (typeof window.handleAdminMutationSuccess === "function") {
            await window.handleAdminMutationSuccess({
              message: responseData.message || "Доступ успешно продлён.",
              tabName: typeof window.resolveCurrentAdminTab === "function" ? window.resolveCurrentAdminTab() : "get_surveys",
              fallbackUrl: window.location.pathname
            });
            return;
          }
          window.siteNotify?.(responseData.message || "Доступ успешно продлён.", "success");
          window.location.reload();
        } catch (submitError) {
          console.error("Ошибка продления анкеты:", submitError);
          window.siteNotify?.(submitError.message || "Не удалось продлить доступ.", "error");
        }
      };
      const render = () => {
        if (disposed) {
          return;
        }
        const template = document.getElementById("admin-extension-modal-template");
        const rowTemplate = document.getElementById("admin-extension-modal-row-template");
        if (!host || !template?.content?.firstElementChild || !rowTemplate?.content?.firstElementChild) {
          return;
        }
        host.replaceChildren();
        const root = template.content.firstElementChild.cloneNode(true);
        root.classList.toggle("admin-extension-modal-root--external-actions", hasExternalActions);
        const surveyName = root.querySelector('[data-role="survey-name"]');
        const errorNode = root.querySelector('[data-role="error"]');
        const rowsContainer = root.querySelector('[data-role="rows-container"]');
        const emptyState = root.querySelector('[data-role="empty-state"]');
        const submitButton = externalSubmitButton || root.querySelector('[data-role="submit"]');
        const cancelButton = externalCancelButton || root.querySelector('[data-role="cancel"]');
        if (surveyName) {
          surveyName.textContent = `Анкета: "${survey?.name_survey || ""}"`;
        }
        if (errorNode) {
          errorNode.textContent = error || "";
          errorNode.style.display = error ? "block" : "none";
        }
        const showRows = !loading && organizations.length > 0;
        if (rowsContainer) {
          rowsContainer.style.display = showRows ? "" : "none";
        }
        if (emptyState) {
          emptyState.style.display = !loading && !error && organizations.length === 0 ? "" : "none";
        }
        if (showRows && rowsContainer) {
          const row = rowTemplate.content.firstElementChild.cloneNode(true);
          const organizationTrigger = row.querySelector('[data-role="organization-trigger"]');
          const organizationLabel = row.querySelector('[data-role="organization-label"]');
          const organizationPanel = row.querySelector('[data-role="organization-panel"]');
          const organizationOptions = row.querySelector('[data-role="organization-options"]');
          const dateInput = row.querySelector('[data-role="date-input"]');
          const selectedOrganizationIds = new Set(extension.organizationIds);
          if (organizationTrigger) {
            organizationTrigger.setAttribute("aria-expanded", isOrganizationPanelOpen ? "true" : "false");
            organizationTrigger.addEventListener("click", (event) => {
              event.preventDefault();
              isOrganizationPanelOpen = !isOrganizationPanelOpen;
              render();
            });
          }
          if (organizationLabel) {
            const selectedOrganizations = organizations.filter((organization) => selectedOrganizationIds.has(organization.organizationId));
            organizationLabel.textContent = selectedOrganizations.length === 0 ? "Выберите организации" : selectedOrganizations.length === 1 ? selectedOrganizations[0].organizationName : `Выбрано: ${selectedOrganizations.length}`;
          }
          if (organizationPanel) {
            organizationPanel.classList.toggle("is-hidden", !isOrganizationPanelOpen);
          }
          if (organizationOptions) {
            organizations.forEach((organization) => {
              const optionLabel = document.createElement("label");
              const checkbox = document.createElement("input");
              const labelText = document.createElement("span");
              const isSelected = selectedOrganizationIds.has(organization.organizationId);
              optionLabel.className = "app-checkbox-option";
              optionLabel.classList.toggle("is-selected", isSelected);
              optionLabel.setAttribute("role", "option");
              optionLabel.setAttribute("aria-selected", isSelected ? "true" : "false");
              checkbox.type = "checkbox";
              checkbox.className = "app-checkbox-input";
              checkbox.checked = isSelected;
              checkbox.value = organization.organizationId;
              checkbox.addEventListener("change", (event) => {
                toggleOrganization(organization.organizationId, event.target.checked);
              });
              labelText.className = "app-checkbox-text";
              labelText.textContent = organization.organizationName;
              optionLabel.appendChild(checkbox);
              optionLabel.appendChild(labelText);
              organizationOptions.appendChild(optionLabel);
            });
          }
          if (dateInput) {
            dateInput.dataset.dateMin = minEndDate;
            dateInput.min = minEndDate;
            dateInput.value = extension.extendedUntil;
            if (window.AppDate?.enhanceDateInputs) {
              window.AppDate.enhanceDateInputs(row);
            }
            if (window.AppDate?.setInputValue) {
              window.AppDate.setInputValue(dateInput, extension.extendedUntil);
            } else {
              dateInput.value = extension.extendedUntil;
            }
            dateInput.addEventListener("change", (event) => {
              handleChange("extendedUntil", window.AppDate?.getInputIso(event.target) || event.target.value);
            });
          }
          rowsContainer.appendChild(row);
        }
        if (submitButton) {
          submitButton.disabled = !isFormValid() || loading;
          submitButton.textContent = loading ? "Обработка..." : "Продлить доступ";
          submitButton.style.removeProperty("background-color");
          submitButton.style.cursor = isFormValid() ? "pointer" : "not-allowed";
          submitButton.style.opacity = isFormValid() ? "1" : "0.6";
          submitButton.onclick = handleSubmit;
        }
        if (cancelButton) {
          cancelButton.onclick = closeModal;
        }
        host.appendChild(root);
        if (isOrganizationPanelOpen) {
          scheduleCheckboxListHeightUpdate(root);
        }
      };
      const fetchOrganizations = async () => {
        try {
          loading = true;
          render();
          const response = await fetch("/organizations/data");
          if (!response.ok) {
            throw new Error(
              window.getResponseErrorMessage ? window.getResponseErrorMessage(response, "Не удалось загрузить организации") : `Не удалось загрузить организации: ${response.status}`
            );
          }
          const data = await response.json();
          organizations = Array.isArray(data) ? data.filter((org) => org && (org.id_organization !== void 0 || org.id !== void 0)).map((org) => ({
            organizationId: String(org.id_organization ?? org.id),
            organizationName: String(org.organization_name ?? org.name ?? "")
          })).filter((org) => org.organizationName) : [];
          error = "";
        } catch (fetchError) {
          console.error("Ошибка загрузки организаций:", fetchError);
          error = fetchError.message || "Не удалось загрузить список организаций";
        } finally {
          loading = false;
          render();
        }
      };
      document.addEventListener("pointerdown", handleDocumentPointerDown, true);
      document.addEventListener("keydown", handleDocumentKeyDown);
      render();
      fetchOrganizations();
      return () => {
        disposed = true;
        document.removeEventListener("pointerdown", handleDocumentPointerDown, true);
        document.removeEventListener("keydown", handleDocumentKeyDown);
        if (externalSubmitButton) {
          externalSubmitButton.onclick = null;
          externalSubmitButton.disabled = true;
          externalSubmitButton.style.removeProperty("background-color");
          externalSubmitButton.style.removeProperty("cursor");
          externalSubmitButton.style.removeProperty("opacity");
        }
        if (externalCancelButton) {
          externalCancelButton.onclick = null;
        }
        host.replaceChildren();
      };
    };
    adminInlineAppPages.mountStatisticsPage = function mountStatisticsPage(host) {
      if (!host) {
        return null;
      }
      let disposed = false;
      let chartsData = null;
      let loading = true;
      let error = "";
      const chartRefs = {
        line: null,
        bar: null,
        radar: null
      };
      const chartInstances = {
        line: null,
        bar: null,
        radar: null
      };
      const destroyCharts = () => {
        Object.values(chartInstances).forEach((chart) => {
          if (chart) {
            chart.destroy();
          }
        });
        chartInstances.line = null;
        chartInstances.bar = null;
        chartInstances.radar = null;
      };
      const renderCharts = () => {
        if (loading || error || !chartsData) {
          return;
        }
        if (typeof Chart === "undefined") {
          error = "Chart.js не загружен.";
          render();
          return;
        }
        destroyCharts();
        const yearGuideLinePlugin = {
          id: "adminStatisticsYearGuideLine",
          beforeDatasetsDraw(chart, _args, options) {
            const yScale = chart.scales.y;
            const meta = chart.getDatasetMeta(0);
            if (!yScale || !meta || meta.hidden) {
              return;
            }
            const startY = yScale.getPixelForValue(0);
            const color = options?.color || "rgba(79, 70, 229, 0.25)";
            const lineWidth = options?.lineWidth || 2;
            chart.ctx.save();
            chart.ctx.strokeStyle = color;
            chart.ctx.lineWidth = lineWidth;
            meta.data.forEach((point) => {
              if (!point || point.skip) {
                return;
              }
              chart.ctx.beginPath();
              chart.ctx.moveTo(point.x, startY);
              chart.ctx.lineTo(point.x, point.y);
              chart.ctx.stroke();
            });
            chart.ctx.restore();
          }
        };
        const getScoreScale = () => ({
          type: "linear",
          min: 0,
          max: 5,
          ticks: {
            stepSize: 1
          },
          title: {
            display: true,
            text: "Средняя оценка"
          }
        });
        const buildCommonOptions = (showLegend) => ({
          responsive: true,
          maintainAspectRatio: false,
          plugins: {
            legend: {
              display: Boolean(showLegend),
              position: "bottom",
              labels: {
                padding: 14,
                boxWidth: 12,
                font: {
                  size: 12
                }
              }
            },
            tooltip: {
              callbacks: {
                label(context) {
                  const value = context.parsed?.y ?? context.parsed?.x ?? context.parsed;
                  const numericValue = Number(value);
                  if (Number.isFinite(numericValue)) {
                    return `${context.dataset.label || "Средняя оценка"}: ${numericValue.toFixed(2)}`;
                  }
                  return context.dataset.label || "";
                }
              }
            }
          },
          layout: {
            padding: {
              top: 10,
              bottom: showLegend ? 20 : 10
            }
          }
        });
        if (chartRefs.line && chartsData.lineChart) {
          const yearLabels = chartsData.lineChart.labels || [];
          const yearData = chartsData.lineChart.data || [];
          chartInstances.line = new Chart(chartRefs.line, {
            type: "line",
            data: {
              labels: yearLabels,
              datasets: [{
                label: chartsData.lineChart.label || "Средняя оценка",
                data: yearData,
                borderColor: "rgb(79, 70, 229)",
                backgroundColor: "rgb(79, 70, 229)",
                borderWidth: 2,
                pointRadius: 4,
                pointHoverRadius: 6,
                tension: 0.2
              }]
            },
            options: {
              ...buildCommonOptions(false),
              scales: {
                x: {
                  grid: {
                    display: false
                  }
                },
                y: getScoreScale()
              },
              plugins: {
                ...buildCommonOptions(false).plugins,
                adminStatisticsYearGuideLine: {
                  color: "rgba(79, 70, 229, 0.32)",
                  lineWidth: 2
                }
              }
            },
            plugins: [yearGuideLinePlugin]
          });
        }
        if (chartRefs.bar && chartsData.barChart) {
          chartInstances.bar = new Chart(chartRefs.bar, {
            type: "bar",
            data: {
              labels: chartsData.barChart.labels || [],
              datasets: [{
                label: chartsData.barChart.label || "Средняя оценка",
                data: chartsData.barChart.data || [],
                backgroundColor: "rgba(14, 165, 233, 0.72)",
                borderColor: "rgb(14, 165, 233)",
                borderWidth: 1
              }]
            },
            options: {
              ...buildCommonOptions(false),
              scales: {
                x: {
                  grid: {
                    display: false
                  }
                },
                y: getScoreScale()
              }
            }
          });
        }
        if (chartRefs.radar && chartsData.avgScoreByOrganizationRadar) {
          chartInstances.radar = new Chart(chartRefs.radar, {
            type: "bar",
            data: {
              labels: chartsData.avgScoreByOrganizationRadar.labels || [],
              datasets: (chartsData.avgScoreByOrganizationRadar.datasets || []).map((dataset) => ({
                ...dataset,
                grouped: false,
                borderWidth: 1,
                barPercentage: 0.78,
                categoryPercentage: 0.92
              }))
            },
            options: {
              ...buildCommonOptions(true),
              scales: {
                x: {
                  ticks: {
                    display: false
                  },
                  grid: {
                    display: false
                  }
                },
                y: getScoreScale()
              },
              plugins: {
                ...buildCommonOptions(true).plugins,
                tooltip: {
                  callbacks: {
                    title(items) {
                      return items[0]?.dataset?.label || "";
                    },
                    label(context) {
                      const value = Number(context.parsed?.y || 0);
                      return `Средняя оценка: ${value.toFixed(2)}`;
                    }
                  }
                }
              }
            }
          });
        }
      };
      const render = () => {
        if (disposed) {
          return;
        }
        host.innerHTML = "";
        if (loading) {
          const loadingNode = document.createElement("div");
          loadingNode.className = "loading";
          loadingNode.textContent = "Загрузка данных...";
          host.appendChild(loadingNode);
          return;
        }
        if (error) {
          const errorNode = document.createElement("div");
          errorNode.className = "error";
          errorNode.textContent = `Ошибка: ${error}`;
          host.appendChild(errorNode);
          return;
        }
        const template = document.getElementById("admin-statistics-template");
        if (!template?.content?.firstElementChild) {
          return;
        }
        const root = template.content.firstElementChild.cloneNode(true);
        chartRefs.line = root.querySelector('[data-role="line-chart"]');
        chartRefs.bar = root.querySelector('[data-role="bar-chart"]');
        chartRefs.radar = root.querySelector('[data-role="radar-chart"]');
        host.appendChild(root);
        renderCharts();
      };
      const loadData = async () => {
        try {
          await fetch("/statistics");
          const response = await fetch("/statistics/data");
          if (!response.ok) {
            throw new Error(
              window.getResponseErrorMessage ? window.getResponseErrorMessage(response, "Ошибка загрузки статистики") : "Ошибка загрузки статистики"
            );
          }
          chartsData = await response.json();
        } catch (loadError) {
          console.error("Ошибка загрузки статистики:", loadError);
          error = loadError.message || "Не удалось загрузить данные статистики.";
        } finally {
          loading = false;
          render();
        }
      };
      render();
      loadData();
      return () => {
        disposed = true;
        destroyCharts();
        host.innerHTML = "";
      };
    };
    function getEmailField(id) {
      return document.getElementById(id);
    }
    function getEmailTrimmedValue(id) {
      return (getEmailField(id)?.value || "").trim();
    }
    function splitEmailRecipients(value) {
      return String(value || "").split(/[;,\r\n]+/).map((item) => item.trim()).filter(Boolean);
    }
    function isValidEmailAddress(email) {
      const value = String(email || "").trim();
      if (!value) {
        return false;
      }
      return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
    }
    function setEmailInvalidState(id, isInvalid) {
      const element = getEmailField(id);
      if (!element) {
        return;
      }
      element.classList.toggle("invalid", Boolean(isInvalid));
      element.setAttribute("aria-invalid", isInvalid ? "true" : "false");
    }
    function clearEmailInvalidStates() {
      [
        "email-to",
        "email-subject",
        "email-content",
        "email-smtp-host",
        "email-smtp-port",
        "email-smtp-user-name",
        "email-smtp-password",
        "email-from-address",
        "email-from-display-name"
      ].forEach((id) => setEmailInvalidState(id, false));
    }
    function collectEmailSettingsPayload() {
      const smtpPortValue = Number.parseInt(getEmailField("email-smtp-port")?.value || "", 10);
      return {
        to: getEmailTrimmedValue("email-to"),
        subject: getEmailTrimmedValue("email-subject"),
        content: (getEmailField("email-content")?.value || "").trim(),
        smtpHost: getEmailTrimmedValue("email-smtp-host"),
        smtpPort: Number.isFinite(smtpPortValue) ? smtpPortValue : 0,
        smtpEnableSsl: (getEmailField("email-smtp-enable-ssl")?.value || "true") === "true",
        smtpUserName: getEmailTrimmedValue("email-smtp-user-name"),
        smtpPassword: getEmailField("email-smtp-password")?.value || "",
        fromAddress: getEmailTrimmedValue("email-from-address"),
        fromDisplayName: getEmailTrimmedValue("email-from-display-name")
      };
    }
    function validateEmailSettingsPayload(settings) {
      clearEmailInvalidStates();
      const errors = [];
      const recipients = splitEmailRecipients(settings.to);
      if (recipients.length === 0) {
        errors.push("Поле «Кому» должно содержать хотя бы один email.");
        setEmailInvalidState("email-to", true);
      } else {
        const invalidRecipients = recipients.filter((email) => !isValidEmailAddress(email));
        if (invalidRecipients.length > 0) {
          errors.push(`Поле «Кому» содержит некорректные email: ${invalidRecipients.join(", ")}.`);
          setEmailInvalidState("email-to", true);
        }
      }
      if (!settings.subject) {
        errors.push("Поле «Тема» обязательно.");
        setEmailInvalidState("email-subject", true);
      }
      if (!settings.content) {
        errors.push("Поле «Содержание» обязательно.");
        setEmailInvalidState("email-content", true);
      }
      if (!settings.smtpHost) {
        errors.push("Поле «SMTP сервер» обязательно.");
        setEmailInvalidState("email-smtp-host", true);
      }
      if (!Number.isInteger(settings.smtpPort) || settings.smtpPort < 1 || settings.smtpPort > 65535) {
        errors.push("Поле «Порт SMTP» должно быть числом от 1 до 65535.");
        setEmailInvalidState("email-smtp-port", true);
      }
      if (!isValidEmailAddress(settings.fromAddress)) {
        errors.push("Поле «Email отправителя» заполнено некорректно.");
        setEmailInvalidState("email-from-address", true);
      }
      const hasUserName = Boolean(settings.smtpUserName);
      const hasPassword = Boolean(settings.smtpPassword);
      if (hasUserName !== hasPassword) {
        errors.push("Логин SMTP и пароль SMTP должны быть заполнены вместе.");
        setEmailInvalidState("email-smtp-user-name", true);
        setEmailInvalidState("email-smtp-password", true);
      }
      return errors;
    }
    async function extractEmailApiErrors(response) {
      const fallbackMessage = typeof window.getResponseErrorMessage === "function" ? window.getResponseErrorMessage(response, "Ошибка") : "Не удалось выполнить запрос.";
      const responseText = await response.text();
      if (!responseText) {
        return [fallbackMessage];
      }
      try {
        const payload = JSON.parse(responseText);
        if (Array.isArray(payload?.errors) && payload.errors.length > 0) {
          return payload.errors.filter(Boolean);
        }
        if (payload?.error) {
          return [payload.error];
        }
        if (payload?.message) {
          return [payload.message];
        }
      } catch (error) {
        return [responseText];
      }
      return [fallbackMessage];
    }
    function showEmailToast(message, type, title, options = {}) {
      const normalizedMessage = String(message || "").trim();
      if (!normalizedMessage) {
        return;
      }
      if (typeof window.siteNotify === "function") {
        window.siteNotify(normalizedMessage, type, {
          title,
          duration: options.duration ?? (type === "error" ? 0 : 4500)
        });
        return;
      }
      window.alert(normalizedMessage);
    }
    function showEmailValidationErrors(errors) {
      const normalizedErrors = (Array.isArray(errors) ? errors : [errors]).map((item) => String(item || "").trim()).filter(Boolean);
      if (normalizedErrors.length === 0) {
        return;
      }
      showEmailToast(normalizedErrors.join(" • "), "error", "Проверьте поля", { duration: 0 });
    }
    function setEmailButtonsBusy(isBusy, options = {}) {
      const activeButtonId = options.activeButtonId || "";
      const busyLabel = options.busyLabel || "";
      document.querySelectorAll(".email-settings-page__actions button").forEach((button) => {
        button.disabled = isBusy;
        if (!button.dataset.defaultLabel) {
          button.dataset.defaultLabel = button.textContent || "";
        }
        if (isBusy) {
          button.textContent = activeButtonId && button.id === activeButtonId ? busyLabel || button.dataset.defaultLabel || button.textContent : button.dataset.defaultLabel || button.textContent;
          return;
        }
        button.textContent = button.dataset.defaultLabel || button.textContent;
      });
    }
    async function submitEmailSettings(url, options) {
      const settings = collectEmailSettingsPayload();
      const validationErrors = validateEmailSettingsPayload(settings);
      if (validationErrors.length > 0) {
        showEmailValidationErrors(validationErrors);
        return false;
      }
      setEmailButtonsBusy(true, {
        activeButtonId: options.busyButtonId,
        busyLabel: options.busyLabel
      });
      try {
        const response = await fetch(url, {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "Accept": "application/json"
          },
          body: JSON.stringify(settings)
        });
        if (!response.ok) {
          throw new Error((await extractEmailApiErrors(response)).join(" "));
        }
        const payload = await response.json();
        clearEmailInvalidStates();
        showEmailToast(
          payload?.message || options.successMessage,
          "success",
          options.successTitle
        );
        return true;
      } catch (error) {
        showEmailToast(
          error.message || options.errorMessage || "Не удалось выполнить операцию.",
          "error",
          options.errorTitle,
          { duration: 0 }
        );
        return false;
      } finally {
        setEmailButtonsBusy(false);
      }
    }
    window.saveEmailSettings = function saveEmailSettings() {
      return submitEmailSettings("/mail/settings", {
        busyButtonId: "email-save-button",
        busyLabel: "Сохранение...",
        successTitle: "Настройки сохранены",
        successMessage: "Настройки электронной почты сохранены.",
        errorTitle: "Сохранение не выполнено",
        errorMessage: "Не удалось сохранить настройки."
      });
    };
    window.sendEmailMessage = function sendEmailMessage() {
      return submitEmailSettings("/mail/send", {
        busyButtonId: "email-send-button",
        busyLabel: "Отправка...",
        successTitle: "Письмо отправлено",
        successMessage: "Письмо отправлено.",
        errorTitle: "Письмо не отправлено",
        errorMessage: "Не удалось отправить письмо."
      });
    };
    function bindEmailAction(buttonId, action) {
      const button = document.getElementById(buttonId);
      if (!button || button.dataset.emailActionBound === "true") {
        return;
      }
      button.dataset.emailActionBound = "true";
      button.addEventListener("click", (event) => {
        event.preventDefault();
        event.stopPropagation();
        action();
      });
    }
    window.initEmailSettingsPage = function initEmailSettingsPage() {
      bindEmailAction("email-save-button", window.saveEmailSettings);
      bindEmailAction("email-send-button", window.sendEmailMessage);
    };
  })();

  // Web/wwwroot/js/features/admin/admin-inline-core.js
  (() => {
    const DETACHED_CONTENT_HOST_ID = "admin-inline-detached-content";
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
          return buildQueryHistoryEntry(tab, "/surveys", id, { preserveCurrentWhenMissing: id === void 0 });
        case "list_answers_users":
          return buildQueryHistoryEntry(tab, "/surveys/answers", id, { preserveCurrentWhenMissing: id === void 0 });
        case "archived_surveys":
          return buildQueryHistoryEntry(tab, "/surveys/archive", id, { preserveCurrentWhenMissing: id === void 0 });
        case "get_survey_signatures":
          return surveyId ? { tab, id: surveyId, url: `/surveys/${surveyId}/signatures` } : null;
        case "add_survey":
          return { tab, id: null, url: "/surveys/create" };
        case "copy_survey":
          return surveyId ? { tab, id: surveyId, url: `/surveys/${surveyId}/copy` } : null;
        case "update_survey":
          return surveyId ? { tab, id: surveyId, url: `/surveys/${surveyId}/edit` } : null;
        case "update_archived_survey":
          return surveyId ? { tab, id: surveyId, url: `/surveys/archive/${surveyId}/edit` } : null;
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
          return { tab, id: null, url: "/organizations/surveys" };
        case "add_organization":
          return { tab, id: null, url: "/organizations/create" };
        case "update_organization":
          return organizationId ? { tab, id: organizationId, url: `/organizations/${organizationId}/edit` } : null;
        case "archive_list_organizations":
          return buildQueryHistoryEntry(tab, "/organizations/archive", id, { preserveCurrentWhenMissing: id === void 0 });
        case "reports":
          return { tab, id: null, url: "/reports" };
        case "survey_auto_creation":
          return { tab, id: null, url: "/survey-auto-creation" };
        case "get_logs":
          return buildQueryHistoryEntry(tab, "/event-log", id, { preserveCurrentWhenMissing: id === void 0 });
        case "email":
        case "email_new":
          return { tab: tab === "email" ? "email_new" : tab, id: null, url: "/mail" };
        case "email_settings":
          return { tab, id: null, url: "/mail/configuration" };
        case "help":
          return { tab, id: null, url: "/help" };
        default:
          return null;
      }
    }
    function getAdminHistoryEntryFromLocation(pathname, search = "") {
      const normalizedPath = normalizePathname(pathname);
      if (normalizedPath === "/surveys") {
        return buildAdminHistoryEntry("get_surveys", search || "");
      }
      if (normalizedPath === "/surveys/answers") {
        return buildAdminHistoryEntry("list_answers_users", search || "");
      }
      if (normalizedPath === "/surveys/archive") {
        return buildAdminHistoryEntry("archived_surveys", search || "");
      }
      if (normalizedPath === "/surveys/create") {
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
      if (normalizedPath === "/organizations/surveys") {
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
      if (normalizedPath === "/survey-auto-creation") {
        return buildAdminHistoryEntry("survey_auto_creation");
      }
      if (normalizedPath === "/event-log") {
        return buildAdminHistoryEntry("get_logs", search || "");
      }
      if (normalizedPath === "/mail" || normalizedPath === "/mail/new") {
        return buildAdminHistoryEntry("email_new");
      }
      if (normalizedPath === "/mail/configuration" || normalizedPath === "/mail-settings") {
        return buildAdminHistoryEntry("email_settings");
      }
      if (normalizedPath === "/help") {
        return buildAdminHistoryEntry("help");
      }
      let match = normalizedPath.match(/^\/surveys\/(\d+)\/signatures$/);
      if (match) {
        return buildAdminHistoryEntry("get_survey_signatures", Number(match[1]));
      }
      match = normalizedPath.match(/^\/surveys\/archive\/(\d+)\/edit$/);
      if (match) {
        return buildAdminHistoryEntry("update_archived_survey", Number(match[1]));
      }
      match = normalizedPath.match(/^\/surveys\/(\d+)\/edit$/);
      if (match) {
        return buildAdminHistoryEntry("update_survey", Number(match[1]));
      }
      match = normalizedPath.match(/^\/surveys\/(\d+)\/copy$/);
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
    function createClosedModalState() {
      return {
        isOpen: false,
        content: "",
        data: null,
        message: null,
        isSuccess: false
      };
    }
    function getRequestVerificationToken() {
      return document.querySelector('input[name="__RequestVerificationToken"]')?.value || "";
    }
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
    function isStylesheetLoaded(href) {
      return Array.from(document.querySelectorAll('link[rel="stylesheet"][href]')).some((link) => {
        return normalizeAssetUrl(link.href) === href;
      });
    }
    function isScriptLoaded(src) {
      return Array.from(document.querySelectorAll("script[src]")).some((script) => {
        return normalizeAssetUrl(script.src) === src;
      });
    }
    function loadStylesheetsFromDocument(parsedDocument) {
      parsedDocument.querySelectorAll('link[rel="stylesheet"][href]').forEach((sourceLink) => {
        const href = normalizeAssetUrl(sourceLink.getAttribute("href"));
        if (!href || isStylesheetLoaded(href)) {
          return;
        }
        const link = document.createElement("link");
        link.rel = "stylesheet";
        link.href = href;
        if (sourceLink.media) {
          link.media = sourceLink.media;
        }
        document.head.appendChild(link);
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
      const scriptSources = Array.from(parsedDocument.querySelectorAll("script[src]")).map((script) => normalizeAssetUrl(script.getAttribute("src"))).filter(Boolean).filter((src, index, list) => list.indexOf(src) === index);
      for (const src of scriptSources) {
        if (isScriptLoaded(src)) {
          continue;
        }
        await loadScriptAsset(src);
      }
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
      if (["global-antiforgery-token", "layout-chrome-context", "chrome-context", "chrome-header", "chrome-navigation", "chrome-footer", "root", DETACHED_CONTENT_HOST_ID].includes(element.id)) {
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
      return contentHost ? Array.from(contentHost.childNodes) : Array.from(sourceDocument.body.childNodes);
    }
    function getDetachedRenderableNodes(sourceDocument) {
      const contentHost = sourceDocument.getElementById("content_admin");
      if (!contentHost) {
        return [];
      }
      return Array.from(sourceDocument.body.childNodes).filter((node) => {
        return node !== contentHost && !shouldSkipFetchedNode(node) && !(node.nodeType === Node.ELEMENT_NODE && node.querySelector?.("#content_admin"));
      });
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
    const availablePages = window.AdminInlineAppPages || {};
    const mountExtensionModal = availablePages.mountExtensionModal;
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
      modal: createClosedModalState()
    };
    let contentCleanup = null;
    let modalCleanup = null;
    let headerCleanup = null;
    let navCleanup = null;
    let footerCleanup = null;
    let loaderTimer = null;
    let initTogglesTimer = null;
    let initEditTimer = null;
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
    const modalBodyHost = document.createElement("div");
    modalBodyHost.className = "modal-body";
    modalClose.appendChild(modalIcon);
    modalContent.appendChild(modalClose);
    modalContent.appendChild(modalBodyHost);
    modalNode.appendChild(modalContent);
    pageContainer.appendChild(modalNode);
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
    const remountChrome = () => {
      if (typeof headerCleanup === "function") {
        headerCleanup();
      }
      if (typeof navCleanup === "function") {
        navCleanup();
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
      navCleanup = typeof window.mountNavigation === "function" ? window.mountNavigation(navHost, {
        openTab,
        activeTab: state.activeTab,
        userRole: initialData.userRole,
        userId: initialData.userId
      }) : null;
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
      state.modal = createClosedModalState();
      renderModal();
    };
    const setModal = (nextModal) => {
      state.modal = nextModal;
      renderModal();
    };
    const schedulePostContentHooks = () => {
      if (initTogglesTimer) {
        window.clearTimeout(initTogglesTimer);
      }
      initTogglesTimer = window.setTimeout(() => {
        if (window.initPasswordToggles) {
          window.initPasswordToggles(document);
        }
      }, 0);
      if (initEditTimer) {
        window.clearTimeout(initEditTimer);
        initEditTimer = null;
      }
      if (state.activeTab === "update_survey") {
        initEditTimer = window.setTimeout(() => {
          if (typeof window.surveyEditInit === "function") {
            window.surveyEditInit();
          }
        }, 0);
      }
      if (state.activeTab === "open_statistics") {
        window.setTimeout(() => {
          if (typeof window.initAnswerStatisticsPage === "function") {
            window.initAnswerStatisticsPage();
          }
        }, 0);
      }
      if (["email", "email_new", "email_settings"].includes(state.activeTab)) {
        window.setTimeout(() => {
          if (typeof window.initEmailSettingsPage === "function") {
            window.initEmailSettingsPage();
          }
        }, 0);
      }
      if (state.activeTab === "survey_auto_creation") {
        window.setTimeout(() => {
          if (typeof window.initSurveyAutoCreationPage === "function") {
            window.initSurveyAutoCreationPage();
          }
        }, 0);
      }
    };
    const setContentMount = (mountFn) => {
      if (typeof contentCleanup === "function") {
        contentCleanup();
        contentCleanup = null;
      }
      contentAdmin.innerHTML = "";
      const wrapper = createContentWrapper();
      contentAdmin.appendChild(wrapper);
      if (typeof mountFn === "function") {
        contentCleanup = mountFn(wrapper) || null;
      }
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
      await loadScriptsFromDocument(parsedDocument);
      schedulePostContentHooks();
      return response;
    };
    const deleteSurvey = async (surveyId) => {
      const response = await fetch(`/surveys/${surveyId}/delete`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "RequestVerificationToken": getRequestVerificationToken()
        },
        body: JSON.stringify({ surveyId })
      });
      const result = await response.json();
      if (!response.ok) {
        throw new Error(result.message || "Ошибка при удалении анкеты.");
      }
      return result;
    };
    const deleteUser = async (userId) => {
      const response = await fetch(`/users/${userId}/delete`, {
        method: "POST",
        headers: {
          RequestVerificationToken: getRequestVerificationToken()
        }
      });
      const responseText = await response.text();
      if (!response.ok) {
        throw new Error(responseText || "Ошибка при удалении пользователя.");
      }
      return responseText;
    };
    const revealRenderedModal = () => {
      modalNode.classList.add("modal--visible");
      modalNode.setAttribute("aria-hidden", "false");
      window.syncSiteModalBodyState?.();
    };
    const renderModal = () => {
      modalNode.className = "modal";
      modalNode.setAttribute("aria-hidden", "true");
      if (typeof modalCleanup === "function") {
        modalCleanup();
        modalCleanup = null;
      }
      modalBodyHost.innerHTML = "";
      if (!state.modal.isOpen) {
        window.syncSiteModalBodyState?.();
        return;
      }
      const modalData = state.modal.data;
      switch (state.modal.content) {
        case "extend":
          if (typeof mountExtensionModal === "function") {
            modalCleanup = mountExtensionModal(modalBodyHost, { survey: modalData, onClose: closeModal }) || null;
          } else {
            const msg = document.createElement("div");
            msg.textContent = "Модуль продления не загружен.";
            modalBodyHost.appendChild(msg);
          }
          revealRenderedModal();
          return;
        case "report": {
          const wrap = document.createElement("div");
          const title = document.createElement("h2");
          title.className = "modal-title";
          title.textContent = "Создать отчёт";
          wrap.appendChild(title);
          const actions = document.createElement("div");
          actions.style.display = "flex";
          actions.style.gap = "10px";
          actions.style.justifyContent = "space-between";
          actions.style.marginTop = "1.5rem";
          const month = document.createElement("div");
          month.className = "submenu2-container";
          month.style.flex = "1";
          const monthBtn = document.createElement("button");
          monthBtn.style.width = "100%";
          monthBtn.textContent = "Отчёт за месяц";
          const monthMenu = document.createElement("div");
          monthMenu.className = "submenu2";
          const bySurvey = document.createElement("div");
          bySurvey.textContent = "По выбранной анкете";
          bySurvey.addEventListener("click", () => createMonthlyReport(modalData?.id_survey));
          const allSurveys = document.createElement("div");
          allSurveys.textContent = "По всем анкетам";
          allSurveys.addEventListener("click", () => createMonthlySummaryReport());
          monthMenu.appendChild(bySurvey);
          monthMenu.appendChild(allSurveys);
          month.appendChild(monthBtn);
          month.appendChild(monthMenu);
          const quarter = document.createElement("div");
          quarter.className = "submenu2-container";
          quarter.style.flex = "1";
          const quarterBtn = document.createElement("button");
          quarterBtn.style.width = "100%";
          quarterBtn.textContent = "Отчёт за квартал";
          const quarterMenu = document.createElement("div");
          quarterMenu.className = "submenu2";
          [1, 2, 3, 4].forEach((q) => {
            const item = document.createElement("div");
            item.textContent = `${q} квартал`;
            item.addEventListener("click", () => createQuarterlyReport(q));
            quarterMenu.appendChild(item);
          });
          quarter.appendChild(quarterBtn);
          quarter.appendChild(quarterMenu);
          actions.appendChild(month);
          actions.appendChild(quarter);
          wrap.appendChild(actions);
          modalBodyHost.appendChild(wrap);
          revealRenderedModal();
          return;
        }
        case "copy":
        case "update":
        case "delete": {
          const isCopy = state.modal.content === "copy";
          const isUpdate = state.modal.content === "update";
          const titleText = isCopy ? "Копирование анкеты" : isUpdate ? "Редактирование анкеты" : "Удаление анкеты";
          const messageText = isCopy ? `Вы уверены, что хотите создать копию анкеты "${modalData?.name_survey}"?` : isUpdate ? `Вы переходите к редактированию анкеты "${modalData?.name_survey}".` : `Вы уверены, что хотите удалить анкету "${modalData?.name_survey}"?`;
          const okText = isCopy ? "Копировать" : isUpdate ? "Продолжить" : "Удалить";
          const okHandler = isCopy ? handleCopySurvey : isUpdate ? handleUpdateSurvey : handleDeleteSurvey;
          const root = document.createElement("div");
          const header = document.createElement("div");
          header.className = "modal-header";
          const h2 = document.createElement("h2");
          h2.className = "h2_modal";
          h2.textContent = titleText;
          header.appendChild(h2);
          const body = document.createElement("div");
          body.className = "modal-body";
          const p = document.createElement("p");
          p.className = "modal-message";
          p.textContent = messageText;
          body.appendChild(p);
          const footer = document.createElement("div");
          footer.className = "modal-footer";
          const ok = document.createElement("button");
          ok.className = "modal_btn modal_btn-primary";
          ok.textContent = okText;
          ok.addEventListener("click", okHandler);
          const cancel = document.createElement("button");
          cancel.className = "modal_btn modal_btn-secondary";
          cancel.textContent = "Отмена";
          cancel.addEventListener("click", closeModal);
          footer.appendChild(ok);
          footer.appendChild(cancel);
          root.appendChild(header);
          root.appendChild(body);
          root.appendChild(footer);
          modalBodyHost.appendChild(root);
          revealRenderedModal();
          return;
        }
        case "message": {
          const root = document.createElement("div");
          const header = document.createElement("div");
          header.className = "modal-header";
          const h2 = document.createElement("h2");
          h2.className = "h2_modal";
          h2.textContent = state.modal.isSuccess ? "Успешно" : "Ошибка";
          header.appendChild(h2);
          const body = document.createElement("div");
          body.className = "modal-body";
          const message = document.createElement("div");
          message.className = `modal-message ${state.modal.isSuccess ? "success-message" : "error-message"}`;
          message.textContent = state.modal.message || "";
          body.appendChild(message);
          const footer = document.createElement("div");
          footer.className = "modal-footer";
          const ok = document.createElement("button");
          ok.className = "modal_btn modal_btn-primary";
          ok.textContent = "OK";
          ok.addEventListener("click", closeModal);
          footer.appendChild(ok);
          root.appendChild(header);
          root.appendChild(body);
          root.appendChild(footer);
          modalBodyHost.appendChild(root);
          revealRenderedModal();
          return;
        }
        default:
          return;
      }
    };
    const setActiveTabAndRefreshNav = (tab) => {
      state.activeTab = tab;
      remountChrome();
      schedulePostContentHooks();
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
      if (tab === "get_surveys") {
        await fetchHtmlPage(buildListRequestUrl("/surveys", resolvedId));
        setActiveTabAndRefreshNav(tab);
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
        switch (tab) {
          case "open_statistics":
            await fetchHtmlPage("/statistics");
            setActiveTabAndRefreshNav(tab);
            break;
          case "list_answers_users":
            await fetchHtmlPage(buildListRequestUrl("/surveys/answers", resolvedId));
            setActiveTabAndRefreshNav(tab);
            break;
          case "archived_surveys":
            await fetchHtmlPage(buildListRequestUrl("/surveys/archive", resolvedId));
            setActiveTabAndRefreshNav(tab);
            break;
          case "get_survey_signatures":
            if (!id) throw new Error("ID анкеты не указан.");
            await fetchHtmlPage(`/surveys/${id}/signatures`);
            setActiveTabAndRefreshNav(tab);
            break;
          case "add_survey":
            if (!(state.activeTab === "get_surveys" && document.getElementById("surveyEditorModal") && !document.getElementById("surveyId"))) {
              await fetchHtmlPage("/surveys");
            }
            setActiveTabAndRefreshNav("get_surveys");
            openModalWhenReady("surveyEditorModal", window.openAddSurveyModal);
            break;
          case "get_logs":
            await fetchHtmlPage(buildListRequestUrl("/event-log", resolvedId));
            setActiveTabAndRefreshNav(tab);
            break;
          case "download_logs": {
            const response = await fetch("/event-log/export");
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
          case "get_users":
            await fetchHtmlPage(buildListRequestUrl("/users", resolvedId));
            setActiveTabAndRefreshNav(tab);
            break;
          case "get_organization":
            await fetchHtmlPage(buildListRequestUrl("/organizations", resolvedId));
            setActiveTabAndRefreshNav(tab);
            break;
          case "organization_surveys":
            await fetchHtmlPage("/organizations/surveys");
            setActiveTabAndRefreshNav(tab);
            break;
          case "copy_survey":
            if (!resolvedId) throw new Error("ID анкеты не указан.");
            await fetchHtmlPage("/surveys");
            setActiveTabAndRefreshNav("get_surveys");
            openModalWhenReady("surveyEditorModal", () => window.openCopySurveyModalById?.(resolvedId, { skipListRefresh: true }));
            break;
          case "update_survey":
            if (!resolvedId) throw new Error("ID анкеты не указан.");
            await fetchHtmlPage(`/surveys/${resolvedId}/edit`);
            setActiveTabAndRefreshNav("get_surveys");
            openModalWhenReady("surveyEditorModal", window.openEditSurveyModal);
            break;
          case "update_archived_survey":
            if (!resolvedId) throw new Error("ID анкеты не указан.");
            await fetchHtmlPage(`/surveys/archive/${resolvedId}/edit`);
            setActiveTabAndRefreshNav("archived_surveys");
            openModalWhenReady("surveyEditorModal", window.openEditSurveyModal);
            break;
          case "delete_survey": {
            const result = await deleteSurvey(state.modal.data?.id_survey);
            await fetchHtmlPage("/surveys");
            window.siteNotify?.(result.message, "success");
            setActiveTabAndRefreshNav("get_surveys");
            break;
          }
          case "add_user":
            if (!(state.activeTab === "get_users" && document.getElementById("addUserModal"))) {
              await fetchHtmlPage("/users");
            }
            setActiveTabAndRefreshNav("get_users");
            openModalWhenReady("addUserModal", window.openAddUserModal);
            break;
          case "update_user":
            if (!resolvedId) throw new Error("ID пользователя не указан.");
            await fetchHtmlPage(`/users/${resolvedId}/edit`);
            setActiveTabAndRefreshNav(tab);
            break;
          case "delete_user": {
            const message = await deleteUser(state.modal.data?.id_user);
            await fetchHtmlPage("/users");
            window.siteNotify?.(message, "success");
            setActiveTabAndRefreshNav("get_users");
            break;
          }
          case "archive_list_organizations":
            await fetchHtmlPage(buildListRequestUrl("/organizations/archive", resolvedId));
            setActiveTabAndRefreshNav(tab);
            break;
          case "archived_users":
          case "archive_list_users":
            await fetchHtmlPage(buildListRequestUrl("/users/archive", resolvedId));
            setActiveTabAndRefreshNav("archived_users");
            break;
          case "add_organization":
            if (!(state.activeTab === "get_organization" && document.getElementById("addOrganizationModal"))) {
              await fetchHtmlPage("/organizations");
            }
            setActiveTabAndRefreshNav("get_organization");
            openModalWhenReady("addOrganizationModal", window.openAddOrganizationModal);
            break;
          case "update_organization":
            if (!resolvedId) throw new Error("ID организации не указан.");
            await fetchHtmlPage(`/organizations/${resolvedId}/edit`);
            setActiveTabAndRefreshNav(tab);
            break;
          case "delete_organization":
            {
              const response = await fetch(`/organizations/${state.modal.data?.id_organization ?? state.modal.data?.organizationId}/delete`, {
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
            }
            await fetchHtmlPage("/organizations");
            setActiveTabAndRefreshNav("get_organization");
            break;
          case "help":
            window.open("/help/download", "_blank");
            await fetchHtmlPage("/help");
            setActiveTabAndRefreshNav(tab);
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
          case "reports":
            await fetchHtmlPage("/reports");
            setActiveTabAndRefreshNav(tab);
            break;
          case "survey_auto_creation":
            await fetchHtmlPage("/survey-auto-creation");
            setActiveTabAndRefreshNav(tab);
            break;
          case "email":
          case "email_new":
            await fetchHtmlPage("/mail");
            setActiveTabAndRefreshNav("email_new");
            break;
          case "email_settings":
            await fetchHtmlPage("/mail/configuration");
            setActiveTabAndRefreshNav("email_settings");
            break;
          default:
            console.warn(`Вкладка ${tab} не обработана.`);
            break;
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
        window.siteNotify?.(error.message || "Произошла ошибка загрузки.", "error");
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
        const result = await deleteSurvey(state.modal.data?.id_survey);
        await fetchHtmlPage("/surveys");
        window.siteNotify?.(result.message, "success");
        setActiveTabAndRefreshNav("get_surveys");
      } catch (error) {
        console.error("Ошибка при удалении анкеты:", error);
        window.siteNotify?.(error.message || "Не удалось удалить анкету.", "error");
      } finally {
        setLoading(false);
      }
    };
    modalClose.addEventListener("click", closeModal);
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
      const isMobileNavigationViewport = typeof window.matchMedia === "function" ? window.matchMedia("(max-width: 900px)").matches : window.innerWidth <= 900;
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

  // Web/wwwroot/js/features/admin/admin-survey-edit.js
  function surveyEditNotify(message, type = "error", options = {}) {
    const normalizedMessage = String(message || "").trim();
    if (!normalizedMessage) {
      return;
    }
    if (typeof window.siteNotify === "function") {
      window.siteNotify(normalizedMessage, type, {
        title: options.title,
        duration: options.duration ?? (type === "error" ? 0 : 4500)
      });
      return;
    }
    window.alert(normalizedMessage);
  }
  function surveyEditToggleOrganizationSelection(element) {
    const orgId = parseInt(element.dataset.id, 10);
    const orgName = element.dataset.name || element.querySelector("label")?.textContent?.trim() || "";
    if (!Number.isFinite(orgId) || !orgName) {
      return;
    }
    if (typeof window.toggleOrganizationSelection === "function") {
      window.toggleOrganizationSelection(orgId, orgName);
      return;
    }
    const checkbox = element.querySelector('input[type="checkbox"]');
    const nextSelected = element.dataset.selected !== "true";
    element.dataset.selected = nextSelected ? "true" : "false";
    element.classList.toggle("selected", nextSelected);
    if (checkbox) {
      checkbox.checked = nextSelected;
    }
  }
  function surveyEditSaveSelectedOrganization() {
    if (typeof window.surveyEditCloseOrganizationDropdown === "function") {
      window.surveyEditCloseOrganizationDropdown();
    } else {
      surveyEditCloseModal("organizationModal");
    }
    if (typeof window.updateSelectedOrganizationDisplay === "function") {
      window.updateSelectedOrganizationDisplay();
    }
  }
  function surveyEditUpdateSelectedOrganizationDisplay() {
    if (typeof window.updateSelectedOrganizationDisplay === "function") {
      window.updateSelectedOrganizationDisplay();
    }
  }
  function surveyEditRemoveOrganization(orgId) {
    if (typeof window.removeSelectedOrganization === "function") {
      window.removeSelectedOrganization(orgId);
      return;
    }
  }
  function surveyEditAddCriteria() {
    if (typeof window.appendSurveyCriteriaField === "function") {
      window.appendSurveyCriteriaField("");
    }
  }
  async function surveyEditUpdate() {
    const surveyTitle = document.getElementById("surveyTitle");
    const surveyDescription = document.getElementById("surveyDescription");
    const startDate = document.getElementById("startDate");
    const endDate = document.getElementById("endDate");
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    const surveyId = document.getElementById("surveyId")?.value;
    try {
      if (typeof window.surveyEditValidateForm === "function" && !window.surveyEditValidateForm()) {
        return;
      }
      if (!token || !surveyId) {
        surveyEditNotify("Ошибка безопасности. Пожалуйста, обновите страницу.");
        return;
      }
      const formData = {
        Title: surveyTitle.value.trim(),
        Description: surveyDescription?.value.trim() || "",
        StartDate: window.AppDate?.getInputIso(startDate) || "",
        EndDate: window.AppDate?.getInputIso(endDate) || "",
        Organizations: (typeof window.getSelectedOrganizations === "function" ? window.getSelectedOrganizations() : surveyEditSelectedOrganization).map((org) => org.id),
        Criteria: Array.from(document.querySelectorAll(".criteriy")).map((input) => input.value.trim()).filter((text) => text !== "")
      };
      const response = await fetch(`/surveys/${surveyId}/update`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "RequestVerificationToken": token,
          "Accept": "application/json"
        },
        body: JSON.stringify(formData)
      });
      if (!response.ok) {
        let errorMessage = "Ошибка сервера";
        try {
          const errorData = await response.json();
          errorMessage = errorData.message || errorData.error || errorMessage;
        } catch (e) {
          console.error("Ошибка при чтении ответа:", e);
        }
        throw new Error(errorMessage);
      }
      const result = await response.json();
      if (result.success) {
        if (typeof window.handleSurveyUpdateSuccess === "function") {
          window.handleSurveyUpdateSuccess(result);
          return;
        }
        if (typeof window.handleAdminMutationSuccess === "function") {
          await window.handleAdminMutationSuccess({
            message: result.message || "Анкета успешно обновлена!",
            tabName: "get_surveys",
            fallbackUrl: "/surveys"
          });
          return;
        }
        surveyEditNotify(result.message || "Анкета успешно обновлена!", "success");
        window.location.reload();
      } else {
        throw new Error(result.message || "Неизвестная ошибка");
      }
    } catch (error) {
      console.error("Ошибка при обновлении анкеты:", error);
      let userMessage = error.message;
      if (error.message.includes("jsonb") && error.message.includes("text")) {
        userMessage = "Ошибка формата данных. Пожалуйста, обновите страницу и попробуйте снова.";
      } else if (error.message.includes("date")) {
        userMessage = "Ошибка в датах. Проверьте правильность введенных дат.";
      } else if (error.message.includes("validation")) {
        userMessage = "Ошибка валидации данных: " + error.message;
      }
      surveyEditNotify(userMessage);
      const showDetails = await window.siteConfirm("Показать технические подробности ошибки?", {
        title: "Подробности ошибки",
        confirmText: "Показать",
        cancelText: "Закрыть"
      });
      if (showDetails) {
        console.error("Техническая информация:", error.stack || error.message);
        window.siteNotify("Подробности ошибки выведены в консоль браузера.", "info");
      }
    }
  }
  function surveyEditValidateForm() {
    let isValid = true;
    const requiredFields = [
      { element: document.getElementById("surveyTitle"), errorId: "titleError" },
      { element: document.getElementById("startDate"), errorId: "startDateError" },
      { element: document.getElementById("endDate"), errorId: "endDateError" }
    ];
    requiredFields.forEach((field) => {
      const errorElement = document.getElementById(field.errorId);
      if (!field.element.value.trim()) {
        field.element.classList.add("invalid");
        if (errorElement) errorElement.style.display = "block";
        isValid = false;
      } else {
        field.element.classList.remove("invalid");
        if (errorElement) errorElement.style.display = "none";
      }
    });
    const startDate = document.getElementById("startDate");
    const endDate = document.getElementById("endDate");
    const endDateError = document.getElementById("endDateError");
    const startDateIso = window.AppDate?.getInputIso(startDate) || "";
    const endDateIso = window.AppDate?.getInputIso(endDate) || "";
    if (startDate.value && !startDateIso || endDate.value && !endDateIso) {
      surveyEditNotify("Используйте формат даты ДД.ММ.ГГГГ.");
      isValid = false;
    } else if (startDateIso && endDateIso && window.AppDate?.compare(endDateIso, startDateIso) <= 0) {
      endDate.classList.add("invalid");
      if (endDateError) {
        endDateError.textContent = "Дата конца должна быть позже даты начала";
        endDateError.style.display = "block";
      }
      isValid = false;
    }
    const organizationError = document.getElementById("organizationError");
    const selectedOrganizations = typeof window.getSelectedOrganizations === "function" ? window.getSelectedOrganizations() : surveyEditSelectedOrganization;
    if (selectedOrganizations.length === 0) {
      if (organizationError) organizationError.style.display = "block";
      isValid = false;
    } else {
      if (organizationError) organizationError.style.display = "none";
    }
    if (typeof window.validateSurveyCriteriaFields === "function" && !window.validateSurveyCriteriaFields()) {
      isValid = false;
    }
    return isValid;
  }
  function copySurvey(id) {
    const startDate = window.AppDate?.getInputIso("startDate") || "";
    const endDate = window.AppDate?.getInputIso("endDate") || "";
    const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
    if (!startDate || !endDate) {
      showNotification("Пожалуйста, заполните все обязательные поля", false);
      return;
    }
    if ((window.AppDate?.compare(endDate, startDate) ?? -1) <= 0) {
      showNotification("Дата конца должна быть позже даты начала", false);
      return;
    }
    document.getElementById("loadingOverlay").style.display = "flex";
    fetch("/surveys/" + id + "/copy", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "RequestVerificationToken": token
      },
      body: JSON.stringify({
        StartDate: startDate,
        EndDate: endDate
      })
    }).then((response) => {
      if (!response.ok) {
        return response.json().then((err) => {
          throw new Error(err.message || "Ошибка сервера");
        });
      }
      return response.json();
    }).then((data) => {
      document.getElementById("loadingOverlay").style.display = "none";
      if (data.success) {
        if (typeof window.handleAdminMutationSuccess === "function") {
          return window.handleAdminMutationSuccess({
            message: data.message || "Анкета успешно скопирована!",
            tabName: "get_surveys",
            fallbackUrl: "/surveys"
          });
        }
        surveyEditNotify("Анкета успешно скопирована!", "success");
        window.location.reload();
      } else {
        throw new Error(data.message || "Ошибка при копировании анкеты");
      }
    }).catch((error) => {
      document.getElementById("loadingOverlay").style.display = "none";
      showNotification(error.message, false);
      console.error("Error:", error);
    });
  }
  window.surveyEditToggleOrganizationSelection = surveyEditToggleOrganizationSelection;
  window.surveyEditSaveSelectedOrganization = surveyEditSaveSelectedOrganization;
  window.surveyEditUpdateSelectedOrganizationDisplay = surveyEditUpdateSelectedOrganizationDisplay;
  window.surveyEditRemoveOrganization = surveyEditRemoveOrganization;
  window.surveyEditAddCriteria = surveyEditAddCriteria;
  window.surveyEditUpdate = surveyEditUpdate;
  window.surveyEditValidateForm = surveyEditValidateForm;
  window.copySurvey = copySurvey;
})();
//# sourceMappingURL=admin-inline-app.js.map
