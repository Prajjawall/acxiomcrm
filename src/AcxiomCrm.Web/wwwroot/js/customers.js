(function () {
    var searchInput = document.getElementById('customerSearchInput');
    var statusSelect = document.getElementById('customerStatusSelect');
    var ownerSelect = document.getElementById('customerOwnerSelect');
    var filterForm = document.getElementById('customerFilterForm');

    if (!filterForm) {
        return;
    }

    if (statusSelect) {
        statusSelect.addEventListener('change', function () {
            filterForm.submit();
        });
    }

    if (ownerSelect) {
        ownerSelect.addEventListener('change', function () {
            filterForm.submit();
        });
    }

    if (searchInput) {
        var timeout = null;
        searchInput.addEventListener('input', function () {
            clearTimeout(timeout);
            timeout = setTimeout(function () {
                filterForm.submit();
            }, 500);
        });
    }
})();
