// 通知管理系統 JavaScript - 重構改進版本
// 主要改進：模組化、錯誤處理、現代 JavaScript 語法、性能優化

class NotificationManagementSystem {
    constructor() {
        // 防止重複載入（使用專案範圍的旗標名稱以降低衝突）
        if (window.TeamNotificationManagerLoaded) {
            console.warn('通知管理系統已載入，跳過重複初始化');
            return;
        }
        window.TeamNotificationManagerLoaded = true;

        this.state = {
            currentData: [],
            currentPage: 1,
            itemsPerPage: 10,
            searchTerm: "",
            filters: {
                category: "",
                emailStatus: "",
                channel: "",
                startDate: "",
                endDate: ""
            },
            selectedItems: new Set(),
            sortColumn: "sentat",
            sortDirection: "desc",
            totalCount: 0,
            totalPages: 0,
            editModal: null,
            statsModal: null,
            statsChart: null,
            isLoading: false,
            apiBasePath: '/Notification',
            _deletingItems: new Set(),
            _batchDeleting: false
        };

        this.constants = {
            categoryLabels: {
                'order': '訂單', 'payment': '付款', 'account': '帳戶',
                'security': '安全', 'promotion': '促銷', 'system': '系統'
            },
            emailStatusLabels: {
                'immediate': '立即發送', 'scheduled': '排程', 'draft': '草稿'
            },
            channelLabels: {
                'email': '電子郵件', 'push': '推播'
            },
            debounceDelay: 300,
            apiTimeout: 10000,
            maxMessageLength: 2000,
            defaultTemplates: {
                short: '親愛的會員，您好！我們想通知您，您有新的系統更新或重要資訊。請登入會員中心查看詳細內容。如有任何問題，歡迎聯絡客服。',
                promotion: '限時優惠！立即下單享受獨家折扣，數量有限，售完為止。點擊查看活動詳情並使用折扣碼。'
            }
        };

        this.utils = new NotificationUtils();
        this.api = new NotificationAPI(this.state, this.utils);
        this.ui = new NotificationUI(this.state, this.constants, this.utils);

        this.initializeSystem();
    }

    async initializeSystem() {
        try {
            console.log('通知管理系統初始化中...');

            this.setupErrorHandling();
            this.injectStyles();

            // 檢查是否為統計頁面
            if (window.location.pathname.includes('/ChartNotification')) {
                console.log('檢測到統計圖表頁面，只初始化基本功能');
                return;
            }

            await this.initBootstrapComponents();
            this.bindEvents();
            await this.loadNotifications();

            // 新增: 載入會員 Email 下拉選單
            this.populateMemberEmailSelect();

            console.log('通知管理系統初始化完成');
        } catch (error) {
            console.error('系統初始化失敗:', error);
            this.utils.showAlert('系統初始化失敗，請重新整理頁面', 'danger');
        }
    }

    async populateMemberEmailSelect() {
        try {
            const selectEl = document.getElementById('specificAccountSelect');
            const inputEl = document.getElementById('specificAccount');
            if (!selectEl) return;

            // 先清空（保留第一兩個 options）
            const preserve = Array.from(selectEl.options).slice(0, 2);
            selectEl.innerHTML = '';
            preserve.forEach(opt => selectEl.appendChild(opt));

            // 呼叫後端 API 取得會員 email 列表
            const resp = await this.api.call(`${this.state.apiBasePath}/GetMemberEmails`);
            let emails = [];
            if (resp && resp.data && Array.isArray(resp.data)) {
                emails = resp.data;
            }

            // 將 emails 加入下拉
            emails.forEach(email => {
                const opt = document.createElement('option');
                opt.value = email;
                opt.textContent = email;
                selectEl.appendChild(opt);
            });

            // 新增自訂選項（如果不存在）
            if (!Array.from(selectEl.options).some(o => o.value === '__custom__')) {
                const customOpt = document.createElement('option');
                customOpt.value = '__custom__';
                customOpt.textContent = '輸入自訂Email';
                selectEl.appendChild(customOpt);
            }

            // 綁定 change
            selectEl.addEventListener('change', () => {
                const val = selectEl.value;
                if (val === '__custom__') {
                    if (inputEl) {
                        inputEl.disabled = false;
                        inputEl.required = true;
                        inputEl.focus();
                    }
                } else {
                    if (inputEl) {
                        inputEl.disabled = true;
                        inputEl.required = false;
                        inputEl.value = val || '';
                    }
                }
            });
        } catch (e) {
            console.warn('載入會員 Email 列表失敗', e);
        }
    }

    setupErrorHandling() {
        window.addEventListener('error', (e) => {
            if (e.message?.includes('Cannot access rules') ||
                e.message?.includes('stylesheet') ||
                e.message?.includes('CORS')) {
                console.warn('CSS/樣式錯誤已被忽略:', e.message);
                e.preventDefault();
                return false;
            }
        }, true);

        window.addEventListener('unhandledrejection', (e) => {
            console.error('未處理的 Promise 拒絕:', e.reason);
            if (!e.reason?.message?.includes('AbortError')) {
                this.utils.showAlert('系統發生錯誤，請稍後再試', 'warning');
            }
        });
    }

    injectStyles() {
        // CSS moved to external file: /css/notification-styles.css
        // Remove any previously injected style element with id 'notification-styles'
        try {
            const existingStyle = document.getElementById('notification-styles');
            if (existingStyle && existingStyle.parentNode) {
                existingStyle.parentNode.removeChild(existingStyle);
            }
        } catch (e) {
            console.warn('移除內嵌樣式失敗:', e);
        }

        // Ensure the external stylesheet link exists in the document head (in case Razor layout didn't include it)
        try {
            const href = '/css/notification-styles.css';
            const existing = Array.from(document.querySelectorAll('link[rel="stylesheet"]')).some(l => {
                const h = l.getAttribute('href');
                return h === href || h === ('~' + href) || h === ('./' + href) || h.endsWith('notification-styles.css');
            });
            if (!existing) {
                const link = document.createElement('link');
                link.rel = 'stylesheet';
                link.href = href;
                document.head.appendChild(link);
            }
        } catch (e) {
            console.warn('注入外部樣式失敗:', e);
        }
    }

    async initBootstrapComponents() {
        try {
            const bootstrapVersion = this.detectBootstrapVersion();
            console.log('檢測到 Bootstrap 版本:', bootstrapVersion);

            if (typeof jQuery !== 'undefined' && jQuery.fn.modal) {
                this.state.editModal = $('#editModal');
                this.state.statsModal = $('#statsModal');
            }
        } catch (error) {
            console.error('Bootstrap 組件初始化失敗:', error);
            this.utils.showAlert('部分 UI 組件初始化失敗，可能影響模態框功能', 'warning');
        }
    }

    detectBootstrapVersion() {
        if (typeof bootstrap !== 'undefined') return 5;
        if (typeof jQuery !== 'undefined' && jQuery.fn.modal) return 4;
        return null;
    }

    bindEvents() {
        // Use wrapper listeners to avoid calling .bind on possibly-undefined handlers
        const self = this;

        document.addEventListener('click', function (e) {
            if (typeof self.handleGlobalClick === 'function') {
                try { self.handleGlobalClick(e); } catch (err) { console.error('handleGlobalClick error', err); }
            }
        });

        document.addEventListener('change', function (e) {
            if (typeof self.handleGlobalChange === 'function') {
                try { self.handleGlobalChange(e); } catch (err) { console.error('handleGlobalChange error', err); }
            }
        });

        document.addEventListener('input', function (e) {
            if (typeof self.handleGlobalInput === 'function') {
                try { self.handleGlobalInput(e); } catch (err) { console.error('handleGlobalInput error', err); }
            }
        });

        // Ensure select-all checkbox has correct bootstrap markup and event
        const selectAll = document.getElementById('selectAll');
        if (selectAll) {
            // add form-check-input class
            selectAll.classList.add('form-check-input');

            // wrap in .form-check if not already
            const parent = selectAll.parentElement;
            if (parent && !parent.classList.contains('form-check')) {
                const wrapper = document.createElement('div');
                wrapper.className = 'form-check text-center';
                // preserve parent's location
                parent.insertBefore(wrapper, selectAll);
                wrapper.appendChild(selectAll);

                // if parent was a TH/TD, ensure padding alignment
                if (parent.tagName === 'TH' || parent.tagName === 'TD') {
                    wrapper.style.margin = '0 auto';
                }
            }

            // bind change to toggle select all
            selectAll.addEventListener('change', (e) => {
                this.toggleSelectAll(e.target.checked);
            });
        }

        // Bind targetType radios to show/hide specific account controls
        const targetRadios = document.querySelectorAll('input[name="targetType"]');
        if (targetRadios && targetRadios.length > 0) {
            targetRadios.forEach(r => r.addEventListener('change', () => this.handleTargetTypeChange()));
        }

        // 搜尋功能
        const searchInput = document.getElementById('searchInput');
        if (searchInput) {
            const debouncedSearch = this.utils.debounce(
                this.performSearch.bind(this),
                this.constants.debounceDelay
            );

            searchInput.addEventListener('input', () => {
                this.updateClearButton();
                debouncedSearch();
            });

            searchInput.addEventListener('keypress', (e) => {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    this.performSearch();
                }
            });
        }

