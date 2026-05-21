'use strict';

/**
 * Single-selection autocomplete.
 * Used for Project Manager on the wizard step 3 and the Edit form.
 *
 * Options:
 *   searchInput     – text <input> the user types into
 *   dropdown        – container <div> for results
 *   hiddenInput     – <input type="hidden"> that holds the selected ID
 *   selectedDisplay – element shown when a selection is made
 *   selectedName    – element inside selectedDisplay showing the name
 *   clearBtn        – button that clears the selection
 *   searchUrl       – URL prefix, e.g. '/employees/search'
 *   initialId       – pre-selected ID (0 = none)
 *   initialName     – pre-selected name (for repopulating on validation error)
 */
function initSingleAutocomplete(opts) {
    const { searchInput, dropdown, hiddenInput, selectedDisplay,
            selectedName, clearBtn, searchUrl } = opts;

    let debounce;

    function selectEmployee(id, name) {
        hiddenInput.value = id;
        selectedName.textContent = name;
        selectedDisplay.style.display = '';
        searchInput.value = '';
        dropdown.style.display = 'none';
        searchInput.style.display = 'none';
    }

    function clearSelection() {
        hiddenInput.value = '0';
        selectedDisplay.style.display = 'none';
        searchInput.style.display = '';
        searchInput.value = '';
        searchInput.focus();
    }

    // Restore pre-selected value on page load (e.g. after validation error)
    if (opts.initialId && opts.initialId > 0 && opts.initialName) {
        selectEmployee(opts.initialId, opts.initialName);
    }

    clearBtn.addEventListener('click', clearSelection);

    searchInput.addEventListener('input', function () {
        clearTimeout(debounce);
        const term = this.value.trim();
        if (term.length < 2) {
            dropdown.style.display = 'none';
            return;
        }
        debounce = setTimeout(() => fetchAndRender(term), 280);
    });

    async function fetchAndRender(term) {
        try {
            const res = await fetch(`${searchUrl}?term=${encodeURIComponent(term)}&limit=10`);
            const employees = await res.json();
            renderDropdown(employees);
        } catch {
            dropdown.style.display = 'none';
        }
    }

    function renderDropdown(employees) {
        dropdown.innerHTML = '';
        if (!employees.length) {
            const item = document.createElement('div');
            item.className = 'autocomplete-item no-results';
            item.textContent = 'No employees found';
            dropdown.appendChild(item);
        } else {
            employees.forEach(emp => {
                const item = document.createElement('div');
                item.className = 'autocomplete-item';
                item.textContent = emp.fullName;
                item.addEventListener('mousedown', e => {
                    e.preventDefault();
                    selectEmployee(emp.id, emp.fullName);
                });
                dropdown.appendChild(item);
            });
        }
        dropdown.style.display = 'block';
    }

    document.addEventListener('click', e => {
        if (!searchInput.contains(e.target) && !dropdown.contains(e.target)) {
            dropdown.style.display = 'none';
        }
    });

    searchInput.addEventListener('keydown', e => {
        if (e.key === 'Escape') dropdown.style.display = 'none';
    });
}

/**
 * Multi-selection autocomplete.
 * Used for Team Members on the wizard step 4.
 *
 * Options:
 *   searchInput     – text <input>
 *   dropdown        – container <div> for results
 *   chipsContainer  – element where selected chips are rendered
 *   hiddenContainer – element where hidden inputs are placed
 *   emptyHint       – hint shown when no employees are selected
 *   searchUrl       – URL prefix
 *   hiddenInputName – name attribute for the hidden inputs (e.g. 'EmployeeIds')
 *   excludeInputId  – optional ID of a hidden input whose value to exclude (PM)
 */
