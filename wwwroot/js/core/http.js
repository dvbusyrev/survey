(function(){
  async function requestJson(url, options){
    var response = await fetch(url, options || {});
    var data;
    try { data = await response.json(); } catch (e) { data = { success: false, message: 'Сервер вернул не JSON' }; }
    if (!response.ok) {
      throw new Error((data && data.message) || ('HTTP ' + response.status));
    }
    return data;
  }

  function antiforgeryToken(){
    var tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
    return tokenEl ? tokenEl.value : '';
  }

  function jsonHeaders(){
    var token = antiforgeryToken();
    var headers = { 'Content-Type': 'application/json' };
    if (token) headers['RequestVerificationToken'] = token;
    return headers;
  }

  window.Http = window.Http || {
    requestJson: requestJson,
    antiforgeryToken: antiforgeryToken,
    jsonHeaders: jsonHeaders
  };
})();