        // 新增通知按鈕 - 明確綁定以避免委派失效導致無法開啟 modal
        const addBtn = document.getElementById('addNotificationBtn');
        if (addBtn) {
            addBtn.addEventListener('click', (e) => {
                e.preventDefault();
                // 先打開 modal
                this.addNotification();

                // 在 Modal 打開後稍微延遲，確保元素存在再填入預設內容（僅在內容為空時）
                setTimeout(() => {
                    const messageEl = document.getElementById('editMessage');
                    if (messageEl && messageEl.value.trim() === '') {
                        messageEl.value = this.constants.defaultTemplates?.short || '';
                        this.updateCharacterCount();
                    }
                }, 200);
            });
        }

        // 生成 AI 文案按鈕
        const genBtn = document.getElementById('generateAiBtn');
        if (genBtn) {
            genBtn.addEventListener('click', async (e) => {
                e.preventDefault();
                await this.generateAiContent();
            });
        }

        // 編輯分類改變時反應
        const editCategory = document.getElementById('editCategory');
        if (editCategory) {
            editCategory.addEventListener('change', (e) => {
                this.handleCategoryChange(e.target.value);
            });
        }

        // Bind editEmailStatus change to show/hide scheduled input
        const editEmailStatus = document.getElementById('editEmailStatus');
        if (editEmailStatus) {
            editEmailStatus.addEventListener('change', () => this.handleEmailStatusChange());
        }

        // 明確綁定篩選控件 onchange，若全域 change 綁定失效也能運作
        const categoryFilter = document.getElementById('categoryFilter');
        if (categoryFilter) categoryFilter.addEventListener('change', (e) => {
            this.state.filters.category = e.target.value;
            this.applyFilters();
        });

        const statusFilter = document.getElementById('statusFilter');
        if (statusFilter) statusFilter.addEventListener('change', (e) => {
            this.state.filters.emailStatus = e.target.value;
            this.applyFilters();
        });

        const channelFilter = document.getElementById('channelFilter');
        if (channelFilter) channelFilter.addEventListener('change', (e) => {
            this.state.filters.channel = e.target.value;
            this.applyFilters();
        });

        const startDateFilter = document.getElementById('startDateFilter');
        if (startDateFilter) startDateFilter.addEventListener('change', (e) => {
            this.state.filters.startDate = e.target.value;
            this.applyFilters();
        });

