(function () {
    if (window.__appThemeCoreLoaded) {
        if (typeof window.reapplyCurrentThemeSettings === 'function') {
            window.reapplyCurrentThemeSettings();
        }
        return;
    }

    window.__appThemeCoreLoaded = true;

    const DEFAULTS = {
        fontColor: '#343D4B',
        backgroundColor: '#B2A8FF',
        gradientEnabled: false,
        gradientStartColor: '#B2A8FF',
        gradientEndColor: '#B2A8FF',
        effectSnow: false,
        effectFireworks: false,
        effectGrass: false,
        effectRain: false,
        backgroundImageDataUrl: '',
        backgroundImageFileName: '',
        backgroundImageOpacity: 35,
        softLightenPercent: 0,
        headerDarkenPercent: 42,
        footerDarkenPercent: 42,
        buttonDarkenPercent: 42,
        buttonStrongDarkenPercent: 50,
        surfaceTintOpacityPercent: 59
    };
    const LEGACY_PERSISTED_THEME_STORAGE_KEY = 'app-theme-settings';
    const LIGHT_TEXT_LUMINANCE_THRESHOLD = 0.52;
    let effectCleanup = [];
    let pendingEffectRender = false;
    let activeEffectsSignature = '';
    let activeThemeVariablesSignature = '';
    let pendingChromeTextColorSync = false;

    function clampOpacity(value) {
        const numericValue = Number.parseInt(value, 10);
        if (!Number.isFinite(numericValue)) {
            return DEFAULTS.backgroundImageOpacity;
        }

        return Math.max(0, Math.min(100, numericValue));
    }

    function clampPercent(value, fallback) {
        const numericValue = Number.parseInt(value, 10);
        if (!Number.isFinite(numericValue)) {
            return fallback;
        }

        return Math.max(0, Math.min(100, numericValue));
    }

    function normalizeHexColor(value, fallback) {
        const normalized = String(value || '').trim().toUpperCase();
        return /^#[0-9A-F]{6}$/.test(normalized) ? normalized : fallback;
    }

    function normalizeImageDataUrl(value) {
        return String(value || '').trim();
    }

    function buildBackgroundImageCssValue(value) {
        const normalized = normalizeImageDataUrl(value);
        return normalized ? `url("${normalized}")` : 'none';
    }

    function getImageSignature(value) {
        const normalized = normalizeImageDataUrl(value);
        if (!normalized) {
            return '';
        }

        return [
            normalized.length,
            normalized.slice(0, 64),
            normalized.slice(-64)
        ].join(':');
    }

    function buildThemeVariablesSignature(settings) {
        return JSON.stringify({
            fontColor: settings.fontColor,
            backgroundColor: settings.backgroundColor,
            backgroundImageDataUrl: getImageSignature(settings.backgroundImageDataUrl),
            backgroundImageOpacity: settings.backgroundImageOpacity,
            softLightenPercent: settings.softLightenPercent,
            headerDarkenPercent: settings.headerDarkenPercent,
            footerDarkenPercent: settings.footerDarkenPercent,
            buttonDarkenPercent: settings.buttonDarkenPercent,
            surfaceTintOpacityPercent: settings.surfaceTintOpacityPercent
        });
    }

    function toCamelThemeSettings(raw) {
        if (!raw || typeof raw !== 'object') {
            return { ...DEFAULTS };
        }

        return {
            fontColor: normalizeHexColor(raw.fontColor || raw.FontColor, DEFAULTS.fontColor),
            backgroundColor: normalizeHexColor(raw.backgroundColor || raw.BackgroundColor, DEFAULTS.backgroundColor),
            gradientEnabled: false,
            gradientStartColor: normalizeHexColor(raw.gradientStartColor || raw.GradientStartColor, DEFAULTS.gradientStartColor),
            gradientEndColor: normalizeHexColor(raw.gradientEndColor || raw.GradientEndColor, DEFAULTS.gradientEndColor),
            effectSnow: Boolean(raw.effectSnow ?? raw.EffectSnow),
            effectFireworks: Boolean(raw.effectFireworks ?? raw.EffectFireworks),
            effectGrass: Boolean(raw.effectGrass ?? raw.EffectGrass),
            effectRain: Boolean(raw.effectRain ?? raw.EffectRain),
            backgroundImageDataUrl: normalizeImageDataUrl(raw.backgroundImageDataUrl || raw.BackgroundImageDataUrl),
            backgroundImageFileName: String(raw.backgroundImageFileName || raw.BackgroundImageFileName || '').trim(),
            backgroundImageOpacity: clampOpacity(raw.backgroundImageOpacity ?? raw.BackgroundImageOpacity),
            softLightenPercent: clampPercent(raw.softLightenPercent ?? raw.SoftLightenPercent, DEFAULTS.softLightenPercent),
            headerDarkenPercent: clampPercent(raw.headerDarkenPercent ?? raw.HeaderDarkenPercent, DEFAULTS.headerDarkenPercent),
            footerDarkenPercent: clampPercent(raw.footerDarkenPercent ?? raw.FooterDarkenPercent, DEFAULTS.footerDarkenPercent),
            buttonDarkenPercent: clampPercent(raw.buttonDarkenPercent ?? raw.ButtonDarkenPercent, DEFAULTS.buttonDarkenPercent),
            buttonStrongDarkenPercent: DEFAULTS.buttonStrongDarkenPercent,
            surfaceTintOpacityPercent: clampPercent(raw.surfaceTintOpacityPercent ?? raw.SurfaceTintOpacityPercent, DEFAULTS.surfaceTintOpacityPercent)
        };
    }

    function updateThemeConfigDom(settings) {
        const themeConfigNode = document.getElementById('app-theme-config');
        if (themeConfigNode) {
            themeConfigNode.textContent = JSON.stringify(settings);
        }

        const inlineThemeNode = document.getElementById('app-theme-inline');
        if (inlineThemeNode) {
            const inlineBackgroundImage = settings.backgroundImageDataUrl
                ? `url('${String(settings.backgroundImageDataUrl).replace(/'/g, "\\'")}')`
                : 'none';
            const inlineImageOpacity = `${settings.backgroundImageOpacity / 100}`;
            inlineThemeNode.textContent = `
        :root {
            --app-theme-font-color: ${settings.fontColor};
            --app-theme-background-color: ${settings.backgroundColor};
            --app-theme-background-start-color: ${settings.backgroundColor};
            --app-theme-background-end-color: ${settings.backgroundColor};
            --app-theme-gradient-opacity: 0;
            --app-theme-background-image: ${inlineBackgroundImage};
            --app-theme-background-image-opacity: ${inlineImageOpacity};
        }

        body,
        .page-container,
        .admin-container,
        .content-wrapper {
            background: transparent !important;
        }

        #app-theme-background {
            background: ${settings.backgroundColor} !important;
        }

        #app-theme-background::before {
            background: none !important;
            opacity: 0 !important;
        }
    `;
        }
    }

    function persistThemeSettings(rawSettings) {
        const settings = toCamelThemeSettings(rawSettings);
        updateThemeConfigDom(settings);
        return settings;
    }

    function hexToRgb(hex) {
        const normalized = normalizeHexColor(hex, '#000000');
        return {
            red: Number.parseInt(normalized.slice(1, 3), 16),
            green: Number.parseInt(normalized.slice(3, 5), 16),
            blue: Number.parseInt(normalized.slice(5, 7), 16)
        };
    }

    function rgbToHex(red, green, blue) {
        const clampChannel = (value) => {
            const rounded = Math.max(0, Math.min(255, Math.round(value)));
            return rounded.toString(16).padStart(2, '0').toUpperCase();
        };

        return `#${clampChannel(red)}${clampChannel(green)}${clampChannel(blue)}`;
    }

    function rgbToHsl(red, green, blue) {
        const normalizedRed = red / 255;
        const normalizedGreen = green / 255;
        const normalizedBlue = blue / 255;
        const max = Math.max(normalizedRed, normalizedGreen, normalizedBlue);
        const min = Math.min(normalizedRed, normalizedGreen, normalizedBlue);
        const lightness = (max + min) / 2;

        if (Math.abs(max - min) < Number.EPSILON) {
            return { hue: 0, saturation: 0, lightness: lightness * 100 };
        }

        const delta = max - min;
        const saturation = lightness > 0.5
            ? delta / (2 - max - min)
            : delta / (max + min);

        let hue;
        switch (max) {
        case normalizedRed:
            hue = ((normalizedGreen - normalizedBlue) / delta) + (normalizedGreen < normalizedBlue ? 6 : 0);
            break;
        case normalizedGreen:
            hue = ((normalizedBlue - normalizedRed) / delta) + 2;
            break;
        default:
            hue = ((normalizedRed - normalizedGreen) / delta) + 4;
            break;
        }

        return {
            hue: hue * 60,
            saturation: saturation * 100,
            lightness: lightness * 100
        };
    }

    function normalizeHue(hue) {
        const normalized = hue % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    function hueToRgb(primary, secondary, hue) {
        let adjustedHue = hue;
        if (adjustedHue < 0) {
            adjustedHue += 1;
        } else if (adjustedHue > 1) {
            adjustedHue -= 1;
        }

        if (adjustedHue < 1 / 6) {
            return primary + ((secondary - primary) * 6 * adjustedHue);
        }

        if (adjustedHue < 1 / 2) {
            return secondary;
        }

        if (adjustedHue < 2 / 3) {
            return primary + ((secondary - primary) * ((2 / 3) - adjustedHue) * 6);
        }

        return primary;
    }

    function hslToRgb(hue, saturation, lightness) {
        const normalizedHue = normalizeHue(hue) / 360;
        const normalizedSaturation = Math.max(0, Math.min(100, saturation)) / 100;
        const normalizedLightness = Math.max(0, Math.min(100, lightness)) / 100;

        if (normalizedSaturation === 0) {
            const channel = normalizedLightness * 255;
            return { red: channel, green: channel, blue: channel };
        }

        const secondary = normalizedLightness < 0.5
            ? normalizedLightness * (1 + normalizedSaturation)
            : normalizedLightness + normalizedSaturation - (normalizedLightness * normalizedSaturation);
        const primary = (2 * normalizedLightness) - secondary;

        return {
            red: 255 * hueToRgb(primary, secondary, normalizedHue + (1 / 3)),
            green: 255 * hueToRgb(primary, secondary, normalizedHue),
            blue: 255 * hueToRgb(primary, secondary, normalizedHue - (1 / 3))
        };
    }

    function mixHexColors(primaryHex, secondaryHex, secondaryWeight) {
        const primary = hexToRgb(primaryHex);
        const secondary = hexToRgb(secondaryHex);
        const weight = Math.max(0, Math.min(1, Number(secondaryWeight) || 0));
        const primaryWeight = 1 - weight;

        return rgbToHex(
            primary.red * primaryWeight + secondary.red * weight,
            primary.green * primaryWeight + secondary.green * weight,
            primary.blue * primaryWeight + secondary.blue * weight
        );
    }

    function adjustBrightnessFromMidpoint(baseHex, percent, fallback) {
        const value = clampPercent(percent, fallback);
        if (value === 50) {
            return baseHex;
        }

        if (value < 50) {
            return mixHexColors(baseHex, '#000000', (50 - value) / 50);
        }

        return mixHexColors(baseHex, '#FFFFFF', (value - 50) / 50);
    }

    function hexToRgba(hex, alpha) {
        const color = hexToRgb(hex);
        const safeAlpha = Math.max(0, Math.min(1, Number(alpha) || 0));
        return `rgba(${color.red}, ${color.green}, ${color.blue}, ${safeAlpha})`;
    }

    function toLinearChannel(channel) {
        const normalized = channel / 255;
        return normalized <= 0.03928
            ? normalized / 12.92
            : ((normalized + 0.055) / 1.055) ** 2.4;
    }

    function getRelativeLuminance(hex) {
        const color = hexToRgb(hex);
        const red = toLinearChannel(color.red);
        const green = toLinearChannel(color.green);
        const blue = toLinearChannel(color.blue);
        return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
    }

    function getContrastRatio(firstHex, secondHex) {
        const firstLuminance = getRelativeLuminance(firstHex);
        const secondLuminance = getRelativeLuminance(secondHex);
        const lighter = Math.max(firstLuminance, secondLuminance);
        const darker = Math.min(firstLuminance, secondLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    function getContrastTextColor(backgroundHex) {
        return getRelativeLuminance(backgroundHex) > LIGHT_TEXT_LUMINANCE_THRESHOLD ? '#111827' : '#FFFFFF';
    }

    function getMutedContrastTextColor(textColor) {
        return textColor === '#FFFFFF'
            ? 'rgba(255, 255, 255, 0.86)'
            : 'rgba(17, 24, 39, 0.82)';
    }

    function shiftHexColor(sourceHex, { hueDelta = 0, saturationDelta = 0, lightnessDelta = 0 } = {}) {
        const { red, green, blue } = hexToRgb(sourceHex);
        const { hue, saturation, lightness } = rgbToHsl(red, green, blue);
        const shiftedRgb = hslToRgb(
            hue + hueDelta,
            saturation + saturationDelta,
            lightness + lightnessDelta
        );

        return rgbToHex(shiftedRgb.red, shiftedRgb.green, shiftedRgb.blue);
    }

    function buildThemePalette(settings) {
        const backgroundBase = normalizeHexColor(settings.backgroundColor, DEFAULTS.backgroundColor);
        const fontColor = normalizeHexColor(settings.fontColor, DEFAULTS.fontColor);
        const accentColor = adjustBrightnessFromMidpoint(backgroundBase, settings.buttonDarkenPercent, DEFAULTS.buttonDarkenPercent);
        const accentStrong = accentColor;
        const accentSoft = mixHexColors(backgroundBase, '#FFFFFF', 0.16);
        const accentSubtle = mixHexColors(backgroundBase, '#FFFFFF', 0.08);
        const headerColor = adjustBrightnessFromMidpoint(backgroundBase, settings.headerDarkenPercent, DEFAULTS.headerDarkenPercent);
        const footerColor = adjustBrightnessFromMidpoint(backgroundBase, settings.footerDarkenPercent, DEFAULTS.footerDarkenPercent);
        const headerStrongColor = headerColor;
        const footerStrongColor = footerColor;
        const buttonColor = accentColor;
        const buttonStrongColor = accentStrong;
        const backgroundStart = backgroundBase;
        const backgroundEnd = backgroundBase;
        const surfaceColor = adjustBrightnessFromMidpoint(backgroundBase, settings.surfaceTintOpacityPercent, DEFAULTS.surfaceTintOpacityPercent);
        const contrastTextColor = getContrastTextColor(buttonColor);
        const contrastMutedTextColor = getMutedContrastTextColor(contrastTextColor);
        const headerTextColor = getContrastTextColor(headerColor);
        const footerTextColor = getContrastTextColor(footerColor);
        const detailTextColor = getContrastTextColor(surfaceColor);
        const detailBorderColor = hexToRgba(detailTextColor, 0.18);

        return {
            backgroundStart,
            backgroundEnd,
            accentSoft,
            accentColor,
            accentStrong,
            accentSubtle,
            contrastTextColor,
            contrastMutedTextColor,
            headerColor,
            headerStrongColor,
            headerTextColor,
            headerMutedTextColor: getMutedContrastTextColor(headerTextColor),
            footerColor,
            footerStrongColor,
            footerTextColor,
            footerMutedTextColor: getMutedContrastTextColor(footerTextColor),
            detailColor: surfaceColor,
            detailTextColor,
            detailMutedTextColor: getMutedContrastTextColor(detailTextColor),
            buttonColor,
            buttonStrongColor,
            textMain: fontColor,
            textSecondary: fontColor,
            textLight: fontColor,
            border: hexToRgba(fontColor, 0.12),
            borderDark: hexToRgba(fontColor, 0.18),
            shadowSm: '0 1px 2px rgba(15, 23, 42, 0.08)',
            shadowMd: '0 8px 20px rgba(15, 23, 42, 0.12)',
            shadowLg: '0 16px 34px rgba(15, 23, 42, 0.16)',
            tableHeaderBackground: surfaceColor,
            tableHeaderBorder: detailBorderColor,
            navHoverBackground: surfaceColor,
            navActiveBackground: surfaceColor,
            iconHoverBackground: surfaceColor,
            iconHoverBorder: detailBorderColor,
            iconHoverColor: detailTextColor,
            snowColor: '#FFFFFF',
            snowCoreColor: '#FFFFFF',
            snowEdgeColor: '#E2E8F0',
            snowGlowColor: '#FFFFFF',
            rainColor: getRelativeLuminance(backgroundBase) > 0.42 ? '#64748B' : '#BFDBFE',
            grassColor: '#16A34A',
            grassShadowColor: '#166534'
        };
    }

    function getViewportWidth() {
        if (window.visualViewport?.width) {
            return window.visualViewport.width;
        }

        return window.innerWidth || document.documentElement.clientWidth || 1280;
    }

    function getEffectsRoot() {
        return document.getElementById('app-theme-effects-root');
    }

    function ensureThemeRoots(settings) {
        if (!document.body) {
            return;
        }

        let backgroundRoot = document.getElementById('app-theme-background');
        if (!backgroundRoot) {
            backgroundRoot = document.createElement('div');
            backgroundRoot.id = 'app-theme-background';
            backgroundRoot.setAttribute('aria-hidden', 'true');
            document.body.insertBefore(backgroundRoot, document.body.firstChild);
        }

        let effectsRoot = document.getElementById('app-theme-effects-root');
        if (!effectsRoot) {
            effectsRoot = document.createElement('div');
            effectsRoot.id = 'app-theme-effects-root';
            effectsRoot.setAttribute('aria-hidden', 'true');
            backgroundRoot.insertAdjacentElement('afterend', effectsRoot);
        }

        document.documentElement.style.setProperty('background', settings.backgroundColor, 'important');
        backgroundRoot.style.setProperty('background', settings.backgroundColor, 'important');
    }

    function getForegroundEffectsRoot() {
        let root = document.getElementById('app-theme-foreground-effects-root');
        if (root) {
            return root;
        }

        if (!document.body) {
            return null;
        }

        root = document.createElement('div');
        root.id = 'app-theme-foreground-effects-root';
        root.setAttribute('aria-hidden', 'true');
        document.body.appendChild(root);
        return root;
    }

    function clearEffects() {
        effectCleanup.forEach((cleanup) => {
            try {
                cleanup();
            } catch (error) {
                console.warn('Не удалось очистить эффект темы.', error);
            }
        });
        effectCleanup = [];

        const root = getEffectsRoot();
        if (root) {
            root.replaceChildren();
        }

        const foregroundRoot = document.getElementById('app-theme-foreground-effects-root');
        if (foregroundRoot) {
            foregroundRoot.replaceChildren();
        }

        activeEffectsSignature = '';
    }

    function createLayer(className, useForegroundRoot = false) {
        const root = useForegroundRoot ? getForegroundEffectsRoot() : getEffectsRoot();
        if (!root) {
            return null;
        }

        const layer = document.createElement('div');
        layer.className = `app-theme-effect-layer ${className}`;
        root.appendChild(layer);
        return layer;
    }

    function createSnowEffect(palette) {
        const backLayer = createLayer('app-theme-effect-layer--snow app-theme-effect-layer--snow-back');
        const frontLayer = createLayer('app-theme-effect-layer--snow app-theme-effect-layer--snow-front');
        if (!backLayer || !frontLayer) {
            return;
        }

        backLayer.style.backgroundImage = [
            `radial-gradient(circle at 12% 18%, ${hexToRgba(palette.snowColor, 0.34)} 0 0.08rem, transparent 0.1rem)`,
            `radial-gradient(circle at 32% 74%, ${hexToRgba(palette.snowColor, 0.28)} 0 0.06rem, transparent 0.08rem)`,
            `radial-gradient(circle at 48% 26%, ${hexToRgba(palette.snowColor, 0.26)} 0 0.05rem, transparent 0.07rem)`,
            `radial-gradient(circle at 66% 58%, ${hexToRgba(palette.snowColor, 0.3)} 0 0.07rem, transparent 0.09rem)`,
            `radial-gradient(circle at 82% 20%, ${hexToRgba(palette.snowColor, 0.28)} 0 0.06rem, transparent 0.08rem)`,
            `radial-gradient(circle at 92% 78%, ${hexToRgba(palette.snowColor, 0.24)} 0 0.05rem, transparent 0.07rem)`
        ].join(',');
        backLayer.style.backgroundSize = '18rem 18rem';
        backLayer.style.opacity = '0.9';
        backLayer.style.animationDuration = '26s';

        frontLayer.style.backgroundImage = [
            `radial-gradient(circle at 8% 12%, ${hexToRgba(palette.snowCoreColor, 0.6)} 0 0.11rem, transparent 0.13rem)`,
            `radial-gradient(circle at 24% 62%, ${hexToRgba(palette.snowCoreColor, 0.54)} 0 0.09rem, transparent 0.11rem)`,
            `radial-gradient(circle at 44% 30%, ${hexToRgba(palette.snowCoreColor, 0.58)} 0 0.12rem, transparent 0.14rem)`,
            `radial-gradient(circle at 60% 82%, ${hexToRgba(palette.snowCoreColor, 0.52)} 0 0.08rem, transparent 0.1rem)`,
            `radial-gradient(circle at 76% 18%, ${hexToRgba(palette.snowCoreColor, 0.62)} 0 0.1rem, transparent 0.12rem)`,
            `radial-gradient(circle at 94% 54%, ${hexToRgba(palette.snowCoreColor, 0.56)} 0 0.09rem, transparent 0.11rem)`
        ].join(',');
        frontLayer.style.backgroundSize = '14rem 14rem';
        frontLayer.style.opacity = '1';
        frontLayer.style.animationDuration = '18s';
    }

    function createRainEffect(palette) {
        const layer = createLayer('app-theme-effect-layer--rain');
        if (!layer) {
            return;
        }

        const dropCount = Math.max(16, Math.min(28, Math.round(getViewportWidth() / 70)));
        for (let index = 0; index < dropCount; index += 1) {
            const drop = document.createElement('span');
            drop.className = 'app-theme-raindrop';
            drop.style.left = `${Math.random() * 100}%`;
            drop.style.opacity = `${0.42 + Math.random() * 0.28}`;
            drop.style.height = `${2 + Math.random() * 2.2}rem`;
            drop.style.animationDuration = `${0.82 + Math.random() * 0.38}s`;
            drop.style.animationDelay = `${Math.random() * 1.2}s`;
            drop.style.setProperty('--app-theme-rain-width', `${0.06 + Math.random() * 0.04}rem`);
            drop.style.setProperty('--app-theme-rain-color', hexToRgba(palette.rainColor, 0.56 + Math.random() * 0.2));
            layer.appendChild(drop);
        }
    }

    function createGrassEffect(palette) {
        const layer = createLayer('app-theme-effect-layer--grass', true);
        if (!layer) {
            return;
        }

        const baseClumpCount = Math.max(10, Math.min(18, Math.round(getViewportWidth() / 105)));
        const clumpCount = Math.max(25, Math.min(45, Math.round(baseClumpCount)));
        for (let index = 0; index < clumpCount; index += 1) {
            const basePosition = clumpCount <= 1 ? 50 : (index / (clumpCount - 1)) * 100;
            const leafCount = 2 + Math.floor(Math.random() * 2);

            for (let leafIndex = 0; leafIndex < leafCount; leafIndex += 1) {
                const blade = document.createElement('span');
                blade.className = 'app-theme-grass-blade';
                const jitter = (Math.random() - 0.5) * 1.4;
                const spread = (leafIndex - ((leafCount - 1) / 2)) * 0.45;
                blade.style.left = `${Math.max(0, Math.min(100, basePosition + jitter + spread))}%`;
                blade.style.width = `${0.24 + Math.random() * 0.18}rem`;
                blade.style.height = `${2.4 + Math.random() * 2.4}rem`;
                blade.style.opacity = `${0.84 + Math.random() * 0.12}`;
                blade.style.animationDuration = `${3.8 + Math.random() * 1.6}s`;
                blade.style.animationDelay = `${Math.random() * 1.1}s`;

                const tiltSeed = (leafIndex - ((leafCount - 1) / 2));
                const tiltStart = (-10 + (tiltSeed * 6)) - (Math.random() * 2);
                const tiltEnd = (8 + (tiltSeed * 5)) + (Math.random() * 2);
                blade.style.setProperty('--app-theme-grass-tilt-start', `${tiltStart}deg`);
                blade.style.setProperty('--app-theme-grass-tilt-end', `${tiltEnd}deg`);
                blade.style.setProperty('--app-theme-grass-light', hexToRgba(palette.grassColor, 0.9));
                blade.style.setProperty('--app-theme-grass-dark', hexToRgba(palette.grassShadowColor, 0.84));
                layer.appendChild(blade);
            }
        }
    }

    function bindFireworksEffect(palette) {
        const root = getEffectsRoot();
        if (!root) {
            return;
        }

        const handler = (event) => {
            if (typeof event.button === 'number' && event.button !== 0) {
                return;
            }

            const colors = [
                palette.accentColor,
                palette.accentStrong,
                '#FBBF24',
                '#F472B6',
                '#60A5FA',
                '#34D399'
            ];

            const flash = document.createElement('span');
            flash.className = 'app-theme-firework-flash';
            flash.style.left = `${event.clientX}px`;
            flash.style.top = `${event.clientY}px`;
            flash.style.setProperty('--app-theme-firework-flash-color', hexToRgba(palette.accentSubtle, 0.92));
            root.appendChild(flash);
            window.setTimeout(() => flash.remove(), 380);

            const ring = document.createElement('span');
            ring.className = 'app-theme-firework-ring';
            ring.style.left = `${event.clientX}px`;
            ring.style.top = `${event.clientY}px`;
            ring.style.setProperty('--app-theme-firework-ring-color', hexToRgba(palette.accentSubtle, 0.74));
            root.appendChild(ring);
            window.setTimeout(() => ring.remove(), 700);

            const burstGroups = [
                { count: 12, minDistance: 2.2, maxDistance: 4.1, minSize: 0.16, maxSize: 0.24, className: '' },
                { count: 8, minDistance: 1.1, maxDistance: 2.2, minSize: 0.1, maxSize: 0.16, className: ' app-theme-firework-particle--inner' }
            ];

            burstGroups.forEach((group) => {
                for (let index = 0; index < group.count; index += 1) {
                    const particle = document.createElement('span');
                    particle.className = `app-theme-firework-particle${group.className}`;
                    particle.style.left = `${event.clientX}px`;
                    particle.style.top = `${event.clientY}px`;
                    particle.style.setProperty('--app-theme-particle-color', colors[Math.floor(Math.random() * colors.length)]);
                    particle.style.setProperty('--app-theme-particle-size', `${group.minSize + Math.random() * (group.maxSize - group.minSize)}rem`);
                    particle.style.setProperty('--app-theme-particle-duration', `${0.56 + Math.random() * 0.28}s`);
                    particle.style.setProperty('--app-theme-particle-delay', `${Math.random() * 0.05}s`);

                    const angle = ((Math.PI * 2 * index) / group.count) + ((Math.random() - 0.5) * 0.24);
                    const distance = group.minDistance + Math.random() * (group.maxDistance - group.minDistance);
                    particle.style.setProperty('--app-theme-x', `${Math.cos(angle) * distance}rem`);
                    particle.style.setProperty('--app-theme-y', `${Math.sin(angle) * distance}rem`);

                    root.appendChild(particle);
                    window.setTimeout(() => particle.remove(), 960);
                }
            });
        };

        effectCleanup.push(() => {
            document.removeEventListener('pointerdown', handler);
        });
        document.addEventListener('pointerdown', handler, { passive: true });
    }

    function buildEffectsSignature(settings) {
        return JSON.stringify({
            snow: Boolean(settings.effectSnow),
            rain: Boolean(settings.effectRain),
            grass: Boolean(settings.effectGrass),
            fireworks: Boolean(settings.effectFireworks)
        });
    }

    function hasRequiredEffectLayers(settings) {
        const root = getEffectsRoot();
        const foregroundRoot = document.getElementById('app-theme-foreground-effects-root');

        if (settings.effectSnow && !root?.querySelector('.app-theme-effect-layer--snow')) {
            return false;
        }

        if (settings.effectRain && !root?.querySelector('.app-theme-effect-layer--rain')) {
            return false;
        }

        if (settings.effectGrass && !foregroundRoot?.querySelector('.app-theme-effect-layer--grass')) {
            return false;
        }

        if (settings.effectFireworks && effectCleanup.length === 0) {
            return false;
        }

        return true;
    }

    function renderEffects(settings, palette) {
        if (document.readyState === 'loading' && !getEffectsRoot()) {
            pendingEffectRender = true;
            return;
        }

        const nextEffectsSignature = buildEffectsSignature(settings);
        if (nextEffectsSignature === activeEffectsSignature && hasRequiredEffectLayers(settings)) {
            pendingEffectRender = false;
            return;
        }

        clearEffects();
        activeEffectsSignature = nextEffectsSignature;

        if (
            settings.effectSnow
        ) {
            createSnowEffect(palette);
        }

        if (settings.effectRain) {
            createRainEffect(palette);
        }

        if (settings.effectGrass) {
            createGrassEffect(palette);
        }

        if (settings.effectFireworks) {
            bindFireworksEffect(palette);
        }

        pendingEffectRender = false;
    }

    function applyThemeVariables(settings, palette) {
        const rootStyle = document.documentElement.style;

        rootStyle.setProperty('--app-theme-font-color', settings.fontColor);
        rootStyle.setProperty('--app-theme-background-color', settings.backgroundColor);
        rootStyle.setProperty('--app-theme-background-start-color', palette.backgroundStart);
        rootStyle.setProperty('--app-theme-background-end-color', palette.backgroundEnd);
        rootStyle.setProperty('--app-theme-accent-soft-color', palette.accentSoft);
        rootStyle.setProperty('--app-theme-accent-color', palette.accentColor);
        rootStyle.setProperty('--app-theme-accent-strong-color', palette.accentStrong);
        rootStyle.setProperty('--app-theme-accent-subtle-color', palette.accentSubtle);
        rootStyle.setProperty('--app-theme-accent-text-color', palette.contrastTextColor);
        rootStyle.setProperty('--app-theme-accent-text-muted-color', palette.contrastMutedTextColor);
        rootStyle.setProperty('--app-theme-header-color', palette.headerColor);
        rootStyle.setProperty('--app-theme-header-strong-color', palette.headerStrongColor);
        rootStyle.setProperty('--app-theme-header-text-color', palette.headerTextColor);
        rootStyle.setProperty('--app-theme-header-text-muted-color', palette.headerMutedTextColor);
        rootStyle.setProperty('--app-theme-footer-color', palette.footerColor);
        rootStyle.setProperty('--app-theme-footer-strong-color', palette.footerStrongColor);
        rootStyle.setProperty('--app-theme-footer-text-color', palette.footerTextColor);
        rootStyle.setProperty('--app-theme-footer-text-muted-color', palette.footerMutedTextColor);
        rootStyle.setProperty('--app-theme-detail-color', palette.detailColor);
        rootStyle.setProperty('--app-theme-detail-text-color', palette.detailTextColor);
        rootStyle.setProperty('--app-theme-detail-text-muted-color', palette.detailMutedTextColor);
        rootStyle.setProperty('--app-theme-button-color', palette.buttonColor);
        rootStyle.setProperty('--app-theme-button-strong-color', palette.buttonStrongColor);
        rootStyle.setProperty('--app-theme-button-text-color', palette.contrastTextColor);
        rootStyle.setProperty('--app-theme-gradient-opacity', '0');
        rootStyle.setProperty('--primary', palette.accentColor);
        rootStyle.setProperty('--primary-light', palette.accentSoft);
        rootStyle.setProperty('--primary-dark', palette.accentStrong);
        rootStyle.setProperty('--primary-extra-light', palette.accentSubtle);
        rootStyle.setProperty('--text-main', palette.textMain);
        rootStyle.setProperty('--text-secondary', palette.textSecondary);
        rootStyle.setProperty('--text-light', palette.textLight);
        rootStyle.setProperty('--border', palette.border);
        rootStyle.setProperty('--border-dark', palette.borderDark);
        rootStyle.setProperty('--shadow-sm', palette.shadowSm);
        rootStyle.setProperty('--shadow-md', palette.shadowMd);
        rootStyle.setProperty('--shadow-lg', palette.shadowLg);
        rootStyle.setProperty('--app-theme-table-header-background', palette.tableHeaderBackground);
        rootStyle.setProperty('--app-theme-table-header-border', palette.tableHeaderBorder);
        rootStyle.setProperty('--app-theme-nav-hover-background', palette.navHoverBackground);
        rootStyle.setProperty('--app-theme-nav-active-background', palette.navActiveBackground);
        rootStyle.setProperty('--app-theme-icon-hover-background', palette.iconHoverBackground);
        rootStyle.setProperty('--app-theme-icon-hover-border', palette.iconHoverBorder);
        rootStyle.setProperty('--app-theme-icon-hover-color', palette.iconHoverColor);

        applyThemeBackgroundVariables(settings);
    }

    function applyThemeBackgroundVariables(settings) {
        const rootStyle = document.documentElement.style;
        const backgroundImage = buildBackgroundImageCssValue(settings.backgroundImageDataUrl);
        const backgroundImageOpacity = `${settings.backgroundImageOpacity / 100}`;

        if (rootStyle.getPropertyValue('--app-theme-background-image').trim() !== backgroundImage) {
            rootStyle.setProperty('--app-theme-background-image', backgroundImage);
        }

        if (rootStyle.getPropertyValue('--app-theme-background-image-opacity').trim() !== backgroundImageOpacity) {
            rootStyle.setProperty('--app-theme-background-image-opacity', backgroundImageOpacity);
        }

        document.querySelectorAll('body, .page-container, .admin-container, .content-wrapper').forEach((element) => {
            if (element instanceof HTMLElement) {
                element.style.setProperty('background', 'transparent', 'important');
            }
        });

        const backgroundElement = document.getElementById('app-theme-background');
        if (backgroundElement instanceof HTMLElement) {
            backgroundElement.style.setProperty('background', settings.backgroundColor, 'important');
            backgroundElement.style.setProperty('--app-theme-background-image', backgroundImage);
            backgroundElement.style.setProperty('--app-theme-background-image-opacity', backgroundImageOpacity);
        }
    }

    function syncChromeTextColors(palette) {
        if (!palette || !document.querySelectorAll) {
            return;
        }

        document.querySelectorAll('.app-header').forEach((element) => {
            if (element instanceof HTMLElement) {
                element.style.setProperty('color', palette.headerTextColor, 'important');
            }
        });

        document.querySelectorAll('.header-mode-label').forEach((element) => {
            if (element instanceof HTMLElement) {
                element.style.setProperty('color', palette.headerMutedTextColor, 'important');
            }
        });

        document.querySelectorAll('#chrome-footer, #chrome-footer footer, .page-container > footer').forEach((element) => {
            if (element instanceof HTMLElement) {
                element.style.setProperty('color', palette.footerTextColor, 'important');
            }
        });

        document.querySelectorAll('#chrome-footer footer p, .page-container > footer p').forEach((element) => {
            if (element instanceof HTMLElement) {
                element.style.setProperty('color', 'inherit', 'important');
            }
        });
    }

    function queueChromeTextColorSync(palette) {
        syncChromeTextColors(palette);

        if (document.readyState !== 'loading' || pendingChromeTextColorSync) {
            return;
        }

        pendingChromeTextColorSync = true;
        document.addEventListener('DOMContentLoaded', () => {
            pendingChromeTextColorSync = false;
            syncChromeTextColors(window.__appThemePalette || palette);
        }, { once: true });
    }

    function applyThemeSettings(rawSettings) {
        const settings = toCamelThemeSettings(rawSettings);
        ensureThemeRoots(settings);

        const variablesSignature = buildThemeVariablesSignature(settings);
        const canReusePalette = variablesSignature === activeThemeVariablesSignature && window.__appThemePalette;
        const palette = canReusePalette
            ? window.__appThemePalette
            : buildThemePalette(settings);

        if (!canReusePalette) {
            applyThemeVariables(settings, palette);
            activeThemeVariablesSignature = variablesSignature;
        } else {
            applyThemeBackgroundVariables(settings);
        }
        window.__appThemeSettings = settings;
        window.__appThemePalette = palette;
        queueChromeTextColorSync(palette);

        if (document.readyState === 'loading' && !getEffectsRoot()) {
            pendingEffectRender = true;
            return settings;
        }

        renderEffects(settings, palette);
        return settings;
    }

    function reapplyCurrentThemeSettings() {
        return applyThemeSettings(
            window.__appThemeDraftSettings
            || window.__appThemeSavedSettings
            || window.__appThemeSettings
            || DEFAULTS
        );
    }

    function applyInitialThemeSettings() {
        const node = document.getElementById('app-theme-config');
        if (!node || !node.textContent) {
            applyThemeSettings(DEFAULTS);
            return;
        }

        try {
            applyThemeSettings(JSON.parse(node.textContent));
        } catch (error) {
            console.error('Не удалось прочитать настройки темы.', error);
            applyThemeSettings(DEFAULTS);
        }
    }

    window.applyThemeSettings = applyThemeSettings;
    window.reapplyCurrentThemeSettings = reapplyCurrentThemeSettings;
    window.toCamelThemeSettings = toCamelThemeSettings;
    window.persistThemeSettings = persistThemeSettings;

    try {
        window.sessionStorage.removeItem(LEGACY_PERSISTED_THEME_STORAGE_KEY);
    } catch (error) {
        // Ignore storage access issues.
    }

    applyInitialThemeSettings();

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            reapplyCurrentThemeSettings();
            if (pendingEffectRender) {
                renderEffects(window.__appThemeSettings || DEFAULTS, window.__appThemePalette || buildThemePalette(DEFAULTS));
            }
        }, { once: true });
    } else {
        reapplyCurrentThemeSettings();
    }
})();
