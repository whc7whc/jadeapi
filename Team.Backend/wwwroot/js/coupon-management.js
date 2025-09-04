// coupon-management.js
class CouponManager {
    constructor() {
        this.currentPage = 1;
        this.itemsPerPage = 10;
        // 用小寫欄位名，和 <th data-column="startat"> 對齊；後端會把未知欄位 fallback 到 StartAt
        this.sortBy = 'startat';
        this.sortDirection = 'desc';
        this.filters = {
            search: '',
            discountType: '',
            status: '',
            startDate: '',
            endDate: ''
        };
        this.selectedItems = new Set();
        this.init();
    }

    init() {
        this.bindEvents();
        this.loadData();
        this.setupFormValidation();
    }

    bindEvents() {
        // 搜尋相關
        const searchInput = document.getElementById('searchInput');
        const clearBtn = document.getElementById('clearSearch');

        const toggleClear = () => {
            if (!clearBtn) return;
            clearBtn.classList[searchInput.value.trim() ? 'add' : 'remove']('show');
        };
        toggleClear();

        document.getElementById('searchBtn').addEventListener('click', () => {
            this.filters.search = searchInput.value.trim();
            this.currentPage = 1;
            this.loadData();
            toggleClear();
        });

        searchInput.addEventListener('input', toggleClear);

        searchInput.addEventListener('keypress', (e) => {
            if (e.key === 'Enter') {
                this.filters.search = e.target.value.trim();
                this.currentPage = 1;
                this.loadData();
                toggleClear();
            }
        });

        clearBtn.addEventListener('click', () => {
            searchInput.value = '';
            this.filters.search = '';
            this.currentPage = 1;
            this.loadData();
            toggleClear();
        });

        // 篩選相關
        document.getElementById('typeFilter').addEventListener('change', (e) => {
            this.filters.discountType = e.target.value;
            this.currentPage = 1;
            this.loadData();
        });

        document.getElementById('statusFilter').addEventListener('change', (e) => {
            this.filters.status = e.target.value;
            this.currentPage = 1;
            this.loadData();
        });

        document.getElementById('dateFromFilter').addEventListener('change', (e) => {
            this.filters.startDate = e.target.value;
            this.currentPage = 1;
            this.loadData();
        });

        document.getElementById('dateToFilter').addEventListener('change', (e) => {
            this.filters.endDate = e.target.value;
            this.currentPage = 1;
            this.loadData();
        });

        // 表格操作
        document.getElementById('selectAll').addEventListener('change', (e) => {
            this.toggleSelectAll(e.target.checked);
        });

        // 新增按鈕
        document.getElementById('addCouponBtn').addEventListener('click', () => {
            this.showEditModal();
        });

        // 刪除選取項目
        document.getElementById('deleteSelectedBtn').addEventListener('click', () => {
            this.deleteSelected();
        });

        // 每頁顯示筆數
        document.getElementById('itemsPerPageDropdown').parentElement.addEventListener('click', (e) => {
            if (e.target.classList.contains('dropdown-item')) {
                e.preventDefault();
                this.itemsPerPage = parseInt(e.target.getAttribute('data-items'), 10);
                this.currentPage = 1;
                // BS4: mr-2（避免殘留 me-2）
                document.getElementById('itemsPerPageDropdown').innerHTML =
                    `<i class="bi bi-list-ol mr-2"></i>每頁 ${this.itemsPerPage} 筆`;
                this.loadData();
            }
        });

        // 統計按鈕
        document.getElementById('statsBtn').addEventListener('click', () => {
            this.showStatistics();
        });

        // 模態框：儲存
        document.getElementById('saveBtn').addEventListener('click', () => {
            this.saveCoupon();
        });

        // 優惠類型變更時更新提示文字
        document.getElementById('editDiscountType').addEventListener('change', (e) => {
            this.updateDiscountHint(e.target.value);
        });

        // 日期驗證
        document.getElementById('editStartAt').addEventListener('change', () => {
            this.validateDateRange();
        });

        document.getElementById('editExpiredAt').addEventListener('change', () => {
            this.validateDateRange();
        });
    }