        const endDateFilter = document.getElementById('endDateFilter');
        if (endDateFilter) endDateFilter.addEventListener('change', (e) => {
            this.state.filters.endDate = e.target.value;
            this.applyFilters();
        });
    }

    // 當編輯分類改變時的處理（可擴展）
    handleCategoryChange(value) {
        // value 例如 'order', 'security' 等
        try {
            // 更新一個可見的輔助說明（如果存在）
            const helpElId = 'editCategoryHelp';
            let helpEl = document.getElementById(helpElId);
            if (!helpEl) {
                // 在分類下方建立說明節點
                const select = document.getElementById('editCategory');
                if (select && select.parentElement) {
                    helpEl = document.createElement('div');
                    helpEl.id = helpElId;
                    helpEl.className = 'form-text text-muted';
                    select.parentElement.appendChild(helpEl);
                }
            }

            if (helpEl) {
                const labels = this.constants.categoryLabels || {};
                helpEl.textContent = labels[value] ? `已選擇分類：${labels[value]}` : `已選擇分類：${value}`;
            }

            // 若訊息欄位為空，根據分類自動填入較適合的範例模板（非強制）
            const messageEl = document.getElementById('editMessage');
            if (messageEl && messageEl.value.trim() === '') {
                if (value === 'promotion') {
                    messageEl.value = this.constants.defaultTemplates?.promotion || '';
                } else if (value === 'system') {
                    messageEl.value = this.constants.defaultTemplates?.short || '';
                }
                this.updateCharacterCount();
            }
        } catch (e) {
            console.warn('handleCategoryChange 處理失敗', e);
        }
    }

    // 使用箭頭函式綁定，確保 this 指向實例並避免 .bind 未定義錯誤
    handleGlobalClick = (event) => {
        const target = event.target;

        // handle pagination links or any element with data-page
        const pageEl = target.closest('[data-page]');
        if (pageEl) {
            event.preventDefault();
            const page = parseInt(pageEl.getAttribute('data-page'), 10);
            if (!isNaN(page)) this.changePage(page);
            return;
        }

        const button = target.closest('button, a');
        if (!button) return;

        const buttonId = button.id;
        const action = button.dataset.action;
        const dataId = button.dataset.id ? parseInt(button.dataset.id, 10) : null;

        // 防止重複點擊
        if (button.disabled || button.classList.contains('notification-processing')) {
            event.preventDefault();
            return;
        }

        // Common id-based handlers
        switch (buttonId) {
            case 'addNotificationBtn':
                event.preventDefault();
                this.addNotification();
                return;
            case 'statsBtn':
                event.preventDefault();
                this.showStats();
                return;
            case 'searchBtn':
                event.preventDefault();
                this.performSearch();
                return;
            case 'clearSearch':
                event.preventDefault();
                this.clearSearch();
                return;
            case 'saveBtn':
                event.preventDefault();
                this.saveNotification();
                return;
        }

        // If action attribute is present, delegate to handler
        if (action) {
            event.preventDefault();
            this.handleActionButton(button, action, dataId);
            return;
        }

        // Handle class-based buttons like edit/view
        if (button.classList.contains('edit-btn') && dataId) {
            event.preventDefault();
            this.openEdit(dataId);
            return;
        }

        if (button.classList.contains('view-btn') && dataId) {
            event.preventDefault();
            this.openView(dataId);
            return;
        }

        // Handle sort clicks (headers with data-sort)
        const sortEl = target.closest('[data-sort]');
        if (sortEl) {
            event.preventDefault();
            const sortBy = sortEl.getAttribute('data-sort');
            if (sortBy) {
                if (this.state.sortColumn === sortBy) {
                    this.state.sortDirection = this.state.sortDirection === 'asc' ? 'desc' : 'asc';
                } else {
                    this.state.sortColumn = sortBy;
                    this.state.sortDirection = 'asc';
                }
                this.state.currentPage = 1;
                this.loadNotifications();
            }
        }
    }

    // Central action handler
    handleActionButton(button, action, id = null) {
        switch ((action || '').toString()) {
            case 'delete':
                if (id) this.deleteItem(id);
                break;
            case 'publish':
                if (id) this.publishItem(id);
                break;
            case 'delete-selected':
                this.deleteSelected();
                break;
            case 'publish-selected':
                this.publishSelected();
                break;
            case 'add-notification':
                this.addNotification();
                break;
            case 'create-test-data':
                this.createTestData();
                break;
            default:
                console.warn('未知的 action:', action);
        }
    }

    openEdit(id) {
        try {
            const item = this.state.currentData.find(n => n.id === id);
            if (!item) {
                this.utils.showAlert('找不到指定通知', 'warning');
                return;
            }

            // 如果之前處於檢視模式，恢復為編輯模式
            this.setModalViewMode(false);

            // 填入表單欄位（依頁面存在與否進行賦值）
            const editId = document.getElementById('editId');
            if (editId) editId.value = item.id || '';

            const editCategory = document.getElementById('editCategory');
            if (editCategory && item.category) editCategory.value = item.category;

            const editChannel = document.getElementById('editChannel');
            if (editChannel && item.channel) editChannel.value = item.channel;

            const editEmailStatus = document.getElementById('editEmailStatus');
            if (editEmailStatus && item.emailStatus) editEmailStatus.value = item.emailStatus;

            const editMessage = document.getElementById('editMessage');
            if (editMessage && typeof item.message === 'string') editMessage.value = item.message;

            const editScheduledAt = document.getElementById('editScheduledAt');
            if (editScheduledAt && item.sentAt) {
                // convert ISO to local datetime-local value if possible
                try {
                    const dt = new Date(item.sentAt);
                    const local = new Date(dt.getTime() - dt.getTimezoneOffset() * 60000).toISOString().slice(0,16);
                    editScheduledAt.value = local;

                    // update min to ensure it's in the future
                    const now = new Date();
                    const oneMinuteLater = new Date(now.getTime() + 1 * 60000);
                    const localDateTime = new Date(oneMinuteLater.getTime() - oneMinuteLater.getTimezoneOffset() * 60000)
                        .toISOString().slice(0, 16);
                    editScheduledAt.min = localDateTime;

                } catch (e) { }
            }

            // targetType handling: try to set radios if available
            if (item.emailAddress) {
                const targetSpecific = document.getElementById('targetSpecific');
                if (targetSpecific) {
                    targetSpecific.checked = true;
                    const specificInput = document.getElementById('specificAccount');
                    if (specificInput) { specificInput.disabled = false; specificInput.required = true; specificInput.value = item.emailAddress || ''; }
                }
            }

            this.updateCharacterCount();
            // Ensure scheduled input visibility/min updated according to selected status
            try { this.handleEmailStatusChange(); } catch (e) { /* ignore */ }
            this.showModal();
        } catch (e) {
            console.error('openEdit 失敗', e);
            this.utils.showAlert('打開編輯視窗失敗', 'danger');
        }
    }

    openView(id) {
        try {
            const item = this.state.currentData.find(n => n.id === id);
            if (!item) {
                this.utils.showAlert('找不到指定通知', 'warning');
                return;
            }

            // 如果 edit modal 存在，重用編輯畫面但切換為檢視(只讀)模式
            const modalEl = document.getElementById('editModal');
            if (modalEl) {
                // 將表單填入，但切換為只讀模式
                const editId = document.getElementById('editId');
                if (editId) editId.value = item.id || '';

                const editCategory = document.getElementById('editCategory');
                if (editCategory) editCategory.value = item.category || '';

                const editChannel = document.getElementById('editChannel');
                if (editChannel) editChannel.value = item.channel || '';

                const editEmailStatus = document.getElementById('editEmailStatus');
                if (editEmailStatus) editEmailStatus.value = item.emailStatus || '';

                const editMessage = document.getElementById('editMessage');
                if (editMessage) editMessage.value = item.message || '';

                const editScheduledAt = document.getElementById('editScheduledAt');
                if (editScheduledAt && item.sentAt) {
                    try {
                        const dt = new Date(item.sentAt);
                        const local = new Date(dt.getTime() - dt.getTimezoneOffset() * 60000).toISOString().slice(0,16);
                        editScheduledAt.value = local;
                    } catch (e) { }
                }

                // 如果有指定 email，填入並顯示
                const specificInput = document.getElementById('specificAccount');
                if (specificInput) specificInput.value = item.emailAddress || '';

                // 切換為檢視模式（只讀）
                this.setModalViewMode(true);
                this.showModal();
                return;
            }

            // fallback: 舊方式顯示訊息
            this.utils.showAlert(item.message || '(無內容)', 'info', 7000);
        } catch (e) {
            console.error('openView 失敗', e);
            this.utils.showAlert('打開檢視視窗失敗', 'danger');
        }
    }

    // 切換編輯模態框的檢視(只讀)模式
    setModalViewMode(isViewMode) {
        try {
            const modalEl = document.getElementById('editModal');
            if (!modalEl) return;

            // 表單元素集合
            const selectors = ['input', 'textarea', 'select', 'button'];
            selectors.forEach(sel => {
                modalEl.querySelectorAll(sel).forEach(el => {
                    // 不要禁用關閉或取消按鈕
                    if (el.classList && (el.classList.contains('btn-close') || el.dataset.action === 'cancel')) return;
                    // 儲存按鈕在檢視模式需隱藏或停用
                    if (el.id === 'saveBtn') {
                        el.style.display = isViewMode ? 'none' : '';
                        el.disabled = isViewMode;
                        return;
                    }

                    if (isViewMode) {
                        // 對表單欄位設為只讀/禁用
                        if (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA') {
                            el.setAttribute('readonly', 'readonly');
                            el.disabled = true;
                        } else if (el.tagName === 'SELECT') {
                            el.disabled = true;
                        } else if (el.tagName === 'BUTTON') {
                            // 隱藏產生 AI、其他會修改內容的按鈕
                            if (el.id === 'generateAiBtn') el.style.display = 'none';
                        }
                    } else {
                        // 恢復可編輯狀態
                        if (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA') {
                            el.removeAttribute('readonly');
                            el.disabled = false;
                        } else if (el.tagName === 'SELECT') {
                            el.disabled = false;
                        } else if (el.tagName === 'BUTTON') {
                            if (el.id === 'generateAiBtn') el.style.display = '';
                        }
                    }
                });
            });

            // 加一個標記，方便其他函式檢查
            if (isViewMode) modalEl.setAttribute('data-view-mode', '1'); else modalEl.removeAttribute('data-view-mode');
        } catch (e) {
            console.warn('setModalViewMode 失敗', e);
        }
    }

    // 主要業務邏輯方法
    async loadNotifications() {
        try {
            console.log('開始載入通知資料...');
            this.ui.showLoading(true);

            const params = this.buildApiParams();
            const result = await this.api.call(`${this.state.apiBasePath}/GetNotifications?${params}`);

            // Support different casing from backend (Data/Data, TotalCount/totalCount, etc.)
            const data = result?.data ?? result?.Data ?? null;
            const totalCount = result?.totalCount ?? result?.TotalCount ?? 0;
            const totalPages = result?.totalPages ?? result?.TotalPages ?? 1;
            const currentPage = result?.currentPage ?? result?.CurrentPage ?? this.state.currentPage ?? 1;

            if (data) {
                this.state.currentData = Array.isArray(data) ? data : [];
                this.state.totalCount = totalCount || this.state.currentData.length;
                this.state.totalPages = totalPages || 1;
                this.state.currentPage = currentPage || 1;

                console.log('載入資料成功:', {
                    count: this.state.currentData.length,
                    totalCount: this.state.totalCount,
                    totalPages: this.state.totalPages,
                    currentPage: this.state.currentPage
                });

                this.ui.renderTable();
                this.updatePageInfo();
                this.updatePagination();
            } else {
                throw new Error('資料格式不正確');
            }
        } catch (error) {
            console.error('載入資料失敗:', error);
            this.state.currentData = [];
            this.state.totalCount = 0;
            this.ui.renderTable();
            this.utils.showAlert('載入資料失敗: ' + error.message, 'danger');
        } finally {
            this.ui.showLoading(false);
        }
    }

    buildApiParams() {
        const params = new URLSearchParams();

        if (this.state.searchTerm) params.append('search', this.state.searchTerm);
        if (this.state.filters.category) params.append('category', this.state.filters.category);
        if (this.state.filters.emailStatus) params.append('emailStatus', this.state.filters.emailStatus);
        if (this.state.filters.channel) params.append('channel', this.state.filters.channel);
        if (this.state.filters.startDate) params.append('startDate', this.state.filters.startDate);
        if (this.state.filters.endDate) params.append('endDate', this.state.filters.endDate);

        params.append('page', this.state.currentPage.toString());
        params.append('itemsPerPage', this.state.itemsPerPage.toString());
        params.append('sortBy', this.state.sortColumn);
        params.append('sortDirection', this.state.sortDirection);

        return params;
    }

    async saveNotification() {
        try {
            const formData = this.getFormData();

            if (!this.validateFormData(formData)) {
                return;
            }

            this.ui.showSaveLoading(true);

            const apiData = this.prepareApiData(formData);
            const isEditing = formData.id && parseInt(formData.id) > 0;

            let result;
            if (isEditing) {
                result = await this.api.call(
                    `${this.state.apiBasePath}/UpdateNotification/${formData.id}`,
                    {
                        method: 'PUT',
                        body: JSON.stringify(apiData)
                    }
                );
            } else {
                const endpoint = formData.targetType === 3 ?
                    'CreateNotification' : 'CreateBulkNotification';

                result = await this.api.call(
                    `${this.state.apiBasePath}/${endpoint}`,
                    {
                        method: 'POST',
                        body: JSON.stringify(apiData)
                    }
                );
            }

            if (result?.success !== false) {
                const action = isEditing ? '更新' : '新增';
                let message = result.message || `通知${action}成功`;

                if (!isEditing && result.data?.createdCount) {
                    message = `成功創建 ${result.data.createdCount} 筆通知`;
                }

                this.utils.showAlert(message, 'success');
                this.closeModal();
                await this.loadNotifications();
            } else {
                throw new Error(result?.message || '儲存失敗');
            }
        } catch (error) {
            console.error('儲存通知失敗:', error);
            this.utils.showAlert('儲存失敗: ' + error.message, 'danger');
        } finally {
            this.ui.showSaveLoading(false);
        }
    }

    getFormData() {
        const form = document.getElementById('notificationForm');
        const formData = new FormData(form);

        const targetTypeElement = document.querySelector('input[name="targetType"]:checked');
        const targetType = targetTypeElement ? parseInt(targetTypeElement.value) : 1;

        const data = {
            id: document.getElementById('editId')?.value?.trim() || null,
            category: formData.get('category')?.trim() || '',
            channel: formData.get('channel')?.trim() || 'email',
            emailStatus: formData.get('emailStatus')?.trim() || 'draft',
            message: formData.get('message')?.trim() || '',
            targetType: targetType,
            scheduledAt: document.getElementById('editScheduledAt')?.value || null
        };

        if (targetType === 3) {
            const specificAccountInput = document.getElementById('specificAccount');
            data.specificAccount = specificAccountInput?.value?.trim() || '';
        }

        // 設定發送時間
        const now = new Date();
        switch (data.emailStatus) {
            case 'immediate':
                data.sentAt = now.toISOString();
                break;
            case 'scheduled':
                if (data.scheduledAt) {
                    data.sentAt = new Date(data.scheduledAt).toISOString();
                } else {
                    data.sentAt = now.toISOString();
                }
                break;
            default:
                data.sentAt = now.toISOString();
        }

        return data;
    }

    validateFormData(data) {
        const errors = [];

        if (!data.category) errors.push('請選擇分類');
        if (!data.message) errors.push('請輸入訊息內容');
        if (!data.channel) errors.push('請選擇通知管道');
        if (![1, 2, 3].includes(data.targetType)) errors.push('請選擇發送對象');

        if (data.targetType === 3 && !data.specificAccount) {
            errors.push('選擇指定帳號時，請輸入帳號');
        }

        if (data.targetType === 3 && data.specificAccount) {
            const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
            if (!emailRegex.test(data.specificAccount)) {
                errors.push('請輸入有效的郵件地址');
            }
        }

        if (data.emailStatus === 'scheduled' && !data.scheduledAt) {
            errors.push('選擇排程發送時，請設定排程時間');
        }

        // New: ensure scheduled time is in the future
        if (data.emailStatus === 'scheduled' && data.scheduledAt) {
            try {
                const chosen = new Date(data.scheduledAt);
                const now = new Date();
                if (chosen <= now) {
                    errors.push('排程時間必須是未來時間');
                    const scheduledError = document.getElementById('scheduledDateError');
                    if (scheduledError) scheduledError.style.display = 'block';
                } else {
                    const scheduledError = document.getElementById('scheduledDateError');
                    if (scheduledError) scheduledError.style.display = 'none';
                }
            } catch (e) {
                errors.push('排程時間格式錯誤');
            }
        }

        if (data.message?.length > this.constants.maxMessageLength) {
            errors.push(`訊息內容不能超過${this.constants.maxMessageLength}個字元`);
        }

        if (errors.length > 0) {
            this.utils.showAlert('驗證失敗：' + errors.join(', '), 'danger');
            return false;
        }

        return true;
    }

    prepareApiData(formData) {
        const baseData = {
            category: formData.category,
            channel: formData.channel,
            emailStatus: formData.emailStatus,
            message: formData.message,
            sentAt: formData.sentAt,
            memberId: null,
            sellerId: null
        };

        if (formData.targetType === 3) {
            return {
                ...baseData,
                emailAddress: formData.specificAccount
            };
        } else {
            return {
                ...baseData,
                targetType: formData.targetType
            };
        }
    }

    async deleteItem(id) {
        if (this.state._deletingItems.has(id)) {
            return Promise.resolve();
        }

        this.state._deletingItems.add(id);

        try {
            const confirmed = await this.utils.showCustomConfirm(
                '確認刪除',
                '確定要刪除這筆通知嗎？此操作無法復原。',
                '刪除', '取消'
            );

            if (!confirmed) return;

            const result = await this.api.call(
                `${this.state.apiBasePath}/DeleteNotification`,
                {
                    method: 'POST',
                    body: JSON.stringify({ ids: [id] })
                }
            );

            if (result?.success !== false) {
                this.utils.showAlert(result.message || '通知刪除成功', 'success');
                this.state.selectedItems.delete(id);
                await this.loadNotifications();
            } else {
                throw new Error(result?.message || '刪除通知失敗');
            }
        } catch (error) {
            console.error('刪除通知失敗:', error);
            this.utils.showAlert('刪除失敗: ' + error.message, 'danger');
            throw error;
        } finally {
            this.state._deletingItems.delete(id);
        }
    }

    async deleteSelected() {
        if (this.state.selectedItems.size === 0) {
            this.utils.showAlert('請先選取要刪除的通知', 'warning');
            return;
        }

        if (this.state._batchDeleting) return;

        this.state._batchDeleting = true;

        try {
            const selectedCount = this.state.selectedItems.size;
            const confirmed = await this.utils.showCustomConfirm(
                '批量刪除確認',
                `確定要刪除這 ${selectedCount} 筆通知嗎？此操作無法復原。`,
                '刪除', '取消'
            );

            if (!confirmed) return;

            const selectedIds = Array.from(this.state.selectedItems);
            const result = await this.api.call(
                `${this.state.apiBasePath}/DeleteNotification`,
                {
                    method: 'POST',
                    body: JSON.stringify({ ids: selectedIds })
                }
            );

            if (result?.success !== false) {
                this.utils.showAlert(
                    result.message || `成功刪除 ${selectedCount} 筆通知`,
                    'success'
                );
                this.state.selectedItems.clear();
                await this.loadNotifications();
            } else {
                throw new Error(result?.message || '批量刪除通知失敗');
            }
        } catch (error) {
            console.error('批量刪除通知失敗:', error);
            this.utils.showAlert('批量刪除失敗: ' + error.message, 'danger');
        } finally {
            this.state._batchDeleting = false;
        }
    }

    // 其他輔助方法
    addNotification() {
        this.resetModal();
        const modalTitle = document.getElementById('modalTitle');
        if (modalTitle) {
            modalTitle.innerHTML = '<i class="bi bi-plus-circle me-2"></i>新增通知';
        }
        this.showModal();
    }

    resetModal() {
        const form = document.getElementById('notificationForm');
        if (form) form.reset();

        document.getElementById('editId').value = '';
        document.getElementById('editEmailStatus').value = 'draft';

        const defaultTarget = document.getElementById('targetAllUsers');
        if (defaultTarget) defaultTarget.checked = true;

        this.handleTargetTypeChange();
        this.handleEmailStatusChange();
        this.updateCharacterCount();

        // 如果有 member select，重置為預設
        const select = document.getElementById('specificAccountSelect');
        if (select) select.value = '';
        const input = document.getElementById('specificAccount');
        if (input) { input.value = ''; input.disabled = true; input.required = false; }

        // Ensure scheduled input min is set to future
        const scheduledInput = document.getElementById('editScheduledAt');
        if (scheduledInput) {
            const now = new Date();
            const oneMinuteLater = new Date(now.getTime() + 1 * 60000);
            const localDateTime = new Date(oneMinuteLater.getTime() - oneMinuteLater.getTimezoneOffset() * 60000)
                .toISOString().slice(0, 16);
            scheduledInput.min = localDateTime;
            const scheduledError = document.getElementById('scheduledDateError');
            if (scheduledError) scheduledError.style.display = 'none';
        }
    }

    showModal() {
        // 支援 jQuery (BS4) || bootstrap (BS5) || fallback
        const modalEl = document.getElementById('editModal');
        if (!modalEl) {
            this.utils.showAlert('找不到編輯模態框元素', 'danger');
            return;
        }

        if (typeof jQuery !== 'undefined' && jQuery.fn.modal) {
            $(modalEl).modal('show');
            return;
        }

        if (typeof bootstrap !== 'undefined') {
            try {
                const bsModal = new bootstrap.Modal(modalEl);
                bsModal.show();
                return;
            } catch (e) {
                console.warn('Bootstrap Modal 顯示失敗', e);
            }
        }

        // fallback
        modalEl.style.display = 'block';
        modalEl.classList.add('show');
    }

    closeModal() {
        if (this.state.editModal?.modal) {
            this.state.editModal.modal('hide');
        }
    }

    handleTargetTypeChange() {
        const targetSpecific = document.getElementById('targetSpecific');
        const specificContainer = document.getElementById('specificAccountContainer');
        const specificInput = document.getElementById('specificAccount');
        const specificSelect = document.getElementById('specificAccountSelect');

        if (targetSpecific?.checked) {
            specificContainer.style.display = 'block';
            specificInput.required = true;
            specificInput.disabled = false;

            // 如果有 select，將選擇內容同步到 input
            if (specificSelect && specificSelect.value && specificSelect.value !== '__custom__') {
                specificInput.value = specificSelect.value;
            }
        } else {
            specificContainer.style.display = 'none';
            specificInput.required = false;
            specificInput.disabled = true;
            specificInput.value = '';
        }
    }

    handleEmailStatusChange() {
        const statusSelect = document.getElementById('editEmailStatus');
        const scheduledContainer = document.getElementById('scheduledDateContainer');
        const scheduledInput = document.getElementById('editScheduledAt');
        const scheduledError = document.getElementById('scheduledDateError');

        // helper to set min to a few minutes in the future
        const setScheduledMin = (minutesAhead = 1) => {
            if (!scheduledInput) return;
            const now = new Date();
            const future = new Date(now.getTime() + minutesAhead * 60000);
            const localDateTime = new Date(future.getTime() - future.getTimezoneOffset() * 60000)
                .toISOString().slice(0, 16);
            scheduledInput.min = localDateTime;
        };

        if (statusSelect.value === 'scheduled') {
            scheduledContainer.style.display = 'block';
            scheduledInput.required = true;
            setScheduledMin(1);

            if (!scheduledInput.value) {
                const oneHourLater = new Date(Date.now() + 60 * 60 * 1000);
                const localDateTime = new Date(oneHourLater.getTime() -
                    oneHourLater.getTimezoneOffset() * 60000)
                    .toISOString().slice(0, 16);
                scheduledInput.value = localDateTime;
            } else {
                // Ensure existing value is not earlier than min; if it is, bump it to min
                try {
                    const chosen = new Date(scheduledInput.value);
                    const minDate = new Date(scheduledInput.min);
                    if (chosen <= minDate) {
                        scheduledInput.value = scheduledInput.min;
                    }
                } catch (e) { }
            }

            if (scheduledError) scheduledError.style.display = 'none';
        } else {
            scheduledContainer.style.display = 'none';
            scheduledInput.required = false;
            scheduledInput.value = '';
            if (scheduledError) scheduledError.style.display = 'none';
        }
    }

    updateCharacterCount() {
        const messageInput = document.getElementById('editMessage');
        const charCount = document.getElementById('messageCharCount');

        if (messageInput && charCount) {
            const currentLength = messageInput.value.length;
            const maxLength = this.constants.maxMessageLength;
            charCount.textContent = `${currentLength}/${maxLength}`;

            charCount.classList.toggle('text-danger', currentLength > maxLength);
        }
    }

    // 生成 AI 文案 - 客戶端模擬版本
    async generateAiContent() {
        const btn = document.getElementById('generateAiBtn');
        const spinner = document.getElementById('aiGenerateSpinner');
        try {
            if (btn) {
                btn.disabled = true;
            }
            if (spinner) spinner.style.display = 'block';

            const messageEl = document.getElementById('editMessage');
            const categoryEl = document.getElementById('editCategory');

            if (!messageEl) {
                this.utils.showAlert('找不到訊息輸入欄位', 'danger');
                return;
            }

            const category = (categoryEl && categoryEl.value) ? categoryEl.value.toLowerCase() : 'general';
            const existing = messageEl.value.trim();

            // simple template generation based on category
            let generated = '';
            switch (category) {
                case 'order':
                    generated = '親愛的會員，您的訂單已成功處理，我們正在安排出貨。若需查詢詳情，請至會員中心查看訂單狀態。感謝您的購買！';
                    break;
                case 'payment':
                    generated = '您的付款已成功，我們已收到款項並開始處理您的訂單。如有任何問題，請聯絡客服。';
                    break;
                case 'promotion':
                    generated = '限時促銷：全館商品優惠中，立即下單享受獨家折扣，數量有限，欲購從速！';
                    break;
                case 'security':
                    generated = '安全通知：我們發現可疑登入活動，請確認是否為您本人操作，若非請立即變更密碼並聯絡客服。';
                    break;
                case 'system':
                    generated = '系統通知：網站將於預定時間進行維護，期間部分功能可能暫時無法使用，造成不便敬請見諒。';
                    break;
                case 'account':
                    generated = '帳戶通知：請更新您的帳戶資訊以確保服務正常，若有疑問請聯絡客服協助。';
                    break;
                default:
                    generated = '親愛的會員，感謝您的支持！我們有重要資訊通知您，請至會員中心查看詳細內容。';
            }

            // append or replace intelligently
            if (existing.length === 0) {
                messageEl.value = generated;
            } else {
                // if existing already seems like a customer message, append separated by newline
                messageEl.value = existing + '\n\n' + generated;
            }

            // small delay to simulate processing
            await new Promise(r => setTimeout(r, 500));

            this.updateCharacterCount();
            this.utils.showAlert('已重新生成文案', 'success');
        } catch (err) {
            console.error('generateAiContent 錯誤', err);
            this.utils.showAlert('生成文案發生錯誤', 'danger');
        } finally {
            if (spinner) spinner.style.display = 'none';
            if (btn) btn.disabled = false;
        }
    }

    performSearch() {
        const searchInput = document.getElementById('searchInput');
        if (searchInput) {
            this.state.searchTerm = searchInput.value.trim();
        }
        this.state.currentPage = 1;
        this.state.selectedItems.clear();
        this.loadNotifications();
        this.updateClearButton();
    }

    updateClearButton() {
        const clearBtn = document.getElementById('clearSearch');
        const searchInput = document.getElementById('searchInput');
        if (clearBtn && searchInput) {
            clearBtn.style.display = searchInput.value.length > 0 ? 'flex' : 'none';
        }
    }

    clearSearch() {
        const searchInput = document.getElementById('searchInput');
        if (searchInput) searchInput.value = '';
        this.state.searchTerm = '';
        this.state.currentPage = 1;
        this.loadNotifications();
        this.updateClearButton();
    }

    toggleSelectAll(checked) {
        const currentPageItems = this.state.currentData.map(item => item.id);

        if (checked) {
            currentPageItems.forEach(id => this.state.selectedItems.add(id));
        } else {
            currentPageItems.forEach(id => this.state.selectedItems.delete(id));
        }

        this.ui.renderTable();
        this.ui.updateDeleteButton();
    }

    updatePageInfo() {
        this.ui.updatePageInfo();
    }

    updatePagination() {
        this.ui.updatePagination();
    }

    changePage(page) {
        if (page >= 1 && page <= this.state.totalPages) {
            this.state.currentPage = page;
            this.loadNotifications();
        }
    }

    // 篩選功能
    applyFilters() {
        // 更新篩選器狀態並重新載入資料
        console.log('應用篩選條件:', this.state.filters);
        this.state.currentPage = 1;
        this.state.selectedItems.clear();
        this.loadNotifications();
        
        // 顯示篩選器已啟用的視覺效果
        this.updateFilterIndicators();
    }

    // 更新篩選器指示
    updateFilterIndicators() {
        // 檢查是否有啟用的篩選條件
        const hasActiveFilters = Object.values(this.state.filters).some(value => value !== "");
        
        // 更新篩選按鈕的視覺效果
        const filterBtn = document.querySelector('.filter-btn-integrated');
        if (filterBtn) {
            // 添加或移除活動狀態類
            filterBtn.classList.toggle('active', hasActiveFilters);
            
            // 如果有已啟用的篩選條件，添加徽章
            const badge = filterBtn.querySelector('.filter-badge') || document.createElement('span');
            
            if (hasActiveFilters) {
                badge.className = 'filter-badge badge bg-danger badge-pill';
                badge.textContent = Object.values(this.state.filters).filter(value => value !== "").length;
                
                if (!filterBtn.querySelector('.filter-badge')) {
                    filterBtn.appendChild(badge);
                }
            } else if (filterBtn.querySelector('.filter-badge')) {
                filterBtn.querySelector('.filter-badge').remove();
            }
        }
    }

    // 清除所有篩選條件
    clearFilters() {
        // 重置篩選狀態
        this.state.filters = {
            category: "",
            emailStatus: "",
            channel: "",
            startDate: "",
            endDate: ""
        };
        
        // 重置表單控件
        const categoryFilter = document.getElementById('categoryFilter');
        const statusFilter = document.getElementById('statusFilter');
        const channelFilter = document.getElementById('channelFilter');
        const startDateFilter = document.getElementById('startDateFilter');
        const endDateFilter = document.getElementById('endDateFilter');
        
        if (categoryFilter) categoryFilter.value = '';
        if (statusFilter) statusFilter.value = '';
        if (channelFilter) channelFilter.value = '';
        if (startDateFilter) startDateFilter.value = '';
        if (endDateFilter) endDateFilter.value = '';
        
        // 重新載入資料
        this.state.currentPage = 1;
        this.loadNotifications();
        
        // 更新篩選器指示
        this.updateFilterIndicators();
        
        // 收起篩選面板
        const filterPanel = document.getElementById('filterPanel');
        if (filterPanel && typeof jQuery !== 'undefined') {
            $(filterPanel).collapse('hide');
        }
        
        this.utils.showAlert('已清除所有篩選條件', 'info');
    }

    // 統計功能
    async showStats() {
        try {
            if (this.state.statsModal?.modal) {
                this.state.statsModal.modal('show');

                const statsLoading = document.getElementById('statsLoading');
                const statsContent = document.getElementById('statsContent');

                if (statsLoading) statsLoading.style.display = 'block';
                if (statsContent) statsContent.style.display = 'none';

                const result = await this.api.call(`${this.state.apiBasePath}/GetStatistics`);

                if (result?.data) {
                    this.renderStatistics(result.data);
                } else {
                    throw new Error('統計資料格式錯誤');
                }
            }
        } catch (error) {
            console.error('載入統計資料失敗:', error);
            this.utils.showAlert('載入統計失敗: ' + error.message, 'danger');
        }
    }

    renderStatistics(stats) {
        const statsLoading = document.getElementById('statsLoading');
        const statsContent = document.getElementById('statsContent');

        if (statsLoading) statsLoading.style.display = 'none';
        if (statsContent) statsContent.style.display = 'block';

        const elements = {
            totalCount: document.getElementById('totalCount'),
            todayCount: document.getElementById('todayCount'),
            successRate: document.getElementById('successRate')
        };

        if (elements.totalCount) elements.totalCount.textContent = stats.totalCount || 0;
        if (elements.todayCount) elements.todayCount.textContent = stats.todayCount || 0;
        if (elements.successRate) elements.successRate.textContent = (stats.successRate || 0) + '%';

        if (window.NotificationCharts) {
            NotificationCharts.renderChart(stats);
        }
    }

    // 測試資料和其他操作
    async createTestData() {
        const confirmed = await this.utils.showCustomConfirm(
            '創建測試資料',
            '確定要創建測試通知資料嗎？',
            '確定', '取消'
        );

        if (confirmed) {
            try {
                await this.api.call(`${this.state.apiBasePath}/GetNotifications?page=1&itemsPerPage=1`);
                await this.loadNotifications();
                this.utils.showAlert('測試資料創建成功', 'success');
            } catch (error) {
                console.error('創建測試資料失敗:', error);
                this.utils.showAlert('創建測試資料失敗', 'danger');
            }
        }
    }

    async publishItem(id) {
        try {
            if (!id) return;
            const result = await this.api.call(`${this.state.apiBasePath}/PublishNotification/${id}`, { method: 'POST' });
            if (result?.success !== false) {
                this.utils.showAlert(result.message || '發布成功', 'success');
                await this.loadNotifications();
            } else {
                throw new Error(result?.message || '發布失敗');
            }
        } catch (error) {
            console.error('發布失敗', error);
            this.utils.showAlert('發布失敗：' + error.message, 'danger');
        }
    }
}

