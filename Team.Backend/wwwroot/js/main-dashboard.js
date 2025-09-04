// ����O JS
const MainDashboard = {
    charts: {
        salesChart: null,
        categoryChart: null
    },
    
    // ��l�ƨ��
    init: function() {
        console.log('MainDashboard ��l�Ƥ�...');
        this.setupCardClickHandlers();
        this.setupEventListeners();
        this.loadDashboardData();
        console.log('MainDashboard ��l�Ƨ���');
    },
    
    // �]�m�ƥ��ť
    setupEventListeners: function() {
        // �ˬd�S�wDOM�����O�_�s�b
        const refreshBtn = document.getElementById('refreshDashboardBtn');
        if (refreshBtn) {
            refreshBtn.addEventListener('click', () => {
                this.loadDashboardData();
            });
        }
        const datePicker = document.getElementById('dateRangePicker');
        if (datePicker) {
            datePicker.addEventListener('click', () => {
                console.log('�}�Ҥ����ܾ�');
            });
        }
    },
    
    // �]�m�έp�d���I���B�z���
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
    
    // ���J����O�ƾ�
    loadDashboardData: function (startDate = null, endDate = null) {
        // �ˬd�O�_�٦b��e����
        if (document.hidden || !document.getElementById('totalOrders')) {
            return; // �p�G�����w���é� DOM ���s�b�A������^
        }

        this.showLoading();

        let params = new URLSearchParams();
        if (startDate) params.append('startDate', startDate);
        if (endDate) params.append('endDate', endDate);

        fetch('/Dashboard/GetDashboardStats?' + params.toString())
            .then(response => {
                // �A���ˬd�������A
                if (document.hidden || !document.getElementById('totalOrders')) {
                    return null; // �����w����A���B�z�^��
                }

                if (!response.ok) {
                    throw new Error('���A�����~');
                }
                return response.json();
            })
            .then(data => {
                // �T�O�٦b��e�����~��s DOM
                if (data && !document.hidden && document.getElementById('totalOrders')) {
                    if (data.success) {
                        this.updateDashboardStats(data.data);
                        this.hideLoading();
                    } else {
                        console.error('����O�ƾڿ��~:', data.message);
                        this.showError('�L�k���J����O�ƾ�: ' + data.message);
                    }
                }
            })
            .catch(error => {
                console.error('����O�ƾڲ��`:', error);
                // �u���b�٦b��e�����ɤ~��ܿ��~
                if (!document.hidden && document.getElementById('totalOrders')) {
                    this.showError('�L�k���J����O�ƾڡA�еy��A�աC');
                }
            });
    },
    
    // ��s����O�έp�ƾ�
    updateDashboardStats: function(data) {
        // ��s�򥻲έp�ƾ�
        document.getElementById('totalOrders').textContent = this.formatNumber(data.totalOrders);
        document.getElementById('newOrders').textContent = this.formatNumber(data.newOrders);
        document.getElementById('totalRevenue').textContent = '$' + this.formatNumber(data.totalRevenue);
        document.getElementById('totalMembers').textContent = this.formatNumber(data.totalMembers);
        document.getElementById('newMembers').textContent = this.formatNumber(data.newMembers);
        document.getElementById('totalProducts').textContent = this.formatNumber(data.totalProducts);
        document.getElementById('lowStockProducts').textContent = this.formatNumber(data.lowStockProducts);
        
        // �Ϫ�ƾڧ�s
        if (data.salesChart && Array.isArray(data.salesChart.labels) && Array.isArray(data.salesChart.datasets)) {
            this.updateSalesChart(data.salesChart);
        } else {
            console.warn('salesChart �ƾڮ榡���~�ά���', data.salesChart);
        }
        if (data.categoryChart && Array.isArray(data.categoryChart.labels) && Array.isArray(data.categoryChart.datasets)) {
            this.updateCategoryChart(data.categoryChart);
        } else {
            console.warn('categoryChart �ƾڮ榡���~�ά���', data.categoryChart);
        }
    },
    
    // ��s�P��Ϫ�
    updateSalesChart: function(data) {
        const chartElem = document.getElementById('salesChart');
        if (!chartElem) { console.error('�䤣�� salesChart ����'); return; }
        const ctx = chartElem.getContext('2d');
        
        // �p�G�Ϫ�w�s�b�A���P��
        if (this.charts.salesChart) {
            this.charts.salesChart.destroy();
        }
        
        // �Ыطs���Ϫ�
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
    
    // ��s�����Ϫ�]�ӫ~��������^
    updateCategoryChart: function(data) {
        // ��������i��
        const chartElem = document.getElementById('categoryChart');
        if (!chartElem) { console.error('�䤣�� categoryChart ����'); return; }
        chartElem.innerHTML = '';
        // �ʺA�ͦ������C��
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
    
    // ��ܸ��J���ܾ�
    showLoading: function() {
        const loader = document.getElementById('dashboardLoading');
        if (loader) loader.style.display = 'flex';
    },
    
    // ���ø��J���ܾ�
    hideLoading: function() {
        const loader = document.getElementById('dashboardLoading');
        if (loader) loader.style.display = 'none';
    },
    
    // ��ܿ��~
    showError: function(message) {
        this.hideLoading();
        alert(message); // �i�H�אּ��͵����q��
    },
    
    // �u����: �榡�ƼƦr
    formatNumber: function(num) {
        return new Intl.NumberFormat().format(num);
    }
};

// �������J�����ɪ�l�ƻ���O
document.addEventListener('DOMContentLoaded', function() {
    MainDashboard.init();
});