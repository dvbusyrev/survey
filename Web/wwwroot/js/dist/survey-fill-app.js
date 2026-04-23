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
    function renderNavigation(host, { openTab, activeTab, userRole, userId }) {
      const isAdmin = userRole === "admin";
      const isModifiedNavigationEvent = (event) => event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey;
      const isSurveySectionActive = isAdmin ? ["get_surveys", "add_survey", "list_answers_users", "archived_surveys"].includes(activeTab) : ["active", "archived", "answers_tab", "archived_surveys_for_user"].includes(activeTab);
      const isOrganizationSectionActive = ["get_organization", "organization_surveys", "add_organization", "archive_list_organizations"].includes(activeTab);
      const isEmailSectionActive = ["email", "email_new", "email_settings"].includes(activeTab);
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
          email: "/mail",
          email_new: "/mail",
          email_settings: "/mail/configuration",
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
      host.innerHTML = "";
      const nav = template.content.firstElementChild.cloneNode(true);
      host.appendChild(nav);
      const closeSubmenus = () => closeNavigationSubmenus(nav);
      nav.querySelectorAll(".nav-item").forEach((item) => {
        const tab = item.dataset.tab || "";
        const navClass = item.dataset.navClass || "";
        const isActive = navClass === "surveys" ? isSurveySectionActive : navClass === "organizations" ? isOrganizationSectionActive : navClass === "email" ? isEmailSectionActive : tab === activeTab;
        item.classList.toggle("active", isActive);
      });
      nav.querySelectorAll(".submenu-item").forEach((subItem) => {
        subItem.classList.toggle("active", (subItem.dataset.tab || "") === activeTab);
      });
      nav.querySelectorAll(".nav-item.has-submenu").forEach((item) => {
        const itemTab = item.dataset.tab || "";
        const onEnter = () => {
          if (isNavigationSubmenuSuppressed(itemTab)) {
            releaseNavigationSubmenuSuppression();
          } else if (isNavigationSubmenuSuppressed()) {
            releaseNavigationSubmenuSuppression();
          }
          item.classList.add("submenu-open");
        };
        const onLeave = () => {
          item.classList.remove("submenu-open");
          if (isNavigationSubmenuSuppressed(itemTab)) {
            releaseNavigationSubmenuSuppression();
          }
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
          suppressNavigationSubmenus(nav, item.classList.contains("has-submenu") ? item.dataset.tab || "" : "");
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
          navigate(item?.dataset?.tab || "");
        });
      });
      const onPointerDown = (event) => {
        if (!event.target.closest(".admin-nav")) {
          closeSubmenus();
          releaseNavigationSubmenuSuppression();
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

  // Web/wwwroot/js/features/survey/survey-fill-standalone.js
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
      if (!refs.errorBlock || !refs.errorText) {
        return;
      }
      if (error) {
        refs.errorText.textContent = error;
        refs.errorBlock.classList.remove("u-hidden");
      } else {
        refs.errorText.textContent = "";
        refs.errorBlock.classList.add("u-hidden");
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
            id_survey: initialData.surveyId,
            id_organization: initialData.organizationId,
            answers: payloadAnswers
          })
        });
        if (!response.ok) {
          const errorData = await response.json().catch(() => null);
          throw new Error(errorData?.error || "Ошибка при отправке ответов");
        }
        window.location.assign("/my-surveys/archive");
      } catch (err) {
        error = err?.message || "Не удалось отправить ответы";
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
