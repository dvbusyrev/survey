(() => {
    const adminInlineAppPages = window.AdminInlineAppPages || (window.AdminInlineAppPages = {});

    adminInlineAppPages.ExtensionModal = function ExtensionModal({ survey, onClose }) {
        const [organizations, setOrganizations] = React.useState([]);
        const [loading, setLoading] = React.useState(true);
        const [error, setError] = React.useState('');
        const [extensions, setExtensions] = React.useState([{ organizationId: '', extendedUntil: '' }]);
        const today = new Date().toISOString().split('T')[0];

        React.useEffect(() => {
            let isDisposed = false;

            async function fetchOrganizations() {
                try {
                    setLoading(true);
                    const response = await fetch('/organizations/data');
                    if (!response.ok) {
                        throw new Error(
                            window.getResponseErrorMessage
                                ? window.getResponseErrorMessage(response, 'Не удалось загрузить организации')
                                : `Не удалось загрузить организации: ${response.status}`
                        );
                    }

                    const data = await response.json();
                    const normalizedOrganizations = Array.isArray(data)
                        ? data
                            .filter((org) => org && (org.organization_id !== undefined || org.id !== undefined))
                            .map((org) => ({
                                organizationId: String(org.organization_id ?? org.id),
                                organizationName: String(org.organization_name ?? org.name ?? '')
                            }))
                            .filter((org) => org.organizationName)
                        : [];

                    if (!isDisposed) {
                        setOrganizations(normalizedOrganizations);
                        setError('');
                    }
                } catch (fetchError) {
                    console.error('Ошибка загрузки организаций:', fetchError);
                    if (!isDisposed) {
                        setError(fetchError.message || 'Не удалось загрузить список организаций');
                    }
                } finally {
                    if (!isDisposed) {
                        setLoading(false);
                    }
                }
            }

            fetchOrganizations();

            return () => {
                isDisposed = true;
            };
        }, []);

        const isFormValid = () => {
            return extensions.every((item) => item.organizationId && item.extendedUntil)
                && extensions.some((item) => item.extendedUntil > today);
        };

        const isOrganizationSelected = (organizationId, currentIndex) => {
            return extensions.some((item, index) => index !== currentIndex && item.organizationId === organizationId);
        };

        const handleChange = (index, field, value) => {
            setExtensions((previous) => previous.map((item, itemIndex) => {
                if (itemIndex !== index) {
                    return item;
                }

                return {
                    ...item,
                    [field]: value
                };
            }));
        };

        const addExtensionRow = () => {
            setExtensions((previous) => [...previous, { organizationId: '', extendedUntil: '' }]);
        };

        const removeExtensionRow = (index) => {
            setExtensions((previous) => previous.length > 1
                ? previous.filter((_, itemIndex) => itemIndex !== index)
                : previous);
        };

        const handleSubmit = async () => {
            if (extensions.some((item) => !item.organizationId || !item.extendedUntil)) {
                alert('Пожалуйста, заполните все поля.');
                return;
            }

            if (extensions.some((item) => item.extendedUntil <= today)) {
                alert('Дата окончания должна быть в будущем.');
                return;
            }

            try {
                const response = await fetch('/survey-extensions', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
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
                    console.error('Не удалось разобрать ответ сервера:', parseError);
                }

                if (!response.ok || !responseData?.success) {
                    throw new Error(
                        responseData?.message
                        || responseData?.error
                        || responseText
                        || (window.getResponseErrorMessage
                            ? window.getResponseErrorMessage(response, 'Ошибка продления')
                            : `Ошибка продления: ${response.status}`)
                    );
                }

                alert(responseData.message || 'Доступ успешно продлён.');
                onClose();
                window.location.reload();
            } catch (submitError) {
                console.error('Ошибка продления анкеты:', submitError);
                alert(`Ошибка: ${submitError.message || 'Не удалось продлить доступ.'}`);
            }
        };

        return (
            <div style={{ maxWidth: '800px', margin: '0 auto', padding: '20px' }}>
                <h2 className="h2_modal" style={{ marginTop: 0, marginBottom: '16px', fontSize: '24px' }}>
                    Продление анкеты
                </h2>
                <p style={{ marginBottom: '24px', color: '#555' }}>
                    Анкета: "{survey?.name_survey}"
                </p>

                {error && (
                    <div className="error-message" style={{ display: 'block', marginBottom: '16px' }}>
                        {error}
                    </div>
                )}

                {!loading && organizations.length > 0 && (
                    <>
                        <div style={{ marginBottom: '20px' }}>
                            {extensions.map((extension, index) => (
                                <div
                                    key={index}
                                    style={{
                                        display: 'flex',
                                        gap: '12px',
                                        marginBottom: '16px',
                                        alignItems: 'flex-end'
                                    }}
                                >
                                    <div style={{ flex: 1 }}>
                                        <label>Организация:</label>
                                        <select
                                            value={extension.organizationId}
                                            onChange={(event) => handleChange(index, 'organizationId', event.target.value)}
                                            style={{ width: '100%', padding: '10px', border: '1px solid #ddd', borderRadius: '4px' }}
                                            required
                                        >
                                            <option value="">-- Выберите организацию --</option>
                                            {organizations.map((organization) => {
                                                const alreadySelected = isOrganizationSelected(organization.organizationId, index);
                                                return (
                                                    <option
                                                        key={organization.organizationId}
                                                        value={organization.organizationId}
                                                        disabled={alreadySelected}
                                                    >
                                                        {organization.organizationName}
                                                        {alreadySelected ? ' (уже выбрана)' : ''}
                                                    </option>
                                                );
                                            })}
                                        </select>
                                    </div>

                                    <div style={{ flex: 1 }}>
                                        <label>Дата окончания:</label>
                                        <input
                                            type="date"
                                            value={extension.extendedUntil}
                                            onChange={(event) => handleChange(index, 'extendedUntil', event.target.value)}
                                            style={{ width: '100%', padding: '10px', border: '1px solid #ddd', borderRadius: '4px' }}
                                            min={today}
                                            required
                                        />
                                    </div>

                                    {extensions.length > 1 && (
                                        <button
                                            type="button"
                                            onClick={() => removeExtensionRow(index)}
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

                        <button type="button" onClick={addExtensionRow}>
                            + Добавить организацию
                        </button>
                        <br />
                    </>
                )}

                {!loading && !error && organizations.length === 0 && (
                    <p>Доступных организаций для продления не найдено.</p>
                )}

                <br />
                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
                    <button
                        type="button"
                        onClick={handleSubmit}
                        disabled={!isFormValid() || loading}
                        className="modal_btn modal_btn-primary"
                        style={{
                            backgroundColor: isFormValid() ? '#4caf50' : '#9e9e9e',
                            cursor: isFormValid() ? 'pointer' : 'not-allowed',
                            opacity: isFormValid() ? 1 : 0.6
                        }}
                    >
                        {loading ? 'Обработка...' : 'Продлить доступ'}
                    </button>
                    <button
                        type="button"
                        onClick={onClose}
                        className="modal_btn modal_btn-secondary"
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

    adminInlineAppPages.StatisticsPage = function StatisticsPage() {
        const [chartsData, setChartsData] = React.useState(null);
        const [loading, setLoading] = React.useState(true);
        const [error, setError] = React.useState('');

        const lineChartRef = React.useRef(null);
        const barChartRef = React.useRef(null);
        const pieChartRef = React.useRef(null);
        const radarChartRef = React.useRef(null);
        const chartInstances = React.useRef({
            line: null,
            bar: null,
            pie: null,
            radar: null
        });

        React.useEffect(() => {
            let isDisposed = false;

            async function loadData() {
                try {
                    await fetch('/statistics');
                    const response = await fetch('/statistics/data');
                    if (!response.ok) {
                        throw new Error(
                            window.getResponseErrorMessage
                                ? window.getResponseErrorMessage(response, 'Ошибка загрузки статистики')
                                : 'Ошибка загрузки статистики'
                        );
                    }

                    const data = await response.json();
                    if (!isDisposed) {
                        setChartsData(data);
                    }
                } catch (loadError) {
                    console.error('Ошибка загрузки статистики:', loadError);
                    if (!isDisposed) {
                        setError(loadError.message || 'Не удалось загрузить данные статистики.');
                    }
                } finally {
                    if (!isDisposed) {
                        setLoading(false);
                    }
                }
            }

            loadData();

            return () => {
                isDisposed = true;
                Object.values(chartInstances.current).forEach((chart) => {
                    if (chart) {
                        chart.destroy();
                    }
                });
            };
        }, []);

        React.useEffect(() => {
            if (loading || error || !chartsData) {
                return;
            }

            if (typeof Chart === 'undefined') {
                setError('Chart.js не загружен.');
                return;
            }

            Object.values(chartInstances.current).forEach((chart) => {
                if (chart) {
                    chart.destroy();
                }
            });

            const shouldShowLegend = ({ labels = [], datasets = [] } = {}) => {
                if (datasets.length > 1) {
                    return true;
                }

                if (datasets.length === 1) {
                    if ((datasets[0]?.label || '').trim()) {
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

            if (lineChartRef.current && chartsData.lineChart) {
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

            if (barChartRef.current && chartsData.barChart) {
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

            if (pieChartRef.current && chartsData.pieChart) {
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
                                display: shouldShowLegend({
                                    labels: chartsData.pieChart.labels,
                                    datasets: [{ label: '' }]
                                }),
                                align: 'center'
                            }
                        }
                    }
                });
            }

            if (radarChartRef.current && chartsData.avgScoreByOrganizationRadar) {
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
                                text: 'Средний балл организаций по годам'
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
        }, [loading, error, chartsData]);

        if (loading) {
            return <div className="loading">Загрузка данных...</div>;
        }

        if (error) {
            return <div className="error">Ошибка: {error}</div>;
        }

        return (
            <div style={{ maxWidth: '1200px', margin: '0 auto', padding: '20px' }}>
                <div className="note">
                    <h2>Статистика</h2>
                </div>

                <div className="chart-grid">
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

                    <div className="chart-container">
                        <h3 className="chart-title">Распределение по типам анкет</h3>
                        <div className="chart-wrapper">
                            <canvas ref={pieChartRef}></canvas>
                        </div>
                    </div>

                    <div className="chart-container">
                        <h3 className="chart-title">Средний балл организаций по годам</h3>
                        <div className="chart-wrapper">
                            <canvas ref={radarChartRef}></canvas>
                        </div>
                    </div>
                </div>
            </div>
        );
    };
})();
