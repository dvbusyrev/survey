
const HelpContent = window.HelpContent;
const SurveyFillPage = window.SurveyFillPage;
const CheckAnswersPage = window.CheckAnswersPage;

        const SurveyCard = ({ survey, isArchived, onClick }) => {
            const formatDate = (dateString) => {
                if (!dateString) return 'Не указано';
                try {
                    const date = new Date(dateString);
                    return isNaN(date.getTime()) ? 'Не указано' : date.toLocaleDateString('ru-RU');
                } catch {
                    return 'Не указано';
                }
            };

            if (!survey) return null;

            return (
                <div className="survey-card">
                    <div className="survey-card-content" onClick={() => onClick(survey)}>
                        <h3>{survey.name_survey || 'Без названия'}</h3>
                        <p className="description">{survey.description || 'Нет описания'}</p>
                        
                        {isArchived ? (
                            <p className="date-info">
                                <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                                    <path d="M20 14.66V20a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h5.34"></path>
                                    <polygon points="18 2 22 6 12 16 8 16 8 12 18 2"></polygon>
                                </svg>
                                Заполнено: {formatDate(survey.completion_date)}
                            </p>
                        ) : (
                            <p className="date-info">
                                <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                                    <circle cx="12" cy="12" r="10"></circle>
                                    <polyline points="12 6 12 12 16 14"></polyline>
                                </svg>
                                До завершения: <span className="time-left"></span>
                            </p>
                        )}

{isArchived && (
    <>
        <div className="date-info">
                      <svg className="signature-icon" xmlns="http://www.w3.org/2000/svg" width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                            <path d="M6 17L10 13L13 16L18 11M20 12C20 16.4183 16.4183 20 12 20C7.58172 20 4 16.4183 4 12C4 7.58172 7.58172 4 12 4C16.4183 4 20 7.58172 20 12Z" stroke="#5c6bc0" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"></path>
                            <polygon points="18 2 22 6 12 16 8 16 8 12 18 2"></polygon>
                        </svg>
            Подпись:
            <span className={`signature-status ${survey.csp ? 'signed' : 'not-signed'}`}>
                {survey.csp ? 'подписано' : 'не подписано'}
            </span>
        </div>
    </>
)}
                        
                        <div className="footer">
                            <div className="dates">
                                <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                                    <rect x="3" y="4" width="18" height="18" rx="2" ry="2"></rect>
                                    <line x1="16" y1="2" x2="16" y2="6"></line>
                                    <line x1="8" y1="2" x2="8" y2="6"></line>
                                    <line x1="3" y1="10" x2="21" y2="10"></line>
                                </svg>
                                {formatDate(survey.date_open)} - {formatDate(survey.date_close)}
                            </div>
                            <div className={`status ${isArchived ? 'archived' : 'active'}`}>
                                {isArchived ? 'Завершена' : 'Активна'}
                            </div>
                        </div>
                    </div>
                </div>
            );
        };
