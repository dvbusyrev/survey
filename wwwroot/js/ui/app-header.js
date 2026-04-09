function renderHeader(host, { userRole, displayName, userName, organizationName }) {
    const rawDisplayName = displayName && String(displayName).trim()
        ? String(displayName).trim()
        : (userRole === 'admin' ? 'Администратор' : 'Пользователь');
    const displayNameParts = rawDisplayName.split(':').map(part => part.trim()).filter(Boolean);
    const normalizedUserName = userName && String(userName).trim()
        ? String(userName).trim()
        : (displayNameParts.length > 1 ? displayNameParts.slice(1).join(': ').trim() : rawDisplayName);
    const normalizedOrganizationName = organizationName && String(organizationName).trim()
        ? String(organizationName).trim()
        : (displayNameParts[0] || 'Пользователь');
    const headerTopLine = userRole === 'admin' ? 'Администрирование' : normalizedOrganizationName;
    const normalizedDisplayName = userRole === 'admin'
        ? (normalizedUserName || 'Администратор')
        : (normalizedUserName || rawDisplayName);

    const template = document.getElementById('header-template');
    if (!host || !template?.content?.firstElementChild) {
        return null;
    }

    host.innerHTML = '';
    const header = template.content.firstElementChild.cloneNode(true);
    const modeLabel = header.querySelector('.header-mode-label');
    const role = header.querySelector('#role');
    const logoutButton = header.querySelector('.logout-button');

    if (modeLabel) {
        modeLabel.textContent = headerTopLine;
    }
    if (role) {
        role.textContent = normalizedDisplayName;
        role.setAttribute('title', normalizedDisplayName);
    }
    if (logoutButton) {
        logoutButton.addEventListener('click', () => {
            fetch('/auth/logout', { method: 'POST' })
                .then(response => {
                    if (response.ok) {
                        window.location.href = '/';
                    } else {
                        console.error('Ошибка при выходе');
                    }
                })
                .catch(error => console.error('Ошибка сети:', error));
        });
    }

    host.appendChild(header);
    return () => {
        host.innerHTML = '';
    };
}

window.mountHeader = function mountHeader(host, props) {
    return renderHeader(host, props || {});
};
