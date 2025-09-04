// Categories DataTables 初始化 - Bootstrap 4 相容版本
$(document).ready(function() {
    var categoryTable;

    // 記錄被收合的父分類 ID
    var collapsedParents = new Set();

    // 目前套用中的「父分類」自訂篩選函式參考
    var currentParentFilter = null;

    // 初始化 DataTable
    function initializeCategoryTable() {
        categoryTable = $('#categoryTable').DataTable({
            "language": {
                "lengthMenu": "每頁顯示 _MENU_ 筆資料",
                "zeroRecords": "找不到符合條件的資料",
                "info": "顯示第 _START_ 到 _END_ 筆，共 _TOTAL_ 筆資料",
                "infoEmpty": "沒有資料",
                "infoFiltered": "(從 _MAX_ 筆資料中篩選)",
                "search": "搜尋:",
                "paginate": {
                    "first": "第一頁",
                    "last": "最後一頁",
                    "next": "下一頁",
                    "previous": "上一頁"
                },
                "emptyTable": "目前沒有可顯示的資料",
                "loadingRecords": "載入中...",
                "processing": "處理中..."
            },
            "pageLength": 25,
            "lengthMenu": [
                [5, 10, 25, 50, -1],
                [5, 10, 25, 50, "全部"]
            ],
            "order": [[0, "asc"]], // 預設按分類ID排序
            "columnDefs": [
                {
                    "targets": [0], // 分類ID欄位 - 可排序
                    "className": "text-center",
                    "orderable": true
                },
                {
                    "targets": [1], // 分類名稱欄位 - 不可排序
                    "orderable": false
                },
                {
                    "targets": [2], // 類型欄位 - 不可排序
                    "className": "text-center",
                    "orderable": false
                },
                {
                    "targets": [3], // 操作欄位 - 不可排序也不可搜尋
                    "orderable": false,
                    "searchable": false,
                    "className": "text-center"
                }
            ],
            "responsive": true,
            "processing": true,
            "autoWidth": false,
            "drawCallback": function(settings) {
                // 每次重繪表格後重新綁定事件
                injectToggleCarets();
                bindButtonEvents();
                applyCollapseState();
                updateStatistics();
                applyCustomStyles();
            },
            "initComplete": function() {
                // 表格初始化完成後的設定
                addCustomFilterEvents();
                injectToggleCarets();
                applyCustomStyles();
                updateStatistics();

                // 設定搜尋框提示文字
                $('#categoryTable_filter input').attr('placeholder', '搜尋分類ID、名稱...');

                console.log('Categories DataTables 初始化完成 - Bootstrap 4 版本');
            }
        });
    }

    // 在父分類名稱欄位注入收合/展開圖示
    function injectToggleCarets() {
        $('#categoryTable tbody tr.parent-category').each(function() {
            var $row = $(this);
            var categoryId = $row.data('category-id');
            var $nameCell = $row.find('td').eq(1); // 分類名稱欄位

            if ($nameCell.find('.toggle-caret').length === 0) {
                var caretHtml = '<span class="toggle-caret mr-2" role="button" aria-label="toggle" title="展開/收合"><i class="fas fa-caret-down"></i></span>';
                $nameCell.prepend(caretHtml);
            }

            // 設定圖示狀態
            var isCollapsed = collapsedParents.has(categoryId);
            $row.toggleClass('is-collapsed', isCollapsed);
            $nameCell.find('.toggle-caret i')
                .removeClass('fa-caret-down fa-caret-right')
                .addClass(isCollapsed ? 'fa-caret-right' : 'fa-caret-down');
        });

        // 綁定點擊事件（避免重複註冊先移除再綁定）
        $('#categoryTable').off('click', '.toggle-caret').on('click', '.toggle-caret', function(e) {
            e.stopPropagation();
            var $row = $(this).closest('tr.parent-category');
            var parentId = $row.data('category-id');
            toggleParentCollapse(parentId);
        });

        // 也允許點擊整個父分類列切換
        $('#categoryTable').off('click', 'tr.parent-category').on('click', 'tr.parent-category', function(e) {
            // 避免在點擊操作列或超連結時觸發
            if ($(e.target).closest('.action-btn, button, a').length > 0) return;
            var parentId = $(this).data('category-id');
            toggleParentCollapse(parentId);
        });
    }

    function toggleParentCollapse(parentId) {
        if (collapsedParents.has(parentId)) {
            collapsedParents.delete(parentId);
            showChildren(parentId);
        } else {
            collapsedParents.add(parentId);
            hideChildren(parentId);
        }
        updateStatistics();
    }

    function hideChildren(parentId) {
        var $parentRow = $('#categoryTable tbody tr.parent-category[data-category-id="' + parentId + '"]');
        $parentRow.addClass('is-collapsed');
        $parentRow.find('.toggle-caret i').removeClass('fa-caret-down').addClass('fa-caret-right');
        $('#categoryTable tbody tr.child-category[data-category-id="' + parentId + '"]').hide();
    }

    function showChildren(parentId) {
        var $parentRow = $('#categoryTable tbody tr.parent-category[data-category-id="' + parentId + '"]');
        $parentRow.removeClass('is-collapsed');
        $parentRow.find('.toggle-caret i').removeClass('fa-caret-right').addClass('fa-caret-down');
        $('#categoryTable tbody tr.child-category[data-category-id="' + parentId + '"]').show();
    }

    function applyCollapseState() {
        // 先展開全部，再依狀態收合
        $('#categoryTable tbody tr.child-category').show();
        collapsedParents.forEach(function(pid) {
            hideChildren(pid);
        });
    }

    // 自定義篩選器事件
    function addCustomFilterEvents() {
        // 分類類型篩選
        $('#categoryTypeFilter').on('change', function() {
            var selectedType = $(this).val();
            categoryTable.column(2).search(selectedType).draw();
            applyCollapseState();
            updateStatistics();
        });

        // 父分類篩選
        $('#parentCategoryFilter').on('change', function() {
            var selectedParent = $(this).val();

            // 先移除已存在的父分類自訂篩選
            if (currentParentFilter) {
                $.fn.dataTable.ext.search = $.fn.dataTable.ext.search.filter(function(fn) {
                    return fn !== currentParentFilter;
                });
                currentParentFilter = null;
            }

            if (selectedParent) {
                currentParentFilter = function(settings, data, dataIndex) {
                    var row = categoryTable.row(dataIndex).node();
                    var $row = $(row);

                    // 以 data-category-name 作為比對基準，避免受 HTML 影響
                    if ($row.hasClass('parent-category')) {
                        return ($row.data('category-name') || '') === selectedParent;
                    }
                    if ($row.hasClass('child-category')) {
                        var parentCategoryName = $row.data('category-name');
                        return (parentCategoryName || '') === selectedParent;
                    }
                    return false;
                };
                $.fn.dataTable.ext.search.push(currentParentFilter);
            }

            categoryTable.draw();
            applyCollapseState();
            updateStatistics();
        });

        // 重置所有篩選器
        $('#resetFilters').on('click', function() {
            $('#categoryTypeFilter, #parentCategoryFilter').val('');
            // 清掉我們註冊的父分類篩選器，並保留 DataTables 其他可能的篩選器（此處沒有）
            if (currentParentFilter) {
                $.fn.dataTable.ext.search = $.fn.dataTable.ext.search.filter(function(fn) {
                    return fn !== currentParentFilter;
                });
                currentParentFilter = null;
            } else {
                // 若沒有自訂，保險起見清空所有自訂搜尋
                $.fn.dataTable.ext.search = [];
            }
            categoryTable.search('').columns().search('').draw();
            applyCollapseState();
            updateStatistics();
            console.log('所有篩選器已重置');
        });

        // 若存在一鍵展開/收合按鈕，綁定事件
        $('#expandAll').on('click', function() {
            collapsedParents.clear();
            applyCollapseState();
        });
        $('#collapseAll').on('click', function() {
            // 將當前頁面可見的父分類全部收合
            $('#categoryTable tbody tr.parent-category:visible').each(function() {
                var pid = $(this).data('category-id');
                collapsedParents.add(pid);
            });
            applyCollapseState();
        });
    }

    // 綁定按鈕事件
    function bindButtonEvents() {
        $('.edit-btn').off('click').on('click', function() {
            var row = $(this).closest('tr.child-category');
            if (row.length > 0) {
                $('#editModalLabel').html('<i class="fas fa-edit"></i> 編輯子分類');
                $('#subCategoryId').val(row.data('subcategory-id'));
                $('#subCategoryName').val(row.data('subcategory-name'));
                $('#subCategoryDescription').val(row.data('subcategory-description') || '');
                $('#editForm').attr('action', '/Categories/EditSubCategory');
                loadCategories(row.data('category-id'));
            }
        });

        $('.delete-btn').off('click').on('click', function() {
            var row = $(this).closest('tr.child-category');
            if (row.length > 0) {
                $('#deleteSubCategoryId').val(row.data('subcategory-id'));
            }
        });
    }

    // 更新統計資訊
    function updateStatistics() {
        setTimeout(function() {
            var visibleRows = $('#categoryTable tbody tr:visible');
            var parentCount = visibleRows.filter('.parent-category').length;
            var childCount = visibleRows.filter('.child-category').length;

            $('#parentCount').text(parentCount);
            $('#childCount').text(childCount);
        }, 100);
    }

    // 應用自定義樣式
    function applyCustomStyles() {
        $('#categoryTable tbody tr.parent-category').addClass('parent-category-row');
        $('#categoryTable tbody tr.child-category').addClass('child-category-row');
    }

    // 載入分類下拉選單
    function loadCategories(selectedId = null) {
        $.get('/Categories/GetCategories', function (data) {
            $('#categorySelect').empty();
            $('#categorySelect').append('<option value="">請選擇父分類</option>');

            if (data.error) {
                alert('載入分類失敗：' + data.error);
                return;
            }

            data.forEach(function (item) {
                var selected = item.id === selectedId ? 'selected' : '';
                $('#categorySelect').append(`<option value="${item.id}" ${selected}>${item.name}</option>`);
            });
        }).fail(function() {
            alert('載入分類失敗，請重新嘗試');
        });
    }

    // 新增按鈕事件
    $('#addNewBtn').click(function () {
        $('#editModalLabel').html('<i class="fas fa-plus"></i> 新增子分類');
        $('#subCategoryId').val(0);
        $('#subCategoryName').val('');
        $('#subCategoryDescription').val('');
        $('#editForm').attr('action', '/Categories/CreateSubCategory');
        loadCategories();
    });

    // 表單驗證
    $('#editForm').submit(function(e) {
        var categoryId = $('#categorySelect').val();
        var subCategoryName = $('#subCategoryName').val().trim();
        var description = $('#subCategoryDescription').val();

        if (!categoryId) {
            e.preventDefault();
            alert('請選擇父分類');
            $('#categorySelect').focus();
            return false;
        }

        if (!subCategoryName) {
            e.preventDefault();
            alert('請輸入子分類名稱');
            $('#subCategoryName').focus();
            return false;
        }

        if (subCategoryName.length > 100) {
            e.preventDefault();
            alert('子分類名稱不能超過100個字元');
            $('#subCategoryName').focus();
            return false;
        }

        if (description && description.length > 255) {
            e.preventDefault();
            alert('子分類描述不能超過255個字元');
            $('#subCategoryDescription').focus();
            return false;
        }
    });

    // 自動關閉警告訊息
    setTimeout(function() {
        $('.alert').fadeOut('slow');
    }, 5000);

    // 初始化表格
    initializeCategoryTable();
});