    async loadData() {
        try {
            this.showLoading(true);

            // 組裝查詢參數（避免多餘括號）
            const params = new URLSearchParams();
            if (this.filters.search) params.set('search', this.filters.search);
            if (this.filters.discountType) params.set('discountType', this.filters.discountType);
            if (this.filters.status) params.set('status', this.filters.status);
            if (this.filters.startDate) params.set('startDate', this.filters.startDate);
            if (this.filters.endDate) params.set('endDate', this.filters.endDate);
            params.set('page', String(this.currentPage));
            params.set('itemsPerPage', String(this.itemsPerPage));
            params.set('sortBy', this.sortBy);
            params.set('sortDirection', this.sortDirection);

            const response = await fetch(`/Coupons/GetCoupons?${params}`);
            const result = await response.json();

            if (result.success) {
                this.renderTable(result.data);
                this.renderPagination(result);
                this.updatePageInfo(result);
                this.updateFilterBadge();
            } else {
                this.showError(result.message || '載入資料失敗');
            }
        } catch (error) {
            console.error('載入資料失敗:', error);
            this.showError('載入資料失敗，請稍後再試');
        } finally {
            this.showLoading(false);
        }
    }

    renderTable(data) {
        const tbody = document.getElementById('tableBody');
        tbody.innerHTML = '';

        if (!data || data.length === 0) {
            tbody.innerHTML = '<tr><td colspan="10" class="text-center text-muted">沒有找到相關資料</td></tr>';
            return;
        }

        data.forEach(item => {
            const row = document.createElement('tr');
            // 判斷是否為廠商優惠券（根據sellerId）
            const isVendor = item.sellersId && item.sellersId > 0;
            
            row.innerHTML = `
                <td><input type="checkbox" class="select-item" data-id="${item.id}" /></td>
                <td>
                    ${this.escapeHtml(item.title)}
                    ${isVendor ? '<span class="badge badge-warning ml-2">廠商</span>' : '<span class="badge badge-primary ml-2">平台</span>'}
                </td>
                <td>
                    <span class="badge badge-${this.getTypeBadgeClass(item.discountTypeLabel)}">${item.discountTypeLabel}</span>
                </td>
                <td>${item.formattedDiscount}</td>
                <td>${item.minSpend ? '$' + Number(item.minSpend).toLocaleString() : '無限制'}</td>
                <td>${item.formattedUsage ?? '0/無限'}</td>
                <td>${item.formattedStartAt ?? ''}</td>
                <td>${item.validPeriod ?? ''}</td>
                <td>
                    <span class="badge badge-${this.getStatusBadgeClass(item.status)}">${item.statusLabel}</span>
                </td>
                <td>
                    <button class="btn btn-sm btn-secondary mr-1" onclick="couponManager.viewCoupon(${item.id})" title="檢視詳情">
                        <i class="bi bi-eye"></i>
                    </button>
                    <button class="btn btn-sm btn-info mr-1" onclick="couponManager.editCoupon(${item.id})" title="編輯">
                        <i class="bi bi-pencil"></i>
                    </button>
                    <button class="btn btn-sm btn-danger" onclick="couponManager.deleteCoupon(${item.id})" title="刪除">
                        <i class="bi bi-trash"></i>
                    </button>
                </td>
            `;
            tbody.appendChild(row);
        });

        this.bindTableEvents();
    }

    // 新增：檢視優惠券詳情
    async viewCoupon(id) {
        try {
            this.showLoading(true);
            const response = await fetch(`/Coupons/GetCouponDetail/${id}`);
            
            if (response.ok) {
                const result = await response.json();
                if (result.success) {
                    this.showDetailModal(result.data);
                } else {
                    this.showError(result.message || '無法載入優惠券詳情');
                }
            } else {
                this.showError('無法載入優惠券詳情');
            }
        } catch (error) {
            console.error('載入優惠券詳情失敗:', error);
            this.showError('載入優惠券詳情失敗');
        } finally {
            this.showLoading(false);
        }
    }