const SurveyContent = ({ 
    activeTab, 
    surveys, 
    loading, 
    error, 
    currentPage, 
    totalPages, 
    searchTerm, 
    handleSearch, 
    setSearchTerm, 
    handlePageChange, 
    handleTabChange, 
    activeCount, 
    archivedCount,
    onSurveyClick,
    dateFilter,
    handleDateChange,
    filterSigned,
    handleSignedFilterChange
}) => {
    // Функция для форматирования даты
    const formatDate = (dateString) => {
        if (!dateString) return 'Не указано';
        try {
            const date = new Date(dateString);
            return isNaN(date.getTime()) ? 'Не указано' : date.toLocaleDateString('ru-RU');
        } catch {
            return 'Не указано';
        }
    };

    // Функция проверки вхождения даты в диапазон
    const isDateInRange = (checkDate, rangeStart, rangeEnd) => {
        if (!checkDate) return false;
        
        const date = new Date(checkDate);
        if (isNaN(date.getTime())) return false;

        const startDate = new Date(rangeStart);
        const endDate = new Date(rangeEnd);
        
        // Устанавливаем время конца дня для endDate
        endDate.setHours(23, 59, 59, 999);

        return date >= startDate && date <= endDate;
    };

    // Фильтрация анкет
    const filteredSurveys = React.useMemo(() => {
        if (!dateFilter) return surveys;

        try {
            const filterDate = new Date(dateFilter);
            if (isNaN(filterDate.getTime())) return surveys;

            return surveys.filter(survey => {
                // Для активных анкет проверяем вхождение filterDate в период активности анкеты
                if (activeTab === 'active') {
                    return isDateInRange(dateFilter, survey.date_open, survey.date_close);
                }
                // Для архивных проверяем точное совпадение с датой завершения
                else {
                    return survey.completion_date 
                        ? new Date(survey.completion_date).toDateString() === filterDate.toDateString() 
                        : false;
                }
            });
        } catch {
            return surveys;
        }
    }, [surveys, dateFilter, activeTab, filterSigned]);

    // Применяем дополнительный фильтр по подписи
    const finalSurveys = React.useMemo(() => {
        let result = filteredSurveys;
        
        if (activeTab === 'archived' && filterSigned) {
            result = result.filter(survey => survey.csp);
        }
        
        return result;
    }, [filteredSurveys, filterSigned, activeTab]);

    return (
        <div className="content-wrapper">
            <div className="note">
                <h2>{activeTab === 'active' ? 'Доступные анкеты' : 'Пройденные анкеты'}</h2>
                <p>
                    {activeTab === 'active' 
                        ? 'Ниже вы можете ознакомиться с доступными вам анкетами' 
                        : 'Ниже вы можете ознакомиться с пройденными вами анкетами'}
                </p>
            </div>

            <div className="menu-bar">
                <div 
                    className={`menu-tab ${activeTab === 'active' ? 'active-tab' : ''}`}
                    onClick={() => handleTabChange('active')}
                >
                    Активные анкеты
                    <span className="count-badge">{activeCount}</span>
                </div>
                <div 
                    className={`menu-tab ${activeTab === 'archived' ? 'active-tab' : ''}`}
                    onClick={() => handleTabChange('archived')}
                >
                    Архив анкет
                    <span className="count-badge">{archivedCount}</span>
                </div>
            </div>

            {error && (
                <div className="error-message">
                    <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <circle cx="12" cy="12" r="10"></circle>
                        <line x1="12" y1="8" x2="12" y2="12"></line>
                        <line x1="12" y1="16" x2="12.01" y2="16"></line>
                    </svg>
                    {error}
                </div>
            )}

            <form onSubmit={handleSearch} className="search-container">
                <input
                    type="text"
                    className="search-box"
                    value={searchTerm}
                    onChange={(e) => setSearchTerm(e.target.value)}
                    placeholder="Поиск по названию анкеты..."
                />
                
<div className="filter-sort">
  <select
    id="filterOrganization"
    className="filter-select"
    onClick={() => window.populateMonthOptions && window.populateMonthOptions()}
    onChange={() => window.filterByDate && window.filterByDate()}
  >
    <option value="">За все месяцы</option>
  </select>

  <select
    id="filterSurvey"
    className="filter-select"
    onClick={() => window.populateYearOptions && window.populateYearOptions()}
    onChange={() => window.filterByDate && window.filterByDate()}
  >
    <option value="">По всем годам</option>
  </select>
</div>




                
                {activeTab === 'archived' && (
                    <label className="signed-filter">
                        <input 
                            type="checkbox" 
                            checked={filterSigned}
                            onChange={handleSignedFilterChange}
                        />
                        Только подписанные
                    </label>
                )}
            </form>

            {loading ? (
                <div className="loading">
                    <div className="loading-spinner"></div>
                    Загрузка анкет...
                </div>
            ) : (
                <>
   <div className="survey-grid">
  {finalSurveys.length > 0 ? (
    finalSurveys.map((survey, index) => (
      <div key={`survey-${survey.id_survey}-${index}`} className="survey-card">
        <div className="survey-card-content" onClick={() => onSurveyClick(survey)}>
          <h3>{survey.name_survey || 'Без названия'}</h3>
          <p className="description">{survey.description || 'Нет описания'}</p>

          {activeTab === 'archived' ? (
            <p className="date-info">
              <svg
                xmlns="http://www.w3.org/2000/svg"
                width="24"
                height="24"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              >
                <path d="M20 14.66V20a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h5.34"></path>
                <polygon points="18 2 22 6 12 16 8 16 8 12 18 2"></polygon>
              </svg>
              Заполнено: {formatDate(survey.completion_date)}
            </p>
          ) : (
            <p className="date-info">
              <svg
                xmlns="http://www.w3.org/2000/svg"
                width="24"
                height="24"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              >
                <circle cx="12" cy="12" r="10"></circle>
                <polyline points="12 6 12 12 16 14"></polyline>
              </svg>
              До завершения: <span className="time-left"></span>
            </p>
          )}

          {activeTab === 'archived' && (
            <div className="signature-info">
              <svg
                className="signature-icon"
                xmlns="http://www.w3.org/2000/svg"
                width="26"
                height="26"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
              >
                <path
                  d="M6 17L10 13L13 16L18 11M20 12C20 16.4183 16.4183 20 12 20C7.58172 20 4 16.4183 4 12C4 7.58172 7.58172 4 12 4C16.4183 4 20 7.58172 20 12Z"
                  stroke="#5c6bc0"
                  strokeWidth="2"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                ></path>
                <polygon points="18 2 22 6 12 16 8 16 8 12 18 2"></polygon>
              </svg>
              Подпись:
              <span className={`signature-status ${survey.csp ? 'signed' : 'not-signed'}`}>
                {survey.csp ? 'подписано' : 'не подписано'}
              </span>
            </div>
          )}

          <div className="footer">
            <div className="dates">
              <svg
                xmlns="http://www.w3.org/2000/svg"
                width="24"
                height="24"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              >
                <rect x="3" y="4" width="18" height="18" rx="2" ry="2"></rect>
                <line x1="16" y1="2" x2="16" y2="6"></line>
                <line x1="8" y1="2" x2="8" y2="6"></line>
                <line x1="3" y1="10" x2="21" y2="10"></line>
              </svg>
              {formatDate(survey.date_open)} - {formatDate(survey.date_close)}
            </div>
            <div className={`status ${activeTab === 'archived' ? 'archived' : 'active'}`}>
              {activeTab === 'archived' ? 'Завершена' : 'Активна'}
            </div>
          </div>
        </div>
      </div>
    ))
  ) : (
    <div className="no-surveys">
      <svg
        xmlns="http://www.w3.org/2000/svg"
        width="48"
        height="48"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
      >
        <circle cx="12" cy="13" r="2"></circle>
        <line x1="13.45" y1="11.55" x2="15.5" y2="9.5"></line>
        <path d="M6.4 20.35a9 9 0 1 1 11.2 0"></path>
      </svg>
      <p>Нет анкет, соответствующих критериям фильтрации</p>
    </div>
  )}
</div>


                    {activeTab === 'active' && finalSurveys.length > 0 && (
                        <div className="pagination">
                            <button 
                                onClick={() => handlePageChange(currentPage - 1)}
                                disabled={currentPage === 1}
                            >
                                Назад
                            </button>
                            <span>Страница {currentPage} из {totalPages}</span>
                            <button 
                                onClick={() => handlePageChange(currentPage + 1)}
                                disabled={currentPage >= totalPages}
                            >
                                Вперед
                            </button>
                        </div>
                    )}
                </>
            )}
        </div>
    );
};



            
window.renderSurveyUserList = function(initialData) {
            const SurveyList = () => {
            const [activeTab, setActiveTab] = React.useState('active');
            const [currentContent, setCurrentContent] = React.useState('surveys');
            const [currentView, setCurrentView] = React.useState('survey-list');
            const [currentSurvey, setCurrentSurvey] = React.useState(null);
            const [searchTerm, setSearchTerm] = React.useState(initialData.initialSearchTerm);
            const [loading, setLoading] = React.useState(false);
            const [showLoader, setShowLoader] = React.useState(false);
            const firstDataRenderRef = React.useRef(true);
            const [error, setError] = React.useState(null);
            const [surveys, setSurveys] = React.useState(initialData.initialSurveys || []);
            const [currentPage, setCurrentPage] = React.useState(initialData.initialPage);
            const [totalPages, setTotalPages] = React.useState(initialData.initialTotalPages);
            const [activeCount, setActiveCount] = React.useState(0);
            const [archivedCount, setArchivedCount] = React.useState(0);
            const [helpContent, setHelpContent] = React.useState('');
            const [dateFilter, setDateFilter] = React.useState('');
            const [filterSigned, setFilterSigned] = React.useState(false);

React.useEffect(() => {
    if (loading) {
        const timer = setTimeout(() => setShowLoader(true), 180);
        return () => clearTimeout(timer);
    }

    setShowLoader(false);
}, [loading]);

 const loadSurveyData = async (tab, page = 1, search = '', signedOnly = false, date = '') => {
    setLoading(true);
    setError(null);
    try {
        const endpoint = tab === 'active' 
            ? `/survey_list_user/${initialData.userId}?page=${page}&searchTerm=${search}&date=${date}`
            : `/get_list_archive/${initialData.userId}?searchTerm=${search}&signedOnly=${signedOnly}&date=${date}`;
        
        const response = await fetch(endpoint, {
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });
        
        if (!response.ok) throw new Error('Ошибка загрузки данных анкет');
        
        const data = await response.json();
        
        if (tab === 'archived') {
            setSurveys(data.accessibleSurveys || []);
            setArchivedCount(data.totalCount || 0);
            setCurrentPage(1);
            setTotalPages(1);
        } else {
            setSurveys(data.accessibleSurveys || []);
            setActiveCount(data.totalCount || 0);
            setCurrentPage(data.currentPage || 1);
            setTotalPages(data.totalPages || 1);
        }
        
    } catch (err) {
        console.error('Ошибка:', err);
        setError(err.message);
    } finally {
        setLoading(false);
    }
};

    React.useEffect(() => {
        const loadArchiveCount = async () => {
            try {
                const response = await fetch(`/get_list_archive/${initialData.userId}?countOnly=true`);
                if (!response.ok) throw new Error('Ошибка загрузки количества архивных анкет');
                const data = await response.json();
                setArchivedCount(data.totalCount || 0);
            } catch (err) {
                console.error('Ошибка загрузки количества архивных анкет:', err);
            }
        };
        
        loadArchiveCount();
    }, []);

    const loadArchiveCount = async () => {
    try {
        const response = await fetch(`/get_list_archive/${initialData.userId}?countOnly=true`);
        
        if (!response.ok) {
            const errorData = await response.json().catch(() => null);
            const errorMessage = errorData?.error || 'Ошибка сервера';
            throw new Error(errorMessage);
        }
        
        const data = await response.json();
        setArchivedCount(data.totalCount || 0);
    } catch (err) {
        console.error('Ошибка загрузки количества архивных анкет:', err);
        setError(`Не удалось загрузить количество архивных анкет: ${err.message}`);
    }
};

            const loadHelpContent = async () => {
                setLoading(true);
                setError(null);
                try {
                    const response = await fetch(`/get_file/csp`, {
                        headers: { 'X-Requested-With': 'XMLHttpRequest' }
                    });
                    
                    if (!response.ok) throw new Error('Ошибка загрузки справочной информации');
                    
                    const content = await response.text();
                    setHelpContent(content);
                    
                } catch (err) {
                    console.error('Файл с руководством успешно скачан!:', err);
                    setError(err.message);
                } finally {
                    setLoading(false);
                }
            };

            const loadCounts = async () => {
    try {
        // Загрузка счетчика активных анкет
        const activeResponse = await fetch(`/survey_list_user/${initialData.userId}?page=1&searchTerm=`, {
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });
        
        if (activeResponse.ok) {
            const activeData = await activeResponse.json();
            setActiveCount(activeData.totalCount || activeData.accessibleSurveys?.length || 0);
        }

        // Загрузка счетчика архивных анкет
        const archiveResponse = await fetch(`/get_list_archive/${initialData.userId}?searchTerm=`, {
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });
        
        if (archiveResponse.ok) {
            const archiveData = await archiveResponse.json();
            setArchivedCount(archiveData.totalCount || archiveData.accessibleSurveys?.length || 0);
        }
    } catch (err) {
        console.error('Ошибка загрузки счетчиков:', err);
    }
};


            // Загрузка счетчиков анкет
            // В методе loadCounts компонента SurveyList:
// const loadCounts = async () => {
//     try {
//         // Загрузка счетчика активных анкет
//         const activeResponse = await fetch(`/survey_list_user/${initialData.userId}?page=1&searchTerm=`, {
//             headers: { 'X-Requested-With': 'XMLHttpRequest' }
//         });
        
//         if (activeResponse.ok) {
//             const activeData = await activeResponse.json();
//             setActiveCount(activeData.totalCount || activeData.accessibleSurveys?.length || 0);
//         }

//         // Загрузка счетчика архивных анкет
//         const archiveResponse = await fetch(`/get_list_archive/${initialData.userId}?searchTerm=`, {
//             headers: { 'X-Requested-With': 'XMLHttpRequest' }
//         });
        
//         if (archiveResponse.ok) {
//             const archiveData = await archiveResponse.json();
//             setArchivedCount(archiveData.totalCount || archiveData.accessibleSurveys?.length || 0);
//         }
//     } catch (err) {
//         console.error('Ошибка загрузки счетчиков:', err);
//     }
// };

            const handleSurveyClick = (survey) => {
                setCurrentSurvey(survey);
                if (activeTab === 'active') {
                    setCurrentView('survey-fill');
                } else {
                    setCurrentView('check-answers');
                }
            };

            const handleBackToList = () => {
                setCurrentView('survey-list');
                setCurrentSurvey(null);
                loadSurveyData(activeTab, currentPage, searchTerm);
            };



    const handleTabChange = (tab) => {
        if (tab === 'help') {
            if (currentContent === 'help') return;
            setActiveTab('help');
            setCurrentContent('help');
            setCurrentView('survey-list');
            return;
        }

        if (currentContent === 'surveys' && activeTab === tab && currentView === 'survey-list') {
            return;
        }

        setCurrentContent('surveys');
        if (tab !== 'get_surveys') {
            setActiveTab(tab);
            setCurrentPage(1);
            setCurrentView('survey-list');
            loadSurveyData(tab, 1, searchTerm, filterSigned, dateFilter);
        }
    };

            React.useEffect(() => {
                if (activeTab === 'active' && currentView === 'survey-list') {
                    const updateTimeLeft = () => {
                        const now = new Date();
                        document.querySelectorAll('.time-left').forEach((el, index) => {
                            if (!surveys[index] || !surveys[index].date_close) return;
                            
                            const closeDate = new Date(surveys[index].date_close);
                            const diff = closeDate - now;

                            if (diff > 0) {
                                const days = Math.floor(diff / (1000 * 60 * 60 * 24));
                                const hours = Math.floor((diff % (1000 * 60 * 60 * 24)) / (1000 * 60 * 60));
                                el.textContent = `${days}д ${hours}ч`;
                            } else {
                                el.textContent = 'завершено';
                            }
                        });
                    };

                    updateTimeLeft();
                    const interval = setInterval(updateTimeLeft, 3600000);
                    return () => clearInterval(interval);
                }
            }, [surveys, activeTab, currentView]);

    React.useEffect(() => {
        if (currentView !== 'survey-list') return;

        if (firstDataRenderRef.current && currentContent === 'surveys' && activeTab === 'active' && currentPage === initialData.initialPage && searchTerm === initialData.initialSearchTerm && !filterSigned && !dateFilter) {
            firstDataRenderRef.current = false;
            return;
        }

        firstDataRenderRef.current = false;

        if (currentContent === 'surveys') {
            loadSurveyData(activeTab, currentPage, searchTerm, filterSigned, dateFilter);
        } else if (currentContent === 'help') {
            loadHelpContent();
        }
    }, [currentContent, activeTab, currentPage, searchTerm, currentView, filterSigned, dateFilter]);

const handleSearch = (e) => {
    e.preventDefault();
    loadSurveyData(activeTab, 1, searchTerm, filterSigned, dateFilter);
};

const handleDateChange = (e) => {
    const date = e.target.value;
    setDateFilter(date);
    loadSurveyData(activeTab, currentPage, searchTerm, filterSigned, date);
};

const handleSignedFilterChange = (e) => {
    const value = e.target.checked;
    setFilterSigned(value);
    if (activeTab === 'archived') {
        loadSurveyData('archived', 1, searchTerm, value, dateFilter);
    }
};

            const handlePageChange = (page) => // // Обработчик смены страницы
            {
                if (currentContent === 'surveys') {
                    setCurrentPage(page);
                }
            };

            // Если роль пользователя не подходит, показываем сообщение о запрете доступа
            if (initialData.userRole !== "user" && initialData.userRole !== "Админ") {
                return (
                    <div className="page-container">
                        <Header userRole={initialData.userRole} displayName={initialData.displayName} />
                        <div className="admin-container">
                            <Navigation openVkladka={handleTabChange} activeTab={currentContent === 'help' ? 'help' : activeTab} userRole={initialData.userRole} userId={initialData.userId} />
                            <div id="content_admin">
                                <div className="access-denied">
                                    <h2>Доступ запрещён</h2>
                                    <p>У вас нет прав для просмотра этой страницы.</p>
                                </div>
                            </div>
                        </div>
                        <Footer />
                    </div>
                );
            }

return (
  <div className="page-container">
    <Header userRole={initialData.userRole} displayName={initialData.displayName} />
    <div className="admin-container">
      <Navigation openVkladka={handleTabChange} activeTab={currentContent === 'help' ? 'help' : activeTab} userRole={initialData.userRole} userId={initialData.userId} />
      <div id="content_admin">
        {currentView === 'survey-fill' ? (
          <SurveyFillPage 
            survey={currentSurvey} 
            omsuId={initialData.userOmsuId}
            onBack={handleBackToList}
          />
        ) : currentView === 'check-answers' ? (
          <CheckAnswersPage
            survey={currentSurvey}
            omsuId={initialData.userOmsuId}
            userRole={initialData.userRole}
            onBack={handleBackToList}
          />
        ) : currentContent === 'surveys' ? (
          <SurveyContent 
      activeTab={activeTab}
      surveys={surveys}
      loading={showLoader}
      error={error}
      currentPage={currentPage}
      totalPages={totalPages}
      searchTerm={searchTerm}
      handleSearch={handleSearch}
      setSearchTerm={setSearchTerm}
      handlePageChange={handlePageChange}
      handleTabChange={handleTabChange}
      activeCount={activeCount}
      archivedCount={archivedCount}
      onSurveyClick={handleSurveyClick}
      dateFilter={dateFilter}
      handleDateChange={handleDateChange}
      filterSigned={filterSigned}
      handleSignedFilterChange={handleSignedFilterChange}
          />
        ) : (
          <HelpContent 
            content={helpContent}
            loading={showLoader}
            error={error}
          />
        )}
      </div>
    </div>
    <Footer />
  </div>
);
};

const root = ReactDOM.createRoot(document.getElementById('root'));
root.render(<SurveyList />);
};
