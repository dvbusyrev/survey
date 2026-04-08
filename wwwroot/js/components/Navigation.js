window.Navigation = ({ openTab, activeTab, userRole, userId }) => {
    const isAdmin = userRole === 'admin';
    const [openSubmenu, setOpenSubmenu] = React.useState(null);
    const listSubmenuIcon = 'fa-list-ul';
    const addSubmenuIcon = 'fa-plus';
    const buildListAndAddSubmenu = (listId, listLabel, listClass, addId, addLabel, addClass) => ([
        { id: listId, label: listLabel, class: listClass, icon: listSubmenuIcon },
        { id: addId, label: addLabel, class: addClass, icon: addSubmenuIcon }
    ]);
    const getSubmenuPriority = (label) => {
        if (label?.startsWith('Список')) return 0;
        if (label?.startsWith('Добавить')) return 1;
        return 2;
    };

    React.useEffect(() => {
        const handlePointerDown = (event) => {
            if (!event.target.closest('.admin-nav')) {
                setOpenSubmenu(null);
            }
        };

        document.addEventListener('pointerdown', handlePointerDown);
        return () => document.removeEventListener('pointerdown', handlePointerDown);
    }, []);

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

    const adminNavItems = [
        {
            id: 'open_statistics',
            label: 'Статистика',
            class: 'statistic',
            icon: 'fa-chart-line'
        },
        isAdmin
            ? {
                id: 'get_surveys',
                label: 'Анкеты',
                class: 'surveys',
                icon: 'fa-clipboard-list',
                submenu: [
                    ...buildListAndAddSubmenu(
                        'get_surveys',
                        'Список анкет',
                        'survey-list',
                        'add_survey',
                        'Добавить анкету',
                        'survey-add'
                    ),
                    { id: 'list_answers_users', label: 'Ответы на анкеты', class: 'survey-answers', icon: 'fa-list-check' },
                    { id: 'archived_surveys', label: 'Архив анкет', class: 'survey-archive', icon: 'fa-archive-docs' }
                ]
            }
            : {
                id: 'active',
                label: 'Анкеты',
                class: 'surveys',
                icon: 'fa-clipboard-list'
            },
        {
            id: 'get_users',
            label: 'Пользователи',
            class: 'users',
            icon: 'fa-users',
            submenu: buildListAndAddSubmenu(
                'get_users',
                'Список пользователей',
                'user-list',
                'add_user',
                'Добавить пользователя',
                'user-add'
            )
        },
        {
            id: 'get_organization',
            label: 'Организации',
            class: 'organizations',
            icon: 'fa-building',
            submenu: [
                ...buildListAndAddSubmenu(
                    'get_organization',
                    'Список организаций',
                    'org-list',
                    'add_organization',
                    'Добавить организацию',
                    'org-add'
                ),
                { id: 'archive_list_organizations', label: 'Архив организаций', class: 'org-archive', icon: 'fa-archive-docs' }
            ]
        },
        {
            id: 'reports',
            label: 'Отчёты',
            class: 'reports',
            icon: 'fa-file-alt',
            submenu: [
                { id: 'monthly_summary_report', label: 'Отчёт за месяц', class: 'monthly-summary-report', icon: 'fa-list-ul' },
                { id: 'quarterly_report_q1', label: 'Отчёт за 1 квартал', class: 'quarterly-report-q1', icon: 'fa-list-ul' },
                { id: 'quarterly_report_q2', label: 'Отчёт за 2 квартал', class: 'quarterly-report-q2', icon: 'fa-list-ul' },
                { id: 'quarterly_report_q3', label: 'Отчёт за 3 квартал', class: 'quarterly-report-q3', icon: 'fa-list-ul' },
                { id: 'quarterly_report_q4', label: 'Отчёт за 4 квартал', class: 'quarterly-report-q4', icon: 'fa-list-ul' }
            ]
        },
        {
            id: 'email',
            label: 'Почта',
            class: 'email',
            icon: 'fa-envelope'
        },
        {
            id: 'get_logs',
            label: 'Прочее',
            class: 'other',
            icon: 'fa-ellipsis-h',
            submenu: [
                { id: 'get_logs', label: 'Посмотреть логи', class: 'logs-view', icon: 'fa-scroll' },
                { id: 'download_logs', label: 'Выгрузить файл txt с логами', class: 'logs-download', icon: 'fa-download' }
            ]
        },
        {
            id: 'help',
            label: 'Помощь',
            class: 'help',
            icon: 'fa-question-circle'
        }
    ];

    const userNavItems = [
        {
            id: 'active',
            label: 'Анкеты',
            class: 'surveys',
            icon: 'fa-clipboard-list',
            submenu: [
                { id: 'active', label: 'Список анкет', class: 'survey-list', icon: 'fa-list-ul' },
                { id: 'archived_surveys_for_user', label: 'Архив анкет', class: 'survey-archive', icon: 'fa-archive-docs' }
            ]
        },
        {
            id: 'help',
            label: 'Помощь',
            class: 'help',
            icon: 'fa-question-circle'
        }
    ];

    const navItems = isAdmin ? adminNavItems : userNavItems;

    const orderedNavItems = navItems.map((item) => {
        if (!item.submenu) {
            return item;
        }

        return {
            ...item,
            submenu: [...item.submenu].sort((left, right) => {
                return getSubmenuPriority(left.label) - getSubmenuPriority(right.label);
            })
        };
    });

    return React.createElement('nav', {
        className: 'admin-nav',
        onMouseLeave: () => setOpenSubmenu(null)
    },
        React.createElement('ul', { className: 'nav-list' },
            orderedNavItems.map(item => {
                const itemActive = item.class === 'surveys'
                    ? isSurveySectionActive
                    : item.class === 'organizations'
                        ? isOrganizationSectionActive
                        : item.id === activeTab;

                return React.createElement('li', {
                    key: item.id,
                    className: [
                        'nav-item',
                        item.class || '',
                        itemActive ? 'active' : '',
                        item.submenu ? 'has-submenu' : '',
                        openSubmenu === item.id ? 'submenu-open' : ''
                    ].join(' ').trim(),
                    id: item.id,
                    onMouseEnter: () => item.submenu && setOpenSubmenu(item.id),
                    onMouseLeave: () => item.submenu && setOpenSubmenu(null)
                },
                    React.createElement('a', {
                        href: '#',
                        className: 'nav-link',
                        onClick: (e) => {
                            e.preventDefault();
                            if (item.submenu) {
                                setOpenSubmenu(openSubmenu === item.id ? null : item.id);
                                return;
                            }
                            e.currentTarget.blur();
                            navigate(item.id);
                        },
                        style: {
                            fontWeight: itemActive ? 'bold' : 'normal',
                            display: 'flex',
                            alignItems: 'center',
                            gap: '8px'
                        }
                    },
                        item.icon && React.createElement('i', {
                            className: `fas ${item.icon}`,
                            style: {
                                display: 'inline-flex',
                                alignItems: 'center',
                                justifyContent: 'center',
                                fontSize: '16px',
                                width: '20px',
                                minWidth: '20px',
                                height: '20px',
                                flex: '0 0 20px'
                            }
                        }),
                        item.label
                    ),
                    item.submenu && React.createElement('ul', { className: 'submenu-list' },
                        item.submenu.map(subItem =>
                            React.createElement('li', {
                                key: subItem.id,
                                className: [
                                    'submenu-item',
                                    subItem.class || '',
                                    subItem.id === activeTab ? 'active' : ''
                                ].join(' ').trim()
                            },
                                React.createElement('a', {
                                    href: '#',
                                    className: 'submenu-link',
                                    onClick: (e) => {
                                        e.preventDefault();
                                        e.currentTarget.blur();
                                        setOpenSubmenu(null);
                                        navigate(subItem.id);
                                    },
                                    style: {
                                        fontWeight: subItem.id === activeTab ? 'bold' : 'normal',
                                        display: 'flex',
                                        alignItems: 'center',
                                        gap: '8px'
                                    }
                                },
                                    subItem.icon && React.createElement('i', {
                                        className: `fas ${subItem.icon}`,
                                        style: {
                                            display: 'inline-flex',
                                            alignItems: 'center',
                                            justifyContent: 'center',
                                            fontSize: '14px',
                                            width: '20px',
                                            minWidth: '20px',
                                            height: '20px',
                                            flex: '0 0 20px'
                                        }
                                    }),
                                    subItem.label
                                )
                            )
                        )
                    )
                );
            })
        )
    );
};
