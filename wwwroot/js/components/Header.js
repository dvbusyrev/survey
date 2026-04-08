window.Header = ({ userRole, displayName, userName, organizationName }) => {
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

    return React.createElement('header', null,
        React.createElement('img', { src: '/images/favicon.svg', alt: 'Логотип', draggable: false }),
        React.createElement('h1', { className: 'header-title'}, 'Анкетирование'),
        React.createElement('div', { className: 'header-right' },
            React.createElement('div', { className: 'header-user-info' },
                React.createElement('p', { className: 'header-mode-label' }, headerTopLine),
                React.createElement('p', { id: 'role', title: normalizedDisplayName }, normalizedDisplayName)
            ),
            React.createElement('button', { className: 'logout-button', onClick: () => {
                fetch('/auth/logout', { method: 'POST' })
                    .then(response => {
                        if (response.ok) {
                            window.location.href = '/';
                        } else {
                            console.error('Ошибка при выходе');
                        }
                    })
                    .catch(error => console.error('Ошибка сети:', error));
            }}, 'Выйти')
        )
    );
};
