// 會員等級管理 JavaScript
class MembershipLevelsManager {
    constructor() {
        this.currentEditId = null;
        this.currentDeleteId = null;
        this.init();
    }

    init() {
        this.bindEvents();
        this.loadLevels();
    }

    bindEvents() {
        // 儲存等級按鈕
        document.getElementById('saveLevelBtn').addEventListener('click', () => {
            this.saveLevel();
        });

        // 刪除確認按鈕
        document.getElementById('confirmDeleteBtn').addEventListener('click', () => {
            this.confirmDelete();
        });

        // Modal 隱藏時重置表單 (Bootstrap 4 事件)
        $('#levelModal').on('hidden.bs.modal', () => {
            this.resetLevelForm();
        });

        // 額外：為新增等級按鈕添加direct click事件作為備用方案
        const addLevelBtn = document.querySelector('[data-target="#levelModal"]');
        if (addLevelBtn) {
            addLevelBtn.addEventListener('click', () => {
                setTimeout(() => {
                    // 可在此處添加需要的邏輯
                }, 100);
            });
        }
    }

    // 載入等級列表
    async loadLevels() {
        console.log('🔄 開始載入會員等級列表...');
        try {
            const url = '/MembershipLevels/List?ts=' + Date.now();
            console.log('📡 請求 URL:', url);
            
            const response = await fetch(url);
            console.log('📊 HTTP 狀態:', response.status, response.statusText);
            
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }
            
            const result = await response.json();
            console.log('📋 API 回應:', result);

            if (result.success) {
                console.log('✅ 成功取得資料，項目數量:', result.data?.length || 0);
                this.renderLevelsTable(result.data);
            } else {
                console.error('❌ API 回應失敗:', result.message);
                this.showToast(result.message || '載入等級列表失敗', 'error');
            }
        } catch (error) {
            console.error('💥 載入等級列表失敗:', error);
            console.error('錯誤詳細資訊:', {
                name: error.name,
                message: error.message,
                stack: error.stack
            });
            this.showToast('載入等級列表失敗：' + error.message, 'error');
        }
    }

    // 渲染等級表格
    renderLevelsTable(levels) {
        const tbody = document.getElementById('levelsTableBody');
        tbody.innerHTML = '';

        if (levels.length === 0) {
            tbody.innerHTML = '<tr><td colspan="5" class="text-center">暫無資料</td></tr>';
            return;
        }

        levels.forEach(level => {
            // 同時支援 camelCase / PascalCase
            const id = level.id || level.Id;
            const levelName = level.levelName || level.LevelName;
            const requiredAmount = Number(level.requiredAmount || level.RequiredAmount || 0);
            const isActive = (level.isActive !== undefined ? level.isActive : level.IsActive) ? true : false;
            const createdAt = level.createdAt || level.CreatedAt;

            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${this.escapeHtml(levelName)}</td>
                <td>NT$ ${requiredAmount.toLocaleString()}</td>
                <td>
                    <span class="badge badge-${isActive ? 'success' : 'secondary'}">
                        ${isActive ? '啟用' : '停用'}
                    </span>
                </td>
                <td>${new Date(createdAt).toLocaleDateString('zh-TW')}</td>
                <td>
                    <button class="btn btn-outline-primary btn-sm mr-1" onclick="membershipLevelsManager.editLevel(${id})">
                        <i class="bi bi-pencil"></i> 編輯
                    </button>
                    <button class="btn btn-outline-danger btn-sm" onclick="membershipLevelsManager.deleteLevel(${id})">
                        <i class="bi bi-trash"></i> 刪除
                    </button>
                </td>
            `;
            tbody.appendChild(row);
        });
    }

    // 編輯等級
    async editLevel(id) {
        try {
            const response = await fetch(`/MembershipLevels/List`);
            const result = await response.json();

            if (result.success) {
                const level = result.data.find(l => (l.id || l.Id) === id);
                if (level) {
                    this.currentEditId = id;
                    this.populateLevelForm(level);
                    document.getElementById('levelModalLabel').textContent = '編輯等級';
                    $('#levelModal').modal('show');
                }
            }
        } catch (error) {
            console.error('載入等級資料失敗:', error);
            this.showToast('載入等級資料失敗', 'error');
        }
    }

    // 填充等級表單
    populateLevelForm(level) {
        document.getElementById('levelName').value = (level.levelName || level.LevelName) || '';
        document.getElementById('requiredAmount').value = Number(level.requiredAmount || level.RequiredAmount || 0);
        document.getElementById('isActive').checked = (level.isActive !== undefined ? level.isActive : level.IsActive) ? true : false;
    }

    // 刪除等級
    deleteLevel(id) {
        this.currentDeleteId = id;
        $('#deleteModal').modal('show');
    }

    // 儲存等級
    async saveLevel() {
        const form = document.getElementById('levelForm');
        const formData = new FormData(form);
        
        // 驗證表單
        if (!this.validateLevelForm()) {
            return;
        }

        const data = {
            LevelName: formData.get('LevelName'),
            RequiredAmount: parseInt(formData.get('RequiredAmount')),
            IsActive: formData.get('IsActive') === 'on'
        };

        try {
            const url = this.currentEditId 
                ? `/MembershipLevels/Update/${this.currentEditId}`
                : '/MembershipLevels/Create';
            const method = this.currentEditId ? 'PUT' : 'POST';

            const response = await fetch(url, {
                method: method,
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify(data)
            });

            const result = await response.json();

            if (result.success) {
                this.showToast(result.message, 'success');
                $('#levelModal').modal('hide');
                this.loadLevels();
            } else {
                this.showValidationErrors(result.errors);
                this.showToast(result.message, 'error');
            }
        } catch (error) {
            console.error('儲存等級失敗:', error);
            this.showToast('儲存等級失敗', 'error');
        }
    }

    // 確認刪除
    async confirmDelete() {
        if (!this.currentDeleteId) return;

        try {
            const response = await fetch(`/MembershipLevels/Delete/${this.currentDeleteId}`, {
                method: 'DELETE'
            });

            const result = await response.json();

            if (result.success) {
                this.showToast(result.message, 'success');
                $('#deleteModal').modal('hide');
                this.loadLevels();
            } else {
                this.showToast(result.message, 'error');
            }
        } catch (error) {
            console.error('刪除等級失敗:', error);
            this.showToast('刪除等級失敗', 'error');
        }

        this.currentDeleteId = null;
    }

    // 驗證等級表單
    validateLevelForm() {
        let isValid = true;
        this.clearValidationErrors();

        const levelName = document.getElementById('levelName').value.trim();
        const requiredAmount = document.getElementById('requiredAmount').value;

        if (!levelName) {
            this.showFieldError('levelName', '等級名稱不能為空');
            isValid = false;
        }

        if (!requiredAmount || parseInt(requiredAmount) < 0) {
            this.showFieldError('requiredAmount', '所需金額必須大於等於 0');
            isValid = false;
        }

        return isValid;
    }

    // 顯示欄位錯誤
    showFieldError(fieldId, message) {
        const field = document.getElementById(fieldId);
        const feedback = field.nextElementSibling;
        
        field.classList.add('is-invalid');
        if (feedback && feedback.classList.contains('invalid-feedback')) {
            feedback.textContent = message;
        }
    }

    // 清除驗證錯誤
    clearValidationErrors() {
        document.querySelectorAll('.is-invalid').forEach(field => {
            field.classList.remove('is-invalid');
        });
        document.querySelectorAll('.invalid-feedback').forEach(feedback => {
            feedback.textContent = '';
        });
    }

    // 顯示服務端驗證錯誤
    showValidationErrors(errors) {
        if (!errors) return;

        Object.keys(errors).forEach(fieldName => {
            const message = errors[fieldName];
            // 嘗試找到對應的欄位
            const field = document.querySelector(`[name="${fieldName}"]`);
            if (field) {
                this.showFieldError(field.id, message);
            }
        });
    }

    // 重置等級表單
    resetLevelForm() {
        document.getElementById('levelForm').reset();
        document.getElementById('isActive').checked = true;
        this.clearValidationErrors();
        this.currentEditId = null;
        document.getElementById('levelModalLabel').textContent = '新增等級';
    }

    // 顯示 Toast 訊息
    showToast(message, type = 'info') {
        // 簡單的 toast 實現（使用 Bootstrap 4 alert）
        const toastContainer = this.getOrCreateToastContainer();
        const toast = document.createElement('div');
        toast.className = `alert alert-${type === 'error' ? 'danger' : type === 'success' ? 'success' : 'info'} alert-dismissible fade show`;
        toast.style.position = 'relative';
        toast.style.marginBottom = '10px';
        
        toast.innerHTML = `
            ${message}
            <button type="button" class="close" data-dismiss="alert" aria-label="Close">
                <span aria-hidden="true">&times;</span>
            </button>
        `;

        toastContainer.appendChild(toast);

        // 自動移除
        setTimeout(() => {
            if (toast.parentNode) {
                toast.parentNode.removeChild(toast);
            }
        }, 5000);
    }

    // 獲取或創建 Toast 容器
    getOrCreateToastContainer() {
        let container = document.getElementById('toastContainer');
        if (!container) {
            container = document.createElement('div');
            container.id = 'toastContainer';
            container.style.position = 'fixed';
            container.style.top = '20px';
            container.style.right = '20px';
            container.style.zIndex = '9999';
            container.style.maxWidth = '350px';
            document.body.appendChild(container);
        }
        return container;
    }

    // HTML 轉義
    escapeHtml(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
}

// 初始化管理器
let membershipLevelsManager;
document.addEventListener('DOMContentLoaded', function() {
    membershipLevelsManager = new MembershipLevelsManager();
});
