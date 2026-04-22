(() => {
  // Web/wwwroot/js/ui/app-header.js
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
      const isOtherSectionActive = ["get_logs", "email"].includes(activeTab);
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
          email: "/mail-settings",
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
        const isActive = navClass === "surveys" ? isSurveySectionActive : navClass === "organizations" ? isOrganizationSectionActive : navClass === "other" ? isOtherSectionActive : tab === activeTab;
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

  // Web/wwwroot/js/features/admin/admin-inline-pages.js
  (() => {
    const adminInlineAppPages = window.AdminInlineAppPages || (window.AdminInlineAppPages = {});
    adminInlineAppPages.mountExtensionModal = function mountExtensionModal(host, { survey, onClose }) {
      if (!host) {
        return null;
      }
      let disposed = false;
      let organizations = [];
      let loading = true;
      let error = "";
      let extension = { organizationId: "", extendedUntil: "" };
      const today = (/* @__PURE__ */ new Date()).toISOString().split("T")[0];
      const isFormValid = () => {
        return Boolean(
          extension.organizationId && extension.extendedUntil && extension.extendedUntil > today
        );
      };
      const handleChange = (field, value) => {
        extension = {
          ...extension,
          [field]: value
        };
        render();
      };
      const handleSubmit = async () => {
        if (!extension.organizationId || !extension.extendedUntil) {
          alert("Пожалуйста, заполните все поля.");
          return;
        }
        if (extension.extendedUntil <= today) {
          alert("Дата конца должна быть в будущем.");
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
              extensions: [{
                organizationId: parseInt(extension.organizationId, 10),
                extendedUntil: extension.extendedUntil
              }]
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
          onClose();
          if (typeof window.handleAdminMutationSuccess === "function") {
            await window.handleAdminMutationSuccess({
              message: responseData.message || "Доступ успешно продлён.",
              tabName: typeof window.resolveCurrentAdminTab === "function" ? window.resolveCurrentAdminTab() : "get_surveys",
              fallbackUrl: window.location.pathname
            });
            return;
          }
          alert(responseData.message || "Доступ успешно продлён.");
          window.location.reload();
        } catch (submitError) {
          console.error("Ошибка продления анкеты:", submitError);
          alert(`Ошибка: ${submitError.message || "Не удалось продлить доступ."}`);
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
        host.innerHTML = "";
        const root = template.content.firstElementChild.cloneNode(true);
        const surveyName = root.querySelector('[data-role="survey-name"]');
        const errorNode = root.querySelector('[data-role="error"]');
        const rowsContainer = root.querySelector('[data-role="rows-container"]');
        const emptyState = root.querySelector('[data-role="empty-state"]');
        const submitButton = root.querySelector('[data-role="submit"]');
        const cancelButton = root.querySelector('[data-role="cancel"]');
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
          const orgSelect = row.querySelector('[data-role="org-select"]');
          const dateInput = row.querySelector('[data-role="date-input"]');
          if (orgSelect) {
            const defaultOption = document.createElement("option");
            defaultOption.value = "";
            defaultOption.textContent = "-- Выберите организацию --";
            orgSelect.appendChild(defaultOption);
            organizations.forEach((organization) => {
              const option = document.createElement("option");
              option.value = organization.organizationId;
              option.textContent = organization.organizationName;
              if (extension.organizationId === organization.organizationId) {
                option.selected = true;
              }
              orgSelect.appendChild(option);
            });
            orgSelect.addEventListener("change", (event) => {
              handleChange("organizationId", event.target.value);
            });
          }
          if (dateInput) {
            dateInput.value = extension.extendedUntil;
            dateInput.min = today;
            dateInput.addEventListener("change", (event) => {
              handleChange("extendedUntil", event.target.value);
            });
          }
          rowsContainer.appendChild(row);
        }
        if (submitButton) {
          submitButton.disabled = !isFormValid() || loading;
          submitButton.textContent = loading ? "Обработка..." : "Продлить доступ";
          submitButton.style.backgroundColor = isFormValid() ? "#4caf50" : "#9e9e9e";
          submitButton.style.cursor = isFormValid() ? "pointer" : "not-allowed";
          submitButton.style.opacity = isFormValid() ? "1" : "0.6";
          submitButton.addEventListener("click", handleSubmit);
        }
        if (cancelButton) {
          cancelButton.addEventListener("click", onClose);
        }
        host.appendChild(root);
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
      render();
      fetchOrganizations();
      return () => {
        disposed = true;
        host.innerHTML = "";
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
        pie: null,
        radar: null
      };
      const chartInstances = {
        line: null,
        bar: null,
        pie: null,
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
        chartInstances.pie = null;
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
        const shouldShowLegend = ({ labels = [], datasets = [] } = {}) => {
          if (datasets.length > 1) {
            return true;
          }
          if (datasets.length === 1) {
            if ((datasets[0]?.label || "").trim()) {
              return false;
            }
            return labels.length > 1;
          }
          return labels.length > 1;
        };
        const commonOptions = {
          responsive: true,
          maintainAspectRatio: false,
          plugins: {
            legend: {
              position: "bottom",
              labels: {
                padding: 20,
                boxWidth: 12,
                font: {
                  size: 12
                }
              }
            }
          },
          layout: {
            padding: {
              top: 10,
              bottom: 30
            }
          }
        };
        if (chartRefs.line && chartsData.lineChart) {
          chartInstances.line = new Chart(chartRefs.line, {
            type: "line",
            data: {
              labels: chartsData.lineChart.labels,
              datasets: [{
                label: chartsData.lineChart.label,
                data: chartsData.lineChart.data,
                borderColor: "rgb(75, 192, 192)",
                backgroundColor: "rgba(75, 192, 192, 0.1)",
                tension: 0.1,
                borderWidth: 2,
                pointRadius: 4
              }]
            },
            options: {
              ...commonOptions,
              plugins: {
                ...commonOptions.plugins,
                legend: {
                  ...commonOptions.plugins.legend,
                  display: shouldShowLegend({
                    labels: chartsData.lineChart.labels,
                    datasets: [{ label: chartsData.lineChart.label }]
                  })
                }
              },
              scales: {
                y: {
                  beginAtZero: true
                }
              }
            }
          });
        }
        if (chartRefs.bar && chartsData.barChart) {
          chartInstances.bar = new Chart(chartRefs.bar, {
            type: "bar",
            data: {
              labels: chartsData.barChart.labels,
              datasets: [{
                label: chartsData.barChart.label,
                data: chartsData.barChart.data,
                backgroundColor: "rgba(54, 162, 235, 0.7)",
                borderColor: "rgba(54, 162, 235, 1)",
                borderWidth: 1
              }]
            },
            options: {
              ...commonOptions,
              plugins: {
                ...commonOptions.plugins,
                legend: {
                  ...commonOptions.plugins.legend,
                  display: shouldShowLegend({
                    labels: chartsData.barChart.labels,
                    datasets: [{ label: chartsData.barChart.label }]
                  })
                }
              },
              scales: {
                y: {
                  beginAtZero: true
                }
              }
            }
          });
        }
        if (chartRefs.pie && chartsData.pieChart) {
          chartInstances.pie = new Chart(chartRefs.pie, {
            type: "pie",
            data: {
              labels: chartsData.pieChart.labels,
              datasets: [{
                data: chartsData.pieChart.data,
                backgroundColor: [
                  "rgba(255, 99, 132, 0.7)",
                  "rgba(54, 162, 235, 0.7)",
                  "rgba(255, 206, 86, 0.7)",
                  "rgba(75, 192, 192, 0.7)",
                  "rgba(153, 102, 255, 0.7)"
                ],
                borderWidth: 1
              }]
            },
            options: {
              ...commonOptions,
              plugins: {
                legend: {
                  ...commonOptions.plugins.legend,
                  display: shouldShowLegend({
                    labels: chartsData.pieChart.labels,
                    datasets: [{ label: "" }]
                  }),
                  align: "center"
                }
              }
            }
          });
        }
        if (chartRefs.radar && chartsData.avgScoreByOrganizationRadar) {
          chartInstances.radar = new Chart(chartRefs.radar, {
            type: "radar",
            data: chartsData.avgScoreByOrganizationRadar,
            options: {
              ...commonOptions,
              plugins: {
                ...commonOptions.plugins,
                legend: {
                  ...commonOptions.plugins.legend,
                  display: shouldShowLegend(chartsData.avgScoreByOrganizationRadar)
                },
                title: {
                  display: true,
                  text: "Средний балл организаций по годам"
                }
              },
              scales: {
                r: {
                  beginAtZero: true,
                  min: 0,
                  max: 5
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
        chartRefs.pie = root.querySelector('[data-role="pie-chart"]');
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
    function buildAdminHistoryEntry(tab, id = null, modalData = null) {
      const surveyId = id ?? modalData?.id_survey ?? null;
      const userId = id ?? modalData?.id_user ?? null;
      const organizationId = id ?? modalData?.id_organization ?? modalData?.organizationId ?? null;
      switch (tab) {
        case "get_surveys":
          return { tab, id: null, url: "/surveys" };
        case "list_answers_users":
          return { tab, id: null, url: "/surveys/answers" };
        case "archived_surveys":
          return { tab, id: null, url: "/surveys/archive" };
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
          return { tab, id: null, url: "/users" };
        case "add_user":
          return { tab, id: null, url: "/users/create" };
        case "update_user":
          return userId ? { tab, id: userId, url: `/users/${userId}/edit` } : null;
        case "archived_users":
        case "archive_list_users":
          return { tab, id: null, url: "/users/archive" };
        case "get_organization":
          return { tab, id: null, url: "/organizations" };
        case "organization_surveys":
          return { tab, id: null, url: "/organizations/surveys" };
        case "add_organization":
          return { tab, id: null, url: "/organizations/create" };
        case "update_organization":
          return organizationId ? { tab, id: organizationId, url: `/organizations/${organizationId}/edit` } : null;
        case "archive_list_organizations":
          return { tab, id: null, url: "/organizations/archive" };
        case "reports":
          return { tab, id: null, url: "/reports" };
        case "get_logs":
          return { tab, id: null, url: "/logs" };
        case "email":
          return { tab, id: null, url: "/mail-settings" };
        case "help":
          return { tab, id: null, url: "/help" };
        default:
          return null;
      }
    }
    function getAdminHistoryEntryFromLocation(pathname) {
      const normalizedPath = normalizePathname(pathname);
      if (normalizedPath === "/surveys") {
        return buildAdminHistoryEntry("get_surveys");
      }
      if (normalizedPath === "/surveys/answers") {
        return buildAdminHistoryEntry("list_answers_users");
      }
      if (normalizedPath === "/surveys/archive") {
        return buildAdminHistoryEntry("archived_surveys");
      }
      if (normalizedPath === "/surveys/create") {
        return buildAdminHistoryEntry("add_survey");
      }
      if (normalizedPath === "/statistics") {
        return buildAdminHistoryEntry("open_statistics");
      }
      if (normalizedPath === "/users") {
        return buildAdminHistoryEntry("get_users");
      }
      if (normalizedPath === "/users/create") {
        return buildAdminHistoryEntry("add_user");
      }
      if (normalizedPath === "/users/archive") {
        return buildAdminHistoryEntry("archived_users");
      }
      if (normalizedPath === "/organizations") {
        return buildAdminHistoryEntry("get_organization");
      }
      if (normalizedPath === "/organizations/surveys") {
        return buildAdminHistoryEntry("organization_surveys");
      }
      if (normalizedPath === "/organizations/create") {
        return buildAdminHistoryEntry("add_organization");
      }
      if (normalizedPath === "/organizations/archive") {
        return buildAdminHistoryEntry("archive_list_organizations");
      }
      if (normalizedPath === "/reports") {
        return buildAdminHistoryEntry("reports");
      }
      if (normalizedPath === "/logs") {
        return buildAdminHistoryEntry("get_logs");
      }
      if (normalizedPath === "/mail-settings") {
        return buildAdminHistoryEntry("email");
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
    const initialHistoryEntry = getAdminHistoryEntryFromLocation(window.location.pathname) || buildAdminHistoryEntry("get_surveys");
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
      const currentUrl = normalizePathname(window.location.pathname);
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
      if (isLoading) {
        loaderTimer = window.setTimeout(() => {
          state.showLoader = true;
          renderLoader();
        }, 180);
      } else {
        state.showLoader = false;
        renderLoader();
      }
    };
    const renderLoader = () => {
      const existing = contentAdmin.querySelector(".loading-overlay");
      if (state.showLoader) {
        if (!existing) {
          const overlay = document.createElement("div");
          overlay.className = "loading-overlay";
          const text = document.createElement("div");
          text.textContent = "Загрузка...";
          overlay.appendChild(text);
          contentAdmin.appendChild(overlay);
        }
      } else if (existing) {
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
    const renderModal = () => {
      modalNode.className = `modal ${state.modal.isOpen ? "modal--visible" : ""}`;
      if (typeof modalCleanup === "function") {
        modalCleanup();
        modalCleanup = null;
      }
      modalBodyHost.innerHTML = "";
      if (!state.modal.isOpen) {
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
    const openTab = async (tab, id = null, options = {}) => {
      const historyMode = options.historyMode ?? "push";
      const force = options.force === true;
      const scrollMode = options.scrollMode ?? "restore";
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
        await fetchHtmlPage("/surveys");
        setActiveTabAndRefreshNav(tab);
        if (historyMode !== "none") {
          syncBrowserHistory(historyEntry, historyMode);
        }
        window.AppScrollState?.restoreCurrentPosition({ preferCarry: scrollMode === "carry" });
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
            await fetchHtmlPage("/surveys/answers");
            setActiveTabAndRefreshNav(tab);
            break;
          case "archived_surveys":
            await fetchHtmlPage("/surveys/archive");
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
            await fetchHtmlPage("/logs");
            setActiveTabAndRefreshNav(tab);
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
          case "get_users":
            await fetchHtmlPage("/users");
            setActiveTabAndRefreshNav(tab);
            break;
          case "get_organization":
            await fetchHtmlPage("/organizations");
            setActiveTabAndRefreshNav(tab);
            break;
          case "organization_surveys":
            await fetchHtmlPage("/organizations/surveys");
            setActiveTabAndRefreshNav(tab);
            break;
          case "copy_survey":
            if (!resolvedId) throw new Error("ID анкеты не указан.");
            await fetchHtmlPage(`/surveys/${resolvedId}/copy`);
            setActiveTabAndRefreshNav(tab);
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
            setModal({ isOpen: true, content: "message", message: result.message, isSuccess: true, data: null });
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
            setModal({ isOpen: true, content: "message", message, isSuccess: true, data: null });
            setActiveTabAndRefreshNav("get_users");
            break;
          }
          case "archive_list_organizations":
            await fetchHtmlPage("/organizations/archive");
            setActiveTabAndRefreshNav(tab);
            break;
          case "archived_users":
          case "archive_list_users":
            await fetchHtmlPage("/users/archive");
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
            window.open("/help_files/admin_survey_guide.docx", "_blank");
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
          case "email":
            await fetchHtmlPage("/mail-settings");
            setActiveTabAndRefreshNav(tab);
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
          window.AppScrollState?.restoreCurrentPosition({ preferCarry: scrollMode === "carry" });
        }
      } catch (error) {
        console.error("Ошибка переключения вкладки:", error);
        setModal({
          isOpen: true,
          content: "message",
          message: error.message || "Произошла ошибка загрузки.",
          isSuccess: false,
          data: null
        });
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
        setModal({
          isOpen: true,
          content: "message",
          message: result.message,
          isSuccess: true,
          data: null
        });
        setActiveTabAndRefreshNav("get_surveys");
      } catch (error) {
        console.error("Ошибка при удалении анкеты:", error);
        setModal({
          isOpen: true,
          content: "message",
          message: error.message || "Не удалось удалить анкету.",
          isSuccess: false,
          data: null
        });
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
    window.refreshAdminTab = (tabName, id = null, options = {}) => {
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
      if (isDirectNavDisabled) {
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
      const nextHistoryEntry = getAdminHistoryEntryFromLocation(targetUrl.pathname);
      if (!nextHistoryEntry) {
        return;
      }
      event.preventDefault();
      openTab(nextHistoryEntry.tab, nextHistoryEntry.id, { scrollMode: "carry" });
    });
    syncBrowserHistory(initialHistoryEntry, "replace");
    window.addEventListener("popstate", () => {
      const nextHistoryEntry = window.history.state?.tab ? buildAdminHistoryEntry(window.history.state.tab, window.history.state.id) : getAdminHistoryEntryFromLocation(window.location.pathname);
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
    openTab("get_surveys", null, { historyMode: "replace", force: true, scrollMode: "restore" });
  })();

  // Web/wwwroot/js/features/admin/admin-survey-edit.js
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
    surveyEditCloseModal("organizationModal");
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
        alert("Ошибка безопасности. Пожалуйста, обновите страницу.");
        return;
      }
      const formData = {
        Title: surveyTitle.value.trim(),
        Description: surveyDescription?.value.trim() || "",
        StartDate: new Date(startDate.value).toISOString(),
        EndDate: new Date(endDate.value).toISOString(),
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
        alert(result.message || "Анкета успешно обновлена!");
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
      alert(`Ошибка: ${userMessage}`);
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
    if (startDate.value && endDate.value && new Date(endDate.value) <= new Date(startDate.value)) {
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
    const startDate = document.getElementById("startDate").value;
    const endDate = document.getElementById("endDate").value;
    const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
    if (!startDate || !endDate) {
      showNotification("Пожалуйста, заполните все обязательные поля", false);
      return;
    }
    if (new Date(endDate) <= new Date(startDate)) {
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
        alert("Анкета успешно скопирована!");
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
