(function () {
    // 1. Leads list filters
    var filterForm = document.getElementById('leadFilterForm');
    if (filterForm) {
        var statusSelect = document.getElementById('leadStatusSelect');
        var prioritySelect = document.getElementById('leadPrioritySelect');
        var sourceSelect = document.getElementById('leadSourceSelect');
        var ownerSelect = document.getElementById('leadOwnerSelect');
        var searchInput = document.getElementById('leadSearchInput');

        [statusSelect, prioritySelect, sourceSelect, ownerSelect].forEach(function (el) {
            if (el) {
                el.addEventListener('change', function () {
                    filterForm.submit();
                });
            }
        });

        if (searchInput) {
            var searchTimeout = null;
            searchInput.addEventListener('input', function () {
                clearTimeout(searchTimeout);
                searchTimeout = setTimeout(function () {
                    filterForm.submit();
                }, 400);
            });
        }
    }

    // 2. Lead conversion page interactions
    var convertForm = document.getElementById('convertLeadForm');
    if (convertForm) {
        var modeNew = document.getElementById('modeNewCustomer');
        var modeExisting = document.getElementById('modeExistingCustomer');
        var existingSection = document.getElementById('existingCustomerSection');
        var customerIdInput = document.getElementById('existingCustomerIdInput');
        var customerLabelInput = document.getElementById('existingCustomerLabelInput');
        var lookupInput = document.getElementById('customerLookupInput');
        var lookupResults = document.getElementById('lookupResults');
        var selectedDisplay = document.getElementById('selectedCustomerDisplay');
        var selectedName = document.getElementById('selectedCustomerName');
        var clearBtn = document.getElementById('clearSelectedCustomer');
        var btnUseSuggested = document.getElementById('btnUseSuggested');
        var oppCheck = document.getElementById('createOpportunityCheck');
        var oppContainer = document.getElementById('opportunityFieldsContainer');

        function updateMode() {
            if (modeExisting && modeExisting.checked) {
                if (existingSection) existingSection.classList.remove('d-none');
            } else {
                if (existingSection) existingSection.classList.add('d-none');
                if (customerIdInput) customerIdInput.value = '';
                if (customerLabelInput) customerLabelInput.value = '';
                if (lookupInput) lookupInput.value = '';
                if (selectedDisplay) selectedDisplay.classList.add('d-none');
            }
        }

        if (modeNew && modeExisting) {
            modeNew.addEventListener('change', updateMode);
            modeExisting.addEventListener('change', updateMode);
        }

        function selectCustomer(id, label) {
            if (customerIdInput) customerIdInput.value = id;
            if (customerLabelInput) customerLabelInput.value = label;
            if (lookupInput) lookupInput.value = label;
            if (selectedName) selectedName.textContent = label;
            if (selectedDisplay) selectedDisplay.classList.remove('d-none');
            if (lookupResults) {
                lookupResults.classList.remove('show');
                lookupResults.innerHTML = '';
            }
        }

        if (clearBtn) {
            clearBtn.addEventListener('click', function () {
                if (customerIdInput) customerIdInput.value = '';
                if (customerLabelInput) customerLabelInput.value = '';
                if (lookupInput) lookupInput.value = '';
                if (selectedDisplay) selectedDisplay.classList.add('d-none');
            });
        }

        if (btnUseSuggested) {
            btnUseSuggested.addEventListener('click', function () {
                var suggestedIdEl = document.getElementById('SuggestedCustomerId');
                var suggestedLabelEl = document.getElementById('SuggestedCustomerLabel');
                if (suggestedIdEl && suggestedIdEl.value) {
                    if (modeExisting) modeExisting.checked = true;
                    updateMode();
                    selectCustomer(suggestedIdEl.value, suggestedLabelEl ? suggestedLabelEl.value : 'Customer #' + suggestedIdEl.value);
                }
            });
        }

        if (lookupInput && lookupResults) {
            var lookupTimeout = null;
            lookupInput.addEventListener('input', function () {
                clearTimeout(lookupTimeout);
                var term = lookupInput.value.trim();
                if (term.length < 2) {
                    lookupResults.classList.remove('show');
                    lookupResults.innerHTML = '';
                    return;
                }

                lookupTimeout = setTimeout(function () {
                    fetch('/Customers/Lookup?term=' + encodeURIComponent(term))
                        .then(function (res) { return res.json(); })
                        .then(function (items) {
                            lookupResults.innerHTML = '';
                            if (!items || items.length === 0) {
                                var noMatch = document.createElement('div');
                                noMatch.className = 'dropdown-item text-muted small';
                                noMatch.textContent = 'No matching customers found';
                                lookupResults.appendChild(noMatch);
                            } else {
                                items.forEach(function (item) {
                                    var link = document.createElement('button');
                                    link.type = 'button';
                                    link.className = 'dropdown-item small text-truncate';
                                    link.textContent = item.label;
                                    link.addEventListener('click', function () {
                                        selectCustomer(item.id, item.label);
                                    });
                                    lookupResults.appendChild(link);
                                });
                            }
                            lookupResults.classList.add('show');
                        })
                        .catch(function () {
                            lookupResults.classList.remove('show');
                        });
                }, 300);
            });

            document.addEventListener('click', function (e) {
                if (!lookupInput.contains(e.target) && !lookupResults.contains(e.target)) {
                    lookupResults.classList.remove('show');
                }
            });
        }

        if (oppCheck && oppContainer) {
            oppCheck.addEventListener('change', function () {
                if (oppCheck.checked) {
                    oppContainer.classList.remove('d-none');
                } else {
                    oppContainer.classList.add('d-none');
                }
            });
        }
    }
})();
