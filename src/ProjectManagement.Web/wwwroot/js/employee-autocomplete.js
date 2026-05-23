'use strict';

// ── Shared helpers ────────────────────────────────────────────────────────────

function buildDropdown(items, onSelect) {
    const frag = document.createDocumentFragment();
    if (!items.length) {
        const el = document.createElement('div');
        el.className = 'autocomplete-item no-results';
        el.textContent = 'No employees found';
        frag.appendChild(el);
    } else {
        items.forEach(emp => {
            const el = document.createElement('div');
            el.className = 'autocomplete-item';
            el.textContent = emp.fullName;
            el.addEventListener('mousedown', e => { e.preventDefault(); onSelect(emp); });
            frag.appendChild(el);
        });
    }
    return frag;
}

function closeOnOutsideClick(searchInput, dropdown) {
    document.addEventListener('click', e => {
        if (!searchInput.contains(e.target) && !dropdown.contains(e.target))
            dropdown.style.display = 'none';
    });
    searchInput.addEventListener('keydown', e => {
        if (e.key === 'Escape') dropdown.style.display = 'none';
    });
}

// ── Shared URL builder ────────────────────────────────────────────────────────

function buildSearchUrl(base, term, limit, extra) {
    const usp = new URLSearchParams();
    usp.set('term', term);
    usp.set('limit', String(limit));
    if (extra) {
        for (const [k, v] of Object.entries(extra)) {
            if (v != null && v !== '') usp.set(k, v);
        }
    }
    return `${base}?${usp.toString()}`;
}

// ── Single-selection autocomplete (PM picker — chip based) ────────────────────
//
// Options:
//   searchInput    – text <input>
//   dropdown       – container <div> for results
//   hiddenInput    – <input type="hidden"> holding selected ID
//   chipContainer  – element below the input where the selected-chip appears
//   searchUrl      – URL prefix  e.g. '/employees/search'
//   initialId      – pre-selected ID (0 = none)
//   initialName    – pre-selected display name
//   extraParams    – optional object of extra query-string params, e.g.
//                    { roles: 'Director,ProjectManager' } to restrict the
//                    dropdown to those roles on the server.

function initSingleAutocomplete(opts) {
    const { searchInput, dropdown, hiddenInput, chipContainer, searchUrl } = opts;
    const extraParams = opts.extraParams || null;
    let debounce;

    function renderChip(name) {
        chipContainer.innerHTML = '';
        const chip = document.createElement('div');
        chip.className = 'pm-selected-chip';

        const label = document.createElement('span');
        label.innerHTML = `<i class="bi bi-person-badge me-2"></i>${escapeHtml(name)}`;

        const removeBtn = document.createElement('button');
        removeBtn.type = 'button';
        removeBtn.className = 'pm-chip-remove';
        removeBtn.setAttribute('aria-label', 'Remove');
        removeBtn.innerHTML = '<i class="bi bi-x-lg"></i>';
        removeBtn.addEventListener('click', clearSelection);

        chip.appendChild(label);
        chip.appendChild(removeBtn);
        chipContainer.appendChild(chip);
    }

    function selectEmployee(id, name) {
        hiddenInput.value = id;
        searchInput.value = '';
        dropdown.style.display = 'none';
        renderChip(name);
    }

    function clearSelection() {
        hiddenInput.value = '0';
        chipContainer.innerHTML = '';
        searchInput.value = '';
        searchInput.focus();
    }

    // Restore pre-selected value on page load (e.g. after validation error)
    if (opts.initialId && opts.initialId > 0 && opts.initialName)
        renderChip(opts.initialName);

    // Show dropdown immediately on focus
    searchInput.addEventListener('focus', function () {
        fetchAndRender(this.value.trim());
    });

    searchInput.addEventListener('input', function () {
        clearTimeout(debounce);
        debounce = setTimeout(() => fetchAndRender(this.value.trim()), 220);
    });

    async function fetchAndRender(term) {
        try {
            const res = await fetch(buildSearchUrl(searchUrl, term, 10, extraParams));
            const employees = await res.json();
            dropdown.innerHTML = '';
            dropdown.appendChild(buildDropdown(employees, emp => selectEmployee(emp.id, emp.fullName)));
            dropdown.style.display = 'block';
        } catch {
            dropdown.style.display = 'none';
        }
    }

    closeOnOutsideClick(searchInput, dropdown);
}