// 工具類
class NotificationUtils {
    debounce(func, wait, immediate) {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                timeout = null;
                if (!immediate) func.apply(this, args);
            };
            const callNow = immediate && !timeout;
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
            if (callNow) func.apply(this, args);
        };
    }

    escapeHtml(text) {
        if (typeof text !== 'string') return '';
        const map = {
            '&': '&amp;', '<': '&lt;', '>': '&gt;',
            '"': '&quot;', "'": '&#039;'
        };
        return text.replace(/[&<>\"']/g, m => map[m]);
    }

    escapeRegExp(string) {
        return string.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    }

    formatDate(date, format = 'yyyy/MM/dd HH:mm') {
        if (!date) return '';
        try {
            return new Date(date).toLocaleString('zh-TW');
        } catch (e) {
            return date.toString();
        }
    }

    showAlert(message, type = 'info', duration = 5000) {
        const existingAlert = document.querySelector('.alert-notification-custom');
        if (existingAlert) existingAlert.remove();

        const alertDiv = document.createElement('div');
        alertDiv.className = `alert alert-${type} alert-dismissible fade show alert-notification-custom`;
        alertDiv.style.cssText = `
            position: fixed; top: 80px; right: 20px; z-index: 9999; 
            min-width: 300px; max-width: 500px; box-shadow: 0 4px 12px rgba(0,0,0,0.15);
            border-radius: 8px; animation: slideInRight 0.3s ease-out;
        `;

        const iconMap = {
            'success': 'bi bi-check-circle',
            'danger': 'bi bi-exclamation-triangle',
            'warning': 'bi bi-exclamation-triangle',
            'info': 'bi bi-info-circle'
        };

        const icon = iconMap[type] || 'bi bi-info-circle';
        alertDiv.innerHTML = `
            <div class="d-flex align-items-center">
                <i class="${icon} me-2"></i>
                <span>${message}</span>
                <button type="button" class="btn-close ms-auto" 
                        onclick="this.parentElement.parentElement.remove()"></button>
            </div>
        `;

        document.body.appendChild(alertDiv);

        setTimeout(() => {
            if (alertDiv?.parentNode) {
                alertDiv.style.animation = 'slideOutRight 0.3s ease-in forwards';
                setTimeout(() => alertDiv.remove(), 300);
            }
        }, duration);
    }

    async showCustomConfirm(title, message, confirmText = '確定', cancelText = '取消') {
        return new Promise((resolve) => {
            // 創建自定義確認對話框
            const modalId = 'customConfirmModal' + Date.now();
            const modalHTML = `
                <div class="modal fade" id="${modalId}" tabindex="-1" role="dialog">
                    <div class="modal-dialog modal-dialog-centered" role="document">
                        <div class="modal-content">
                            <div class="modal-header border-0 pb-0">
                                <h5 class="modal-title">
                                    <i class="bi bi-question-circle text-warning me-2"></i>
                                    ${this.escapeHtml(title)}
                                </h5>
                            </div>
                            <div class="modal-body">
                                <p class="mb-0">${this.escapeHtml(message)}</p>
                            </div>
                            <div class="modal-footer border-0">
                                <button type="button" class="btn btn-secondary" data-modal-action="cancel">
                                    ${this.escapeHtml(cancelText)}
                                </button>
                                <button type="button" class="btn btn-danger" data-modal-action="confirm">
                                    ${this.escapeHtml(confirmText)}
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            `;

            document.body.insertAdjacentHTML('beforeend', modalHTML);
            const modal = document.getElementById(modalId);

            let bsModal = null;

            const cleanup = () => {
                try {
                    if (bsModal && typeof bsModal.hide === 'function') {
                        bsModal.hide();
                    } else if (typeof jQuery !== 'undefined' && jQuery.fn.modal) {
                        $(modal).modal('hide');
                    } else if (modal) {
                        modal.classList.remove('show');
                        modal.style.display = 'none';
                    }
                } catch (e) {
                    // ignore
                }

                // ensure backdrop removed (Bootstrap may leave .modal-backdrop)
                const backdrops = document.querySelectorAll('.modal-backdrop');
                backdrops.forEach(b => b.remove());

                // finally remove modal node after short delay
                setTimeout(() => {
                    if (modal && modal.parentNode) modal.parentNode.removeChild(modal);
                }, 250);
            };

            // Use delegated click handler but restrict to buttons with data-modal-action
            const handler = (e) => {
                const btn = e.target.closest('[data-modal-action]');
                if (!btn) return;
                e.stopPropagation(); // prevent global handlers from catching

                const action = btn.dataset.modalAction;
                if (action === 'confirm') {
                    resolve(true);
                    cleanup();
                } else if (action === 'cancel') {
                    resolve(false);
                    cleanup();
                }
            };

            modal.addEventListener('click', handler);

            // Bootstrap modal 顯示
            if (typeof jQuery !== 'undefined' && jQuery.fn.modal) {
                $(modal).modal('show').on('hidden.bs.modal', cleanup);
            } else if (typeof bootstrap !== 'undefined') {
                bsModal = new bootstrap.Modal(modal);
                bsModal.show();
                // ensure cleanup when hidden
                modal.addEventListener('hidden.bs.modal', cleanup);
            } else {
                modal.style.display = 'block';
                modal.classList.add('show');
            }
        });
    }
}

// API 管理類
class NotificationAPI {
    constructor(state, utils) {
        this.state = state;
        this.utils = utils;
        this.timeout = 10000;
    }

    async call(url, options = {}) {
        try {
            const fullUrl = url.startsWith('/') ? url : `/${url}`;
            console.log('API 調用:', fullUrl, options);

            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

            const defaultOptions = {
                headers: {
                    'Content-Type': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                signal: (typeof AbortSignal !== 'undefined' && AbortSignal.timeout) ? AbortSignal.timeout(this.timeout) : undefined
            };

            // fallback for environments without AbortSignal.timeout
            if (!defaultOptions.signal) {
                try {
                    const controller = new AbortController();
                    setTimeout(() => controller.abort(), this.timeout);
                    defaultOptions.signal = controller.signal;
                } catch (e) {
                    // ignore if AbortController not available
                }
            }

            if (token) {
                defaultOptions.headers['RequestVerificationToken'] = token;
            }

            const mergedOptions = {
                ...defaultOptions,
                ...options,
                headers: { ...defaultOptions.headers, ...options.headers }
            };

            const response = await fetch(fullUrl, mergedOptions);

            if (!response.ok) {
                throw new Error(`HTTP 錯誤 ${response.status}: ${response.statusText}`);
            }

            const contentType = response.headers.get('content-type');
            if (!contentType?.includes('application/json')) {
                const text = await response.text();
                if (text.includes('<html') || text.includes('<!DOCTYPE')) {
                    throw new Error('API 端點不存在，返回 HTML 頁面');
                }
                throw new Error('伺服器返回非JSON格式響應');
            }

            const data = await response.json();
            console.log('API 回應:', data);

            if (data.success === false) {
                const errorMessage = data.message || '操作失敗';
                if (data.errors && Object.keys(data.errors).length > 0) {
                    const errorMessages = Object.values(data.errors).join(', ');
                    throw new Error(`${errorMessage}: ${errorMessages}`);
                }
                throw new Error(errorMessage);
            }

            return data;
        } catch (error) {
            console.error('API 調用錯誤:', error);
            throw error;
        }
    }
}

// UI 管理類
class NotificationUI {
    constructor(state, constants, utils) {
        this.state = state;
        this.constants = constants;
        this.utils = utils;
    }

    showLoading(show) {
        const loadingElement = document.getElementById('loadingIndicator');
        if (loadingElement) {
            loadingElement.style.display = show ? 'block' : 'none';
        }
    }

    renderTable() {
        const tableBody = document.getElementById('tableBody');
        if (!tableBody) {
            console.log('找不到 tableBody 元素');
            return;
        }

        if (this.state.currentData.length === 0) {
            this.showEmptyTable();
            return;
        }

        const html = this.state.currentData
            .map(item => this.generateTableRow(item))
            .join('');

        tableBody.innerHTML = html;
        this.bindTableEvents();
        this.updateSelectAllCheckbox();
        this.updateDeleteButton();
    }

    generateTableRow(item) {
        const id = item.id || 0;
        const emailAddress = this.utils.escapeHtml(item.emailAddress || '');
        const category = item.category || 'unknown';
        const categoryLabel = this.utils.escapeHtml(
            item.categoryLabel || this.constants.categoryLabels[category] || category
        );
        const emailStatus = item.emailStatus || 'unknown';
        const emailStatusLabel = this.utils.escapeHtml(
            item.emailStatusLabel || this.constants.emailStatusLabels[emailStatus] || emailStatus
        );
        const channel = item.channel || 'unknown';
        const channelLabel = this.utils.escapeHtml(
            item.channelLabel || this.constants.channelLabels[channel] || channel
        );

        let message = item.message || '';
        let highlightedMessage = this.highlightSearchTerm(message);

        if (highlightedMessage.length > 100) {
            highlightedMessage = highlightedMessage.substring(0, 100) + '...';
        }

        const isChecked = this.state.selectedItems.has(id) ? 'checked' : '';
        const sentAtFormatted = item.formattedSentAt || this.utils.formatDate(item.sentAt);

        return `
            <tr data-id="${id}" class="${emailStatus === 'draft' ? 'draft-row' : ''}">
                <td class="text-center align-middle"><div class="form-check"><input type="checkbox" class="form-check-input row-checkbox" 
                          data-id="${id}" ${isChecked} /></div></td>
                <td class="text-nowrap align-middle">${sentAtFormatted}</td>
                <td class="align-middle">${emailAddress}</td>
                <td class="align-middle"><span class="badge ${this.getCategoryBadgeClass(category)}">${categoryLabel}</span></td>
                <td class="align-middle"><span class="badge ${this.getStatusBadgeClass(emailStatus)}">${emailStatusLabel}</span></td>
                <td class="align-middle">${channelLabel}</td>
                <td class="align-middle">
                    <span class="message-preview" title="${this.utils.escapeHtml(message)}">
                        ${highlightedMessage}
                    </span>
                </td>
                <td class="align-middle">
                    <div class="btn-group btn-group-sm" role="group">
                        ${this.getActionButtons(item)}
                    </div>
                </td>
            </tr>`;
    }

    highlightSearchTerm(message) {
        if (!this.state.searchTerm || !message) {
            return this.utils.escapeHtml(message);
        }

        try {
            const regex = new RegExp(
                `(${this.utils.escapeRegExp(this.state.searchTerm)})`,
                'gi'
            );
            return this.utils.escapeHtml(message).replace(
                regex,
                '<span class="notification-highlight">$1</span>'
            );
        } catch (e) {
            console.warn('搜尋高亮處理失敗:', e);
            return this.utils.escapeHtml(message);
        }
    }

    getCategoryBadgeClass(category) {
        const classMap = {
            'order': 'bg-primary', 'payment': 'bg-success', 'account': 'bg-warning',
            'security': 'bg-danger', 'promotion': 'bg-info', 'system': 'bg-secondary',
            'test': 'bg-dark', 'restock': 'bg-info'
        };
        return classMap[category?.toLowerCase()] || 'bg-secondary';
    }

    getStatusBadgeClass(status) {
        const classMap = {
            'immediate': 'bg-primary', 'scheduled': 'bg-info', 'draft': 'bg-secondary',
            'sent': 'bg-success', 'delivered': 'bg-success', 'failed': 'bg-danger',
            'pending': 'bg-warning'
        };
        return classMap[status?.toLowerCase()] || 'bg-secondary';
    }

    getActionButtons(item) {
        const id = item.id || 0;

        const baseButtons = `
            <button type="button" class="btn btn-outline-info btn-sm me-1 view-btn" 
                    data-id="${id}" title="查看">
                <i class="bi bi-eye"></i>
            </button>
        `;

        let actionButtons = '';
        switch (item.emailStatus?.toLowerCase()) {
            case 'draft':
                actionButtons = `
                    <button type="button" class="btn btn-outline-primary btn-sm me-1 edit-btn" 
                            data-id="${id}" title="編輯">
                        <i class="bi bi-pencil"></i>
                    </button>
                    <button type="button" class="btn btn-outline-success btn-sm me-1" 
                            data-action="publish" data-id="${id}" title="發布">
                        <i class="bi bi-send"></i>
                    </button>
                    <button type="button" class="btn btn-outline-danger btn-sm delete-btn" 
                            data-action="delete" data-id="${id}" title="刪除">
                        <i class="bi bi-trash"></i>
                    </button>
                `;
                break;
            case 'scheduled':
                actionButtons = `
                    <button type="button" class="btn btn-outline-primary btn-sm me-1 edit-btn" 
                            data-id="${id}" title="編輯排程">
                        <i class="bi bi-pencil"></i>
                    </button>
                    <button type="button" class="btn btn-outline-danger btn-sm delete-btn" 
                            data-action="delete" data-id="${id}" title="刪除">
                        <i class="bi bi-trash"></i>
                    </button>
                `;
                break;
            default:
                actionButtons = `
                    <button type="button" class="btn btn-outline-danger btn-sm delete-btn" 
                            data-action="delete" data-id="${id}" title="刪除">
                        <i class="bi bi-trash"></i>
                    </button>
                `;
        }

        return baseButtons + actionButtons;
    }

    showEmptyTable() {
        const tableBody = document.getElementById('tableBody');
        if (!tableBody) return;

        tableBody.innerHTML = `
            <tr>
                <td colspan="8" class="text-center py-5">
                    <div class="d-flex flex-column align-items-center">
                        <i class="bi bi-inbox" style="font-size: 3rem; opacity: 0.5; margin-bottom: 1rem;"></i>
                        <h5 class="text-muted mb-3">目前沒有通知資料</h5>
                        <p class="text-muted mb-3">您可以新增通知或創建一些測試資料來開始使用</p>
                        <div class="btn-group" role="group">
                            <button type="button" class="btn btn-primary" data-action="add-notification">
                                <i class="bi bi-plus-circle"></i> 新增通知
                            </button>
                            <button type="button" class="btn btn-outline-secondary" data-action="create-test-data">
                                <i class="bi bi-database"></i> 創建測試資料
                            </button>
                        </div>
                    </div>
                </td>
            </tr>
        `;
    }

    bindTableEvents() {
        document.querySelectorAll('.row-checkbox').forEach(checkbox => {
            checkbox.addEventListener('change', () => {
                const id = parseInt(checkbox.dataset.id);
                if (checkbox.checked) {
                    this.state.selectedItems.add(id);
                } else {
                    this.state.selectedItems.delete(id);
                }
                this.updateSelectAllCheckbox();
                this.updateDeleteButton();
            });
        });
    }

    updateSelectAllCheckbox() {
        const selectAllCheckbox = document.getElementById('selectAll');
        if (!selectAllCheckbox) return;

        const currentPageItems = this.state.currentData.map(item => item.id);
        const selectedPageItems = currentPageItems.filter(id =>
            this.state.selectedItems.has(id)
        );

        if (selectedPageItems.length === 0) {
            selectAllCheckbox.checked = false;
            selectAllCheckbox.indeterminate = false;
        } else if (selectedPageItems.length === currentPageItems.length) {
            selectAllCheckbox.checked = true;
            selectAllCheckbox.indeterminate = false;
        } else {
            selectAllCheckbox.checked = false;
            selectAllCheckbox.indeterminate = true;
        }
    }

    updateDeleteButton() {
        const deleteBtn = document.getElementById('deleteSelectedBtn');
        if (!deleteBtn) return;

        const hasSelected = this.state.selectedItems.size > 0;

        if (hasSelected) {
            const draftItems = Array.from(this.state.selectedItems).filter(id => {
                const item = this.state.currentData.find(n => n.id === id);
                return item?.emailStatus === 'draft';
            });

            let buttonHtml = '';
            if (draftItems.length > 0) {
                buttonHtml = `
                    <div class="btn-group" role="group">
                        <button type="button" class="btn btn-success btn-sm" 
                                data-action="publish-selected" title="發布選取的草稿">
                            <i class="bi bi-send"></i> 發布草稿 (${draftItems.length})
                        </button>
                        <button type="button" class="btn btn-danger btn-sm" 
                                data-action="delete-selected" title="刪除選取項目">
                            <i class="bi bi-trash"></i> 刪除 (${this.state.selectedItems.size})
                        </button>
                    </div>
                `;
            } else {
                buttonHtml = `
                    <button type="button" class="btn btn-danger btn-sm" 
                            data-action="delete-selected" title="刪除選取項目">
                        <i class="bi bi-trash"></i> 刪除選取項目 (${this.state.selectedItems.size})
                    </button>
                `;
            }

            deleteBtn.innerHTML = buttonHtml;
            deleteBtn.style.display = 'block';
        } else {
            deleteBtn.style.display = 'none';
        }
    }

    updatePageInfo() {
        const pageInfo = document.getElementById('pageInfo');
        if (!pageInfo) return;

        if (this.state.totalCount > 0) {
            const start = (this.state.currentPage - 1) * this.state.itemsPerPage + 1;
            const end = Math.min(start + this.state.itemsPerPage - 1, this.state.totalCount);
            pageInfo.textContent = `顯示第 ${start}-${end} 筆，共 ${this.state.totalCount} 筆`;
        } else {
            pageInfo.textContent = '目前沒有資料';
        }
    }

    updatePagination() {
        const pagination = document.getElementById('pagination');
        if (!pagination) return;

        if (this.state.totalPages <= 1) {
            pagination.innerHTML = '';
            return;
        }

        let html = '';
        html += `<li class="page-item ${this.state.currentPage === 1 ? 'disabled' : ''}">
                    <a class="page-link" href="#" data-page="${this.state.currentPage - 1}">上一頁</a>
                 </li>`;

        const startPage = Math.max(1, this.state.currentPage - 2);
        const endPage = Math.min(this.state.totalPages, this.state.currentPage + 2);

        for (let i = startPage; i <= endPage; i++) {
            html += `<li class="page-item ${i === this.state.currentPage ? 'active' : ''}">
                        <a class="page-link" href="#" data-page="${i}">${i}</a>
                     </li>`;
        }

        html += `<li class="page-item ${this.state.currentPage === this.state.totalPages ? 'disabled' : ''}">
                    <a class="page-link" href="#" data-page="${this.state.currentPage + 1}">下一頁</a>
                 </li>`;

        pagination.innerHTML = html;
    };

    showSaveLoading(show) {
        const saveBtn = document.getElementById('saveBtn');
        if (saveBtn) {
            if (show) {
                saveBtn.disabled = true;
                saveBtn.innerHTML = '<i class="bi bi-hourglass-split me-1"></i>處理中...';
            } else {
                saveBtn.disabled = false;
                saveBtn.innerHTML = '<i class="bi bi-check-circle me-1"></i>儲存';
            }
        }
    }
}

// 初始化系統
function initNotificationSystem() {
    // 主全域物件（使用更不易衝突的名稱）
    const instance = new NotificationManagementSystem();
    window.TeamNotificationManager = instance;

    // 提供向後相容的唯讀別名（讀取舊名稱時會有警告），但主要的全球變數仍為 TeamNotificationManager
    try {
        Object.defineProperty(window, 'NotificationManager', {
            configurable: true,
            enumerable: false,
            get() {
                console.warn('NotificationManager 已過時，請改用 TeamNotificationManager');
                return window.TeamNotificationManager;
            }
        });
    } catch (e) {
        // ignore if defineProperty fails in some environments
        window.NotificationManager = window.TeamNotificationManager;
    }
}

// DOM 準備就緒時初始化
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initNotificationSystem);
} else {
    setTimeout(initNotificationSystem, 100);
}