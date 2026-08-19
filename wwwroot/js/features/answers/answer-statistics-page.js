import Chart from 'chart.js/auto';

(function () {
    if (window.__answerStatisticsPageInitialized) {
        if (typeof window.initAnswerStatisticsPage === 'function') {
            window.initAnswerStatisticsPage();
        }
        return;
    }

    window.__answerStatisticsPageInitialized = true;

    const charts = {
        line: null,
        bar: null,
        organization: null
    };

    const ids = ['line', 'bar', 'radar'];
    const hasAllCanvases = ids.every(function (id) {
        return document.getElementById(id + 'Chart');
    });

    if (!hasAllCanvases) {
        return;
    }

    function setLoadingState(id, isLoading, errorMessage) {
        const loader = document.getElementById(id + 'Loading');
        if (!loader) {
            return;
        }

        loader.textContent = errorMessage || '';
        loader.style.display = errorMessage ? 'block' : 'none';
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

    const yearGuideLinePlugin = {
        id: 'answerStatisticsYearGuideLine',
        beforeDatasetsDraw: function (chart, _args, options) {
            const yScale = chart.scales.y;
            const meta = chart.getDatasetMeta(0);
            if (!yScale || !meta || meta.hidden) {
                return;
            }

            const startY = yScale.getPixelForValue(0);
            const color = options?.color || 'rgba(79, 70, 229, 0.25)';
            const lineWidth = options?.lineWidth || 2;
            chart.ctx.save();
            chart.ctx.strokeStyle = color;
            chart.ctx.lineWidth = lineWidth;

            meta.data.forEach(function (point) {
                if (!point || point.skip) {
                    return;
                }

                chart.ctx.beginPath();
                chart.ctx.moveTo(point.x, startY);
                chart.ctx.lineTo(point.x, point.y);
                chart.ctx.stroke();
            });

            chart.ctx.restore();
        }
    };

    function getThemeCssValue(name, fallback) {
        const value = window.getComputedStyle(document.documentElement).getPropertyValue(name).trim();
        return value || fallback;
    }

    function getChartTextColor() {
        return getThemeCssValue('--text-main', getThemeCssValue('--app-theme-font-color', '#343D4B'));
    }

    function getChartSecondaryTextColor() {
        return getThemeCssValue('--text-secondary', getChartTextColor());
    }

    function getChartGridColor() {
        return getThemeCssValue('--border', 'rgba(52, 61, 75, 0.12)');
    }

    function getScoreScale() {
        const textColor = getChartTextColor();
        const gridColor = getChartGridColor();

        return {
            type: 'linear',
            min: 0,
            max: 5,
            ticks: {
                stepSize: 1,
                color: textColor
            },
            title: {
                display: true,
                text: 'Средняя оценка',
                color: textColor
            },
            grid: {
                color: gridColor
            }
        };
    }

    function buildCommonOptions(showLegend) {
        const textColor = getChartTextColor();

        return {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    display: Boolean(showLegend),
                    position: 'bottom',
                    labels: {
                        padding: 14,
                        boxWidth: 12,
                        color: textColor,
                        font: {
                            size: 12
                        }
                    }
                },
                tooltip: {
                    callbacks: {
                        label: function (context) {
                            const value = context.parsed?.y ?? context.parsed?.x ?? context.parsed;
                            const numericValue = Number(value);
                            if (Number.isFinite(numericValue)) {
                                return `${context.dataset.label || 'Средняя оценка'}: ${numericValue.toFixed(2)}`;
                            }

                            return context.dataset.label || '';
                        }
                    }
                }
            },
            layout: {
                padding: {
                    top: 10,
                    bottom: showLegend ? 20 : 10
                }
            }
        };
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
        const chartTextColor = getChartTextColor();
        const chartSecondaryTextColor = getChartSecondaryTextColor();
        const yearLabels = chartsData.lineChart?.labels || [];
        const yearData = chartsData.lineChart?.data || [];

        charts.line = new Chart(document.getElementById('lineChart'), {
            type: 'line',
            data: {
                labels: yearLabels,
                datasets: [{
                    label: chartsData.lineChart?.label || 'Средняя оценка',
                    data: yearData,
                    borderColor: 'rgb(79, 70, 229)',
                    backgroundColor: 'rgb(79, 70, 229)',
                    borderWidth: 2,
                    pointRadius: 4,
                    pointHoverRadius: 6,
                    tension: 0.2
                }]
            },
            options: {
                ...buildCommonOptions(false),
                scales: {
                    x: {
                        ticks: {
                            color: chartTextColor
                        },
                        grid: {
                            display: false
                        }
                    },
                    y: getScoreScale()
                },
                plugins: {
                    ...buildCommonOptions(false).plugins,
                    answerStatisticsYearGuideLine: {
                        color: 'rgba(79, 70, 229, 0.32)',
                        lineWidth: 2
                    }
                }
            },
            plugins: [yearGuideLinePlugin]
        });

        charts.bar = new Chart(document.getElementById('barChart'), {
            type: 'bar',
            data: {
                labels: chartsData.barChart?.labels || [],
                datasets: [{
                    label: chartsData.barChart?.label || 'Средняя оценка',
                    data: chartsData.barChart?.data || [],
                    backgroundColor: 'rgba(14, 165, 233, 0.72)',
                    borderColor: 'rgb(14, 165, 233)',
                    borderWidth: 1
                }]
            },
            options: {
                ...buildCommonOptions(false),
                scales: {
                    x: {
                        ticks: {
                            color: chartTextColor
                        },
                        grid: {
                            display: false
                        }
                    },
                    y: getScoreScale()
                }
            }
        });

        if (chartsData.avgScoreByOrganizationRadar) {
            charts.organization = new Chart(document.getElementById('radarChart'), {
                type: 'bar',
                data: {
                    labels: chartsData.avgScoreByOrganizationRadar.labels || [],
                    datasets: (chartsData.avgScoreByOrganizationRadar.datasets || []).map(function (dataset) {
                        return {
                            ...dataset,
                            grouped: false,
                            borderWidth: 1,
                            barPercentage: 0.78,
                            categoryPercentage: 0.92
                        };
                    })
                },
                options: {
                    ...buildCommonOptions(true),
                    scales: {
                        x: {
                            ticks: {
                                display: false,
                                color: chartSecondaryTextColor
                            },
                            grid: {
                                display: false
                            }
                        },
                        y: getScoreScale()
                    },
                    plugins: {
                        ...buildCommonOptions(true).plugins,
                        tooltip: {
                            callbacks: {
                                title: function (items) {
                                    return items[0]?.dataset?.label || '';
                                },
                                label: function (context) {
                                    const value = Number(context.parsed?.y || 0);
                                    return `Средняя оценка: ${value.toFixed(2)}`;
                                }
                            }
                        }
                    }
                }
            });
        }
    }

    async function init() {
        if (!ids.every(function (id) {
            return document.getElementById(id + 'Chart');
        })) {
            return;
        }

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
            showGlobalError(
                typeof window.normalizeClientErrorMessage === 'function'
                    ? window.normalizeClientErrorMessage(error instanceof Error ? error.message : 'Не удалось загрузить статистику.')
                    : (error instanceof Error ? error.message : 'Не удалось загрузить статистику.')
            );
        }
    }

    window.initAnswerStatisticsPage = init;
    window.destroyAnswerStatisticsPage = destroyCharts;
    window.addEventListener('beforeunload', destroyCharts);
    if (hasAllCanvases) {
        init();
    }
})();
