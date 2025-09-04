'use strict';

(function () {
    var cfgEl = document.getElementById('ordersConfig');
    if (!cfgEl) return;
    var cfg = cfgEl.dataset;

    var form = document.getElementById('filterForm');
    var list = document.getElementById('listRegion');
    if (!form || !list) return;

    var pageInput = form.querySelector('input[name="Page"]');
    var qs = function () { return new URLSearchParams(new FormData(form)).toString(); };

    async function loadList(pushUrl) {
        pushUrl = pushUrl !== false;
        var res = await fetch(cfg.listUrl + '?' + qs(), { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
        list.innerHTML = await res.text();
        if (pushUrl) history.replaceState(null, '', cfg.indexUrl + '?' + qs());
    }

    // 阻止整頁提交
    form.addEventListener('submit', function (e) { e.preventDefault(); if (pageInput) pageInput.value = 1; loadList(); });

    // debounce
    var debounce = function (fn, d) { var t; d = d || 200; return function () { clearTimeout(t); var a = arguments; t = setTimeout(function () { fn.apply(null, a); }, d); }; };
    var q = form.querySelector('[name="Q"]');
    if (q) q.addEventListener('input', debounce(function () { if (pageInput) pageInput.value = 1; loadList(); }, 400));

    // 下拉/日期等
    ['PaymentStatus', 'OrderStatus', 'DateFrom', 'DateTo', 'PageSize'].forEach(function (n) {
        var el = form.querySelector('[name="' + n + '"]');
        if (el) el.addEventListener('change', function () { if (pageInput) pageInput.value = 1; loadList(); });
    });

    // 分頁攔截
    list.addEventListener('click', function (e) {
        var a = e.target.closest('a.page-link');
        if (!a) return;
        e.preventDefault();
        var u = new URL(a.href, location.origin);
        var p = u.searchParams.get('Page') || '1';
        if (pageInput) pageInput.value = p;
        loadList();
    });

    // Modal（BS4：用 jQuery 綁事件）
    $('#detailModal').on('show.bs.modal', async function (ev) {
        var btn = ev.relatedTarget; if (!btn) return;
        var id = btn.getAttribute('data-id');
        var code = btn.getAttribute('data-code');
        document.getElementById('detailTitle').textContent = '訂單明細：' + code;

        var body = document.getElementById('detailModalBody');
        body.innerHTML = '<div class="p-4 text-center text-muted">載入中…</div>';

        var res = await fetch(cfg.detailUrlBase + '/' + id, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
        body.innerHTML = await res.text();

        document.getElementById('toManageLink').setAttribute('href', cfg.manageUrlBase + '/' + id);
    });
})();
