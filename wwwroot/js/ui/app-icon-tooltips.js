(() => {
    if (window.__appIconTooltipsLoaded) {
        return;
    }

    window.__appIconTooltipsLoaded = true;

    const EDGE_GAP = 8;
    let activeIcon = null;
    let frameId = 0;

    function getBoundaryRect(icon) {
        const boundary = icon.closest('.app-page, .modal-content, #content_admin, .content-wrapper');
        const viewportRect = {
            top: 0,
            right: window.innerWidth,
            bottom: window.innerHeight,
            left: 0
        };
        if (!boundary) {
            return viewportRect;
        }

        const boundaryRect = boundary.getBoundingClientRect();
        const visibleRect = {
            top: Math.max(viewportRect.top, boundaryRect.top),
            right: Math.min(viewportRect.right, boundaryRect.right),
            bottom: Math.min(viewportRect.bottom, boundaryRect.bottom),
            left: Math.max(viewportRect.left, boundaryRect.left)
        };

        return visibleRect.right > visibleRect.left && visibleRect.bottom > visibleRect.top
            ? visibleRect
            : viewportRect;
    }

    function resetTooltip(tooltip) {
        tooltip.style.setProperty('--icon-tooltip-shift-x', '0px');
        tooltip.classList.remove('icon-tooltip--below');
    }

    function syncTooltipPosition(icon) {
        frameId = 0;
        if (!icon || !icon.isConnected) {
            activeIcon = null;
            return;
        }

        const tooltip = icon.querySelector('.icon-tooltip');
        if (!tooltip) {
            return;
        }

        resetTooltip(tooltip);

        const boundaryRect = getBoundaryRect(icon);
        const iconRect = icon.getBoundingClientRect();
        let tooltipRect = tooltip.getBoundingClientRect();
        if (tooltipRect.top < boundaryRect.top + EDGE_GAP) {
            const availableAbove = iconRect.top - boundaryRect.top;
            const availableBelow = boundaryRect.bottom - iconRect.bottom;
            if (availableBelow > availableAbove) {
                tooltip.classList.add('icon-tooltip--below');
                tooltipRect = tooltip.getBoundingClientRect();
            }
        }

        const minLeft = boundaryRect.left + EDGE_GAP;
        const maxRight = boundaryRect.right - EDGE_GAP;
        let shiftX = 0;

        if (tooltipRect.left < minLeft) {
            shiftX = minLeft - tooltipRect.left;
        } else if (tooltipRect.right > maxRight) {
            shiftX = maxRight - tooltipRect.right;
        }

        tooltip.style.setProperty('--icon-tooltip-shift-x', `${shiftX}px`);
    }

    function queueSync(icon) {
        activeIcon = icon;
        if (frameId) {
            window.cancelAnimationFrame(frameId);
        }

        frameId = window.requestAnimationFrame(() => syncTooltipPosition(icon));
    }

    function clearIcon(icon) {
        const tooltip = icon?.querySelector?.('.icon-tooltip');
        if (tooltip) {
            resetTooltip(tooltip);
        }

        if (activeIcon === icon) {
            activeIcon = null;
        }
    }

    document.addEventListener('mouseover', (event) => {
        const icon = event.target.closest('.icon-container');
        if (!icon || icon.contains(event.relatedTarget) || !icon.querySelector('.icon-tooltip')) {
            return;
        }

        queueSync(icon);
    });

    document.addEventListener('mouseout', (event) => {
        const icon = event.target.closest('.icon-container');
        if (!icon || icon.contains(event.relatedTarget)) {
            return;
        }

        clearIcon(icon);
    });

    document.addEventListener('focusin', (event) => {
        const icon = event.target.closest('.icon-container');
        if (!icon || !icon.querySelector('.icon-tooltip')) {
            return;
        }

        queueSync(icon);
    });

    document.addEventListener('focusout', (event) => {
        const icon = event.target.closest('.icon-container');
        if (!icon) {
            return;
        }

        clearIcon(icon);
    });

    window.addEventListener('resize', () => {
        if (activeIcon) {
            queueSync(activeIcon);
        }
    });
})();
