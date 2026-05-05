function readChromeContextNode(contextNode) {
    if (!contextNode?.dataset) {
        return null;
    }

    return {
        userRole: contextNode.dataset.userRole || '',
        userId: Number(contextNode.dataset.userId || 0),
        displayName: contextNode.dataset.displayName || '',
        userName: contextNode.dataset.userName || '',
        organizationName: contextNode.dataset.organizationName || ''
    };
}

function renderHeader(host, { userRole, displayName, userName, organizationName }) {
    const normalizedUserRole = String(userRole || '').trim().toLowerCase();
    const isAdmin = normalizedUserRole === 'admin' || normalizedUserRole === 'administrator' || normalizedUserRole === 'администратор';
    const rawDisplayName = displayName && String(displayName).trim()
        ? String(displayName).trim()
        : (isAdmin ? 'Администратор' : 'Клиент');
    const displayNameParts = rawDisplayName.split(':').map(part => part.trim()).filter(Boolean);
    const normalizedUserName = userName && String(userName).trim()
        ? String(userName).trim()
        : (displayNameParts.length > 1 ? displayNameParts.slice(1).join(': ').trim() : rawDisplayName);
    const normalizedOrganizationName = organizationName && String(organizationName).trim()
        ? String(organizationName).trim()
        : (displayNameParts[0] || 'Клиент');
    const headerTopLine = normalizedOrganizationName;
    const normalizedDisplayName = isAdmin
        ? (normalizedUserName || 'Администратор')
        : normalizedOrganizationName;

    const template = document.getElementById('header-template');
    if (!host || !template?.content?.firstElementChild) {
        return null;
    }

    host.innerHTML = '';
    const header = template.content.firstElementChild.cloneNode(true);
    const modeLabel = header.querySelector('.header-mode-label');
    const role = header.querySelector('.header-user-name');
    const logoutButton = header.querySelector('.logout-button');

    if (modeLabel) {
        modeLabel.textContent = isAdmin ? headerTopLine : '';
        modeLabel.hidden = !isAdmin;
    }
    if (role) {
        role.textContent = normalizedDisplayName;
        role.setAttribute('title', normalizedDisplayName);
        role.hidden = false;
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

window.readAppChromeContext = function readAppChromeContext() {
    return readChromeContextNode(document.getElementById('layout-chrome-context'))
        || readChromeContextNode(document.getElementById('chrome-context'))
        || null;
};
