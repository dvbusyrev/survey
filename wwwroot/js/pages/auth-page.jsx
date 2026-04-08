(() => {
    const rootElement = document.getElementById('root');
    if (!rootElement || typeof React === 'undefined' || typeof ReactDOM === 'undefined') {
        return;
    }

    const Modal = ({ isOpen, onClose, title, message }) => {
        if (!isOpen) {
            return null;
        }

        return (
            <div className="modal-overlay" onClick={onClose}>
                <div className="modal-content" onClick={(event) => event.stopPropagation()}>
                    <h2 className="modal-title">{title}</h2>
                    <p className="modal-message">{message}</p>
                    <button className="modal-button" onClick={onClose}>
                        Закрыть
                    </button>
                </div>
            </div>
        );
    };

    const EyeIcon = ({ visible }) => (
        visible ? (
            <svg viewBox="0 0 24 24" aria-hidden="true">
                <path d="M3 3l18 18"></path>
                <path d="M10.6 10.7a3 3 0 0 0 4 4"></path>
                <path d="M9.9 5.2A11 11 0 0 1 12 5c6.5 0 10 7 10 7a17.3 17.3 0 0 1-4.1 4.8"></path>
                <path d="M6.6 6.7A17.7 17.7 0 0 0 2 12s3.5 7 10 7a10.8 10.8 0 0 0 5.2-1.3"></path>
            </svg>
        ) : (
            <svg viewBox="0 0 24 24" aria-hidden="true">
                <path d="M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6S2 12 2 12z"></path>
                <circle cx="12" cy="12" r="3"></circle>
            </svg>
        )
    );

    function parseJsonSafely(value) {
        if (!value) {
            return null;
        }

        try {
            return JSON.parse(value);
        } catch {
            return null;
        }
    }

    function getErrorMessage(responseText, fallbackMessage) {
        const parsed = parseJsonSafely(responseText);
        if (parsed?.message) {
            return parsed.message;
        }

        if (parsed?.error) {
            return parsed.error;
        }

        const normalizedText = typeof responseText === 'string' ? responseText.trim() : '';
        return normalizedText || fallbackMessage;
    }

    const LoginForm = () => {
        const [formData, setFormData] = React.useState({
            username: '',
            password: ''
        });
        const [showPassword, setShowPassword] = React.useState(false);
        const [isSubmitting, setIsSubmitting] = React.useState(false);
        const [modal, setModal] = React.useState({
            isOpen: false,
            title: '',
            message: ''
        });

        const handleChange = (event) => {
            const { name, value } = event.target;
            setFormData((previous) => ({
                ...previous,
                [name]: value
            }));
        };

        const openModal = (title, message) => {
            setModal({
                isOpen: true,
                title,
                message
            });
        };

        const handleSubmit = async (event) => {
            event.preventDefault();
            setIsSubmitting(true);

            try {
                const response = await fetch('/auth/login', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json;charset=UTF-8'
                    },
                    body: JSON.stringify([formData.username, formData.password])
                });

                const responseText = await response.text();

                if (!response.ok) {
                    throw new Error(getErrorMessage(responseText, 'Проверьте правильность введенных данных.'));
                }

                const payload = parseJsonSafely(responseText);
                if (payload?.role === 'admin') {
                    window.location.href = '/surveys';
                    return;
                }

                if (payload?.role === 'user') {
                    window.location.href = '/my-surveys';
                    return;
                }

                throw new Error('Неизвестная роль пользователя');
            } catch (error) {
                console.error('Ошибка авторизации:', error);
                openModal('Ошибка авторизации', error instanceof Error ? error.message : 'Произошла ошибка при попытке входа.');
            } finally {
                setIsSubmitting(false);
            }
        };

        return (
            <div className="content">
                <div className="inner-container">
                    <form onSubmit={handleSubmit}>
                        <h1 className="form-title">
                            <span className="welcome-text">АНКЕТИРОВАНИЕ</span>
                        </h1>

                        <div className="form-group">
                            <label className="form-label" htmlFor="username">
                                Имя пользователя
                            </label>
                            <div className="input-container">
                                <div className="icon-container">
                                    <i className="fas fa-user"></i>
                                </div>
                                <input
                                    type="text"
                                    className="form-input"
                                    id="username"
                                    name="username"
                                    value={formData.username}
                                    onChange={handleChange}
                                    required
                                    placeholder="Введите имя пользователя"
                                />
                            </div>
                        </div>

                        <div className="form-group">
                            <label className="form-label" htmlFor="password">
                                Пароль
                            </label>
                            <div className="input-container has-toggle">
                                <div className="icon-container">
                                    <i className="fas fa-lock"></i>
                                </div>
                                <input
                                    type="text"
                                    className={`form-input ${showPassword ? '' : 'is-password-masked'}`}
                                    id="password"
                                    name="password"
                                    value={formData.password}
                                    onChange={handleChange}
                                    required
                                    autoComplete="current-password"
                                    autoCapitalize="none"
                                    autoCorrect="off"
                                    spellCheck={false}
                                    inputMode="text"
                                    lang="ru"
                                    data-password-field="true"
                                    placeholder="Введите пароль"
                                />
                                <button
                                    type="button"
                                    className="password-toggle-btn"
                                    onMouseDown={(event) => event.preventDefault()}
                                    onClick={() => {
                                        setShowPassword((previous) => !previous);
                                        window.requestAnimationFrame(() => {
                                            const input = document.getElementById('password');
                                            if (!input) {
                                                return;
                                            }

                                            input.focus({ preventScroll: true });
                                            const length = input.value ? input.value.length : 0;
                                            if (typeof input.setSelectionRange === 'function') {
                                                input.setSelectionRange(length, length);
                                            }
                                        });
                                    }}
                                    aria-label={showPassword ? 'Скрыть пароль' : 'Показать пароль'}
                                    title={showPassword ? 'Скрыть пароль' : 'Показать пароль'}
                                >
                                    <EyeIcon visible={showPassword} />
                                </button>
                            </div>
                        </div>

                        <button type="submit" className="submit-button" disabled={isSubmitting}>
                            {isSubmitting ? 'Вход...' : 'Войти'}
                        </button>
                    </form>
                </div>
                <Modal
                    isOpen={modal.isOpen}
                    onClose={() => setModal((previous) => ({ ...previous, isOpen: false }))}
                    title={modal.title}
                    message={modal.message}
                />
            </div>
        );
    };

    const root = ReactDOM.createRoot(rootElement);
    root.render(<LoginForm />);
})();
