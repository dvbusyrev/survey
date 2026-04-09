(function () {
    const charts = {
        line: null,
        bar: null,
        pie: null,
        radar: null
    };

    const ids = ['line', 'bar', 'pie', 'radar'];
    const hasAllCanvases = ids.every(function (id) {
        return document.getElementById(id + 'Chart');
    });

    if (!hasAllCanvases || typeof window.Chart === 'undefined') {
        return;
    }

    function setLoadingState(id, isLoading, errorMessage) {
        const loader = document.getElementById(id + 'Loading');
        if (!loader) {
            return;
        }

        loader.textContent = errorMessage || 'Загрузка данных...';
        loader.style.display = isLoading || errorMessage ? 'block' : 'none';
    }

    function hideAllLoaders() {
        ids.forEach(function (id) {
            setLoadingState(id, false);
        });
    }

    function showGlobalError(message) {
        ids.forEach(function (id) {
            setLoadingState(id, false, message);
        });
    }

    function buildCommonOptions() {
        return {
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
    }

    function shouldShowLegend(labels, datasets) {
        if (datasets.length > 1) {
            return true;
        }

        if (datasets.length === 1) {
            return !(datasets[0].label || '').trim() && labels.length > 1;
        }

        return labels.length > 1;
    }

    function destroyCharts() {
        Object.keys(charts).forEach(function (key) {
            if (charts[key]) {
                charts[key].destroy();
                charts[key] = null;
            }
        });
    }

    function renderCharts(chartsData) {
        const commonOptions = buildCommonOptions();

        charts.line = new Chart(document.getElementById('lineChart'), {
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
                        display: shouldShowLegend(chartsData.lineChart.labels, [{
                            label: chartsData.lineChart.label
                        }])
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true
                    }
                }
            }
        });

        charts.bar = new Chart(document.getElementById('barChart'), {
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
                        display: shouldShowLegend(chartsData.barChart.labels, [{
                            label: chartsData.barChart.label
                        }])
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true
                    }
                }
            }
        });

        charts.pie = new Chart(document.getElementById('pieChart'), {
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
                        display: shouldShowLegend(chartsData.pieChart.labels, [{
                            label: ''
                        }]),
                        align: 'center'
                    }
                }
            }
        });

        if (chartsData.avgScoreByOrganizationRadar) {
            charts.radar = new Chart(document.getElementById('radarChart'), {
                type: 'radar',
                data: chartsData.avgScoreByOrganizationRadar,
                options: {
                    ...commonOptions,
                    plugins: {
                        ...commonOptions.plugins,
                        legend: {
                            ...commonOptions.plugins.legend,
                            display: shouldShowLegend(
                                chartsData.avgScoreByOrganizationRadar.labels || [],
                                chartsData.avgScoreByOrganizationRadar.datasets || [])
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
    }

    async function init() {
        try {
            const response = await fetch('/statistics/data', {
                headers: {
                    Accept: 'application/json'
                }
            });

            if (!response.ok) {
                throw new Error('Не удалось загрузить данные статистики.');
            }

            const chartsData = await response.json();
            destroyCharts();
            renderCharts(chartsData);
            hideAllLoaders();
        } catch (error) {
            console.error('Ошибка загрузки статистики:', error);
            showGlobalError(error instanceof Error ? error.message : 'Не удалось загрузить статистику.');
        }
    }

    window.addEventListener('beforeunload', destroyCharts);
    init();
})();
