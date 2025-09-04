// admin-logistics.js - 物流管理相關JavaScript功能

$(document).ready(function() {
    // 初始化
    initializeEvents();
    
    // 如果有錯誤訊息，3秒後自動隱藏
    setTimeout(function() {
        $('.alert').fadeOut();
    }, 3000);
});

// 初始化事件監聽
function initializeEvents() {
    // 清除/重置按鈕
    $('#clearBtn').click(function() {
        $('#carrierSelect').val('');
        performSearch();
    });
    
    // 物流商下拉選單變更
    $('#carrierSelect').change(function() {
        performSearch();
    });
    
    // Modal 關閉時清空內容
    $('#carrierDetailModal').on('hidden.bs.modal', function() {
        $('#carrierDetailContent').empty();
    });
}

// 執行搜尋
function performSearch() {
    var carrierId = $('#carrierSelect').val();
    var url = $('#logisticsConfig').data('list-url');
    
    showLoading();
    
    $.ajax({
        url: url,
        type: 'GET',
        data: { 
            carrierId: carrierId,
            page: 1 
        },
        success: function(data) {
            $('#carrierListContainer').html(data);
            hideLoading();
        },
        error: function() {
            hideLoading();
            showNotification('載入失敗，請稍後再試', 'error');
        }
    });
}

// 載入指定頁面
function loadPage(page) {
    var carrierId = $('#carrierSelect').val();
    var url = $('#logisticsConfig').data('list-url');
    
    showLoading();
    
    $.ajax({
        url: url,
        type: 'GET',
        data: { 
            carrierId: carrierId,
            page: page 
        },
        success: function(data) {
            $('#carrierListContainer').html(data);
            hideLoading();
        },
        error: function() {
            hideLoading();
            showNotification('載入失敗，請稍後再試', 'error');
        }
    });
}

// 顯示物流商詳細資訊
function showCarrierDetail(carrierId) {
    var url = $('#logisticsConfig').data('detail-url-base') + '/' + carrierId;
    
    $.ajax({
        url: url,
        type: 'GET',
        success: function(data) {
            $('#carrierDetailContent').html(data);
            $('#carrierDetailModal').modal('show');
        },
        error: function() {
            showNotification('載入詳細資訊失敗', 'error');
        }
    });
}

// 確認刪除物流商
function confirmDelete(carrierId, carrierName, orderCount) {
    // 設定刪除確認modal的內容
    $('#deleteCarrierInfo').html(`
        <strong>物流商：</strong>${carrierName}<br>
        <strong>訂單數量：</strong>${orderCount}
    `);
    
    // 如果有訂單記錄，顯示警告
    if (orderCount > 0) {
        $('#deleteCarrierInfo').removeClass('alert-info').addClass('alert-warning');
        $('#deleteCarrierInfo').append('<br><br><i class="fas fa-exclamation-triangle"></i> <strong>警告：此物流商有 ' + orderCount + ' 筆訂單記錄，建議先處理完相關訂單再刪除。</strong>');
    } else {
        $('#deleteCarrierInfo').removeClass('alert-warning').addClass('alert-info');
    }
    
    // 設定確認按鈕的點擊事件
    $('#confirmDeleteBtn').off('click').on('click', function() {
        executeDelete(carrierId);
    });
    
    $('#deleteConfirmModal').modal('show');
}

// 執行刪除
function executeDelete(carrierId) {
    var url = $('#logisticsConfig').data('delete-url-base') + '/' + carrierId;
    
    $('#deleteConfirmModal').modal('hide');
    showLoading();
    
    $.ajax({
        url: url,
        type: 'POST',
        headers: {
            'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
        },
        success: function(response) {
            hideLoading();
            if (response.success) {
                showNotification(response.message, 'success');
                // 重新載入列表
                performSearch();
            } else {
                showNotification(response.message, 'error');
            }
        },
        error: function() {
            hideLoading();
            showNotification('刪除失敗，請稍後再試', 'error');
        }
    });
}

// 顯示載入指示器
function showLoading() {
    $('#loadingIndicator').show();
    $('#carrierListContainer').hide();
}

// 隱藏載入指示器
function hideLoading() {
    $('#loadingIndicator').hide();
    $('#carrierListContainer').show();
}

// 顯示通知訊息
function showNotification(message, type) {
    var alertClass = 'alert-info';
    var icon = 'fa-info-circle';
    
    switch(type) {
        case 'success':
            alertClass = 'alert-success';
            icon = 'fa-check-circle';
            break;
        case 'error':
            alertClass = 'alert-danger';
            icon = 'fa-exclamation-triangle';
            break;
        case 'warning':
            alertClass = 'alert-warning';
            icon = 'fa-exclamation-circle';
            break;
    }
    
    var alertHtml = `
        <div class="alert ${alertClass} alert-dismissible fade show" role="alert">
            <i class="fas ${icon}"></i> ${message}
            <button type="button" class="close" data-dismiss="alert">
                <span>&times;</span>
            </button>
        </div>
    `;
    
    // 移除現有的alert
    $('.alert:not(.alert-dismissible)').remove();
    
    // 添加新的alert到頁面頂部
    $('h3:first').after(alertHtml);
    
    // 3秒後自動隱藏
    setTimeout(function() {
        $('.alert').fadeOut();
    }, 3000);
}

// 匯出功能
function exportCarriers() {
    window.location.href = '/AdminLogistics/ExportCarriers';
}

// 批量操作相關函數
function selectAllCarriers() {
    $('.carrier-checkbox').prop('checked', true);
    updateBatchButtons();
}

function deselectAllCarriers() {
    $('.carrier-checkbox').prop('checked', false);
    updateBatchButtons();
}

function updateBatchButtons() {
    var selectedCount = $('.carrier-checkbox:checked').length;
    if (selectedCount > 0) {
        $('.batch-actions').show();
        $('.batch-count').text(selectedCount);
    } else {
        $('.batch-actions').hide();
    }
}

// 批量啟用/停用
function batchToggleStatus(isActive) {
    var selectedIds = $('.carrier-checkbox:checked').map(function() {
        return $(this).val();
    }).get();
    
    if (selectedIds.length === 0) {
        showNotification('請至少選擇一個物流商', 'warning');
        return;
    }
    
    var action = isActive ? '啟用' : '停用';
    if (!confirm(`確定要${action} ${selectedIds.length} 個物流商嗎？`)) {
        return;
    }
    
    showLoading();
    
    $.ajax({
        url: '/AdminLogistics/BatchToggleStatus',
        type: 'POST',
        data: {
            ids: selectedIds,
            isActive: isActive
        },
        headers: {
            'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
        },
        success: function(response) {
            hideLoading();
            if (response.success) {
                showNotification(response.message, 'success');
                performSearch();
            } else {
                showNotification(response.message, 'error');
            }
        },
        error: function() {
            hideLoading();
            showNotification('批量操作失敗，請稍後再試', 'error');
        }
    });
}

// 切換物流商啟用/停用狀態
function toggleCarrierStatus(carrierId, carrierName) {
    if (!confirm(`確定要切換「${carrierName}」的狀態嗎？`)) {
        return;
    }
    
    showLoading();
    
    $.ajax({
        url: '/AdminLogistics/ToggleStatus',
        type: 'POST',
        data: { id: carrierId },
        headers: {
            'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
        },
        success: function(response) {
            hideLoading();
            if (response.success) {
                showNotification(response.message, 'success');
                // 重新載入列表
                performSearch();
            } else {
                showNotification(response.message, 'error');
            }
        },
        error: function() {
            hideLoading();
            showNotification('切換狀態失敗，請稍後再試', 'error');
        }
    });
}