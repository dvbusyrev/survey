        var last_page = "";
var newPage = "";

 const { useState, useEffect, useRef } = React;
    const { Header, Footer, Navigation } = window;

    const App = () => {
        // Константы должны быть объявлены внутри компонента
        const recordsPerPage = 10; // Добавляем определение
        
        // Хуки
        const [activeTab, setActiveTab] = useState('get_surveys');
        const [content, setContent] = useState(null);
        const [loading, setLoading] = useState(false);
        const [showLoader, setShowLoader] = useState(false);
        const [monthFilter, setMonthFilter] = useState('');
        const [organizationFilter, setOrganizationFilter] = useState('');
        const [currentPage, setCurrentPage] = useState(1);
        const [modal, setModal] = useState({
            isOpen: false,
            content: '',
            data: null,
            message: null,
            isSuccess: false
        });
        const [searchTerm, setSearchTerm] = useState('');
        
        // Проверка доступа
        const initialData = window.__adminBootstrap || {};
        const userRole = initialData.userRole || '';
        const hasAccess = !!userRole;

        if (userRole == "admin") {
    last_page = "get_surveys";
} else {
    last_page = "survey_list_user";
}

useEffect(() => {
  window.handleTabClick = handleTabClick;
}, [handleTabClick]);

useEffect(() => {
  const timer = setTimeout(() => {
    if (window.initPasswordToggles) {
      window.initPasswordToggles(document);
    }
  }, 0);

  return () => clearTimeout(timer);
}, [content]);

        useEffect(() => {
            if (loading) {
                const timer = setTimeout(() => setShowLoader(true), 180);
                return () => clearTimeout(timer);
            }

            setShowLoader(false);
        }, [loading]);

        if (!hasAccess) {
            return (
                <div className="access-denied">
                    <h2>Доступ запрещён</h2>
                    <p>У вас нет прав для просмотра этой страницы.</p>
                     <br/>
                    <a href="/" className="btn">Вернуться на страницу авторизации</a>
                </div>
            );
        }

        const getCurrentRecords = (surveys) => {
            const filtered = filterSurveys(surveys);
            const indexOfLastRecord = currentPage * recordsPerPage;
            const indexOfFirstRecord = indexOfLastRecord - recordsPerPage;
            return {
                currentRecords: filtered.slice(indexOfFirstRecord, indexOfLastRecord),
                totalPages: Math.ceil(filtered.length / recordsPerPage),
                totalRecords: filtered.length
            };
        };

const ExtensionModal = ({ survey, onClose }) => {
    const [organizations, setOrganizations] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [extensions, setExtensions] = useState([{ organizationId: '', extendedUntil: '' }]);
    
    const today = new Date().toISOString().split('T')[0];

    const isFormValid = () => {
        return extensions.every(ext => ext.organizationId && ext.extendedUntil) && 
               extensions.some(ext => ext.extendedUntil > today);
    };

    useEffect(() => {
        const fetchOrganizations = async () => {
            try {
                setLoading(true);
                const response = await fetch('/organizations/data');
                
                if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);
                
                const data = await response.json();
                
                const orgs = Array.isArray(data) 
                    ? data
                        .filter(org => org && (org.organization_id !== undefined || org.id !== undefined))
                        .map(org => ({
                            organizationId: String(org.organization_id ?? org.id),
                            organization_name: String(org.organization_name ?? org.name)
                        }))
                    : [];

                setOrganizations(orgs);
                setError(null);
            } catch (err) {
                console.error('Ошибка загрузки организации:', err);
                setError('Не удалось загрузить список организаций');
            } finally {
                setLoading(false);
            }
        };

        fetchOrganizations();
    }, []);

    const isOrganizationSelected = (orgId, currentIndex) => {
        return extensions.some((ext, idx) => idx !== currentIndex && ext.organizationId === orgId);
    };

    const handleChange = (index, field, value) => {
        const newExtensions = [...extensions];
        newExtensions[index][field] = value;
        setExtensions(newExtensions);
    };

    const addExtension = () => {
        setExtensions([...extensions, { organizationId: '', extendedUntil: '' }]);
    };

    const removeExtension = (index) => {
        if (extensions.length > 1) {
            setExtensions(extensions.filter((_, i) => i !== index));
        }
    };

 const handleSubmit = async () => {
    if (extensions.some(ext => !ext.organizationId || !ext.extendedUntil)) {
        alert('Пожалуйста, заполните все поля');
        return;
    }

    if (extensions.some(ext => ext.extendedUntil <= today)) {
        alert('Дата окончания должна быть в будущем');
        return;
    }

    try {
        const requestData = {
            surveyId: survey.id_survey,
            extensions: extensions.map(ext => ({
                organizationId: parseInt(ext.organizationId),
                extendedUntil: ext.extendedUntil
            }))
        };


        const response = await fetch('/survey-extensions', {
            method: 'POST',
            headers: { 
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value
            },
            body: JSON.stringify(requestData)
        });

        const responseText = await response.text();
        let responseData;
        
        try {
            responseData = JSON.parse(responseText);
        } catch (e) {
            console.error('Ошибка парсинга JSON:', responseText);
            throw new Error(responseText || 'Не удалось обработать ответ сервера');
        }

        if (!response.ok || !responseData?.success) {
            const errorMsg = responseData?.message || 
                            responseData?.error || 
                            (window.getResponseErrorMessage
                                ? window.getResponseErrorMessage(response, 'Ошибка сервера')
                                : `Ошибка сервера: ${response.status}`);
            throw new Error(errorMsg);
        }

        alert(responseData.message || 'Доступ успешно продлён');
        onClose();
        window.location.reload();
    } catch (error) {
        console.error('Ошибка продления:', error);
        alert(`Ошибка: ${error.message}`);
    }
};

    return (
        <div style={{ maxWidth: '800px', margin: '0 auto', padding: '20px' }}>
            <h2 className='h2_modal' style={{ marginTop: 0, marginBottom: '16px', fontSize: '24px' }}>Продление анкеты</h2>
            <p style={{ marginBottom: '24px', color: '#555' }}>Анкета: "{survey?.name_survey}"</p>
            
            {/* Форма */}
            {!loading && organizations.length > 0 && (
                <>
                    <div style={{ marginBottom: '20px' }}>
                        {extensions.map((ext, index) => (
                            <div key={index} style={{ display: 'flex', gap: '12px', marginBottom: '16px', alignItems: 'flex-end' }}>
                                <div style={{ flex: 1 }}>
                                    <label>ОМСУ:</label>
                                    <select
                                        value={ext.organizationId}
                                        onChange={(e) => handleChange(index, 'organizationId', e.target.value)}
                                        style={{ width: '100%', padding: '10px', border: '1px solid #ddd', borderRadius: '4px' }}
                                        required
                                    >
                                        <option value="">-- Выберите ОМСУ --</option>
                                        {organizations.map((org) => {
                                            const alreadySelected = isOrganizationSelected(org.organizationId, index);
                                            return React.createElement('option', {
                                                key: org.organizationId,
                                                value: org.organizationId,
                                                disabled: alreadySelected
                                            }, `${org.organization_name}${alreadySelected ? ' (уже выбрана)' : ''}`);
                                        })}
                                    </select>
                                </div>

                                <div style={{ flex: 1 }}>
                                    <label>Дата окончания:</label>
                                    <input
                                        type="date"
                                        value={ext.extendedUntil}
                                        onChange={(e) => handleChange(index, 'extendedUntil', e.target.value)}
                                        style={{ width: '100%', padding: '10px', border: '1px solid #ddd', borderRadius: '4px' }}
                                        min={today}
                                        required
                                    />
                                </div>

                                {extensions.length > 1 && (
                                    <button
                                        onClick={() => removeExtension(index)}
                                        style={{
                                            background: '#ffebee',
                                            color: '#e53935',
                                            border: '1px solid #ffcdd2',
                                            padding: '10px 15px',
                                            borderRadius: '4px',
                                            cursor: 'pointer',
                                            height: '40px'
                                        }}
                                    >
                                        Удалить
                                    </button>
                                )}
                            </div>
                        ))}
                    </div>

                    <button
                        onClick={addExtension}
                    >
                        + Добавить организацию
                    </button><br />
                </>
            )}

            {/* Кнопки действий */}
            <br /><div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
                            <button
                    onClick={handleSubmit}
                    disabled={!isFormValid() || loading}
                    className='modal_btn modal_btn-primary'
                    style={{
                        backgroundColor: isFormValid() ? '#4caf50' : '#9e9e9e',
                        cursor: isFormValid() ? 'pointer' : 'not-allowed',
                        opacity: isFormValid() ? 1 : 0.6
                    }}
                >
                    {loading ? 'Обработка...' : 'Продлить доступ'}
                </button>
                <button
                    onClick={onClose}
                    className='modal_btn modal_btn-secondary'
                    style={{
                        padding: '10px 20px',
                        backgroundColor: '#f5f5f5',
                        color: '#333',
                        border: '1px solid #e0e0e0',
                        borderRadius: '4px',
                        cursor: 'pointer'
                    }}
                >
                    Отмена
                </button>
            </div>
        </div>
    );
};

                    
  <div className="filter-sort">
    <input 
        type="text" 
        placeholder="Поиск..." 
        value={searchTerm}
        onChange={(e) => setSearchTerm(e.target.value)} 
    />
    
    {/* Фильтр по месяцу */}
    <select
        value={monthFilter}
        onChange={(e) => setMonthFilter(e.target.value)}
        className="filter-select"
    >
        <option value="">Все месяцы</option>
        {Array.from({ length: 12 }, (_, i) => {
            const monthNum = i + 1;
            return React.createElement(
                'option',
                { key: monthNum, value: monthNum },
                new Date(0, i).toLocaleString('ru', { month: 'long' })
            );
        })}
    </select>
    
    {/* Фильтр по ОМСУ */}
    <select
        value={organizationFilter}
        onChange={(e) => setOrganizationFilter(e.target.value)}
        className="filter-select"
    >
        <option value="">Все ОМСУ</option>
        {Array.from(new Set(
            initialData.surveys.flatMap(s => 
                s.organization_name ? s.organization_name.split(',').map(name => name.trim()) : []
            ).filter(Boolean)
        )).map((organization, index) => React.createElement(
            'option',
            { key: index, value: organization },
            organization
        ))}
    </select>
    
    <button onClick={() => openTab('add_survey')}>
        Добавить
    </button>
    
    {(monthFilter || organizationFilter) && (
        <button 
            onClick={() => {
                setMonthFilter('');
                setOrganizationFilter('');
            }}
            className="reset-filter-btn"
        >
            Сбросить фильтры
        </button>
    )}
