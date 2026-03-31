window.Navigation = ({ openVkladka, activeTab }) => {
    const navItems = [
        { 
            id: 'open_statistic', 
            label: 'Статистика',
            class: 'statistic',
            icon: 'fa-chart-line' 
        },
        { 
            id: 'get_surveys', 
            label: 'Анкеты',
            class: 'surveys',
            icon: 'fa-clipboard-list',
            submenu: [
                { id: 'add_survey', label: 'Добавить анкету', class: 'survey-add', icon: 'fa-plus-circle' },
                { id: 'list_answers_users', label: 'Ответы на анкеты', class: 'survey-answers', icon: 'fa-list-check' },
                { id: 'archiv_surveys', label: 'Архив анкет', class: 'survey-archive', icon: 'fa-box-archive' },
                { id: 'answers_tab', label: 'Активные анкеты', class: 'survey-answers-for-user', icon: 'fa-list-check' },
                { id: 'archiv_surveys_for_user', label: 'Архив анкет', class: 'survey-archive-for-user', icon: 'fa-box-archive' }
            ]
        },
        { 
            id: 'get_users',
            label: 'Пользователи',
            class: 'users',
            icon: 'fa-users',
            submenu: [
                { id: 'add_user', label: 'Добавить пользователя', class: 'user-add', icon: 'fa-user-plus' },
                { id: 'get_users', label: 'Список пользователей', class: 'user-list', icon: 'fa-list' }
            ]
        },
        { 
            id: 'get_omsu', 
            label: 'Организации',
            class: 'organizations',
            icon: 'fa-building',
            submenu: [
                { id: 'add_omsu', label: 'Добавить организацию', class: 'org-add', icon: 'fa-plus' },
                { id: 'get_omsu', label: 'Список организаций', class: 'org-list', icon: 'fa-list-ul' }
            ]
        },

        { 
            id: 'otchets', 
            label: 'Отчёты',
            class: 'otchets',
            icon: 'fa-file-alt',
            submenu: [
                { id: 'otchet_month', label: 'Отчёт за месяц', class: 'otchet-month', icon: 'fa-list-ul' },
                { id: 'otchet-1kv', label: 'Отчёт за 1 квартал', class: 'otchet-1kv', icon: 'fa-list-ul' },
                { id: 'otchet-2kv', label: 'Отчёт за 2 квартал', class: 'otchet-2kv', icon: 'fa-list-ul' },
                { id: 'otchet-3kv', label: 'Отчёт за 3 квартал', class: 'otchet-3kv', icon: 'fa-list-ul' },
                { id: 'otchet-4kv', label: 'Отчёт за 4 квартал', class: 'otchet-4kv', icon: 'fa-list-ul' }
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
                { id: 'download_logs', label: 'Выгрузить файл txt с логами', class: 'logs-download', icon: 'fa-file-download' }
            ]
        },

        { 
            id: 'help', 
            label: 'Помощь',
            class: 'help',
            icon: 'fa-question-circle' 
        }
    ];

    return React.createElement('nav', { className: 'admin-nav' },
        React.createElement('ul', { className: 'nav-list' },
            navItems.map(item => 
                React.createElement('li', { 
                    key: item.id,
                    className: [
                        'nav-item',
                        item.class || '',
                        item.id === activeTab ? 'active' : '',
                        item.submenu ? 'has-submenu' : ''
                    ].join(' ').trim(),
                    id: item.id
                },
                    React.createElement('a', { 
                        href: '#', 
                        className: 'nav-link',
                        onClick: (e) => { 
                            e.preventDefault(); 
                            openVkladka(item.id); 
                        },
                        style: { 
                            fontWeight: item.id === activeTab ? 'bold' : 'normal',
                            display: 'flex',
                            alignItems: 'center',
                            gap: '8px'
                        }
                    }, 
                        item.icon && React.createElement('i', { 
                            className: `fas ${item.icon}`,
                            style: { fontSize: '16px', minWidth: '20px' }
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
                                        openVkladka(subItem.id); 
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
                                        style: { fontSize: '14px', minWidth: '20px' }
                                    }),
                                    subItem.label
                                )
                            )
                        )
                    )
                )
            )
        )
    );
}; 