// 儀表板 JS
const MainDashboard = {
    charts: {
        salesChart: null,
        categoryChart: null
    },
    
    // 初始化函數
    init: function() {
        console.log('MainDashboard 初始化中...');
        this.setupCardClickHandlers();
        this.setupEventListeners();
        this.loadDashboardData();
        console.log('MainDashboard 初始化完成');
    },
    
    // 設置事件監聽
    setupEventListeners: function() {
        // 檢查特定DOM元素是否存在
        const refreshBtn = document.getElementById('refreshDashboardBtn');
        if (refreshBtn) {
            refreshBtn.addEventListener('click', () => {
                this.loadDashboardData();
            });
        }
        const datePicker = document.getElementById('dateRangePicker');
        if (datePicker) {
            datePicker.addEventListener('click', () => {
                console.log('開啟日期選擇器');
            });
        }
    },
    
    // 設置統計卡片點擊處理函數
    setupCardClickHandlers: function() {
        const ordersCard = document.querySelector('.stats-card-orders');
        if (ordersCard) ordersCard.addEventListener('click', () => window.location.href = '/AdminOrders');
        const revenueCard = document.querySelector('.stats-card-revenue');
        if (revenueCard) revenueCard.addEventListener('click', () => window.location.href = '/AdminFinance');
        const membersCard = document.querySelector('.stats-card-members');
        if (membersCard) membersCard.addEventListener('click', () => window.location.href = '/AccountManage/MemberInfo');
        const productsCard = document.querySelector('.stats-card-products');
        if (productsCard) productsCard.addEventListener('click', () => window.location.href = '/Product/Products');
        const notificationsCard = document.querySelector('.stats-card-notifications');
        if (notificationsCard) notificationsCard.addEventListener('click', () => window.location.href = '/Notification/MainNotification');
        const articlesCard = document.querySelector('.stats-card-articles');
        if (articlesCard) articlesCard.addEventListener('click', () => window.location.href = '/Blog/PostManagement');
        const couponsCard = document.querySelector('.stats-card-coupons');
        if (couponsCard) couponsCard.addEventListener('click', () => window.location.href = '/Coupons/CouponsManager');
    },
    
    // 載入儀表板數據
    loadDashboardData: function (startDate = null, endDate = null) {
        // 檢查是否還在當前頁面
        if (document.hidden || !document.getElementById('totalOrders')) {
            return; // 如果頁面已隱藏或 DOM 不存在，直接返回
        }

        this.showLoading();

        let params = new URLSearchParams();
        if (startDate) params.append('startDate', startDate);
        if (endDate) params.append('endDate', endDate);

        fetch('/Dashboard/GetDashboardStats?' + params.toString())
            .then(response => {
                // 再次檢查頁面狀態
                if (document.hidden || !document.getElementById('totalOrders')) {
                    return null; // 頁面已跳轉，不處理回應
                }

                if (!response.ok) {
                    throw new Error('伺服器錯誤');
                }
                return response.json();
            })
            .then(data => {
                // 確保還在當前頁面才更新 DOM
                if (data && !document.hidden && document.getElementById('totalOrders')) {
                    if (data.success) {
                        this.updateDashboardStats(data.data);
                        this.hideLoading();
                    } else {
                        console.error('儀表板數據錯誤:', data.message);
                        this.showError('無法載入儀表板數據: ' + data.message);
                    }
                }
            })
            .catch(error => {
                console.error('儀表板數據異常:', error);
                // 只有在還在當前頁面時才顯示錯誤
                if (!document.hidden && document.getElementById('totalOrders')) {
                    this.showError('無法載入儀表板數據，請稍後再試。');
                }
            });
    },
    
    // 更新儀表板統計數據
    updateDashboardStats: function(data) {
        // 更新基本統計數據
        document.getElementById('totalOrders').textContent = this.formatNumber(data.totalOrders);
        document.getElementById('newOrders').textContent = this.formatNumber(data.newOrders);
        document.getElementById('totalRevenue').textContent = '$' + this.formatNumber(data.totalRevenue);
        document.getElementById('totalMembers').textContent = this.formatNumber(data.totalMembers);
        document.getElementById('newMembers').textContent = this.formatNumber(data.newMembers);
        document.getElementById('totalProducts').textContent = this.formatNumber(data.totalProducts);
        document.getElementById('lowStockProducts').textContent = this.formatNumber(data.lowStockProducts);
        
        // 圖表數據更新
        if (data.salesChart && Array.isArray(data.salesChart.labels) && Array.isArray(data.salesChart.datasets)) {
            this.updateSalesChart(data.salesChart);
        } else {
            console.warn('salesChart 數據格式錯誤或為空', data.salesChart);
        }
        if (data.categoryChart && Array.isArray(data.categoryChart.labels) && Array.isArray(data.categoryChart.datasets)) {
            this.updateCategoryChart(data.categoryChart);
        } else {
            console.warn('categoryChart 數據格式錯誤或為空', data.categoryChart);
        }
    },
    
    // 更新銷售圖表
    updateSalesChart: function(data) {
        const chartElem = document.getElementById('salesChart');
        if (!chartElem) { console.error('找不到 salesChart 元素'); return; }
        const ctx = chartElem.getContext('2d');
        
        // 如果圖表已存在，先銷毀
        if (this.charts.salesChart) {
            this.charts.salesChart.destroy();
        }
        
        // 創建新的圖表
        this.charts.salesChart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: data.labels,
                datasets: data.datasets
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                scales: {
                    x: {
                        grid: {
                            display: false
                        }
                    },
                    y: {
                        beginAtZero: true,
                        grid: {
                            borderDash: [2, 4],
                            color: "rgba(0, 0, 0, 0.05)"
                        }
                    }
                },
                plugins: {
                    legend: {
                        position: 'top',
                    },
                    tooltip: {
                        backgroundColor: 'rgba(255, 255, 255, 0.8)',
                        titleColor: '#6e707e',
                        bodyColor: '#5a5c69',
                        borderColor: '#dddfeb',
                        borderWidth: 1,
                        titleMarginBottom: 10,
                        titleFontSize: 14,
                        bodyFontSize: 14,
                        padding: 15,
                        displayColors: false
                    }
                }
            }
        });
    },
    
    // 更新分類圖表（商品分類佔比）
    updateCategoryChart: function(data) {
        // 分類佔比展示
        const chartElem = document.getElementById('categoryChart');
        if (!chartElem) { console.error('找不到 categoryChart 元素'); return; }
        chartElem.innerHTML = '';
        // 動態生成分類列表
        let listId = 'categoryList';
        let listElem = document.getElementById(listId);
        if (!listElem) {
            listElem = document.createElement('ul');
            listElem.id = listId;
            listElem.className = 'list-group mb-3';
            chartElem.appendChild(listElem);
        }
        listElem.innerHTML = '';
        const total = data.datasets[0].data.reduce((a, b) => a + b, 0);
        data.labels.forEach((label, idx) => {
            const value = data.datasets[0].data[idx];
            const percent = total > 0 ? ((value / total) * 100).toFixed(1) : '0.0';
            const color = data.datasets[0].backgroundColor[idx];
            const item = document.createElement('li');
            item.className = 'list-group-item d-flex justify-content-between align-items-center';
            item.innerHTML = `<span><i class="fas fa-circle" style="color:${color};margin-right:8px;"></i>${label}</span><span> ${value.toLocaleString('zh-TW')} <span class="badge bg-info ms-2">${percent}%</span></span>`;
            listElem.appendChild(item);
        });
    },
    
    // 顯示載入指示器
    showLoading: function() {
        const loader = document.getElementById('dashboardLoading');
        if (loader) loader.style.display = 'flex';
    },
    
    // 隱藏載入指示器
    hideLoading: function() {
        const loader = document.getElementById('dashboardLoading');
        if (loader) loader.style.display = 'none';
    },
    
    // 顯示錯誤
    showError: function(message) {
        this.hideLoading();
        alert(message); // 可以改為更友善的通知
    },
    
    // 工具函數: 格式化數字
    formatNumber: function(num) {
        return new Intl.NumberFormat().format(num);
    }
};

// 當頁面載入完成時初始化儀表板
document.addEventListener('DOMContentLoaded', function() {
    MainDashboard.init();
});