// ── Multi-selection autocomplete (team member picker — chips) ─────────────────
//
// Options:
//   searchInput     – text <input>
//   dropdown        – container <div>
//   chipsContainer  – element where selected chips are rendered
//   hiddenContainer – element where hidden inputs are placed
//   emptyHint       – hint shown when no employees are selected
//   searchUrl       – URL prefix
//   hiddenInputName – name attribute for the hidden inputs (e.g. 'EmployeeIds')
//   excludeInputId  – optional ID of a hidden input whose value to exclude (PM)

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

    // Show dropdown immediately on focus
    searchInput.addEventListener('focus', function () {
        fetchAndRender(this.value.trim());
    });

    searchInput.addEventListener('input', function () {
        clearTimeout(debounce);
        debounce = setTimeout(() => fetchAndRender(this.value.trim()), 220);
    });

    async function fetchAndRender(term) {
        const excludeId = excludeInputId
            ? parseInt(document.getElementById(excludeInputId)?.value || '0', 10)
            : 0;

        try {
            const res = await fetch(`${searchUrl}?term=${encodeURIComponent(term)}&limit=10`);
            const employees = await res.json();
            const filtered = employees.filter(e => !selected.has(e.id) && e.id !== excludeId);

            dropdown.innerHTML = '';
            if (!filtered.length) {
                const item = document.createElement('div');
                item.className = 'autocomplete-item no-results';
                item.textContent = selected.size ? 'No more employees found' : 'No employees found';
                dropdown.appendChild(item);
            } else {
                filtered.forEach(emp => {
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
        } catch {
            dropdown.style.display = 'none';
        }
    }

    function addEmployee(id, name) {
        if (selected.has(id)) return;
        selected.set(id, name);

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
        chipsContainer.querySelector(`[data-id="${id}"]`)?.remove();
        document.getElementById(`emp-hidden-${id}`)?.remove();
        if (emptyHint && selected.size === 0) emptyHint.style.display = '';
    }

    function wireChipRemove(chip, id) {
        chip.querySelector('.remove-btn')?.addEventListener('click', () => removeEmployee(id));
    }

    closeOnOutsideClick(searchInput, dropdown);
}

// ── Assign-panel autocomplete (Detail page team section) ─────────────────────

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

    searchInput.addEventListener('focus', function () {
        fetchAndRender(this.value.trim());
    });

    searchInput.addEventListener('input', function () {
        clearTimeout(debounce);
        debounce = setTimeout(() => fetchAndRender(this.value.trim()), 220);
    });

    async function fetchAndRender(term) {
        try {
            const res = await fetch(`${searchUrl}?term=${encodeURIComponent(term)}&limit=10`);
            const employees = await res.json();
            dropdown.innerHTML = '';
            dropdown.appendChild(buildDropdown(employees, emp => select(emp.id, emp.fullName)));
            dropdown.style.display = 'block';
        } catch {
            dropdown.style.display = 'none';
        }
    }

    closeOnOutsideClick(searchInput, dropdown);
}

// ── Project name autocomplete (Index page quick-search) ──────────────────────
//
// Options:
//   searchInput  – text <input>
//   dropdown     – container <div>
//   searchUrl    – URL prefix e.g. '/projects/search'

function initProjectSearch(opts) {
    const { searchInput, dropdown, searchUrl } = opts;
    let debounce;

    searchInput.addEventListener('focus', function () {
        fetchAndRender(this.value.trim());
    });

    searchInput.addEventListener('input', function () {
        clearTimeout(debounce);
        debounce = setTimeout(() => fetchAndRender(this.value.trim()), 220);
    });

    async function fetchAndRender(term) {
        try {
            const res = await fetch(`${searchUrl}?term=${encodeURIComponent(term)}&limit=10`);
            const projects = await res.json();
            dropdown.innerHTML = '';
            if (!projects.length) {
                const item = document.createElement('div');
                item.className = 'autocomplete-item no-results';
                item.textContent = 'No projects found';
                dropdown.appendChild(item);
            } else {
                projects.forEach(p => {
                    const item = document.createElement('div');
                    item.className = 'autocomplete-item';
                    item.textContent = p.name;
                    item.addEventListener('mousedown', e => {
                        e.preventDefault();
                        window.location.href = `/projects/${p.id}`;
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
        if (!searchInput.contains(e.target) && !dropdown.contains(e.target))
            dropdown.style.display = 'none';
    });
    searchInput.addEventListener('keydown', e => {
        if (e.key === 'Escape') dropdown.style.display = 'none';
    });
}

// ── Employee page search (Index page quick-search, navigates on select) ──────
//
// Options:
//   searchInput  – text <input>
//   dropdown     – container <div>
//   searchUrl    – URL prefix e.g. '/employees/search'

function initEmployeePageSearch(opts) {
    const { searchInput, dropdown, searchUrl } = opts;
    let debounce;

    searchInput.addEventListener('focus', function () {
        fetchAndRender(this.value.trim());
    });

    searchInput.addEventListener('input', function () {
        clearTimeout(debounce);
        debounce = setTimeout(() => fetchAndRender(this.value.trim()), 220);
    });

    async function fetchAndRender(term) {
        try {
            const res = await fetch(`${searchUrl}?term=${encodeURIComponent(term)}&limit=15`);
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
                        window.location.href = `/employees/${emp.id}`;
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
        if (!searchInput.contains(e.target) && !dropdown.contains(e.target))
            dropdown.style.display = 'none';
    });
    searchInput.addEventListener('keydown', e => {
        if (e.key === 'Escape') dropdown.style.display = 'none';
        if (e.key === 'Enter') {
            // Allow normal form submit on Enter
            dropdown.style.display = 'none';
        }
    });
}

// ── Utility ───────────────────────────────────────────────────────────────────

function escapeHtml(str) {
    return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
              .replace(/"/g, '&quot;').replace(/'/g, '&#039;');
}
