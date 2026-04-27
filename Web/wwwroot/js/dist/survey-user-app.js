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
    const rawDisplayName = displayName && String(displayName).trim() ? String(displayName).trim() : userRole === "admin" ? "Администратор" : "Клиент";
    const displayNameParts = rawDisplayName.split(":").map((part) => part.trim()).filter(Boolean);
    const normalizedUserName = userName && String(userName).trim() ? String(userName).trim() : displayNameParts.length > 1 ? displayNameParts.slice(1).join(": ").trim() : rawDisplayName;
    const normalizedOrganizationName = organizationName && String(organizationName).trim() ? String(organizationName).trim() : displayNameParts[0] || "Клиент";
    const headerTopLine = normalizedOrganizationName;
    const normalizedDisplayName = userRole === "admin" ? normalizedUserName || "Администратор" : normalizedUserName || rawDisplayName;
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
    const MOBILE_NAV_MEDIA_QUERY = "(max-width: 900px)";
    function isMobileNavigationViewport() {
      return typeof window.matchMedia === "function" ? window.matchMedia(MOBILE_NAV_MEDIA_QUERY).matches : window.innerWidth <= 900;
    }
    function isMobileNavigationOpen() {
      return document.body.classList.contains(MOBILE_NAV_OPEN_CLASS);
    }
    function syncMobileNavigationToggleButtons() {
      const isOpen = isMobileNavigationOpen();
      document.querySelectorAll(".header-menu-toggle").forEach((button) => {
        button.setAttribute("aria-expanded", isOpen ? "true" : "false");
        button.setAttribute("aria-label", isOpen ? "Закрыть навигацию" : "Открыть навигацию");
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
      };
      window.addEventListener("resize", onResize);
      return () => {
        if (menuToggleButton) {
          menuToggleButton.removeEventListener("click", menuToggleHandler);
        }
        host.removeEventListener("click", navOverlayClickHandler);
        document.removeEventListener("keydown", onEscape);
        document.removeEventListener("pointerdown", onPointerDown);
        window.removeEventListener("resize", onResize);
        nav.removeEventListener("mouseleave", navLeaveHandler);
        closeMobileNavigation();
        host.innerHTML = "";
      };
    }
    window.mountNavigation = function mountNavigation(host, props) {
      return renderNavigation(host, props || {});
    };
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
      const pdfButton = host.querySelector('[data-role="pdf-btn"]');
      pdfButton?.addEventListener("click", () => createPdfReport(survey.id_survey, organizationId));
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
    host.appendChild(modalNode);
    if (typeof window.syncSiteModalBodyState === "function") {
      window.syncSiteModalBodyState();
    } else {
      document.body.classList.add("modal-open");
    }
    document.addEventListener("keydown", handleEscape);
    const bodyCleanup = typeof mountBody === "function" && bodyHost ? mountBody(bodyHost) : null;
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
        prevPage: root?.querySelector('[data-role="prev-page"]'),
        nextPage: root?.querySelector('[data-role="next-page"]'),
        errorWrap: root?.querySelector('[data-role="error"]'),
        errorText: root?.querySelector('[data-role="error-text"]')
      };
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
      const prevPageButton = event.target.closest('[data-role="prev-page"]');
      if (prevPageButton && contentHost.contains(prevPageButton) && !prevPageButton.disabled) {
        loadTabSnapshot(state.activeTab, {
          page: Math.max(1, state.currentSnapshot.currentPage - 1),
          searchTerm: state.currentSnapshot.searchTerm,
          signedOnly: state.currentSnapshot.signedOnly
        });
        return;
      }
      const nextPageButton = event.target.closest('[data-role="next-page"]');
      if (nextPageButton && contentHost.contains(nextPageButton) && !nextPageButton.disabled) {
        loadTabSnapshot(state.activeTab, {
          page: state.currentSnapshot.currentPage + 1,
          searchTerm: state.currentSnapshot.searchTerm,
          signedOnly: state.currentSnapshot.signedOnly
        });
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
