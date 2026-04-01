window.Header = ({ userRole, displayName }) => {
    const headerLabel = displayName && String(displayName).trim() ? displayName : userRole;
    return React.createElement('header', null,
        React.createElement('img', { src: '/images/favicon.svg', alt: 'Логотип'}),
        React.createElement('h1', { className: 'header-title'}, 'Анкетирование'),
        React.createElement('div', { className: 'header-right' },
            React.createElement('p', { id: 'role'}, headerLabel),
            React.createElement('button', { className: 'logout-button', onClick: () => {
                fetch('/Auth/logout_account', { method: 'GET' })
                    .then(response => {
                        if (response.ok) {
                            window.location.href = '/Auth/display_auth';
                        } else {
                            console.error('Ошибка при выходе');
                        }
                    })
                    .catch(error => console.error('Ошибка сети:', error));
            }}, 'Выйти')
        )
    );
};
