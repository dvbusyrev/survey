(() => {
  // wwwroot/js/ui/app-header.js
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
    const existingHeader = host.querySelector(":scope > .app-header");
    const header = existingHeader || template.content.firstElementChild.cloneNode(true);
    if (!existingHeader) {
      host.replaceChildren(header);
    }
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
    if (logoutButton && logoutButton.dataset.logoutBound !== "true") {
      logoutButton.dataset.logoutBound = "true";
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
    return () => {
      if (!existingHeader) {
        header.remove();
      }
    };
  }
  window.mountHeader = function mountHeader(host, props) {
    return renderHeader(host, props || {});
  };
  window.readAppChromeContext = function readAppChromeContext() {
    return readChromeContextNode(document.getElementById("layout-chrome-context")) || readChromeContextNode(document.getElementById("chrome-context")) || null;
  };

  // wwwroot/js/ui/app-navigation.js
  (() => {
    if (window.__appNavigationLoaded) {
      return;
    }
    window.__appNavigationLoaded = true;
    const NAV_SUBMENU_SUPPRESS_STORAGE_KEY = "app-nav-submenu-suppressed";
    const MOBILE_NAV_OPEN_CLASS = "mobile-nav-open";
    const COMPACT_NAVIGATION_CLASS = "compact-nav-mode";
    const PREPAINT_COMPACT_NAVIGATION_CLASS = "app-compact-shell";
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
        document.documentElement.classList.remove(PREPAINT_COMPACT_NAVIGATION_CLASS);
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
      document.documentElement.classList.remove(PREPAINT_COMPACT_NAVIGATION_CLASS);
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
    function renderNavigation(host, { activeTab, userRole }) {
      const isAdmin = userRole === "admin";
      const isModifiedNavigationEvent = (event) => event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey;
      const isSurveySectionActive = isAdmin ? ["get_surveys", "add_survey", "list_answers_users", "archived_surveys"].includes(activeTab) : ["active", "archived", "answers_tab", "archived_surveys_for_user"].includes(activeTab);
      const isOrganizationSectionActive = ["get_organization", "organization_surveys", "add_organization", "archive_list_organizations"].includes(activeTab);
      const isEmailSectionActive = ["email", "email_new"].includes(activeTab);
      const isSettingsSectionActive = ["email_settings", "theme_settings", "survey_auto_creation"].includes(activeTab);
      const navigate = (link) => {
        const href = link?.getAttribute?.("href") || "";
        if (!href || href === "#") {
          return;
        }
        window.AppScrollState?.prepareNavigation({ carry: true });
        window.location.href = href;
      };
      const templateId = isAdmin ? "nav-template-admin" : "nav-template-user";
      const template = document.getElementById(templateId);
      if (!host || !template?.content?.firstElementChild) {
        return null;
      }
      evaluateNavigationLayout();
      const expectedRole = isAdmin ? "admin" : "user";
      const existingNav = host.querySelector(":scope > .admin-nav");
      const canHydrateExistingNav = existingNav && existingNav.dataset.navigationRole === expectedRole && existingNav.dataset.navigationMounted !== "true";
      const nav = canHydrateExistingNav ? existingNav : template.content.firstElementChild.cloneNode(true);
      if (!canHydrateExistingNav) {
        host.replaceChildren(nav);
      }
      nav.dataset.navigationMounted = "true";
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
          navigate(event.currentTarget);
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
          closeMobileNavIfNeeded();
          navigate(event.currentTarget);
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

  // wwwroot/js/ui/app-footer.js
  function renderFooter(host) {
    const template = document.getElementById("footer-template");
    if (!host || !template?.content?.firstElementChild) {
      return null;
    }
    const existingFooter = host.querySelector(":scope > footer");
    if (existingFooter) {
      return () => {
      };
    }
    const footer = template.content.firstElementChild.cloneNode(true);
    host.appendChild(footer);
    return () => {
      footer.remove();
    };
  }
  window.mountFooter = function mountFooter(host) {
    return renderFooter(host);
  };

  // wwwroot/js/features/survey/user-survey-notifications.js
  var ANSWER_SUBMITTED_MESSAGE = "Ответы на анкету успешно отправлены. Анкета перенесена в раздел «Архив анкет».";
  var ANSWER_SUBMISSION_FAILED_MESSAGE = "Не удалось отправить ответы на анкету.";
  var pendingAnswerNotificationKey = "survey:pending-answer-notification";
  function resolveMessage(message, fallbackMessage) {
    const normalizedMessage = String(message || "").trim();
    return normalizedMessage || fallbackMessage;
  }
  function storePendingAnswerSubmittedNotification(message) {
    try {
      window.sessionStorage.setItem(
        pendingAnswerNotificationKey,
        resolveMessage(message, ANSWER_SUBMITTED_MESSAGE)
      );
      return true;
    } catch (error) {
      return false;
    }
  }

  // wwwroot/js/features/survey/survey-fill-standalone.js
  window.bindStandaloneSurveyFillPage = function bindStandaloneSurveyFillPage(initialData) {
    const page = document.querySelector('[data-page="survey-fill-standalone"]');
    if (!page) {
      return;
    }
    const refs = {
      errorBlock: page.querySelector('[data-role="error"]'),
      errorText: page.querySelector('[data-role="error-text"]'),
      submitButton: page.querySelector('[data-role="submit"]'),
      submitLabel: page.querySelector('[data-role="submit-label"]')
    };
    const answers = {};
    let loading = false;
    let error = null;
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
          activeTab: "answers_tab",
          userRole: chromeProps.userRole,
          userId: chromeContext?.userId || initialData.userId
        });
      }
      if (footerHost && typeof window.mountFooter === "function") {
        window.mountFooter(footerHost);
      }
    }
    function getQuestionNodes() {
      return Array.from(page.querySelectorAll('[data-role="survey-question"]'));
    }
    function renderError() {
      refs.errorText && (refs.errorText.textContent = "");
      refs.errorBlock?.classList.add("u-hidden");
      if (error) {
        window.AppUi.notify(error, "error", { title: "Ошибка" });
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
        commentInput.required = showComment;
        commentInput.value = showComment ? answer.comment || "" : "";
        if (!showComment) {
          window.AppValidation?.clearFieldError?.(commentInput);
        }
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
          window.AppValidation?.clearFieldError?.(
            questionElement.querySelector('[data-role="ratings"]')
          );
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
    function validateCompleteAnswers() {
      const errors = [];
      const invalidFields = [];
      getQuestionNodes().forEach((questionNode) => {
        const questionId = questionNode.dataset.questionId || "";
        const answer = answers[questionId] || {};
        const rating = Number(answer.rating || 0);
        const ratings = questionNode.querySelector('[data-role="ratings"]');
        const commentInput = questionNode.querySelector('[data-role="comment-input"]');
        if (!Number.isFinite(rating) || rating < 1 || rating > 5) {
          const message = "Выберите оценку для каждого вопроса.";
          window.AppValidation?.setFieldError?.(ratings, message);
          errors.push(message);
          invalidFields.push(ratings);
        } else {
          window.AppValidation?.clearFieldError?.(ratings);
        }
        if (rating > 0 && rating < 5 && !String(answer.comment || "").trim()) {
          const message = "Для каждой оценки ниже 5 требуется комментарий.";
          window.AppValidation?.setFieldError?.(commentInput, message);
          errors.push(message);
          invalidFields.push(commentInput);
        } else {
          window.AppValidation?.clearFieldError?.(commentInput);
        }
      });
      if (errors.length === 0) {
        return true;
      }
      window.AppValidation?.notifyErrors?.(errors);
      window.AppValidation?.focusFirstInvalid?.({ invalidFields });
      return false;
    }
    async function submitAnswers() {
      if (!validateCompleteAnswers()) {
        return;
      }
      try {
        loading = true;
        error = null;
        renderError();
        renderSubmitState();
        const payloadAnswers = Object.entries(answers).map(([questionId, answer]) => {
          return {
            question_id: questionId,
            rating: answer.rating,
            comment: answer.rating === 5 ? "" : answer.comment || ""
          };
        });
        const response = await fetch("/answers/create", {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "X-Requested-With": "XMLHttpRequest"
          },
          body: JSON.stringify({
            id_survey: initialData.surveyId,
            id_organization: initialData.organizationId,
            answers: payloadAnswers
          })
        });
        if (!response.ok) {
          const errorData = await response.json().catch(() => null);
          throw new Error(errorData?.error || ANSWER_SUBMISSION_FAILED_MESSAGE);
        }
        const responseData = await response.json().catch(() => null);
        storePendingAnswerSubmittedNotification(responseData?.message);
        window.location.assign("/archive");
      } catch (err) {
        error = err?.message || ANSWER_SUBMISSION_FAILED_MESSAGE;
        renderError();
      } finally {
        loading = false;
        renderSubmitState();
      }
    }
    refs.submitButton?.addEventListener("click", submitAnswers);
    getQuestionNodes().forEach(bindQuestion);
    renderChrome();
    renderError();
    renderSubmitState();
  };
  function getStandaloneBootstrapData() {
    const bootstrapElement = document.getElementById("survey-fill-bootstrap");
    if (!bootstrapElement?.textContent) {
      return null;
    }
    try {
      return JSON.parse(bootstrapElement.textContent.trim());
    } catch (error) {
      console.error("Не удалось прочитать bootstrap-данные страницы анкеты:", error);
      return null;
    }
  }
  var standaloneBootstrapData = getStandaloneBootstrapData();
  if (document.querySelector('[data-page="survey-fill-standalone"]') && standaloneBootstrapData) {
    window.bindStandaloneSurveyFillPage(standaloneBootstrapData);
  }
})();
//# sourceMappingURL=survey-fill-app.js.map