function initMultiAutocomplete(opts) {
    const { searchInput, dropdown, chipsContainer, hiddenContainer,
            emptyHint, searchUrl, hiddenInputName, excludeInputId } = opts;

    // Build initial set from already-rendered chips (after validation error)
    const selected = new Map();
    chipsContainer.querySelectorAll('.employee-chip').forEach(chip => {
        const id = parseInt(chip.dataset.id, 10);
        const name = chip.querySelector('.remove-btn')
            ? chip.childNodes[0].textContent.trim()
            : chip.textContent.trim();
        selected.set(id, name);
        wireChipRemove(chip, id);
    });

    let debounce;

    searchInput.addEventListener('input', function () {
        clearTimeout(debounce);
        const term = this.value.trim();
        if (term.length < 2) {
            dropdown.style.display = 'none';
            return;
        }
        debounce = setTimeout(() => fetchAndRender(term), 280);
    });

    async function fetchAndRender(term) {
        const excludeId = excludeInputId
            ? parseInt(document.getElementById(excludeInputId)?.value || '0', 10)
            : 0;

        try {
            const res = await fetch(`${searchUrl}?term=${encodeURIComponent(term)}&limit=10`);
            const employees = await res.json();
            const filtered = employees.filter(e =>
                !selected.has(e.id) && e.id !== excludeId);
            renderDropdown(filtered);
        } catch {
            dropdown.style.display = 'none';
        }
    }

    function renderDropdown(employees) {
        dropdown.innerHTML = '';
        if (!employees.length) {
            const item = document.createElement('div');
            item.className = 'autocomplete-item no-results';
            item.textContent = selected.size ? 'No more employees found' : 'No employees found';
            dropdown.appendChild(item);
        } else {
            employees.forEach(emp => {
                const item = document.createElement('div');
                item.className = 'autocomplete-item';
                item.textContent = emp.fullName;
                item.addEventListener('mousedown', e => {
                    e.preventDefault();
                    addEmployee(emp.id, emp.fullName);
                    searchInput.value = '';
                    dropdown.style.display = 'none';
                    searchInput.focus();
                });
                dropdown.appendChild(item);
            });
        }
        dropdown.style.display = 'block';
    }

    function addEmployee(id, name) {
        if (selected.has(id)) return;
        selected.set(id, name);

        // Chip
        const chip = document.createElement('span');
        chip.className = 'employee-chip';
        chip.dataset.id = id;
        chip.textContent = name;
        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'remove-btn';
        btn.setAttribute('aria-label', 'Remove');
        btn.innerHTML = '<i class="bi bi-x"></i>';
        chip.appendChild(btn);
        chipsContainer.appendChild(chip);
        wireChipRemove(chip, id);

        // Hidden input
        const hidden = document.createElement('input');
        hidden.type = 'hidden';
        hidden.name = hiddenInputName;
        hidden.value = id;
        hidden.id = `emp-hidden-${id}`;
        hiddenContainer.appendChild(hidden);

        if (emptyHint) emptyHint.style.display = 'none';
    }

    function removeEmployee(id) {
        selected.delete(id);
        const chip = chipsContainer.querySelector(`[data-id="${id}"]`);
        if (chip) chip.remove();
        const hidden = document.getElementById(`emp-hidden-${id}`);
        if (hidden) hidden.remove();
        if (emptyHint && selected.size === 0) emptyHint.style.display = '';
    }

    function wireChipRemove(chip, id) {
        chip.querySelector('.remove-btn')?.addEventListener('click', () => removeEmployee(id));
    }

    document.addEventListener('click', e => {
        if (!searchInput.contains(e.target) && !dropdown.contains(e.target)) {
            dropdown.style.display = 'none';
        }
    });
}

/**
 * Assign-panel autocomplete (single, used on the Detail page team section).
 * Same as single but also controls a submit button's disabled state.
 */
function initAssignAutocomplete(opts) {
    const { searchInput, dropdown, hiddenInput, selectedDisplay,
            selectedName, clearBtn, submitBtn, searchUrl } = opts;

    let debounce;

    function select(id, name) {
        hiddenInput.value = id;
        selectedName.textContent = name;
        selectedDisplay.style.display = '';
        searchInput.value = '';
        dropdown.style.display = 'none';
        if (submitBtn) submitBtn.disabled = false;
    }

    function clear() {
        hiddenInput.value = '0';
        selectedDisplay.style.display = 'none';
        searchInput.value = '';
        searchInput.focus();
        if (submitBtn) submitBtn.disabled = true;
    }

    clearBtn.addEventListener('click', clear);

    searchInput.addEventListener('input', function () {
        clearTimeout(debounce);
        const term = this.value.trim();
        if (term.length < 2) {
            dropdown.style.display = 'none';
            return;
        }
        debounce = setTimeout(() => fetchAndRender(term), 280);
    });

    async function fetchAndRender(term) {
        try {
            const res = await fetch(`${searchUrl}?term=${encodeURIComponent(term)}&limit=10`);
            const employees = await res.json();
            dropdown.innerHTML = '';
            if (!employees.length) {
                const item = document.createElement('div');
                item.className = 'autocomplete-item no-results';
                item.textContent = 'No employees found';
                dropdown.appendChild(item);
            } else {
                employees.forEach(emp => {
                    const item = document.createElement('div');
                    item.className = 'autocomplete-item';
                    item.textContent = emp.fullName;
                    item.addEventListener('mousedown', e => {
                        e.preventDefault();
                        select(emp.id, emp.fullName);
                    });
                    dropdown.appendChild(item);
                });
            }
            dropdown.style.display = 'block';
        } catch {
            dropdown.style.display = 'none';
        }
    }

    document.addEventListener('click', e => {
        if (!searchInput.contains(e.target) && !dropdown.contains(e.target)) {
            dropdown.style.display = 'none';
        }
    });
}

/**
 * Drop-zone stub: visual feedback only, no actual file upload.
 * Accepts a drop-zone element and wires drag events + file input.
 */
function initDropZoneStub(zone) {
    if (!zone) return;

    ['dragenter', 'dragover'].forEach(evt =>
        zone.addEventListener(evt, e => {
            e.preventDefault();
            zone.classList.add('drag-over');
        })
    );

    ['dragleave', 'drop'].forEach(evt =>
        zone.addEventListener(evt, e => {
            e.preventDefault();
            zone.classList.remove('drag-over');
        })
    );
}
