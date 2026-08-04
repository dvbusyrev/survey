(function () {
    if (window.__adminAnswersListPageLoaded) {
        return;
    }

    window.__adminAnswersListPageLoaded = true;

    const tooltip = window.AppUi.createRowTooltip();

    document.addEventListener('mouseover', (event) => {
        const row = event.target.closest('.answers-page[data-page="answers-list"] .answers-page__row');
        if (!row || tooltip.isActiveRow(row)) {
            return;
        }

        tooltip.show(row, event);
    });

    document.addEventListener('mousemove', (event) => {
        if (!tooltip.hasActiveRow()) {
            return;
        }

        tooltip.move(event);
    });

    document.addEventListener('mouseout', (event) => {
        if (!tooltip.hasActiveRow() || tooltip.activeRowContains(event.relatedTarget)) {
            return;
        }

        tooltip.hide();
    });
})();
