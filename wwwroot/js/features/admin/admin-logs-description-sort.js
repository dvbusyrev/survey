(function () {
    function createDescriptionSortSync(getRoot) {
        let frameId = 0;

        function cancel() {
            if (!frameId) {
                return;
            }

            window.cancelAnimationFrame(frameId);
            frameId = 0;
        }

        function sync() {
            cancel();

            frameId = window.requestAnimationFrame(() => {
                frameId = 0;

                const table = getRoot()?.querySelector('.logs-table');
                const header = table?.querySelector('th.table-col--description.table-sortable');
                const cells = table ? Array.from(table.querySelectorAll('tbody td.table-col--description')) : [];
                if (!table || !header || cells.length === 0) {
                    return;
                }

                const descriptionCell = cells.find((cell) => {
                    const text = (cell.textContent || '').trim();
                    return text.length > 0 && cell.getClientRects().length > 0;
                });
                if (!descriptionCell) {
                    return;
                }

                const range = document.createRange();
                range.selectNodeContents(descriptionCell);
                const textRects = Array.from(range.getClientRects());
                range.detach();

                const textRect = textRects
                    .filter((rect) => rect.width > 0 && rect.height > 0)
                    .reduce((rightmostRect, rect) => (
                        !rightmostRect || rect.right > rightmostRect.right ? rect : rightmostRect
                    ), null);
                if (!textRect) {
                    return;
                }

                const headerRect = header.getBoundingClientRect();
                const headerStyle = window.getComputedStyle(header);
                const headerPaddingLeft = Number.parseFloat(headerStyle.paddingLeft) || 0;
                const markerGap = 10;
                const markerWidth = 12;
                const minMarkerLeft = headerPaddingLeft;
                const maxMarkerLeft = Math.max(minMarkerLeft, headerRect.width - markerWidth);
                const markerLeft = Math.min(
                    Math.max(textRect.right - headerRect.left + markerGap, minMarkerLeft),
                    maxMarkerLeft
                );

                header.style.setProperty('--logs-description-sort-left', `${Math.ceil(markerLeft)}px`);
            });
        }

        return {
            sync,
            cancel
        };
    }

    window.AdminLogsDescriptionSort = {
        createDescriptionSortSync
    };
})();