    // 新增：顯示詳情模態框
    showDetailModal(coupon) {
        // 創建詳情模態框內容
        const modalContent = `
            <div class="modal fade" id="detailModal" tabindex="-1" role="dialog">
                <div class="modal-dialog modal-lg" role="document">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title">
                                <i class="bi bi-eye"></i> 優惠券詳情
                                <span class="badge badge-${coupon.sourceBadgeClass} ml-2">${coupon.couponSource}</span>
                            </h5>
                            <button type="button" class="close" data-dismiss="modal">
                                <span>&times;</span>
                            </button>
                        </div>
                        <div class="modal-body">
                            ${this.renderCouponDetail(coupon)}
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-info" onclick="couponManager.editCouponFromDetail(${coupon.id})">
                                <i class="bi bi-pencil"></i> 編輯優惠券
                            </button>
                            <button type="button" class="btn btn-secondary" data-dismiss="modal">關閉</button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        // 移除現有的詳情模態框
        const existingModal = document.getElementById('detailModal');
        if (existingModal) {
            existingModal.remove();
        }

        // 添加新的模態框
        document.body.insertAdjacentHTML('beforeend', modalContent);

        // 顯示模態框
        if (window.jQuery) {
            window.jQuery('#detailModal').modal('show');
        }
    }

    // 新增：渲染優惠券詳情
    renderCouponDetail(coupon) {
        const isVendor = coupon.isVendorCoupon;
        
        return `
            <div class="row">
                <!-- 基本資訊 -->
                <div class="col-md-6">
                    <div class="card mb-3">
                        <div class="card-header">
                            <h6 class="mb-0"><i class="bi bi-info-circle"></i> 基本資訊</h6>
                        </div>
                        <div class="card-body">
                            <table class="table table-sm table-borderless">
                                <tr><td class="font-weight-bold">優惠券名稱</td><td>${this.escapeHtml(coupon.title)}</td></tr>
                                <tr><td class="font-weight-bold">優惠類型</td><td><span class="badge badge-${this.getTypeBadgeClass(coupon.discountTypeLabel)}">${coupon.discountTypeLabel}</span></td></tr>
                                <tr><td class="font-weight-bold">折扣內容</td><td>${coupon.formattedDiscount}</td></tr>
                                <tr><td class="font-weight-bold">最低消費</td><td>${coupon.minSpend ? '$' + Number(coupon.minSpend).toLocaleString() : '無限制'}</td></tr>
                                <tr><td class="font-weight-bold">有效期間</td><td>${coupon.validPeriod}</td></tr>
                                <tr><td class="font-weight-bold">狀態</td><td><span class="badge badge-${this.getStatusBadgeClass(coupon.status)}">${coupon.statusLabel}</span></td></tr>
                            </table>
                        </div>
                    </div>
                </div>

                <!-- 使用統計 -->
                <div class="col-md-6">
                    <div class="card mb-3">
                        <div class="card-header">
                            <h6 class="mb-0"><i class="bi bi-graph-up"></i> 使用統計</h6>
                        </div>
                        <div class="card-body">
                            <table class="table table-sm table-borderless">
                                <tr><td class="font-weight-bold">使用次數</td><td>${coupon.formattedUsage}</td></tr>
                                <tr><td class="font-weight-bold">剩餘使用</td><td>${coupon.remainingUsage}</td></tr>
                                <tr><td class="font-weight-bold">總節省金額</td><td class="text-success font-weight-bold">${coupon.formattedTotalSavings}</td></tr>
                                <tr><td class="font-weight-bold">最後使用</td><td>${coupon.formattedLastUsed}</td></tr>
                            </table>
                        </div>
                    </div>
                </div>

                ${isVendor ? this.renderVendorInfo(coupon) : ''}
                ${coupon.recentUsages && coupon.recentUsages.length > 0 ? this.renderRecentUsages(coupon.recentUsages) : ''}
            </div>
        `;
    }

    // 新增：渲染廠商資訊
    renderVendorInfo(coupon) {
        return `
            <!-- 廠商資訊 -->
            <div class="col-12">
                <div class="card mb-3 border-warning">
                    <div class="card-header bg-warning text-dark">
                        <h6 class="mb-0"><i class="bi bi-shop"></i> 廠商資訊</h6>
                    </div>
                    <div class="card-body">
                        <div class="row">
                            <div class="col-md-6">
                                <table class="table table-sm table-borderless">
                                    <tr><td class="font-weight-bold">廠商名稱</td><td>${this.escapeHtml(coupon.sellerRealName || '未知廠商')}</td></tr>
                                    <tr><td class="font-weight-bold">Email</td><td>${this.escapeHtml(coupon.sellerEmail || '')}</td></tr>
                                    <tr><td class="font-weight-bold">狀態</td><td>${this.escapeHtml(coupon.sellerStatus || '')}</td></tr>
                                </table>
                            </div>
                            <div class="col-md-6">
                                <table class="table table-sm table-borderless">
                                    <tr><td class="font-weight-bold">統一編號</td><td>${this.escapeHtml(coupon.sellerIdNumber || '未提供')}</td></tr>
                                    <tr><td class="font-weight-bold">聯絡電話</td><td>${this.escapeHtml(coupon.sellerPhone || '未提供')}</td></tr>
                                    <tr><td class="font-weight-bold">加入日期</td><td>${coupon.formattedSellerJoinDate || '未知'}</td></tr>
                                </table>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `;
    }

    // 新增：渲染最近使用記錄
    renderRecentUsages(usages) {
        return `
            <!-- 最近使用記錄 -->
            <div class="col-12">
                <div class="card">
                    <div class="card-header">
                        <h6 class="mb-0"><i class="bi bi-clock-history"></i> 最近使用記錄</h6>
                    </div>
                    <div class="card-body">
                        <div class="table-responsive">
                            <table class="table table-sm">
                                <thead>
                                    <tr>
                                        <th>會員Email</th>
                                        <th>使用時間</th>
                                        <th>訂單金額</th>
                                        <th>折扣金額</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    ${usages.map(usage => `
                                        <tr>
                                            <td>${this.escapeHtml(usage.memberEmail)}</td>
                                            <td>${usage.formattedUsedAt}</td>
                                            <td>${usage.formattedOrderAmount}</td>
                                            <td class="text-success">${usage.formattedDiscountAmount}</td>
                                        </tr>
                                    `).join('')}
                                </tbody>
                            </table>
                        </div>
                    </div>
                </div>
            </div>
        `;
    }

    bindTableEvents() {
        // 排序按鈕
        document.querySelectorAll('.sortable').forEach(th => {
            th.addEventListener('click', (e) => {
                const column = th.getAttribute('data-column'); // e.g. "usage"|"startat"|...
                // usage 欄位：一般點 → 'usagelimit'；按住 Shift → 'usedcount'
                const col = (column === 'usage')
                    ? (e.shiftKey ? 'usedcount' : 'usagelimit')
                    : column;

                if (this.sortBy === col) {
                    this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
                } else {
                    this.sortBy = col;
                    this.sortDirection = 'desc';
                }
                this.loadData();
            });
        });

        // 單項選擇
        document.querySelectorAll('.select-item').forEach(checkbox => {
            checkbox.addEventListener('change', () => {
                this.updateSelectedItems();
            });
        });
    }

    renderPagination(result) {
        const pagination = document.getElementById('pagination');
        pagination.innerHTML = '';

        if (result.totalPages <= 1) return;

        // 上一頁
        if (result.hasPreviousPage) {
            const prevLi = document.createElement('li');
            prevLi.className = 'page-item';
            prevLi.innerHTML = `<a class="page-link" href="#" data-page="${result.currentPage - 1}" aria-label="上一頁"><i class="bi bi-chevron-left"></i></a>`;
            pagination.appendChild(prevLi);
        }

        // 頁碼
        const startPage = Math.max(1, result.currentPage - 2);
        const endPage = Math.min(result.totalPages, result.currentPage + 2);

        if (startPage > 1) {
            const firstLi = document.createElement('li');
            firstLi.className = 'page-item';
            firstLi.innerHTML = '<a class="page-link" href="#" data-page="1">1</a>';
            pagination.appendChild(firstLi);

            if (startPage > 2) {
                const ellipsisLi = document.createElement('li');
                ellipsisLi.className = 'page-item disabled';
                ellipsisLi.innerHTML = '<span class="page-link">...</span>';
                pagination.appendChild(ellipsisLi);
            }
        }

        for (let i = startPage; i <= endPage; i++) {
            const li = document.createElement('li');
            li.className = i === result.currentPage ? 'page-item active' : 'page-item';
            li.innerHTML = `<a class="page-link" href="#" data-page="${i}">${i}</a>`;
            pagination.appendChild(li);
        }

        if (endPage < result.totalPages) {
            if (endPage < result.totalPages - 1) {
                const ellipsisLi = document.createElement('li');
                ellipsisLi.className = 'page-item disabled';
                ellipsisLi.innerHTML = '<span class="page-link">...</span>';
                pagination.appendChild(ellipsisLi);
            }

            const lastLi = document.createElement('li');
            lastLi.className = 'page-item';
            lastLi.innerHTML = `<a class="page-link" href="#" data-page="${result.totalPages}">${result.totalPages}</a>`;
            pagination.appendChild(lastLi);
        }

        // 下一頁
        if (result.hasNextPage) {
            const nextLi = document.createElement('li');
            nextLi.className = 'page-item';
            nextLi.innerHTML = `<a class="page-link" href="#" data-page="${result.currentPage + 1}" aria-label="下一頁"><i class="bi bi-chevron-right"></i></a>`;
            pagination.appendChild(nextLi);
        }

        // 綁定分頁事件
        // 讓點擊 <i> 也能觸發：用事件代理找最近的 .page-link
        pagination.addEventListener('click', (e) => {
            const link = e.target.closest('.page-link');
            if (link && link.hasAttribute('data-page')) {
                e.preventDefault();
                this.currentPage = parseInt(link.getAttribute('data-page'), 10);
                this.loadData();
            }
        });
    }

    updatePageInfo(result) {
        const start = (result.currentPage - 1) * result.itemsPerPage + 1;
        const end = Math.min(result.currentPage * result.itemsPerPage, result.totalCount);
        document.getElementById('pageInfo').textContent =
            `顯示第 ${start} 到 ${end} 項，共 ${result.totalCount} 項`;
    }

    updateFilterBadge() {
        let count = 0;
        if (this.filters.search) count++;
        if (this.filters.discountType) count++;
        if (this.filters.status) count++;
        if (this.filters.startDate) count++;
        if (this.filters.endDate) count++;

        const badge = document.getElementById('filterBadge');
        badge.textContent = count;
        badge.style.display = count > 0 ? 'inline' : 'none';
    }

    toggleSelectAll(checked) {
        document.querySelectorAll('.select-item').forEach(checkbox => {
            checkbox.checked = checked;
        });
        this.updateSelectedItems();
    }

    updateSelectedItems() {
        this.selectedItems.clear();
        document.querySelectorAll('.select-item:checked').forEach(checkbox => {
            this.selectedItems.add(parseInt(checkbox.getAttribute('data-id'), 10));
        });

        const deleteBtn = document.getElementById('deleteSelectedBtn');
        deleteBtn.style.display = this.selectedItems.size > 0 ? 'inline-block' : 'none';

        const selectAllCheckbox = document.getElementById('selectAll');
        const checkboxes = document.querySelectorAll('.select-item');
        selectAllCheckbox.checked = checkboxes.length > 0 && this.selectedItems.size === checkboxes.length;
        selectAllCheckbox.indeterminate = this.selectedItems.size > 0 && this.selectedItems.size < checkboxes.length;
    }

    showEditModal(coupon = null) {
        const form = document.getElementById('couponForm');
        const title = document.getElementById('modalTitle');

        form.reset();

        if (coupon) {
            title.textContent = '編輯優惠券';
            document.getElementById('editId').value = coupon.id;
            document.getElementById('editTitle').value = coupon.title ?? '';
            document.getElementById('editDiscountType').value = (coupon.discountTypeLabel ?? coupon.discountType ?? '');
            document.getElementById('editDiscountAmount').value = coupon.discountAmount ?? '';
            document.getElementById('editMinSpend').value = (coupon.minSpend ?? '').toString();
            document.getElementById('editUsageLimit').value = (coupon.usageLimit ?? '').toString();
            document.getElementById('editUsedCount').value = coupon.usedCount ?? 0;
            // 從 API 回來的屬性是 camelCase
            document.getElementById('editStartAt').value = this.formatDateTimeLocal(coupon.startAt);
            document.getElementById('editExpiredAt').value = this.formatDateTimeLocal(coupon.expiredAt);
            document.getElementById('editApplicableLevelId').value = coupon.applicableLevelId ?? '';
        } else {
            title.textContent = '新增優惠券';
            document.getElementById('editUsedCount').value = 0; // 新增時已用次數為0
            // 預設值：現在 ~ 明天
            const now = new Date();
            const tomorrow = new Date(now.getTime() + 24 * 60 * 60 * 1000);
            document.getElementById('editStartAt').value = this.formatDateTimeLocal(now);
            document.getElementById('editExpiredAt').value = this.formatDateTimeLocal(tomorrow);
        }

        // 預設類型提示
        this.updateDiscountHint(document.getElementById('editDiscountType').value);

        // Bootstrap 4：用 jQuery 控 modal
        if (window.jQuery) {
            window.jQuery('#editModal').modal('show');
        }
    }

    async saveCoupon() {
        const form = document.getElementById('couponForm');
        const formData = new FormData(form);
        const id = document.getElementById('editId').value;

        // 驗證表單
        if (!this.validateForm()) {
            return;
        }

        const couponData = {
            title: formData.get('title'),
            discountType: formData.get('discountType'),
            discountAmount: formData.get('discountAmount') ? parseInt(formData.get('discountAmount'), 10) : null,
            minSpend: formData.get('minSpend') ? parseInt(formData.get('minSpend'), 10) : null,
            usageLimit: formData.get('usageLimit') ? parseInt(formData.get('usageLimit'), 10) : null,
            startAt: formData.get('startAt'),
            expiredAt: formData.get('expiredAt'),
            applicableLevelId: formData.get('applicableLevelId') ? parseInt(formData.get('applicableLevelId'), 10) : null
        };

        try {
            let response;
            if (id) {
                response = await fetch(`/Coupons/UpdateCoupon/${id}`, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(couponData)
                });
            } else {
                response = await fetch('/Coupons/CreateCoupon', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(couponData)
                });
            }

            const result = await response.json();

            if (result.success) {
                // 檢查是否有警告訊息（廠商優惠券編輯警示）
                if (result.hasWarning && result.warningMessage) {
                    this.showVendorWarning(result.warningMessage, result.warningData);
                } else {
                    this.showSuccess(result.message || '已儲存');
                }
                
                // Bootstrap 4：直接關閉 modal
                if (window.jQuery) {
                    window.jQuery('#editModal').modal('hide');
                }
                this.loadData();
            } else {
                this.showError(result.message || '儲存失敗');
                if (result.errors) {
                    console.error('驗證錯誤:', result.errors);
                }
            }
        } catch (error) {
            console.error('儲存失敗:', error);
            this.showError('儲存失敗，請稍後再試');
        }
    }

    // 新增：顯示廠商警告
    showVendorWarning(warningMessage, warningData = {}) {
        const sellerName = warningData.sellerName || '未知廠商';
        const sellerEmail = warningData.sellerEmail || '';
        
        const confirmed = confirm(
            `✅ 優惠券更新成功！\n\n` +
            `⚠️ 重要提醒：您剛剛編輯了廠商優惠券\n\n` +
            `廠商：${sellerName}\n` +
            `Email：${sellerEmail}\n\n` +
            `建議您主動聯繫廠商告知此次修改。\n\n` +
            `點擊「確定」將開啟Email程式聯絡廠商\n` +
            `點擊「取消」直接關閉此提醒`
        );

        if (confirmed && sellerEmail) {
            window.location.href = `mailto:${sellerEmail}?subject=優惠券修改通知&body=您好，您的優惠券已被系統管理員修改，請登入查看詳情。`;
        }
    }

    // 從詳情視窗編輯優惠券（先關閉詳情視窗）
    editCouponFromDetail(id) {
        // 關閉詳情視窗
        if (window.jQuery) {
            window.jQuery('#detailModal').modal('hide');
        }
        
        // 延遲一點時間確保詳情視窗關閉完成，然後開啟編輯視窗
        setTimeout(() => {
            this.editCoupon(id);
        }, 300);
    }

    async editCoupon(id) {
        try {
            const response = await fetch(`/Coupons/GetCoupon/${id}`);
            if (response.ok) {
                const result = await response.json();
                if (result.success) {
                    this.showEditModal(result.data);
                } else {
                    this.showError('無法載入優惠券資料');
                }
            } else {
                this.showError('無法載入優惠券資料');
            }
        } catch (error) {
            console.error('載入優惠券資料失敗:', error);
            this.showError('載入優惠券資料失敗');
        }
    }

    async deleteCoupon(id) {
        if (!confirm('確定要刪除此優惠券嗎？此操作無法復原。')) return;

        try {
            const response = await fetch(`/Coupons/DeleteCoupon/${id}`, { method: 'DELETE' });
            const result = await response.json();

            if (result.success) {
                this.showSuccess(result.message || '已刪除');
                this.loadData();
            } else {
                this.showError(result.message || '刪除失敗');
            }
        } catch (error) {
            console.error('刪除失敗:', error);
            this.showError('刪除失敗，請稍後再試');
        }
    }

    async deleteSelected() {
        if (this.selectedItems.size === 0) {
            this.showError('請先選擇要刪除的項目');
            return;
        }

        if (!confirm(`確定要刪除選取的 ${this.selectedItems.size} 項優惠券嗎？此操作無法復原。`)) {
            return;
        }

        try {
            const response = await fetch('/Coupons/DeleteCoupons', {
                method: 'DELETE',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ ids: Array.from(this.selectedItems) })
            });

            const result = await response.json();

            if (result.success) {
                this.showSuccess(result.message || '已刪除選取項目');
                this.selectedItems.clear();
                this.loadData();
            } else {
                this.showError(result.message || '批量刪除失敗');
            }
        } catch (error) {
            console.error('批量刪除失敗:', error);
            this.showError('批量刪除失敗，請稍後再試');
        }
    }

    async showStatistics() {
        try {
            // 先顯示模態框和載入狀態
            if (window.jQuery) {
                window.jQuery('#statsModal').modal('show');
            }

            // 重置統計內容顯示狀態
            this.resetStatisticsView();

            // 強制略過瀏覽器快取 + 伺服器 noCache
            const response = await fetch(`/Coupons/GetStatistics?noCache=1&ts=${Date.now()}`, { cache: 'no-store' });
            
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }
            
            const result = await response.json();
            console.log('統計 API 回應:', result);

            if (result.success) {
                this.renderStatistics(result.data);
            } else {
                this.showStatisticsError(result.message || '讀取統計資料失敗');
            }
        } catch (error) {
            console.error('載入統計資料失敗:', error);
            this.showStatisticsError('載入統計資料失敗: ' + error.message);
        }
    }

    resetStatisticsView() {
        // 顯示載入狀態
        const loadingElement = document.getElementById('statsLoading');
        if (loadingElement) {
            loadingElement.classList.remove('d-none');
        }

        // 隱藏所有統計卡片和錯誤訊息
        const hideElements = [
            'statsCard1', 'statsCard2', 'statsCard3', 'statsCard4',
            'statsChart-container', 'sourceStatsCard', 'statsError'
        ];
        
        hideElements.forEach(id => {
            const element = document.getElementById(id);
            if (element) {
                element.classList.add('d-none');
            }
        });
    }

    showStatisticsError(message) {
        // 隱藏載入狀態
        const loadingElement = document.getElementById('statsLoading');
        if (loadingElement) {
            loadingElement.classList.add('d-none');
        }

        // 顯示錯誤訊息
        const errorElement = document.getElementById('statsError');
        const errorMessageElement = document.getElementById('statsErrorMessage');
        
        if (errorElement && errorMessageElement) {
            errorMessageElement.textContent = message;
            errorElement.classList.remove('d-none');
        }
    }

    renderStatistics(stats) {
        console.log('渲染統計資料:', stats);
        
        // 隱藏載入提示
        const loadingElement = document.getElementById('statsLoading');
        if (loadingElement) {
            loadingElement.classList.add('d-none');
        }

        // 更新數字統計
        const elements = {
            totalCount: stats.totalCount ?? 0,
            activeCount: stats.activeCount ?? 0,
            expiredCount: stats.expiredCount ?? 0,
            notStartedCount: (stats.statusStats && stats.statusStats['未開始']) ?? 0,
            platformCount: stats.platformCouponCount ?? 0,
            vendorCount: stats.vendorCouponCount ?? 0
        };

        // 更新各個統計卡片
        Object.keys(elements).forEach(key => {
            const element = document.getElementById(key);
            if (element) {
                element.textContent = elements[key];
                // 顯示對應的卡片
                const card = element.closest('[id^="statsCard"]') || element.closest('#sourceStatsCard');
                if (card) {
                    card.classList.remove('d-none');
                }
            }
        });

        // 渲染圖表
        this.renderChart(stats);
        
        // 顯示來源統計卡片
        const sourceStatsCard = document.getElementById('sourceStatsCard');
        if (sourceStatsCard) {
            sourceStatsCard.classList.remove('d-none');
        }
    }

    renderChart(stats) {
        const canvas = document.getElementById('statsChart');
        if (!canvas) return;
        
        const ctx = canvas.getContext('2d');
        
        if (window.couponChart) {
            window.couponChart.destroy();
        }

        // 確保只顯示三種標準化類型
        const standardTypes = ['%數折扣', 'J幣回饋', '滿減'];
        const typeLabels = [];
        const typeData = [];
        const typeColors = [];

        // 定義標準化類型的顏色
        const colorMap = {
            '%數折扣': '#28a745',  // 綠色
            'J幣回饋': '#007bff',   // 藍色  
            '滿減': '#ffc107'       // 黃色
        };

        // 只處理三種標準化類型
        standardTypes.forEach(type => {
            if (stats.typeStats && stats.typeStats[type] && stats.typeStats[type] > 0) {
                typeLabels.push(type);
                typeData.push(stats.typeStats[type]);
                typeColors.push(colorMap[type]);
            }
        });

        // 如果有資料才顯示圖表
        if (typeData.length > 0) {
            const chartContainer = document.getElementById('statsChart-container');
            if (chartContainer) {
                chartContainer.classList.remove('d-none');
            }

            // Chart.js 3.x 設定
            window.couponChart = new Chart(ctx, {
                type: 'doughnut',
                data: {
                    labels: typeLabels,
                    datasets: [{
                        data: typeData,
                        backgroundColor: typeColors
                    }]
                },
                options: {
                    responsive: true,
                    plugins: {
                        title: { display: true, text: '優惠券類型分布' },
                        legend: { position: 'bottom' }
                    }
                }
            });
        }
    }

    setupFormValidation() {
        const form = document.getElementById('couponForm');
        form.addEventListener('submit', (e) => {
            e.preventDefault();
            this.saveCoupon();
        });
    }

    validateForm() {
        let isValid = true;
        const errors = [];

        const title = document.getElementById('editTitle').value.trim();
        if (!title) {
            errors.push('優惠券名稱為必填');
            isValid = false;
        }

        const discountType = document.getElementById('editDiscountType').value;
        if (!discountType) {
            errors.push('優惠類型為必填');
            isValid = false;
        }

        const discountAmount = parseInt(document.getElementById('editDiscountAmount').value, 10);
        if (!discountAmount || discountAmount <= 0) {
            errors.push('折扣金額必須大於 0');
            isValid = false;
        }
        
        // 根據新的三種類型驗證
        if (discountType === '%數折扣' && (discountAmount < 1 || discountAmount > 100)) {
            errors.push('%數折扣必須介於 1~100');
            isValid = false;
        }

        // 滿減類型必須設定最低消費
        const minSpend = parseInt(document.getElementById('editMinSpend').value, 10);
        if (discountType === '滿減' && (!minSpend || minSpend <= 0)) {
            errors.push('滿減優惠必須設定最低消費金額');
            isValid = false;
        }

        // 驗證使用上限
        const usageLimit = parseInt(document.getElementById('editUsageLimit').value, 10);
        if (usageLimit && usageLimit <= 0) {
            errors.push('使用上限必須大於 0');
            isValid = false;
        }

        // 驗證日期範圍
        if (!this.validateDateRange()) {
            isValid = false;
        }

        if (!isValid) {
            this.showError(errors.join('、'));
        }

        return isValid;
    }

    validateDateRange() {
        const startDate = new Date(document.getElementById('editStartAt').value);
        const endDate = new Date(document.getElementById('editExpiredAt').value);
        const errorElement = document.getElementById('dateRangeError');

        if (!isFinite(startDate.getTime()) || !isFinite(endDate.getTime())) {
            if (errorElement) errorElement.style.display = 'block';
            return false;
        }

        if (endDate <= startDate) {
            if (errorElement) errorElement.style.display = 'block';
            return false;
        } else {
            if (errorElement) errorElement.style.display = 'none';
            return true;
        }
    }

    updateDiscountHint(discountType) {
        const hintElement = document.getElementById('discountAmountHint');
        const amountInput = document.getElementById('editDiscountAmount');

        if (!hintElement || !amountInput) return;

        // 預設為整數
        amountInput.setAttribute('step', '1');
        amountInput.removeAttribute('max');
        amountInput.setAttribute('min', '1');

        switch (discountType) {
            case 'J幣回饋':
                hintElement.textContent = '請輸入回饋 J幣數量（例：100）';
                amountInput.placeholder = '100';
                break;
            case '%數折扣':
                hintElement.textContent = '請輸入折扣百分比（1~100）';
                amountInput.placeholder = '10';
                amountInput.setAttribute('max', '100');
                break;
            case '滿減':
                hintElement.textContent = '請輸入減免金額（例：50）';
                amountInput.placeholder = '50';
                break;
            default:
                hintElement.textContent = '請輸入折扣金額或比例';
                amountInput.placeholder = '';
        }
    }

    // 工具方法
    formatDateTimeLocal(dateInput) {
        const date = dateInput ? new Date(dateInput) : new Date();
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        const hours = String(date.getHours()).padStart(2, '0');
        const minutes = String(date.getMinutes()).padStart(2, '0');
        return `${year}-${month}-${day}T${hours}:${minutes}`;
    }

    escapeHtml(unsafe) {
        const s = (unsafe ?? '').toString();
        return s
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    getTypeBadgeClass(type) {
        const classes = {
            '%數折扣': 'success',
            'J幣回饋': 'primary',
            '滿減': 'warning',
            // 兼容舊資料
            '點數返還': 'primary',
            '折扣碼': 'success',
            '免運費': 'warning'
        };
        return classes[type] || 'secondary';
    }

    getStatusBadgeClass(status) {
        const classes = {
            '啟用': 'success',
            '已過期': 'danger',
            '未開始': 'warning'
        };
        return classes[status] || 'secondary';
    }

    showLoading(show) {
        const loadingIndicator = document.getElementById('loadingIndicator');
        if (loadingIndicator) {
            loadingIndicator.style.display = show ? 'block' : 'none';
        }
    }

    showSuccess(message) {
        // 簡化：實務可改 Toast
        alert(message);
    }

    showError(message) {
        alert('錯誤：' + message);
    }
}

// 初始化
document.addEventListener('DOMContentLoaded', function () {
    window.couponManager = new CouponManager();
});
