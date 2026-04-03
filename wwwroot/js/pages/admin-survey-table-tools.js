(function () {
  function normalizeText(value) {
    return (value || '').toString().trim().toLowerCase();
  }

  function filterSurveys(surveys, filters) {
    const list = Array.isArray(surveys) ? surveys : [];
    const searchTerm = normalizeText(filters && filters.searchTerm);
    const monthFilter = (filters && filters.monthFilter ? String(filters.monthFilter) : '').trim();
    const omsuFilter = normalizeText(filters && filters.omsuFilter);

    return list.filter(function (survey) {
      const surveyName = normalizeText(survey && survey.name_survey);
      const omsuRaw = survey && survey.name_omsu ? String(survey.name_omsu) : '';
      const omsuName = normalizeText(omsuRaw);
      const surveyDate = survey && survey.date_open ? new Date(survey.date_open) : null;
      const surveyMonth = surveyDate && !Number.isNaN(surveyDate.getTime()) ? String(surveyDate.getMonth() + 1) : '';

      const matchesSearch = !searchTerm || surveyName.includes(searchTerm) || omsuName.includes(searchTerm);
      const matchesMonth = !monthFilter || surveyMonth === monthFilter;
      const matchesOmsu = !omsuFilter || omsuRaw.split(',').some(function (name) {
        return normalizeText(name) === omsuFilter;
      });

      return matchesSearch && matchesMonth && matchesOmsu;
    });
  }

  function paginateSurveys(filteredSurveys, currentPage, recordsPerPage) {
    const list = Array.isArray(filteredSurveys) ? filteredSurveys : [];
    const size = Math.max(1, parseInt(recordsPerPage, 10) || 10);
    const totalRecords = list.length;
    const totalPages = Math.max(1, Math.ceil(totalRecords / size));
    const safeCurrentPage = Math.min(Math.max(parseInt(currentPage, 10) || 1, 1), totalPages);
    const start = (safeCurrentPage - 1) * size;
    const end = start + size;

    return {
      currentRecords: list.slice(start, end),
      totalPages: totalPages,
      totalRecords: totalRecords,
      currentPage: safeCurrentPage
    };
  }

  function getOmsuOptions(surveys) {
    const values = new Set();
    (Array.isArray(surveys) ? surveys : []).forEach(function (survey) {
      const raw = survey && survey.name_omsu ? String(survey.name_omsu) : '';
      raw.split(',').forEach(function (name) {
        const trimmed = (name || '').trim();
        if (trimmed) {
          values.add(trimmed);
        }
      });
    });
    return Array.from(values);
  }

  window.AdminSurveyTableTools = {
    filterSurveys: filterSurveys,
    paginateSurveys: paginateSurveys,
    getOmsuOptions: getOmsuOptions
  };
})();
