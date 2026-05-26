(() => {
    if (window.AppViewportScale) {
        window.AppViewportScale.queueApply();
        return;
    }

    const DEFAULT_ROOT_FONT_SIZE = 149.25;
    const ROOT_FONT_SIZE_BREAKPOINTS = [
        { maxWidth: 900, fontSize: 118 },
        { maxWidth: 1080, fontSize: 126 },
        { maxWidth: 1220, fontSize: 134 },
        { maxWidth: 1366, fontSize: 140 },
        { maxWidth: 1536, fontSize: 145 }
    ];

    let applyFrameId = 0;
    let visualViewportHandlerAttached = false;

    function getCssViewportWidth() {
        if (window.visualViewport?.width) {
            return window.visualViewport.width;
        }

        return window.innerWidth || document.documentElement.clientWidth || 0;
    }

    function getDeviceScaleFactor() {
        const ratio = Number(window.devicePixelRatio || 1);
        return Number.isFinite(ratio) && ratio > 0
            ? ratio
            : 1;
    }

    function getNormalizedViewportWidth() {
        return getCssViewportWidth() * getDeviceScaleFactor();
    }

    function resolveRootFontSize(normalizedWidth) {
        for (const breakpoint of ROOT_FONT_SIZE_BREAKPOINTS) {
            if (normalizedWidth <= breakpoint.maxWidth) {
                return breakpoint.fontSize;
            }
        }

        return DEFAULT_ROOT_FONT_SIZE;
    }

    function applyRootFontSize() {
        applyFrameId = 0;

        const nextFontSize = `${resolveRootFontSize(getNormalizedViewportWidth())}%`;
        if (document.documentElement.style.getPropertyValue('--app-root-font-size') === nextFontSize) {
            return;
        }

        document.documentElement.style.setProperty('--app-root-font-size', nextFontSize);
    }

    function queueApply() {
        if (applyFrameId) {
            window.cancelAnimationFrame(applyFrameId);
        }

        applyFrameId = window.requestAnimationFrame(applyRootFontSize);
    }

    function attachViewportListeners() {
        if (visualViewportHandlerAttached) {
            return;
        }

        const handleViewportChange = () => {
            queueApply();
        };

        window.addEventListener('resize', handleViewportChange, { passive: true });

        if (window.visualViewport) {
            window.visualViewport.addEventListener('resize', handleViewportChange, { passive: true });
            window.visualViewport.addEventListener('scroll', handleViewportChange, { passive: true });
        }

        visualViewportHandlerAttached = true;
    }

    window.AppViewportScale = {
        apply: applyRootFontSize,
        getCssViewportWidth,
        getDeviceScaleFactor,
        getNormalizedViewportWidth,
        queueApply
    };

    attachViewportListeners();

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            applyRootFontSize();
        }, { once: true });
    } else {
        applyRootFontSize();
    }
})();
