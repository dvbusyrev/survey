(function () {
    function normalizeSourceTable(value) {
        return String(value || '').trim().toLowerCase();
    }

    function getEntryKey(logId, sourceTable) {
        return `${Number(logId) || 0}:${normalizeSourceTable(sourceTable)}`;
    }

    function createLogEntryStore(getRoot) {
        let cachedBootstrapText = '';
        let cachedLogsMap = new Map();
        let cachedDetailsPromises = new Map();

        function reset() {
            cachedBootstrapText = '';
            cachedLogsMap = new Map();
            cachedDetailsPromises = new Map();
        }

        function readLogsMap() {
            const bootstrapNode = getRoot()?.querySelector('#logs-page-bootstrap');
            const rawText = bootstrapNode?.textContent?.trim() || '';
            if (!rawText) {
                cachedBootstrapText = '';
                cachedLogsMap = new Map();
                return cachedLogsMap;
            }

            if (rawText === cachedBootstrapText) {
                return cachedLogsMap;
            }

            try {
                const items = JSON.parse(rawText);
                cachedLogsMap = new Map(
                    (Array.isArray(items) ? items : [])
                        .map((item) => [getEntryKey(item.id, item.sourceTable), item])
                        .filter(([, item]) => {
                            const id = Number(item?.id || 0);
                            return Number.isFinite(id) && id > 0;
                        })
                );
                cachedBootstrapText = rawText;
            } catch (error) {
                console.error('Не удалось разобрать данные журнала событий:', error);
                cachedBootstrapText = rawText;
                cachedLogsMap = new Map();
            }

            return cachedLogsMap;
        }

        function getEntry(logId, sourceTable) {
            if (!Number.isFinite(logId) || logId <= 0) {
                return null;
            }

            const logsMap = readLogsMap();
            return logsMap.get(getEntryKey(logId, sourceTable))
                || Array.from(logsMap.values()).find((entry) => Number(entry?.id || 0) === logId)
                || null;
        }

        function hasDetails(entry) {
            return typeof entry?.extraDataJson === 'string' && entry.extraDataJson.trim().length > 0;
        }

        function buildDetailsUrl(logId, entry) {
            const url = new URL(`/logs/details/${encodeURIComponent(String(logId))}`, window.location.origin);
            const currentParams = new URLSearchParams(window.location.search);
            const sourceTable = String(entry?.sourceTable || '').trim();

            ['page', 'sortBy', 'sortDirection'].forEach((name) => {
                const value = currentParams.get(name);
                if (value) {
                    url.searchParams.set(name, value);
                }
            });

            if (sourceTable) {
                url.searchParams.set('sourceTable', sourceTable);
            }

            return url.toString();
        }

        async function loadDetails(logId, sourceTable) {
            const cachedEntry = getEntry(logId, sourceTable);
            if (!cachedEntry || hasDetails(cachedEntry)) {
                return cachedEntry;
            }

            const cacheKey = getEntryKey(logId, cachedEntry.sourceTable || sourceTable);
            if (cachedDetailsPromises.has(cacheKey)) {
                return cachedDetailsPromises.get(cacheKey);
            }

            const promise = fetch(buildDetailsUrl(logId, cachedEntry), {
                cache: 'no-store',
                headers: {
                    'Accept': 'application/json'
                }
            })
                .then(async (response) => {
                    if (!response.ok) {
                        const payload = await response.json().catch(() => ({}));
                        throw new Error(payload.message || 'Не удалось загрузить событие.');
                    }

                    return response.json();
                })
                .then((details) => {
                    const mergedEntry = {
                        ...cachedEntry,
                        ...details,
                        sourceTable: cachedEntry.sourceTable || sourceTable || details.sourceTable || ''
                    };
                    readLogsMap().set(getEntryKey(logId, mergedEntry.sourceTable), mergedEntry);
                    return mergedEntry;
                })
                .finally(() => {
                    cachedDetailsPromises.delete(cacheKey);
                });

            cachedDetailsPromises.set(cacheKey, promise);
            return promise;
        }

        return {
            reset,
            readLogsMap,
            getEntry,
            loadDetails
        };
    }

    window.AdminLogsData = {
        createLogEntryStore,
        normalizeSourceTable
    };
})();
