window.Navigation = ({ openTab, activeTab, userRole, userId }) => {
    const isAdmin = userRole === 'admin';
    const hostRef = React.useRef(null);

    const isSurveySectionActive = isAdmin
        ? ['get_surveys', 'add_survey', 'list_answers_users', 'archived_surveys'].includes(activeTab)
        : ['active', 'archived', 'answers_tab', 'archived_surveys_for_user'].includes(activeTab);
    const isOrganizationSectionActive = ['get_organization', 'add_organization', 'archive_list_organizations'].includes(activeTab);

    const navigate = (tab) => {
        setOpenSubmenu(null);

        if (tab === 'add_user') {
            const tryOpenAddUserModal = () => {
                if (typeof window.openAddUserModal === 'function' && document.getElementById('addUserModal')) {
                    window.openAddUserModal();
                    return true;
                }
                return false;
            };

            if (tryOpenAddUserModal()) {
                return;
            }

            if (typeof openTab === 'function') {
                openTab('get_users');

                let attempts = 0;
                const timer = window.setInterval(() => {
                    attempts += 1;
                    if (tryOpenAddUserModal() || attempts >= 30) {
                        window.clearInterval(timer);
                    }
                }, 200);
                return;
            }

            window.location.href = '/users';
            return;
        }

        if (tab === 'add_organization') {
            const tryOpenAddOrganizationModal = () => {
                if (typeof window.openAddOrganizationModal === 'function' && document.getElementById('addOrganizationModal')) {
                    window.openAddOrganizationModal();
                    return true;
                }
                return false;
            };

            if (tryOpenAddOrganizationModal()) {
                return;
            }

            if (typeof openTab === 'function') {
                openTab('get_organization');

                let attempts = 0;
                const timer = window.setInterval(() => {
                    attempts += 1;
                    if (tryOpenAddOrganizationModal() || attempts >= 30) {
                        window.clearInterval(timer);
                    }
                }, 200);
                return;
            }

            window.location.href = '/organizations/create';
            return;
        }

        if (typeof openTab === 'function') {
            openTab(tab);
            return;
        }

        if (tab === 'help') {
            window.location.href = '/help';
            return;
        }

        if ((tab === 'active' || tab === 'answers_tab') && userId) {
            window.location.href = '/my-surveys';
            return;
        }

        if ((tab === 'archived' || tab === 'archived_surveys_for_user') && userId) {
            window.location.href = '/my-surveys/archive';
            return;
        }

        const routes = {
            get_surveys: '/surveys',
            open_statistics: '/statistics',
            get_users: '/users',
            get_organization: '/organizations',
            archive_list_organizations: '/organizations/archive',
            reports: '/reports',
            email: '/mail-settings',
            get_logs: '/logs'
        };

        if (routes[tab]) {
            window.location.href = routes[tab];
            return;
        }

        if (tab === 'monthly_summary_report') {
            window.location.href = '/reports';
            return;
        }

        if (tab.startsWith('quarterly_report_q')) {
            window.location.href = '/reports';
        }
    };

    React.useEffect(() => {
        const host = hostRef.current;
        const templateId = isAdmin ? 'nav-template-admin' : 'nav-template-user';
        const template = document.getElementById(templateId);
        if (!host || !template?.content?.firstElementChild) {
            return;
        }

        host.innerHTML = '';
        const nav = template.content.firstElementChild.cloneNode(true);
        host.appendChild(nav);

        const closeSubmenus = () => {
            nav.querySelectorAll('.nav-item.has-submenu.submenu-open').forEach((item) => {
                item.classList.remove('submenu-open');
            });
        };

        nav.querySelectorAll('.nav-item').forEach((item) => {
            const tab = item.dataset.tab || '';
            const navClass = item.dataset.navClass || '';
            const isActive = navClass === 'surveys'
                ? isSurveySectionActive
                : navClass === 'organizations'
                    ? isOrganizationSectionActive
                    : tab === activeTab;
            item.classList.toggle('active', isActive);
        });

        nav.querySelectorAll('.submenu-item').forEach((subItem) => {
            subItem.classList.toggle('active', (subItem.dataset.tab || '') === activeTab);
        });

        nav.querySelectorAll('.nav-item.has-submenu').forEach((item) => {
            const onEnter = () => item.classList.add('submenu-open');
            const onLeave = () => item.classList.remove('submenu-open');
            item.addEventListener('mouseenter', onEnter);
            item.addEventListener('mouseleave', onLeave);
        });

        const navLeaveHandler = () => closeSubmenus();
        nav.addEventListener('mouseleave', navLeaveHandler);

        nav.querySelectorAll('.nav-link').forEach((link) => {
            link.addEventListener('click', (event) => {
                event.preventDefault();
                const item = event.currentTarget.closest('.nav-item');
                if (!item) {
                    return;
                }

                if (item.classList.contains('has-submenu')) {
                    const willOpen = !item.classList.contains('submenu-open');
                    closeSubmenus();
                    if (willOpen) {
                        item.classList.add('submenu-open');
                    }
                    return;
                }

                closeSubmenus();
                navigate(item.dataset.tab || '');
            });
        });

        nav.querySelectorAll('.submenu-link').forEach((link) => {
            link.addEventListener('click', (event) => {
                event.preventDefault();
                closeSubmenus();
                const item = event.currentTarget.closest('.submenu-item');
                navigate(item?.dataset?.tab || '');
            });
        });

        const onPointerDown = (event) => {
            if (!event.target.closest('.admin-nav')) {
                closeSubmenus();
            }
        };
        document.addEventListener('pointerdown', onPointerDown);

        return () => {
            document.removeEventListener('pointerdown', onPointerDown);
            nav.removeEventListener('mouseleave', navLeaveHandler);
        };
    }, [isAdmin, activeTab, isSurveySectionActive, isOrganizationSectionActive, openTab, userId, userRole]);

    return React.createElement('div', { ref: hostRef });
};