</div>

        const renderMenuAndFilters = (isFixed = false) => (
            <div className={isFixed ? "fixed-menu-container" : ""}>
                <div className="menu-bar">
                    <div 
                        className={`menu-tab ${activeTab === 'get_surveys' ? 'active-tab' : ''}`}
                        onClick={() => handleTabClick('get_surveys')}>
                        Анкеты
                    </div>
                    <div 
                        className={`menu-tab ${activeTab === 'list_answers_users' ? 'active-tab' : ''}`}
                        onClick={() => handleTabClick('list_answers_users')}>
                        Ответы
                    </div>
                    <div 
                        className={`menu-tab ${activeTab === 'archived_surveys' ? 'active-tab' : ''}`}
                        onClick={() => handleTabClick('archived_surveys')}>
                        Архив
                    </div>
                </div>
            </div>
        );

            const handleTabClick = (tab) => {
            openTab(tab);
        };

      const filterSurveys = (surveys) => {
    return surveys.filter(survey => {
        const matchesSearch = searchTerm === '' || 
            survey.name_survey.toLowerCase().includes(searchTerm.toLowerCase()) ||
            survey.organization_name?.toLowerCase().includes(searchTerm.toLowerCase());
        
        const matchesMonth = monthFilter === '' || 
            (new Date(survey.date_open).getMonth() + 1).toString() === monthFilter;
        
        const matchesOrganization = organizationFilter === '' || 
            (survey.organization_name && survey.organization_name.split(',').some(
                name => name.trim().toLowerCase() === organizationFilter.toLowerCase()
            ));
        
        return matchesSearch && matchesMonth && matchesOrganization;
    });
};


    const renderSurveys = () => {
    const surveysToRender = initialData.surveys;
       const { currentRecords, totalPages, totalRecords } = getCurrentRecords(initialData.surveys);
    let filteredSurveys = filterSurveys(initialData.surveys);
    
    filteredSurveys = filteredSurveys.filter(survey => 
        survey.name_survey.toLowerCase().includes(searchTerm.toLowerCase()) ||
        survey.organization_name?.toLowerCase().includes(searchTerm.toLowerCase())
    );

            return (
                <div className="content surveys-page" id="default_content" data-page="surveys-list">
                    <div className="note">
                        <h2>Список существующих анкет</h2>
                        <p>На данной странице представлен перечень анкет. В таблице вы можете редактировать, удалять, а также выгружать отчёт по решённым пользователями анкетам, а также просматривать созданные анкеты.</p>
                    </div>


                    <div className="filter-sort">
                                            <button id="add_survey_btn" onClick={() => openTab('add_survey')}>
                            Добавить анкету
                        </button>
<input 
                    type="text" 
                    placeholder="Поиск..." 
                    value={searchTerm}
                    onChange={(e) => {
                        setSearchTerm(e.target.value);
                        setCurrentPage(1);
                    }} 
                />
 {/* Фильтр по месяцу */}
                <select 
                    value={monthFilter}
                    onChange={(e) => setMonthFilter(e.target.value)}
                    className="filter-select"
                >
                    <option value="">Все месяцы</option>
                    <option value="1">Январь</option>
                    <option value="2">Февраль</option>
                    <option value="3">Март</option>
                    <option value="4">Апрель</option>
                    <option value="5">Май</option>
                    <option value="6">Июнь</option>
                    <option value="7">Июль</option>
                    <option value="8">Август</option>
                    <option value="9">Сентябрь</option>
                    <option value="10">Октябрь</option>
                    <option value="11">Ноябрь</option>
                    <option value="12">Декабрь</option>
                </select>
                <select 
                    value={organizationFilter}
                    onChange={(e) => setOrganizationFilter(e.target.value)}
                    className="filter-select"
                >
                    <option value="">Все ОМСУ</option>
                    {Array.from(new Set(
                        initialData.surveys
                            .flatMap(s => s.organization_name?.split(',').map(name => name.trim()) || [])
                            .filter(Boolean)
                    )).map((organization, index) => React.createElement(
                        'option',
                        { key: index, value: organization },
                        organization
                    ))}
                </select>
                    </div>

                    <table id="data_table" className="surveys-table">
                        <thead>
                            <tr className="table_tr">
                                <th className="table-th--start">Название анкеты</th>
                                <th>Месяц</th>
                                <th>ОМСУ</th>
                                <th className="table-th--end">Действия</th>
                            </tr>
                        </thead>
                        <tbody>
                        {totalRecords === 0 ? (
                            <tr className="no-results-row">
                                <td colSpan="4">
                                    <div className="no-results-message">
                                        <i className="fas fa-search"></i>
                                        <span>Анкеты не найдены</span>
                                    </div>
                                </td>
                            </tr>
                        ) : (
                            currentRecords.map(survey => (
                                <tr key={survey.id_survey}>
                                    <td>{survey.name_survey}</td>
                                    <td>{new Date(survey.date_open).toLocaleString('ru', { month: 'long' })}</td>
                                    <td>
                                        {survey.organization_name 
                                            ? survey.organization_name.split(',')
                                                .map(name => name.trim())
                                                .filter(name => name)
                                                .join(', ')
                                            : 'Не указано'
                                        }
                                    </td>
                                    <td className="action-icons">
                                        <div className="icon-container" onClick={() => setModal({isOpen: true, content: 'report', data: survey})}>
                                            <span><i className="fas fa-list-check"></i></span>
                                            <span className="icon-tooltip">Сформировать отчёт</span>
                                        </div>
                                        <div className="icon-container" onClick={() => {
                                            openTab('get_survey_signatures', survey.id_survey);
                                        }}>
                                            <span><i className="fas fa-check"></i></span>
                                            <span className="icon-tooltip">Проверить подпись</span>
                                        </div>
                                        <div className="icon-container" onClick={() => setModal({isOpen: true, content: 'extend', data: survey})}>
                                            <span><i className="fas fa-hourglass-half"></i></span>
                                            <span className="icon-tooltip">Продлить</span>
                                        </div>
                                        <div className="icon-container" onClick={() => setModal({isOpen: true, content: 'copy', data: survey})}>
                                            <span><i className="fas fa-clipboard-copy"></i></span>
                                            <span className="icon-tooltip">Копировать</span>
                                        </div>
                                        <div className="icon-container" onClick={() => setModal({isOpen: true, content: 'update', data: survey})}>
                                            <span><i className="fas fa-pen-to-square"></i></span>
                                            <span className="icon-tooltip">Редактировать</span>
                                        </div>
                                        <div className="icon-container" onClick={() => setModal({isOpen: true, content: 'delete', data: survey})}>
                                            <span><i className="fas fa-trash"></i></span>
                                            <span className="icon-tooltip">Удалить</span>
                                        </div>
                                    </td>
                                </tr>
                                                        ))
                        )}
                        </tbody>
                    </table>
                    
  {totalRecords > 0 && (
                    <div className="pagination-container">
                        {currentPage > 1 && (
                            <button 
                                className="pagination-button prev"
                                onClick={() => setCurrentPage(p => p - 1)}
                            >
                                <i className="fas fa-chevron-left"></i> Предыдущая
                            </button>
                        )}
                        
                        <div className="page-info">
                            Страница {currentPage} из {totalPages}
                        </div>
                        
                        {currentPage < totalPages && (
                            <button 
                                className="pagination-button next"
                                onClick={() => setCurrentPage(p => p + 1)}
                            >
                                Следующая <i className="fas fa-chevron-right"></i>
                            </button>
                        )}
                    </div>
                )}
                </div>
        );
    };


            // СКРИПТЫ ДЛЯ ДИАГРАММ (ВКЛАДКА СТАТИСТИКА)
            const renderContentWrapper = (content) => (
                <div className="content-wrapper">
                    {content}
                </div>
            );
        
            const StatisticsPage = () => {
                const [chartsData, setChartsData] = useState(null);
                const [loading, setLoading] = useState(true);
                const [error, setError] = useState(null);
                
                const lineChartRef = useRef(null);
                const barChartRef = useRef(null);
                const pieChartRef = useRef(null);
const radarChartRef = useRef(null);



                const chartInstances = useRef({
    line: null,
    bar: null,
    pie: null,
    radar: null,
    avgScore: null
                });

                useEffect(() => {
                    const loadData = async () => {
                        try {
                            await fetch('/statistics');
                            const response = await fetch('/statistics/data');
                            if (!response.ok) throw new Error('Ошибка загрузки данных');
                            setChartsData(await response.json());
                        } catch (err) {
                            console.error('Ошибка:', err);
                            setError(err.message);
                        } finally {
                            setLoading(false);
                        }
                    };

                    loadData();

                    return () => {
                        Object.values(chartInstances.current).forEach(chart => {
                            if (chart) chart.destroy();
                        });
                    };
                }, []);

                useEffect(() => {
                    if (loading || error || !chartsData) return;

                    // Общие настройки для всех диаграмм
                    const shouldShowLegend = ({ labels = [], datasets = [] } = {}) => {
                        if (datasets.length > 1) return true;
                        if (datasets.length === 1) {
                            if ((datasets[0]?.label || '').trim()) return false;
                            return labels.length > 1;
                        }
                        return labels.length > 1;
                    };

                    const commonOptions = {
                        responsive: true,
                        maintainAspectRatio: false,
                        plugins: {
                            legend: {
                                position: 'bottom',
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

                    // Линейная диаграмма (ответы по месяцам)
                    if (lineChartRef.current) {
                        chartInstances.current.line = new Chart(lineChartRef.current, {
                            type: 'line',
                            data: {
                                labels: chartsData.lineChart.labels,
                                datasets: [{
                                    label: chartsData.lineChart.label,
                                    data: chartsData.lineChart.data,
                                    borderColor: 'rgb(75, 192, 192)',
                                    backgroundColor: 'rgba(75, 192, 192, 0.1)',
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


 if (radarChartRef.current && chartsData.avgScoreByOrganizationRadar) {
        console.log('Radar data:', chartsData.avgScoreByOrganizationRadar); // ЛОГ для отладки структуры
        chartInstances.current.radar = new Chart(radarChartRef.current, {
            type: 'radar',
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
                        text: 'Средний балл по ОМСУ по годам'
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







                    // Гистограмма (ответы по годам)
                    if (barChartRef.current) {
                        chartInstances.current.bar = new Chart(barChartRef.current, {
                            type: 'bar',
                            data: {
                                labels: chartsData.barChart.labels,
                                datasets: [{
                                    label: chartsData.barChart.label,
                                    data: chartsData.barChart.data,
                                    backgroundColor: 'rgba(54, 162, 235, 0.7)',
                                    borderColor: 'rgba(54, 162, 235, 1)',
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

                    // Круговая диаграмма (распределение по типам анкет)
                    if (pieChartRef.current) {
                        chartInstances.current.pie = new Chart(pieChartRef.current, {
                            type: 'pie',
                            data: {
                                labels: chartsData.pieChart.labels,
                                datasets: [{
                                    data: chartsData.pieChart.data,
                                    backgroundColor: [
                                        'rgba(255, 99, 132, 0.7)',
                                        'rgba(54, 162, 235, 0.7)',
                                        'rgba(255, 206, 86, 0.7)',
                                        'rgba(75, 192, 192, 0.7)',
                                        'rgba(153, 102, 255, 0.7)'
                                    ],
                                    borderWidth: 1
                                }]
                            },
                            options: {
                                ...commonOptions,
                                plugins: {
                                    legend: {
                                        ...commonOptions.plugins.legend,
                                        display: shouldShowLegend({ labels: chartsData.pieChart.labels, datasets: [{ label: '' }] }),
                                        align: 'center'
                                    }
                                }
                            }
                        });
                    }
                }, [loading, error, chartsData]);

                if (loading) return <div className="loading">Загрузка данных...</div>;
                if (error) return <div className="error">Ошибка: {error}</div>;

                return (
                    <div style={{maxWidth: '1200px', margin: '0 auto', padding: '20px'}}>
                            <div className="note">
            <h2>Статистика</h2>
        </div>
                        
                        <div className="chart-grid">
                            {/* Первая строка - 2 диаграммы */}
                            <div className="chart-container">
                                <h3 className="chart-title">Ответы по месяцам</h3>
                                <div className="chart-wrapper">
                                    <canvas ref={lineChartRef}></canvas>
                                </div>
                            </div>
                            <div className="chart-container">
                                <h3 className="chart-title">Ответы по годам</h3>
                                <div className="chart-wrapper">
                                    <canvas ref={barChartRef}></canvas>
                                </div>
                            </div>
                            
                            {/* Вторая строка - 2 диаграммы */}
                            <div className="chart-container">
                                <h3 className="chart-title">Распределение по типам анкет</h3>
                                <div className="chart-wrapper">
                                    <canvas ref={pieChartRef}></canvas>
                                </div>
                            </div>

<div className="chart-container">
    <h3 className="chart-title">Средний балл по ОМСУ по годам </h3>
    <div className="chart-wrapper">
        <canvas ref={radarChartRef}></canvas>
    </div>
</div>




                        </div>
                    </div>
                );
            };


    // Основная функция переключения вкладок

 window.onpopstate = function() {
    console.log("вызов на" + last_page)
       openTab(last_page);
       console.log(newPage);
       console.log(last_page);
       last_page = newPage;
    }; history.pushState({}, '');
    
    const extractRenderableHtml = (html) => {
        if (!html) {
            return '';
        }

        try {
            const parser = new DOMParser();
            const doc = parser.parseFromString(html, 'text/html');
            doc.querySelectorAll('link, style, script, meta, title').forEach((node) => node.remove());
            return doc.body && doc.body.innerHTML.trim()
                ? doc.body.innerHTML
                : html;
        } catch (error) {
            console.error('Ошибка парсинга HTML:', error);
            return html;
        }
    };

    const openTab = async (tab, id = null) => {
        const tabAlreadyOpen = !id && activeTab === tab;
        if (tabAlreadyOpen) return;

        setActiveTab(tab);

        if (tab !== 'get_surveys') {
            setLoading(true);
        }

        try {
                if (tab === 'open_statistics') // вкладка статистики
                {
                    setContent(
                        <>
                            {renderContentWrapper(<StatisticsPage />)}
                        </>
                    );

                    newPage = "open_statistics";
                    return;
                }
  
    if (tab === 'get_surveys') // вкладка списка анкет  
    {
        setContent(
            <>
                {renderContentWrapper(renderSurveys())}
            </>
        );
        return;
    }

                if (tab === 'list_answers_users') // вкладка ответов
                {
                    const response = await fetch('/surveys/answers');
                    const html = extractRenderableHtml(await response.text());
                    setContent(
                        renderContentWrapper(<div dangerouslySetInnerHTML={{ __html: html }} />)
                    );
                    newPage = "list_answers_users";
                    return;
                }

                if (tab === 'archived_surveys') // вкладка архива анкет
                {
                    const response = await fetch('/surveys/archive');
                    const html = extractRenderableHtml(await response.text());
                    setContent(
                        renderContentWrapper(<div dangerouslySetInnerHTML={{ __html: html }} />)
                    );
                    newPage = "archived_surveys";
                    return;
                }

            let endpoint = '';
            const options = {
                method: 'GET',
                headers: {
                    'Content-Type': 'application/json'
                }
            };
            
            switch(tab) {
case 'get_survey_signatures': 
                if (!id) {
                    console.error('Не передан ID анкеты для проверки подписей');
                    throw new Error('ID анкеты не указан');
                }
                newPage = "get_survey_signatures";
                endpoint = `/surveys/${id}/signatures`;
                break;
                case 'add_survey': endpoint = '/surveys/create'; newPage = "add_survey"; break;
                case 'get_logs': endpoint = '/logs'; newPage = "get_logs"; break;
                case 'download_logs': endpoint = '/logs/export'; newPage = "download_logs"; break;
                case 'get_users': endpoint = '/users'; newPage = "get_users"; break;
                case 'get_organization': endpoint = '/organizations'; newPage = "get_organization"; break;
                case 'copy_survey': 
                    endpoint = `/surveys/${modal.data?.id_survey}/copy`;
                    newPage = "copy_survey";
                    break;
                case 'update_survey':
                    newPage = "update_survey"; 
                    endpoint = `/surveys/${modal.data?.id_survey}/edit`;
                    break;
                case 'delete_survey':
                    newPage = "delete_survey"; 
                    endpoint = `/surveys/${modal.data?.id_survey}/delete`;
                    options.method = 'POST';
                    options.body = JSON.stringify({ surveyId: modal.data?.id_survey });
                    break;
                case 'add_user': endpoint = '/users/create'; newPage = "add_user"; break;
case 'update_user': endpoint = `/users/${modal.data?.id_user}/edit`; newPage = "update_user"; break;
case 'delete_user':
                endpoint = `/users/${modal.data?.id_user}/delete`;
                newPage = "delete_user";
                options.method = 'POST';
                break;
case 'archive_list_organizations': endpoint = '/organizations/archive'; newPage = "archive_list_organizations"; break;
case 'archive_list_users': endpoint = '/users/archive'; newPage = "archive_list_users"; break;

                case 'add_organization': endpoint = '/organizations/create'; newPage = "add_organization"; break;
                case 'update_organization': endpoint = `/organizations/${modal.data?.organization_id}/edit`; newPage = "update_organization"; break;
                case 'delete_organization':
                    endpoint = `/organizations/${modal.data?.organization_id}/delete`;
                    newPage = "delete_organization";
                    options.method = 'POST';
                    break;
                               case 'help':
                                newPage = "help";
                        window.open('/help_files/admin_survey_guide.docx', '_blank');
                        endpoint = `/help`;
                        break;


                case 'monthly_summary_report': create_monthly_summary_report();
                endpoint = '/reports'; break;
                case 'quarterly_report_q1': createQuarterlyReport(1);
                endpoint = '/reports'; break;
                case 'quarterly_report_q2': createQuarterlyReport(2);
                endpoint = '/reports'; break;
                case 'quarterly_report_q3': createQuarterlyReport(3);
                endpoint = '/reports'; break;
                case 'quarterly_report_q4': createQuarterlyReport(4);
                endpoint = '/reports'; break;
                

                case 'reports': endpoint = '/reports'; newPage = "reports"; break;
                case 'email': endpoint = '/mail-settings'; newPage = "update_email"; break;

                default: 
                    console.log(`Вкладка ${tab} не обработана`);
                    return;
            }
            
            const response = await fetch(endpoint, options);
            if (!response.ok) {
                throw new Error(
                    window.getResponseErrorMessage
                        ? window.getResponseErrorMessage(response, 'Ошибка загрузки')
                        : `Ошибка загрузки: ${response.status}`
                );
            }

            if (tab === 'download_logs') {
                newPage = "download_logs";
                const blob = await response.blob();
                const url = window.URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = 'logs.txt';
                document.body.appendChild(a);
                a.click();
                a.remove();
                return;
            }
            
            if (tab === 'delete_survey') {
                newPage = "delete_survey";
                const result = await response.json();
                setModal({
                    isOpen: true,
                    content: 'message',
                    message: result.message,
                    isSuccess: response.ok,
                    data: null
                });
                openTab('get_surveys');
                return;
            }

            
            
            const html = extractRenderableHtml(await response.text());
            setContent(
                renderContentWrapper(<div dangerouslySetInnerHTML={{ __html: html }} />)
            );
        } catch (error) {
            console.error('Ошибка:', error);
            setModal({
                isOpen: true,
                content: 'message',
                message: error.message,
                isSuccess: false,
                data: null
            });
        } finally {
            setLoading(false);
        }
    };

                const handleCopySurvey = async () => {
                    setModal({ isOpen: false });
                    await openTab('copy_survey');
                };

                const handleUpdateSurvey = async () => {
                    setModal({ isOpen: false });
                    await openTab('update_survey');
                };

    const handleDeleteSurvey = async () => {
        setLoading(true);
        try {
            const response = await fetch('/Survey/delete_survey', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    surveyId: modal.data?.id_survey
                })
            });
            
            const result = await response.json();
            
            if (!response.ok) {
                throw new Error(result.message || 'Ошибка при удалении анкеты');
            }

            setModal({
                isOpen: true,
                content: 'message',
                message: result.message,
                isSuccess: true,
                data: null
            });

            initialData.surveys = result.surveys;
            setContent(renderContentWrapper(renderSurveys()));
            
        } catch (error) {
            console.error('Ошибка при удалении анкеты:', error);
            setModal({
                isOpen: true,
                content: 'message',
                message: error.message,
                isSuccess: false,
                data: null
            });
        } finally {
            setLoading(false);
        }
    };
                const renderModalContent = () => {
                    switch(modal.content) {
                        case 'extend':
        return <ExtensionModal
            survey={modal.data} 
            onClose={() => setModal({ isOpen: false })} 
        />;
                        case 'report':
                            return (
                            <div>
        <h2 className="modal-title">Сформировать отчёт</h2>
        <div style={{ 
            display: 'flex', 
            gap: '10px', 
            justifyContent: 'space-between',
            marginTop: '1.5rem'
        }}>
            <div className="submenu2-container" style={{ flex: 1 }}>
                <button style={{ width: '100%' }}>Отчёт за месяц</button>
                <div className="submenu2">
                    <div onClick={() => create_monthly_report(modal.data?.id_survey)}>По выбранной анкете</div>
                    <div onClick={() => create_monthly_summary_report()}>По всем анкетам</div>
                </div>
            </div>
            
            <div className="submenu2-container" style={{ flex: 1 }}>
                <button style={{ width: '100%' }}>Отчёт за квартал</button>
                <div className="submenu2">
                    <div onClick={() => createQuarterlyReport(1)}>1 квартал</div>
                    <div onClick={() => createQuarterlyReport(2)}>2 квартал</div>
                    <div onClick={() => createQuarterlyReport(3)}>3 квартал</div>
                    <div onClick={() => createQuarterlyReport(4)}>4 квартал</div>
                </div>
            </div>
        </div>
    </div>
                            );
                            
                        case 'copy':
                            return (
                                <div>
                                    <div className="modal-header">
                                        <h2 className="h2_modal">Копирование анкеты</h2>
                                    </div>
                                    <div className="modal-body">
                                        <p className="modal-message">Вы уверены, что хотите создать копию анкеты "{modal.data?.name_survey}"?</p>
                                    </div>
                                    <div className="modal-footer">
                                        <button className="modal_btn modal_btn-primary" onClick={handleCopySurvey}>Копировать</button>
                                        <button className="modal_btn modal_btn-secondary" onClick={() => setModal({ isOpen: false })}>Отмена</button>
                                    </div>
                                </div>
                            );
                        case 'update':
                            return (
                                <div>
                                    <div className="modal-header">
                                        <h2 className="h2_modal">Редактирование анкеты</h2>
                                    </div>
                                    <div className="modal-body">
                                        <p className="modal-message">Вы переходите к редактированию анкеты "{modal.data?.name_survey}"</p>
                                    </div>
                                    <div className="modal-footer">
                                        <button className="modal_btn modal_btn-primary" onClick={handleUpdateSurvey}>Продолжить</button>
                                        <button className="modal_btn modal_btn-secondary" onClick={() => setModal({ isOpen: false })}>Отмена</button>
                                    </div>
                                </div>
                            );
                        case 'delete':
                            return (
                                <div>
                                    <div className="modal-header">
                                        <h2 className="h2_modal">Удаление анкеты</h2>
                                    </div>
                                    <div className="modal-body">
                                        <p className="modal-message">Вы уверены, что хотите удалить анкету "{modal.data?.name_survey}"?</p>
                                    </div>
                                    <div className="modal-footer">
                                        <button className="modal_btn modal_btn-primary" onClick={handleDeleteSurvey}>Удалить</button>
                                        <button className="modal_btn modal_btn-secondary" onClick={() => setModal({ isOpen: false })}>Отмена</button>
                                    </div>
                                </div>
                            );
                        case 'message':
                            return (
                                <div>
                                    <div className="modal-header">
                                        <h2 className="h2_modal">{modal.isSuccess ? 'Успешно' : 'Ошибка'}</h2>
                                    </div>
                                    <div className="modal-body">
                                        <div className={`modal-message ${modal.isSuccess ? 'success-message' : 'error-message'}`}>
                                            {modal.message}
                                        </div>
                                    </div>
                                    <div className="modal-footer">
                                        <button className="modal_btn modal_btn-primary" onClick={() => setModal({ isOpen: false })}>OK</button>
                                    </div>
                                </div>
                            );
                        default:
                            return null;
                    }
                };

                useEffect(() => {
                    openTab('get_surveys');
                }, []);

                useEffect(() => {
    if (activeTab === 'get_surveys') {
        setContent(renderSurveys());
    }
}, [searchTerm, monthFilter, organizationFilter, activeTab, currentPage]);

                useEffect(() => {
                    if (activeTab !== 'update_survey') {
                        return;
                    }

                    const timer = window.setTimeout(() => {
                        if (typeof window.surveyEditInit === 'function') {
                            window.surveyEditInit();
                        }
                    }, 0);

                    return () => window.clearTimeout(timer);
                }, [activeTab, content]);
            return (
            <div className="page-container">
                <Header
                    userRole={initialData.userRole}
                    displayName={initialData.displayName}
                    userName={initialData.userName}
                    organizationName={initialData.organizationName}
                />
                <div className="admin-container">
                    <Navigation 
                        openTab={openTab} 
                        activeTab={activeTab}
                        userRole={initialData.userRole}
                        userId={initialData.userId}
                    />
                    <div id="content_admin">
                        {showLoader ? (
                            <div className="loading-overlay">
                                <div>Загрузка...</div>
                            </div>
                        ) : (
                            <>
                                {['get_surveys', 'list_answers_users', 'archived_surveys'].includes(activeTab) && renderMenuAndFilters()}
                                {content}
                            </>
                        )}
                    </div>
                </div>
                <Footer />
                
                <div className={`modal ${modal.isOpen ? 'modal--visible' : ''}`}>
                    <div className="modal-content">
                        <span className="modal-close" onClick={() => setModal({ isOpen: false })}><i className="fas fa-xmark"></i></span>
                        {renderModalContent()}
                    </div>
                </div>
            </div>
        );
    };
            // СКРИПТЫ ДЛЯ ВКЛАДКИ ДОБАВЛЕНИЯ НОВОЙ АНКЕТЫ
            let selectedOrganization = [];
            let allOrganizations = [];
            let criteriaConfirmed = false;

            // Функция для безопасного получения элемента
            function safeGetElement(id) {
                const el = document.getElementById(id);
                if (!el) console.error('Элемент не найден:', id);
                return el;
            }

            // Функция для безопасного получения значения
            function safeGetValue(id) {
                const el = safeGetElement(id);
                return el ? el.value.trim() : '';
            }

            // Открытие модального окна для выбора организаций
            function openOrganizationModal() {
                const modal = safeGetElement('organizationModal');
                if (window.showSiteModal) {
                    window.showSiteModal(modal);
                } else {
                    modal.classList.add('active');
                }
                loadOrganizations();
            }

            function closeModal(modalId) {
                const modal = document.getElementById(modalId);
                if (window.hideSiteModal) {
                    window.hideSiteModal(modal);
                } else {
                    modal.classList.remove('active');
                }
            }

            // Загрузка организаций с сервера
            function loadOrganizations() {
                const loadingElement = safeGetElement('loadingOrgs');
                const orgListElement = safeGetElement('organizationList');
                
                loadingElement.style.display = 'block';
                orgListElement.style.display = 'none';
                
                fetch('/organizations/data', {
                    headers: {
                        'Accept': 'application/json'
                    }
                })
                .then(response => {
                    if (!response.ok) {
                        throw new Error('Ошибка сервера: ' + response.status);
                    }
                    return response.json();
                })
                .then(data => {
                    if (data.error) {
                        throw new Error(data.error);
                    }
                    
                    if (!Array.isArray(data)) {
                        throw new Error('Получены некорректные данные');
                    }
                    
                    allOrganizations = data;
                    renderOrganizationsList();
                })
                .catch(error => {
                    console.error('Ошибка:', error);
                    showError('Ошибка', 'Не удалось загрузить организации: ' + error.message);
                })
                .finally(() => {
                    loadingElement.style.display = 'none';
                    orgListElement.style.display = 'block';
                });
            }

            function renderOrganizationsList() {
                const orgList = safeGetElement('organizationList');
                orgList.innerHTML = '';
                
                allOrganizations.forEach(org => {
                    const isSelected = selectedOrganization.some(o => o.id === org.id);
                    const orgItem = document.createElement('div');
                    orgItem.className = `organization-item ${isSelected ? 'selected' : ''}`;
                    orgItem.innerHTML = `
                        <input type="checkbox" id="org-${org.id}" 
                            ${isSelected ? 'checked' : ''}
                            onchange="toggleOrganizationSelection(${org.id}, '${escapeHtml(org.name)}')">
                        <label for="org-${org.id}">${escapeHtml(org.name)}</label>
                    `;
                    orgList.appendChild(orgItem);
                });
            }

            // Экранирование HTML
            function escapeHtml(unsafe) {
                return unsafe
                    .replace(/&/g, "&amp;")
                    .replace(/</g, "&lt;")
                    .replace(/>/g, "&gt;")
                    .replace(/"/g, "&quot;")
                    .replace(/'/g, "&#039;");
            }

            // Переключение выбора организации
            function toggleOrganizationSelection(id, name) {
                const index = selectedOrganization.findIndex(o => o.id === id);
                
                if (index === -1) {
                    selectedOrganization.push({ id, name });
                } else {
                    selectedOrganization.splice(index, 1);
                }
            }

            function saveSelectedOrganization() {
                closeModal('organizationModal');
                updateSelectedOrganizationDisplay();
            }

            function updateSelectedOrganizationDisplay() {
                const container = safeGetElement('selectedOrganizationContainer');
                const list = safeGetElement('selectedOrganizationList');
                
                if (selectedOrganization.length === 0) {
                    container.style.display = 'none';
                    return;
                }
                
                container.style.display = 'block';
                list.innerHTML = '';
                
                selectedOrganization.forEach(org => {
                    const item = document.createElement('div');
                    item.className = 'selected-organization-item';
                    item.innerHTML = `
                        ${escapeHtml(org.name)}
                        <button onclick="removeSelectedOrganization(${org.id})"><i class="fas fa-xmark"></i></button>
                    `;
                    list.appendChild(item);
                });
            }

            function removeSelectedOrganization(id) {
                selectedOrganization = selectedOrganization.filter(org => org.id !== id);
                updateSelectedOrganizationDisplay();
                
                if (document.getElementById('organizationModal').classList.contains('active')) {
                    renderOrganizationsList();
                }
            }

            // Добавление нового критерия
            function addRowCriteriy() {
                const container = safeGetElement('cont_criteries');
                const count = container.querySelectorAll('.criteriy').length + 1;
                
                const div = document.createElement('div');
                div.className = 'form-group';
                div.innerHTML = `
                    <label for="criteriy${count}">Критерий оценки ${count}:</label>
                    <input type="text" class="form-control criteriy" id="criteriy${count}" 
                        placeholder="Введите критерий оценки" required />
                `;
                
                container.appendChild(div);
            }

            // Подтверждение критериев
            function confirmCriteries() {
                const criteriaInputs = document.querySelectorAll('.criteriy');
                let allValid = true;
                
                criteriaInputs.forEach(input => {
                    if (!input.value.trim()) {
                        input.classList.add('invalid');
                        allValid = false;
                    } else {
                        input.classList.remove('invalid');
                    }
                });
                
                if (!allValid) {
                    showError('Ошибка', 'Заполните все критерии оценки');
                    return;
                }
                
                criteriaInputs.forEach(input => {
                    input.readOnly = true;
                });
                
                safeGetElement('two_step').classList.add('confirmed-criteria');
                safeGetElement('add_survey_btn').style.display = 'inline-block';
                safeGetElement('add_crit').style.display = 'none';
                safeGetElement('conf_btn').style.display = 'none';
                
                criteriaConfirmed = true;
                showSuccess('Успех', 'Критерии успешно подтверждены');
            }

            function showSuccess(title, message) {
                const notification = safeGetElement('notification');
                document.getElementById('notificationTitle').textContent = title;
                document.getElementById('notificationTitle').className = 'notification-title notification-success';
                document.getElementById('notificationMessage').textContent = message;
                if (window.showSiteModal) {
                    window.showSiteModal(notification);
                } else {
                    notification.classList.add('active');
                }
                
                setTimeout(() => {
                    if (window.hideSiteModal) {
                        window.hideSiteModal(notification);
                    } else {
                        notification.classList.remove('active');
                    }
                }, 3000);
            }

            function showError(title, message) {
                const notification = safeGetElement('notification');
                document.getElementById('notificationTitle').textContent = title;
                document.getElementById('notificationTitle').className = 'notification-title notification-error';
                document.getElementById('notificationMessage').textContent = message;
                if (window.showSiteModal) {
                    window.showSiteModal(notification);
                } else {
                    notification.classList.add('active');
                }
                
                setTimeout(() => {
                    if (window.hideSiteModal) {
                        window.hideSiteModal(notification);
                    } else {
                        notification.classList.remove('active');
                    }
                }, 3000);
            }

            function hideNotification() {
                const notification = safeGetElement('notification');
                if (window.hideSiteModal) {
                    window.hideSiteModal(notification);
                } else {
                    notification.classList.remove('active');
                }
            }

            function addSurvey() {
                if (!validateForm()) {
                    return;
                }
                
                const surveyData = {
                    Title: safeGetElement('surveyTitle').value.trim(),
                    Description: safeGetElement('surveyDescription').value.trim(),
                    StartDate: safeGetElement('startDate').value,
                    EndDate: safeGetElement('endDate').value,
                    Organizations: selectedOrganization.map(org => org.id),
                    Criteria: Array.from(document.querySelectorAll('.criteriy')).map(input => input.value.trim())
                };
                
                const loadingOverlay = safeGetElement('loadingOverlay');
                if (window.showSiteModal) {
                    window.showSiteModal(loadingOverlay);
                } else {
                    loadingOverlay.style.display = 'flex';
                }
                
                fetch('/surveys/create', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                    },
                    body: JSON.stringify(surveyData)
                })
                .then(response => {
                    if (!response.ok) {
                        return response.json().then(err => {
                            throw new Error(err.message || 'Ошибка сервера');
                        });
                    }
                    return response.json();
                })
                .then(data => {
                    if (data.success) {
                        showSuccess('Успех', 'Анкета успешно создана! Пожалуйста, перезагрузите страницу!');
                        setTimeout(() => {
                            window.location.reload();
                        }, 2000);
                    } else {
                        throw new Error(data.message || 'Ошибка при создании анкеты');
                    }
                })
                .catch(error => {
                    showError('Ошибка', error.message);
                    console.error('Error:', error);
                })
                .finally(() => {
                    const loadingOverlay = safeGetElement('loadingOverlay');
                    if (window.hideSiteModal) {
                        window.hideSiteModal(loadingOverlay);
                    } else {
                        loadingOverlay.style.display = 'none';
                    }
                });
            }

            function validateForm() {
                let isValid = true;
                
                ['surveyTitle', 'surveyDescription', 'startDate', 'endDate'].forEach(id => {
                    const el = safeGetElement(id);
                    if (!el.value.trim()) {
                        el.classList.add('invalid');
                        isValid = false;
                    } else {
                        el.classList.remove('invalid');
                    }
                });
                
                const startDate = new Date(safeGetElement('startDate').value);
                const endDate = new Date(safeGetElement('endDate').value);
                
                if (endDate <= startDate) {
                    safeGetElement('endDate').classList.add('invalid');
                    showError('Ошибка', 'Дата окончания должна быть позже даты начала');
                    isValid = false;
                } else {
                    safeGetElement('endDate').classList.remove('invalid');
                }
                
                if (selectedOrganization.length === 0) {
                    showError('Ошибка', 'Выберите хотя бы одну организацию');
                    isValid = false;
                }
                
                if (!criteriaConfirmed) {
                    showError('Ошибка', 'Подтвердите критерии оценки');
                    isValid = false;
                }
                
                return isValid;
            }

            // СКРИПТЫ ДЛЯ ВКЛАДКИ РЕДАКТИРОВАНИЯ АНКЕТЫ
            
            var surveyEditSelectedOrganization = [];
            var surveyEditModalOpen = false;
            var surveyEditAllOrganizations = [];

            function surveyEditInit() {
                var selectedIdsInput = document.getElementById('selectedOrganizationIds');
                surveyEditSelectedOrganization = [];
                if (selectedIdsInput && selectedIdsInput.value) {
                    var ids = selectedIdsInput.value.split(',');
                    var names = window.selectedOrganizationNames || initialData.selectedOrganizationNames || [];
                    
                    for (var i = 0; i < ids.length; i++) {
                        if (!ids[i]) {
                            continue;
                        }

                        var parsedId = parseInt(ids[i], 10);
                        if (Number.isNaN(parsedId)) {
                            continue;
                        }

                        var resolvedName = names[i];
                        if (!resolvedName) {
                            var orgElement = document.querySelector('#organizationList .organization-item[data-id="' + parsedId + '"]');
                            resolvedName = orgElement ? orgElement.dataset.name : '';
                        }

                        if (resolvedName) {
                            surveyEditSelectedOrganization.push({
                                id: parsedId,
                                name: resolvedName
                            });
                        }
                    }
                }

                if (surveyEditSelectedOrganization.length === 0) {
                    var preselectedItems = document.querySelectorAll('#organizationList .organization-item[data-selected="true"]');
                    preselectedItems.forEach(function(item) {
                        var fallbackId = parseInt(item.dataset.id, 10);
                        if (!Number.isNaN(fallbackId)) {
                            surveyEditSelectedOrganization.push({
                                id: fallbackId,
                                name: item.dataset.name || ''
                            });
                        }
                    });
                }
                
                var orgItems = document.querySelectorAll('#organizationList .organization-item');
                orgItems.forEach(item => {
                    if (item.dataset.selected === 'true') {
                        item.classList.add('selected');
                    }
                });

                if (typeof surveyEditUpdateSelectedOrganizationDisplay === 'function') {
                    surveyEditUpdateSelectedOrganizationDisplay();
                }
            }

            function surveyEditOpenOrganizationModal() {
                var orgItems = document.querySelectorAll('#organizationList .organization-item');
                orgItems.forEach(item => {
                    var isSelected = surveyEditSelectedOrganization.some(org => org.id === parseInt(item.dataset.id, 10));
                    item.dataset.selected = isSelected ? 'true' : 'false';
                    item.classList.toggle('selected', isSelected);
                });
                if (window.showSiteModal) {
                    window.showSiteModal('organizationModal');
                } else {
                    document.getElementById('organizationModal').style.display = 'flex';
                }
                surveyEditModalOpen = true;
            }

            function surveyEditCloseModal(modalId) {
                if (window.hideSiteModal) {
                    window.hideSiteModal(modalId);
                } else {
                    document.getElementById(modalId).style.display = 'none';
                }
                surveyEditModalOpen = false;
            }

// Переключение выделения организации по клику
