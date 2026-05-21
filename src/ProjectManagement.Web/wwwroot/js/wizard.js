'use strict';

/**
 * Multi-step wizard controller.
 *
 * Options:
 *   totalSteps    – number of wizard steps
 *   validateStep  – function(stepNumber) → boolean; called before advancing
 */
function initWizard(opts) {
    const { totalSteps, validateStep } = opts;

    const panels       = document.querySelectorAll('.wizard-panel');
    const stepEls      = document.querySelectorAll('.wizard-step');
    const prevBtn      = document.getElementById('prev-btn');
    const nextBtn      = document.getElementById('next-btn');
    const submitBtn    = document.getElementById('submit-btn');
    const errorEl      = getOrCreateErrorEl();

    let current = 1;

    function getOrCreateErrorEl() {
        let el = document.getElementById('step-error');
        if (!el) {
            el = document.createElement('div');
            el.id = 'step-error';
            el.className = 'alert alert-warning alert-dismissible py-2';
            el.setAttribute('role', 'alert');
            const close = document.createElement('button');
            close.type = 'button';
            close.className = 'btn-close';
            close.addEventListener('click', () => hideError());
            el.appendChild(close);
            // Insert before navigation row
            const nav = document.querySelector('.wizard-panel.active')?.closest('form');
            if (nav) nav.appendChild(el);
        }
        return el;
    }

    // Make showStepError available globally for the inline <script> blocks.
    window.showStepError = function (msg) {
        errorEl.innerHTML = `<i class="bi bi-exclamation-triangle me-2"></i>${msg}`;
        const close = document.createElement('button');
        close.type = 'button';
        close.className = 'btn-close';
        close.addEventListener('click', () => hideError());
        errorEl.appendChild(close);
        errorEl.style.display = 'block';
    };

    function hideError() {
        errorEl.style.display = 'none';
    }

    function goTo(n) {
        panels.forEach(p => p.classList.remove('active'));
        stepEls.forEach(s => {
            const sn = parseInt(s.dataset.step, 10);
            s.classList.remove('active', 'completed');
            if (sn === n)  s.classList.add('active');
            if (sn < n)    s.classList.add('completed');
        });
        const panel = document.querySelector(`.wizard-panel[data-panel="${n}"]`);
        if (panel) panel.classList.add('active');

        prevBtn.style.display  = n === 1 ? 'none' : '';
        nextBtn.style.display  = n === totalSteps ? 'none' : '';
        submitBtn.style.display = n === totalSteps ? '' : 'none';

        hideError();
        current = n;

        // Scroll the card into view smoothly on mobile
        panel?.closest('.card')?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }

    nextBtn.addEventListener('click', () => {
        if (!validateStep(current)) return;
        if (current < totalSteps) goTo(current + 1);
    });

    prevBtn.addEventListener('click', () => {
        if (current > 1) goTo(current - 1);
    });

    goTo(1);
}
