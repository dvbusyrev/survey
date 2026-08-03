(function () {
    if (window.SurveyFilterCore) {
        return;
    }

    const MONTH_NAMES = [
        'Январь',
        'Февраль',
        'Март',
        'Апрель',
        'Май',
        'Июнь',
        'Июль',
        'Август',
        'Сентябрь',
        'Октябрь',
        'Ноябрь',
        'Декабрь'
    ];

    const WEEKDAY_NAMES = ['Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб', 'Вс'];

    function pad(value) {
        return String(value).padStart(2, '0');
    }

    function toIso(date) {
        if (!(date instanceof Date) || Number.isNaN(date.getTime())) {
            return '';
        }

        return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
    }

    function parseIso(isoValue) {
        const match = String(isoValue || '').trim().match(/^(\d{4})-(\d{2})-(\d{2})$/);
        if (!match) {
            return null;
        }

        const year = Number.parseInt(match[1], 10);
        const month = Number.parseInt(match[2], 10);
        const day = Number.parseInt(match[3], 10);
        const date = new Date(year, month - 1, day);

        if (Number.isNaN(date.getTime())
            || date.getFullYear() !== year
            || date.getMonth() !== month - 1
            || date.getDate() !== day) {
            return null;
        }

        return date;
    }

    function shiftMonth(sourceDate, monthOffset) {
        const date = sourceDate instanceof Date
            ? new Date(sourceDate.getFullYear(), sourceDate.getMonth(), 1)
            : new Date();
        date.setMonth(date.getMonth() + monthOffset);
        return new Date(date.getFullYear(), date.getMonth(), 1);
    }

    function getMonthBounds(year, monthIndex) {
        const startDate = new Date(year, monthIndex, 1);
        const endDate = new Date(year, monthIndex + 1, 0);

        return {
            start: toIso(startDate),
            end: toIso(endDate)
        };
    }

    function getYearBounds(year) {
        return {
            start: `${year}-01-01`,
            end: `${year}-12-31`
        };
    }

    function getDecadeStart(year) {
        return Math.floor(year / 10) * 10;
    }

    function getDisplayDate(isoValue) {
        if (window.AppDate?.toDisplay) {
            return window.AppDate.toDisplay(isoValue);
        }

        const date = parseIso(isoValue);
        if (!date) {
            return '';
        }

        return `${pad(date.getDate())}.${pad(date.getMonth() + 1)}.${date.getFullYear()}`;
    }

    function compareIso(left, right) {
        if (!left || !right) {
            return 0;
        }

        return left === right ? 0 : (left > right ? 1 : -1);
    }

    function isIsoWithin(isoValue, startIso, endIso) {
        return Boolean(isoValue)
            && (!startIso || compareIso(isoValue, startIso) >= 0)
            && (!endIso || compareIso(isoValue, endIso) <= 0);
    }

    function getRangeDescription(startIso, endIso) {
        if (!startIso || !endIso) {
            return '';
        }

        return `${getDisplayDate(startIso)} - ${getDisplayDate(endIso)}`;
    }

    function getMonthDescription(year, monthIndex) {
        return `${MONTH_NAMES[monthIndex]} ${year}`;
    }

    function getYearDescription(year) {
        return `${year} год`;
    }

    function createElement(tagName, className, textContent) {
        if (typeof window.AppUi?.createElement === 'function') {
            return window.AppUi.createElement(tagName, {
                className,
                text: textContent
            });
        }

        const element = document.createElement(tagName);
        if (className) {
            element.className = className;
        }
        if (textContent !== undefined) {
            element.textContent = textContent;
        }
        return element;
    }

    window.SurveyFilterCore = {
        MONTH_NAMES,
        WEEKDAY_NAMES,
        pad,
        toIso,
        parseIso,
        shiftMonth,
        getMonthBounds,
        getYearBounds,
        getDecadeStart,
        getDisplayDate,
        compareIso,
        isIsoWithin,
        getRangeDescription,
        getMonthDescription,
        getYearDescription,
        createElement
    };
})();
