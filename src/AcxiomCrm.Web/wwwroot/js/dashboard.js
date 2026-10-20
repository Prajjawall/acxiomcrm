(function () {
    'use strict';

    const leadStatusCanvas = document.getElementById('leadStatusChart');
    const pipelineCanvas = document.getElementById('pipelineChart');
    const monthlySalesCanvas = document.getElementById('monthlySalesChart');

    if (!leadStatusCanvas || !pipelineCanvas || !monthlySalesCanvas) {
        return;
    }

    let leadStatusChart = null;
    let pipelineChart = null;
    let monthlySalesChart = null;

    function formatInr(val) {
        return new Intl.NumberFormat('en-IN', {
            style: 'currency',
            currency: 'INR',
            maximumFractionDigits: 0
        }).format(val || 0);
    }

    function initCharts(data) {
        const themeColors = {
            primary: '#0F5C5A',
            accent: '#2B8A88',
            secondary: '#4A6984',
            light: '#9BBEC8',
            warning: '#D97706',
            success: '#16A34A',
            danger: '#DC2626'
        };

        // 1. Lead Status Chart (horizontal bar)
        const lsLabels = (data.leadStatus || []).map(x => x.label);
        const lsValues = (data.leadStatus || []).map(x => x.value);

        if (leadStatusChart) {
            leadStatusChart.destroy();
        }

        leadStatusChart = new Chart(leadStatusCanvas, {
            type: 'bar',
            data: {
                labels: lsLabels,
                datasets: [{
                    label: 'Leads',
                    data: lsValues,
                    backgroundColor: [
                        themeColors.primary,
                        themeColors.accent,
                        themeColors.success,
                        themeColors.warning,
                        themeColors.secondary,
                        themeColors.danger
                    ],
                    borderRadius: 4
                }]
            },
            options: {
                indexAxis: 'y',
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false }
                },
                scales: {
                    x: {
                        beginAtZero: true,
                        ticks: { stepSize: 1 }
                    }
                }
            }
        });

        // 2. Pipeline by Stage Chart (bar with amount and weighted)
        const pipeLabels = (data.pipeline || []).map(x => x.stage);
        const pipeAmounts = (data.pipeline || []).map(x => x.amount);
        const pipeWeighted = (data.pipeline || []).map(x => x.weighted);

        if (pipelineChart) {
            pipelineChart.destroy();
        }

        pipelineChart = new Chart(pipelineCanvas, {
            type: 'bar',
            data: {
                labels: pipeLabels,
                datasets: [
                    {
                        label: 'Total Value',
                        data: pipeAmounts,
                        backgroundColor: themeColors.primary,
                        borderRadius: 4
                    },
                    {
                        label: 'Weighted Value',
                        data: pipeWeighted,
                        backgroundColor: themeColors.accent,
                        borderRadius: 4
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    tooltip: {
                        callbacks: {
                            label: function (ctx) {
                                return ctx.dataset.label + ': ' + formatInr(ctx.raw);
                            }
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        ticks: {
                            callback: function (val) {
                                return formatInr(val);
                            }
                        }
                    }
                }
            }
        });

        // 3. Monthly Sales Chart (bar of won deals)
        const msLabels = (data.monthlySales || []).map(x => {
            const parts = x.month.split('-');
            const date = new Date(parts[0], parts[1] - 1, parts[2] || 1);
            return date.toLocaleDateString('en-US', { month: 'short', year: 'numeric' });
        });
        const msAmounts = (data.monthlySales || []).map(x => x.amount);

        if (monthlySalesChart) {
            monthlySalesChart.destroy();
        }

        monthlySalesChart = new Chart(monthlySalesCanvas, {
            type: 'bar',
            data: {
                labels: msLabels,
                datasets: [{
                    label: 'Won Revenue',
                    data: msAmounts,
                    backgroundColor: themeColors.primary,
                    borderRadius: 4
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    tooltip: {
                        callbacks: {
                            label: function (ctx) {
                                return 'Sales: ' + formatInr(ctx.raw);
                            }
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        ticks: {
                            callback: function (val) {
                                return formatInr(val);
                            }
                        }
                    }
                }
            }
        });
    }

    // Load initial data
    const container = document.querySelector('[data-dashboard-data]');
    if (container) {
        try {
            const initialData = JSON.parse(container.getAttribute('data-dashboard-data'));
            initCharts(initialData);
        } catch (e) {
            console.error('Failed to parse initial dashboard data', e);
        }
    }
})();
