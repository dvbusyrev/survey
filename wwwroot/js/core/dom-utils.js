(function(){
  window.DomUtils = window.DomUtils || {
    byId: function(id){ return document.getElementById(id); },
    setValue: function(id, value){ var el = document.getElementById(id); if (el) el.value = value == null ? '' : value; return el; },
    setText: function(id, value){ var el = document.getElementById(id); if (el) el.textContent = value == null ? '' : value; return el; },
    show: function(id){ var el = document.getElementById(id); if (el) el.style.display = 'block'; return el; },
    hide: function(id){ var el = document.getElementById(id); if (el) el.style.display = 'none'; return el; },
    exists: function(id){ return !!document.getElementById(id); }
  };
})();
