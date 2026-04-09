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

  // Web/wwwroot/js/features/survey/survey-fill-standalone.js
  window.renderStandaloneSurveyFill = function(initialData) {
    const root = document.getElementById("root");
    const pageTemplate = document.getElementById("survey-fill-page-template");
    const questionTemplate = document.getElementById("survey-fill-question-template");
    const successTemplate = document.getElementById("survey-fill-success-template");
    if (!root || !pageTemplate?.content?.firstElementChild || !questionTemplate?.content?.firstElementChild) {
      return;
    }
    const answers = {};
    let loading = false;
    let error = null;
    root.innerHTML = "";
    const page = pageTemplate.content.firstElementChild.cloneNode(true);
    root.appendChild(page);
    const headerHost = page.querySelector('[data-component="header"]');
    const navHost = page.querySelector('[data-component="navigation"]');
    const footerHost = page.querySelector('[data-component="footer"]');
    const questionsHost = page.querySelector('[data-role="questions"]');
    const errorBlock = page.querySelector('[data-role="error"]');
    const errorText = page.querySelector('[data-role="error-text"]');
    const submitButton = page.querySelector('[data-role="submit"]');
    const submitLabel = page.querySelector('[data-role="submit-label"]');
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
        activeTab: "answers_tab",
        userRole: initialData.userRole,
        userId: initialData.userId
      });
    }
    if (footerHost && typeof window.mountFooter === "function") {
      window.mountFooter(footerHost);
    }
    function renderError() {
      if (!errorBlock || !errorText) {
        return;
      }
      if (error) {
        errorText.textContent = error;
        errorBlock.style.display = "flex";
      } else {
        errorText.textContent = "";
        errorBlock.style.display = "none";
      }
    }
    function renderSubmitState() {
      if (!submitButton || !submitLabel) {
        return;
      }
      submitButton.disabled = loading;
      submitButton.querySelector(".loading-spinner")?.remove();
      if (loading) {
        const spinner = document.createElement("span");
        spinner.className = "loading-spinner";
        submitButton.insertBefore(spinner, submitLabel);
        submitLabel.textContent = "Отправка...";
      } else {
        submitLabel.textContent = "Отправить ответы";
      }
    }
    function updateQuestionState(questionId, questionElement) {
      const answer = answers[questionId] || {};
      questionElement.querySelectorAll(".btn_crit").forEach((button) => {
        const rating = Number(button.dataset.rating || 0);
        button.classList.toggle("active", answer.rating === rating);
      });
      const commentBlock = questionElement.querySelector('[data-role="comment-block"]');
      const commentInput = questionElement.querySelector("textarea");
      const showComment = answer.rating > 0 && answer.rating < 5;
      if (commentBlock) {
        commentBlock.style.display = showComment ? "" : "none";
      }
      if (commentInput) {
        commentInput.value = answer.comment || "";
      }
    }
    function buildQuestion(question, index) {
      const questionId = String(question.id || question.Id || index);
      const questionText = question.text || question.Text || `Вопрос ${index + 1}`;
      const questionNode = questionTemplate.content.firstElementChild.cloneNode(true);
      const title = questionNode.querySelector('[data-role="question-title"]');
      const ratingsHost = questionNode.querySelector('[data-role="ratings"]');
      const commentInput = questionNode.querySelector("textarea");
      if (title) {
        title.textContent = questionText;
      }
      for (let rating = 1; rating <= 5; rating += 1) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "btn_crit";
        button.dataset.rating = String(rating);
        button.textContent = String(rating);
        button.addEventListener("click", () => {
          error = null;
          answers[questionId] = {
            ...answers[questionId],
            rating,
            comment: rating < 5 ? answers[questionId]?.comment || "" : ""
          };
          renderError();
          updateQuestionState(questionId, questionNode);
        });
        ratingsHost?.appendChild(button);
      }
      commentInput?.addEventListener("input", (event) => {
        error = null;
        answers[questionId] = {
          ...answers[questionId],
          comment: event.target.value
        };
        renderError();
      });
      updateQuestionState(questionId, questionNode);
      return questionNode;
    }
    function showSuccessAndRedirect() {
      if (!successTemplate?.content?.firstElementChild) {
        window.location.href = "/survey/thank-you";
        return;
      }
      root.innerHTML = "";
      root.appendChild(successTemplate.content.firstElementChild.cloneNode(true));
      window.setTimeout(() => {
        window.location.href = "/survey/thank-you";
      }, 2e3);
    }
    async function submitAnswers() {
      try {
        loading = true;
        error = null;
        renderError();
        renderSubmitState();
        const payloadAnswers = Object.entries(answers).map(([questionId, answer]) => ({
          question_id: questionId,
          question_text: initialData.questions.find((q) => String(q.id || q.Id) === String(questionId))?.text || initialData.questions.find((q) => String(q.id || q.Id) === String(questionId))?.Text || "",
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
            id_survey: initialData.surveyId,
            organization_id: initialData.organizationId,
            answers: payloadAnswers
          })
        });
        if (!response.ok) {
          const errorData = await response.json().catch(() => null);
          throw new Error(errorData?.error || "Ошибка при отправке ответов");
        }
        showSuccessAndRedirect();
      } catch (err) {
        error = err?.message || "Не удалось отправить ответы";
        renderError();
      } finally {
        loading = false;
        renderSubmitState();
      }
    }
    submitButton?.addEventListener("click", submitAnswers);
    renderError();
    renderSubmitState();
    (initialData.questions || []).forEach((question, index) => {
      questionsHost?.appendChild(buildQuestion(question, index));
    });
  };
  function getStandaloneBootstrapData() {
    const bootstrapElement = document.getElementById("survey-fill-bootstrap");
    if (!bootstrapElement?.content?.textContent) {
      return null;
    }
    try {
      return JSON.parse(bootstrapElement.content.textContent.trim());
    } catch (error) {
      console.error("Не удалось прочитать bootstrap-данные страницы анкеты:", error);
      return null;
    }
  }
  var standaloneBootstrapData = getStandaloneBootstrapData();
  if (document.getElementById("root") && standaloneBootstrapData) {
    window.renderStandaloneSurveyFill(standaloneBootstrapData);
  }
})();
//# sourceMappingURL=survey-fill-app.js.map
