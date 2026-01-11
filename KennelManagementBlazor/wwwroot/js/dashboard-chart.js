// Chart.js initialization for Dashboard
window.initializeChart = function(canvasId, chartData) {
    // Retry logic to wait for canvas to be available
    let retries = 0;
    const maxRetries = 10;

    const tryInitialize = () => {
        const ctx = document.getElementById(canvasId);

        if (!ctx) {
            retries++;
            if (retries < maxRetries) {
                console.log('Waiting for canvas element...', retries);
                setTimeout(tryInitialize, 100);
                return;
            } else {
                console.error('Canvas element not found after retries:', canvasId);
                return;
            }
        }

        // Destroy existing chart if it exists
        if (window[canvasId + '_chart']) {
            window[canvasId + '_chart'].destroy();
        }

        // Create new chart
        window[canvasId + '_chart'] = new Chart(ctx, {
            type: 'line',
            data: chartData,
            options: {
                responsive: true,
                maintainAspectRatio: false, // CHANGED: allows chart to fill container height
                devicePixelRatio: 2, // ADDED: higher quality on retina displays
                interaction: {
                    intersect: false,
                    mode: 'index'
                },
                plugins: {
                    legend: {
                        display: true,
                        position: 'top',
                        align: 'end',
                        labels: {
                            boxWidth: 12,
                            boxHeight: 12,
                            padding: 16,
                            font: {
                                family: "'Nunito', sans-serif",
                                size: 13,
                                weight: '600'
                            },
                            color: '#636E72',
                            usePointStyle: true,
                            pointStyle: 'rectRounded'
                        }
                    },
                    tooltip: {
                        enabled: true,
                        backgroundColor: '#2D3436',
                        titleColor: '#FFFFFF',
                        bodyColor: '#FFFFFF',
                        titleFont: {
                            family: "'Nunito', sans-serif",
                            size: 12,
                            weight: '700'
                        },
                        bodyFont: {
                            family: "'Nunito', sans-serif",
                            size: 14,
                            weight: '600'
                        },
                        padding: 12,
                        cornerRadius: 8,
                        displayColors: false,
                        callbacks: {
                            title: function(context) {
                                return 'Day ' + context[0].label;
                            },
                            label: function(context) {
                                return '$' + context.parsed.y.toFixed(2);
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        display: true,
                        grid: {
                            display: false
                        },
                        border: {
                            display: false
                        },
                        ticks: {
                            font: {
                                family: "'Nunito', sans-serif",
                                size: 11,
                                weight: '600'
                            },
                            color: '#A0AEC0',
                            maxRotation: 0,
                            autoSkip: true,
                            maxTicksLimit: 15
                        }
                    },
                    y: {
                        beginAtZero: true,
                        display: true,
                        grid: {
                            color: '#F0F0F0',
                            drawBorder: false
                        },
                        border: {
                            display: false
                        },
                        ticks: {
                            font: {
                                family: "'Nunito', sans-serif",
                                size: 11,
                                weight: '600'
                            },
                            color: '#A0AEC0',
                            padding: 10,
                            callback: function(value) {
                                return '$' + value.toFixed(2);
                            }
                        }
                    }
                },
                elements: {
                    line: {
                        tension: 0.4,
                        borderWidth: 2.5,
                        borderColor: '#2D3436',
                        fill: true,
                        backgroundColor: 'rgba(45, 52, 54, 0.08)'
                    },
                    point: {
                        radius: 0,
                        hoverRadius: 6,
                        hoverBackgroundColor: '#2D3436',
                        hoverBorderColor: '#FFFFFF',
                        hoverBorderWidth: 2
                    }
                }
            }
        });
    };

    tryInitialize();
};