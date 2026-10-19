(function () {
    // 1. Opportunities list filters
    var filterForm = document.getElementById('oppFilterForm');
    if (filterForm) {
        var statusSelect = document.getElementById('oppStatusSelect');
        var stageSelect = document.getElementById('oppStageSelect');
        var ownerSelect = document.getElementById('oppOwnerSelect');
        var searchInput = document.getElementById('oppSearchInput');

        [statusSelect, stageSelect, ownerSelect].forEach(function (el) {
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

    // 2. Customer Lookup on Opportunity Form
    var customerIdInput = document.getElementById('customerIdInput');
    var customerNameInput = document.getElementById('customerNameInput');
    var lookupInput = document.getElementById('customerLookupInput');
    var lookupResults = document.getElementById('lookupResults');
    var selectedDisplay = document.getElementById('selectedCustomerDisplay');
    var selectedName = document.getElementById('selectedCustomerName');
    var clearBtn = document.getElementById('clearSelectedCustomer');

    function selectCustomer(id, label) {
        if (customerIdInput) customerIdInput.value = id;
        if (customerNameInput) customerNameInput.value = label;
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
            if (customerNameInput) customerNameInput.value = '';
            if (lookupInput) lookupInput.value = '';
            if (selectedDisplay) selectedDisplay.classList.add('d-none');
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

    // 3. Stage probability hint update
    var stageSelectInput = document.getElementById('oppStageSelectInput');
    var probInput = document.getElementById('oppProbabilityInput');
    var probHint = document.getElementById('probHint');
    var stageProbs = {
        'Qualification': 20,
        'Proposal': 50,
        'Negotiation': 75
    };

    if (stageSelectInput && probInput) {
        stageSelectInput.addEventListener('change', function () {
            var stg = stageSelectInput.value;
            if (stageProbs[stg] !== undefined) {
                probInput.value = stageProbs[stg];
                if (probHint) {
                    probHint.textContent = 'Typical for ' + stg + ': ' + stageProbs[stg] + '%';
                }
            }
        });
    }

    // 4. Close Modal setup
    var closeModal = document.getElementById('closeOppModal');
    if (closeModal) {
        closeModal.addEventListener('show.bs.modal', function (event) {
            var button = event.relatedTarget;
            if (!button) return;

            var stage = button.getAttribute('data-stage') || 'Won';
            var title = button.getAttribute('data-title') || 'Close Opportunity';
            var noteLabel = button.getAttribute('data-note-label') || 'Close Note';

            var targetStageInput = document.getElementById('closeModalTargetStage');
            var modalTitle = document.getElementById('closeOppModalTitle');
            var modalNoteLabel = document.getElementById('closeModalNoteLabel');
            var noteInput = document.getElementById('closeModalNote');

            if (targetStageInput) targetStageInput.value = stage;
            if (modalTitle) modalTitle.textContent = title;
            if (modalNoteLabel) modalNoteLabel.innerHTML = noteLabel + ' <span class="text-danger">*</span>';
            if (noteInput) {
                noteInput.value = '';
                setTimeout(function () { noteInput.focus(); }, 300);
            }
        });
    }
})();
