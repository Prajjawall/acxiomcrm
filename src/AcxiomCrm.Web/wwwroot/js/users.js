(function () {
    var roleSelect = document.getElementById('roleSelect');
    var managerGroup = document.getElementById('managerGroup');

    function toggleManagerGroup() {
        if (!roleSelect || !managerGroup) return;
        if (roleSelect.value === 'SalesExecutive') {
            managerGroup.style.display = '';
        } else {
            managerGroup.style.display = 'none';
        }
    }

    if (roleSelect && managerGroup) {
        roleSelect.addEventListener('change', toggleManagerGroup);
        toggleManagerGroup();
    }

    var filterForm = document.getElementById('userFilterForm');
    if (filterForm) {
        var userRoleSelect = document.getElementById('userRoleSelect');
        var userActiveSelect = document.getElementById('userActiveSelect');
        var userLockedSelect = document.getElementById('userLockedSelect');
        var userManagerSelect = document.getElementById('userManagerSelect');

        [userRoleSelect, userActiveSelect, userLockedSelect, userManagerSelect].forEach(function (select) {
            if (select) {
                select.addEventListener('change', function () {
                    filterForm.submit();
                });
            }
        });
    }
})();
