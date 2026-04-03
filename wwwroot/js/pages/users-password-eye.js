
(function () {
  function addPasswordEye(input) {
    if (!input || input.dataset.eyeApplied === 'true') return;
    const wrapper = document.createElement('div');
    wrapper.className = 'password-eye-wrap';
    input.parentNode.insertBefore(wrapper, input);
    wrapper.appendChild(input);
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'password-eye-btn';
    btn.setAttribute('aria-label', 'Показать пароль');
    btn.innerHTML = `
      <svg class="eye-open" viewBox="0 0 24 24" aria-hidden="true">
          <path d="M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6S2 12 2 12z"></path>
          <circle cx="12" cy="12" r="3"></circle>
      </svg>
      <svg class="eye-closed" viewBox="0 0 24 24" aria-hidden="true">
          <path d="M3 3l18 18"></path>
          <path d="M10.6 10.7a3 3 0 0 0 4 4"></path>
          <path d="M9.9 5.2A11 11 0 0 1 12 5c6.5 0 10 7 10 7a17.3 17.3 0 0 1-4.1 4.8"></path>
          <path d="M6.6 6.7A17.7 17.7 0 0 0 2 12s3.5 7 10 7a10.8 10.8 0 0 0 5.2-1.3"></path>
      </svg>`;
    btn.addEventListener('click', function () {
      const isPassword = input.type === 'password';
      input.type = isPassword ? 'text' : 'password';
      btn.classList.toggle('is-visible', isPassword);
      btn.setAttribute('aria-label', isPassword ? 'Скрыть пароль' : 'Показать пароль');
    });
    wrapper.appendChild(btn);
    input.dataset.eyeApplied = 'true';
  }

  function initUserModalPasswordEyes() {
    const passwordFields = document.querySelectorAll('#addUserModal input[type="password"], #editUserModal input[type="password"], input[name="password"]');
    passwordFields.forEach(addPasswordEye);
  }

  window.initUserModalPasswordEyes = initUserModalPasswordEyes;
  setTimeout(initUserModalPasswordEyes, 0);
})();
