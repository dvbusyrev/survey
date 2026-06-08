(function () {
    if (window.__adminAnswersListPageLoaded) {
        return;
    }

    window.__adminAnswersListPageLoaded = true;

    const TOOLTIP_OFFSET_X = 12;
    const TOOLTIP_OFFSET_Y = 14;
    let tooltip = null;
    let activeRow = null;
    let latestX = 0;
    let latestY = 0;
    let frameId = 0;

    function ensureTooltip() {
        if (tooltip) {
            return tooltip;
        }

        tooltip = document.createElement('div');
        tooltip.className = 'answers-page__cursor-tooltip';
        tooltip.textContent = 'Смотреть';
        tooltip.setAttribute('aria-hidden', 'true');
        document.body.appendChild(tooltip);
        return tooltip;
    }

    function applyTooltipPosition() {
        frameId = 0;
        if (!activeRow || !tooltip) {
            return;
        }

        tooltip.style.transform = `translate3d(${latestX + TOOLTIP_OFFSET_X}px, ${latestY + TOOLTIP_OFFSET_Y}px, 0)`;
    }

    function queueTooltipPosition(event) {
        if (activeRow && !activeRow.isConnected) {
            hideTooltip();
            return;
        }

        latestX = event.clientX;
        latestY = event.clientY;

        if (!frameId) {
            frameId = window.requestAnimationFrame(applyTooltipPosition);
        }
    }

    function showTooltip(row, event) {
        activeRow = row;
        ensureTooltip().classList.add('is-visible');
        queueTooltipPosition(event);
    }

    function hideTooltip() {
        activeRow = null;
        if (frameId) {
            window.cancelAnimationFrame(frameId);
            frameId = 0;
        }

        if (tooltip) {
            tooltip.classList.remove('is-visible');
            tooltip.style.transform = 'translate3d(-9999px, -9999px, 0)';
        }
    }

    document.addEventListener('mouseover', (event) => {
        const row = event.target.closest('.answers-page[data-page="answers-list"] .answers-page__row');
        if (!row || activeRow === row) {
            return;
        }

        showTooltip(row, event);
    });

    document.addEventListener('mousemove', (event) => {
        if (!activeRow) {
            return;
        }

        queueTooltipPosition(event);
    });

    document.addEventListener('mouseout', (event) => {
        if (!activeRow || activeRow.contains(event.relatedTarget)) {
            return;
        }

        hideTooltip();
    });
})();
