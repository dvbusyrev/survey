const Header = ({ userRole }) => {
    return (
        <header>
            <img src="/images/favicon.png" alt="Логотип"/>
            <h1 className="header-title">Анкетирование</h1>
            <p id="role">{userRole}</p>
            <button className="logout-button" onClick={() => window.location.href = '/Auth/logout'}>Выйти</button>
        </header>
    );
};

const Navigation = ({ openVkladka, activeTab }) => {
    const handleClick = (tab) => {
        openVkladka(tab);
    };

    return (
        <nav className="admin-nav">
            <ul>
                <li>
                    <a 
                        href="#" 
                        id="statistic" 
                        onClick={(e) => {
                            e.preventDefault();
                            handleClick('statistic');
                        }}
                        style={{
                            fontWeight: activeTab === 'statistic' ? 'bold' : 'normal',
                            backgroundColor: activeTab === 'statistic' ? '#ddf0ff' : 'transparent'
                        }}
                    >
                        Статистика
                    </a>
                </li>
                <li>
                    <a 
                        href="#" 
                        id="surveys" 
                        onClick={(e) => {
                            e.preventDefault();
                            handleClick('surveys');
                        }}
                        style={{
                            fontWeight: activeTab === 'surveys' ? 'bold' : 'normal',
                            backgroundColor: activeTab === 'surveys' ? '#ddf0ff' : 'transparent'
                        }}
                    >
                        Анкеты
                    </a>
                    <div className="submenu" id="submenu_survey">
                        <a 
                            href="#" 
                            id="list_surveys" 
                            onClick={(e) => {
                                e.preventDefault();
                                handleClick('surveys');
                            }}
                        >
                            Список анкет
                        </a>
                        <a 
                            href="#" 
                            id="list_answers" 
                            onClick={(e) => {
                                e.preventDefault();
                                handleClick('list_answers_users');
                            }}
                        >
                            Ответы на анкеты
                        </a>
                        <a 
                            href="#" 
                            id="create_survey" 
                            onClick={(e) => {
                                e.preventDefault();
                                handleClick('add_survey');
                            }}
                        >
                            Создание анкеты
                        </a>
                    </div>
                </li>
                <li>
                    <a 
                        href="#" 
                        id="users" 
                        onClick={(e) => {
                            e.preventDefault();
                            handleClick('users');
                        }}
                        style={{
                            fontWeight: activeTab === 'users' ? 'bold' : 'normal',
                            backgroundColor: activeTab === 'users' ? '#ddf0ff' : 'transparent'
                        }}
                    >
                        Пользователи
                    </a>
                    <div className="submenu">
                        <a 
                            href="#" 
                            onClick={(e) => {
                                e.preventDefault();
                                handleClick('users');
                            }}
                        >
                            Список пользователей
                        </a>
                        <a 
                            href="#" 
                            onClick={(e) => {
                                e.preventDefault();
                                handleClick('add_user');
                            }}
                        >
                            Добавление пользователя
                        </a>
                    </div>
                </li>
                <li>
                    <a 
                        href="#" 
                        id="omsu" 
                        onClick={(e) => {
                            e.preventDefault();
                            handleClick('omsu');
                        }}
                        style={{
                            fontWeight: activeTab === 'omsu' ? 'bold' : 'normal',
                            backgroundColor: activeTab === 'omsu' ? '#ddf0ff' : 'transparent'
                        }}
                    >
                        Организации
                    </a>
                    <div className="submenu">
                        <a 
                            href="#" 
                            onClick={(e) => {
                                e.preventDefault();
                                handleClick('omsu');
                            }}
                        >
                            Список организаций
                        </a>
                        <a 
                            href="#" 
                            onClick={(e) => {
                                e.preventDefault();
                                handleClick('add_omsu');
                            }}
                        >
                            Добавление организации
                        </a>
                    </div>
                </li>
                <li>
                    <a 
                        href="#" 
                        id="prochee" 
                        onClick={(e) => {
                            e.preventDefault();
                            handleClick('logs');
                        }}
                        style={{
                            fontWeight: activeTab === 'logs' ? 'bold' : 'normal',
                            backgroundColor: activeTab === 'logs' ? '#ddf0ff' : 'transparent'
                        }}
                    >
                        Прочее
                    </a>
                    <div className="submenu">
                        <a 
                            href="#" 
                            onClick={(e) => {
                                e.preventDefault();
                                handleClick('logs');
                            }}
                        >
                            Посмотреть логи
                        </a>
                        <a 
                            href="#" 
                            onClick={(e) => {
                                e.preventDefault();
                                window.location.href = '/Survey/dump_logs';
                            }}
                        >
                            Выгрузить файл txt с логами
                        </a>
                        <a 
                            href="#" 
                            style={{display: 'none'}} 
                            onClick={(e) => {
                                e.preventDefault();
                                handleClick('rassilka');
                            }}
                        >
                            Настройка рассылки
                        </a>
                    </div>
                </li>
                <li>
                    <a 
                        href="#" 
                        id="help" 
                        onClick={(e) => {
                            e.preventDefault();
                            handleClick('help');
                        }}
                        style={{
                            fontWeight: activeTab === 'help' ? 'bold' : 'normal',
                            backgroundColor: activeTab === 'help' ? '#ddf0ff' : 'transparent'
                        }}
                    >
                        Помощь2
                    </a>
                    <div className="submenu">
                        <a 
                            href="#" 
                            onClick={(e) => {
                                e.preventDefault();
                                window.location.href = '/Survey/get_file/ryk';
                            }}
                        >
                            Руководство администратора
                        </a>
                        <a 
                            href="#" 
                            onClick={(e) => {
                                e.preventDefault();
                                window.location.href = '/Survey/get_file/csp';
                            }}
                        >
                            Использование ЭЦП
                        </a>
                    </div>
                </li>
            </ul>
        </nav>
    );
};

const Footer = () => {
    return (
        <footer>
            <p>© 2024 АИС Анкетирование</p>
        </footer>
    );
};

const Layout = ({ children, userRole, openVkladka, activeTab }) => {
    return (
        <div className="page-container">
            <Header userRole={userRole} />
            <div className="admin-container">
                <Navigation openVkladka={openVkladka} activeTab={activeTab} />
                <div className="content">
                    {children}
                </div>
            </div>
            <Footer />
        </div>
    );
};

window.ReactComponents = {
    Header,
    Navigation,
    Footer,
    Layout
}; 