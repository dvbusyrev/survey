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
      let extensions = [{ organizationId: "", extendedUntil: "" }];
      const today = (/* @__PURE__ */ new Date()).toISOString().split("T")[0];
      const isFormValid = () => {
        return extensions.every((item) => item.organizationId && item.extendedUntil) && extensions.some((item) => item.extendedUntil > today);
      };
      const isOrganizationSelected = (organizationId, currentIndex) => {
        return extensions.some((item, index) => index !== currentIndex && item.organizationId === organizationId);
      };
      const handleChange = (index, field, value) => {
        extensions = extensions.map((item, itemIndex) => {
          if (itemIndex !== index) {
            return item;
          }
          return {
            ...item,
            [field]: value
          };
        });
        render();
      };
      const addExtensionRow = () => {
        extensions = [...extensions, { organizationId: "", extendedUntil: "" }];
        render();
      };
      const removeExtensionRow = (index) => {
        extensions = extensions.length > 1 ? extensions.filter((_, itemIndex) => itemIndex !== index) : extensions;
        render();
      };
      const handleSubmit = async () => {
        if (extensions.some((item) => !item.organizationId || !item.extendedUntil)) {
          alert("Пожалуйста, заполните все поля.");
          return;
        }
        if (extensions.some((item) => item.extendedUntil <= today)) {
          alert("Дата окончания должна быть в будущем.");
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
              extensions: extensions.map((item) => ({
                organizationId: parseInt(item.organizationId, 10),
                extendedUntil: item.extendedUntil
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
            throw new Error(
              responseData?.message || responseData?.error || responseText || (window.getResponseErrorMessage ? window.getResponseErrorMessage(response, "Ошибка продления") : `Ошибка продления: ${response.status}`)
            );
          }
          alert(responseData.message || "Доступ успешно продлён.");
          onClose();
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
        const addRowButton = root.querySelector('[data-role="add-row"]');
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
        if (addRowButton) {
          addRowButton.style.display = showRows ? "" : "none";
        }
        if (showRows && rowsContainer) {
          extensions.forEach((extension, index) => {
            const row = rowTemplate.content.firstElementChild.cloneNode(true);
            const orgSelect = row.querySelector('[data-role="org-select"]');
            const dateInput = row.querySelector('[data-role="date-input"]');
            const removeButton = row.querySelector('[data-role="remove-row"]');
            if (orgSelect) {
              const defaultOption = document.createElement("option");
              defaultOption.value = "";
              defaultOption.textContent = "-- Выберите организацию --";
              orgSelect.appendChild(defaultOption);
              organizations.forEach((organization) => {
                const option = document.createElement("option");
                const alreadySelected = isOrganizationSelected(organization.organizationId, index);
                option.value = organization.organizationId;
                option.disabled = alreadySelected;
                option.textContent = `${organization.organizationName}${alreadySelected ? " (уже выбрана)" : ""}`;
                if (extension.organizationId === organization.organizationId) {
                  option.selected = true;
                }
                orgSelect.appendChild(option);
              });
              orgSelect.addEventListener("change", (event) => {
                handleChange(index, "organizationId", event.target.value);
              });
            }
            if (dateInput) {
              dateInput.value = extension.extendedUntil;
              dateInput.min = today;
              dateInput.addEventListener("change", (event) => {
                handleChange(index, "extendedUntil", event.target.value);
              });
            }
            if (removeButton) {
              removeButton.style.display = extensions.length > 1 ? "" : "none";
              removeButton.addEventListener("click", () => removeExtensionRow(index));
            }
            rowsContainer.appendChild(row);
          });
        }
        if (addRowButton) {
          addRowButton.addEventListener("click", addExtensionRow);
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
          organizations = Array.isArray(data) ? data.filter((org) => org && (org.organization_id !== void 0 || org.id !== void 0)).map((org) => ({
            organizationId: String(org.organization_id ?? org.id),
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
    function normalizePathname(pathname) {
      if (!pathname) {
        return "/";
      }
      return pathname.length > 1 && pathname.endsWith("/") ? pathname.slice(0, -1) : pathname;
    }
    function buildAdminHistoryEntry(tab, id = null, modalData = null) {
      const surveyId = id ?? modalData?.id_survey ?? null;
      const userId = id ?? modalData?.id_user ?? null;
      const organizationId = id ?? modalData?.organization_id ?? null;
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
        case "open_statistics":
          return { tab, id: null, url: "/statistics" };
        case "get_users":
          return { tab, id: null, url: "/users" };
        case "add_user":
          return { tab, id: null, url: "/users/create" };
        case "update_user":
          return userId ? { tab, id: userId, url: `/users/${userId}/edit` } : null;
        case "archive_list_users":
          return { tab, id: null, url: "/users/archive" };
        case "get_organization":
          return { tab, id: null, url: "/organizations" };
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
        return buildAdminHistoryEntry("archive_list_users");
      }
      if (normalizedPath === "/organizations") {
        return buildAdminHistoryEntry("get_organization");
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
    function extractRenderableHtml(html) {
      if (!html) {
        return "";
      }
      try {
        const parser = new DOMParser();
        const documentFragment = parser.parseFromString(html, "text/html");
        documentFragment.querySelectorAll("link, style, script, meta, title").forEach((node) => node.remove());
        return documentFragment.body && documentFragment.body.innerHTML.trim() ? documentFragment.body.innerHTML : html;
      } catch (error) {
        console.error("Ошибка парсинга HTML:", error);
        return html;
      }
    }
    function createContentWrapper() {
      const wrapper = document.createElement("div");
      wrapper.className = "content-wrapper";
      return wrapper;
    }
    const rootElement = document.getElementById("root");
    if (!rootElement) {
      return;
    }
    const initialData = window.__adminBootstrap || {};
    const initialHistoryEntry = getAdminHistoryEntryFromLocation(window.location.pathname) || buildAdminHistoryEntry("get_surveys");
    const userRole = initialData.userRole || "";
    const hasAccess = Boolean(userRole);
    const availablePages = window.AdminInlineAppPages || {};
    const mountExtensionModal = availablePages.mountExtensionModal;
    const mountStatisticsPage = availablePages.mountStatisticsPage;
    if (!hasAccess) {
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
    rootElement.innerHTML = "";
    const pageContainer = document.createElement("div");
    pageContainer.className = "page-container";
    const headerHost = document.createElement("div");
    const adminContainer = document.createElement("div");
    adminContainer.className = "admin-container";
    const navHost = document.createElement("div");
    const contentAdmin = document.createElement("div");
    contentAdmin.id = "content_admin";
    const footerHost = document.createElement("div");
    const modalNode = document.createElement("div");
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
    adminContainer.appendChild(navHost);
    adminContainer.appendChild(contentAdmin);
    pageContainer.appendChild(headerHost);
    pageContainer.appendChild(adminContainer);
    pageContainer.appendChild(footerHost);
    pageContainer.appendChild(modalNode);
    rootElement.appendChild(pageContainer);
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
    const setHtmlContent = (html) => {
      const parsedHtml = extractRenderableHtml(html);
      const parser = new DOMParser();
      const parsedDocument = parser.parseFromString(parsedHtml, "text/html");
      const fragment = document.createDocumentFragment();
      Array.from(parsedDocument.body.childNodes).forEach((node) => {
        fragment.appendChild(node.cloneNode(true));
      });
      setContentMount((host) => {
        host.appendChild(fragment);
        return null;
      });
    };
    const fetchHtmlPage = async (endpoint, options) => {
      const response = await fetch(endpoint, options);
      if (!response.ok) {
        throw new Error(
          window.getResponseErrorMessage ? window.getResponseErrorMessage(response, "Ошибка загрузки") : `Ошибка загрузки: ${response.status}`
        );
      }
      const html = await response.text();
      setHtmlContent(html);
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
          title.textContent = "Сформировать отчёт";
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
    const openTab = async (tab, id = null, options = {}) => {
      const historyMode = options.historyMode ?? "push";
      const force = options.force === true;
      const historyEntry = buildAdminHistoryEntry(tab, id, state.modal.data);
      const resolvedId = historyEntry?.id ?? id ?? null;
      if (!force && state.activeTab === tab && resolvedId === (window.history.state?.id ?? null)) {
        return;
      }
      if (tab === "get_surveys") {
        await fetchHtmlPage("/surveys");
        setActiveTabAndRefreshNav(tab);
        if (historyMode !== "none") {
          syncBrowserHistory(historyEntry, historyMode);
        }
        return;
      }
      setLoading(true);
      try {
        switch (tab) {
          case "open_statistics":
            if (typeof mountStatisticsPage !== "function") {
              throw new Error("Модуль статистики не загружен.");
            }
            setContentMount((host) => mountStatisticsPage(host));
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
            await fetchHtmlPage("/surveys/create");
            setActiveTabAndRefreshNav(tab);
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
          case "copy_survey":
            if (!resolvedId) throw new Error("ID анкеты не указан.");
            await fetchHtmlPage(`/surveys/${resolvedId}/copy`);
            setActiveTabAndRefreshNav(tab);
            break;
          case "update_survey":
            if (!resolvedId) throw new Error("ID анкеты не указан.");
            await fetchHtmlPage(`/surveys/${resolvedId}/edit`);
            setActiveTabAndRefreshNav(tab);
            break;
          case "delete_survey": {
            const result = await deleteSurvey(state.modal.data?.id_survey);
            setModal({ isOpen: true, content: "message", message: result.message, isSuccess: true, data: null });
            setActiveTabAndRefreshNav("get_surveys");
            break;
          }
          case "add_user":
            await fetchHtmlPage("/users/create");
            setActiveTabAndRefreshNav(tab);
            break;
          case "update_user":
            if (!resolvedId) throw new Error("ID пользователя не указан.");
            await fetchHtmlPage(`/users/${resolvedId}/edit`);
            setActiveTabAndRefreshNav(tab);
            break;
          case "delete_user":
            await fetchHtmlPage(`/users/${state.modal.data?.id_user}/delete`, {
              method: "POST",
              headers: { RequestVerificationToken: getRequestVerificationToken() }
            });
            setActiveTabAndRefreshNav("get_users");
            break;
          case "archive_list_organizations":
            await fetchHtmlPage("/organizations/archive");
            setActiveTabAndRefreshNav(tab);
            break;
          case "archive_list_users":
            await fetchHtmlPage("/users/archive");
            setActiveTabAndRefreshNav(tab);
            break;
          case "add_organization":
            await fetchHtmlPage("/organizations/create");
            setActiveTabAndRefreshNav(tab);
            break;
          case "update_organization":
            if (!resolvedId) throw new Error("ID организации не указан.");
            await fetchHtmlPage(`/organizations/${resolvedId}/edit`);
            setActiveTabAndRefreshNav(tab);
            break;
          case "delete_organization":
            await fetchHtmlPage(`/organizations/${state.modal.data?.organization_id}/delete`, {
              method: "POST",
              headers: { RequestVerificationToken: getRequestVerificationToken() }
            });
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
          const nextHistory = ["delete_survey"].includes(tab) ? buildAdminHistoryEntry("get_surveys") : ["delete_user"].includes(tab) ? buildAdminHistoryEntry("get_users") : ["delete_organization"].includes(tab) ? buildAdminHistoryEntry("get_organization") : ["monthly_summary_report", "quarterly_report_q1", "quarterly_report_q2", "quarterly_report_q3", "quarterly_report_q4"].includes(tab) ? buildAdminHistoryEntry("reports") : historyEntry;
          syncBrowserHistory(nextHistory, ["delete_survey", "delete_user", "delete_organization"].includes(tab) ? "replace" : historyMode);
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
    window.handleTabClick = (tabName) => {
      openTab(tabName);
    };
    syncBrowserHistory(initialHistoryEntry, "replace");
    window.addEventListener("popstate", () => {
      const nextHistoryEntry = window.history.state?.tab ? buildAdminHistoryEntry(window.history.state.tab, window.history.state.id) : getAdminHistoryEntryFromLocation(window.location.pathname);
      if (nextHistoryEntry) {
        openTab(nextHistoryEntry.tab, nextHistoryEntry.id, {
          historyMode: "none",
          force: true
        });
      }
    });
    if (initialHistoryEntry?.tab && initialHistoryEntry.tab !== "get_surveys") {
      window.setTimeout(() => {
        openTab(initialHistoryEntry.tab, initialHistoryEntry.id, {
          historyMode: "replace",
          force: true
        });
      }, 0);
    } else {
      openTab("get_surveys", null, { historyMode: "replace", force: true });
    }
  })();
})();
//# sourceMappingURL=admin-inline-app.js.map
