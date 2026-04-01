import React, { useState } from 'react';
import ReactDOM from 'react-dom/client';

const Modal = ({ isOpen, onClose, title, message }) => {
  if (!isOpen) return null;

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <h2 className="modal-title">{title}</h2>
        <p className="modal-message">{message}</p>
        <button className="modal-button" onClick={onClose}>
          Закрыть
        </button>
      </div>
    </div>
  );
};

const LoginForm = () => {
  const [formData, setFormData] = useState({
    username: '',
    password: '',
    rememberMe: false,
  });

  const [modal, setModal] = useState({
    isOpen: false,
    title: '',
    message: '',
  });

  const handleChange = (e) => {
    const { name, value, type, checked } = e.target;
    setFormData((prev) => ({
      ...prev,
      [name]: type === 'checkbox' ? checked : value,
    }));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();

    try {
      const dataUser = [formData.username, formData.password];
      const xhr = new XMLHttpRequest();

      xhr.onreadystatechange = function () {
        if (xhr.readyState === 4) {
          if (xhr.status === 200) {
            try {
              const response = JSON.parse(xhr.responseText);

              if (response.role === 'Админ') {
                window.location.href = '/get_surveys';
              } else if (response.role === 'user') {
                window.location.href = `/survey_list_user/${response.userId}`;
              } else {
                setModal({
                  isOpen: true,
                  title: 'Ошибка',
                  message: 'Неизвестная роль пользователя',
                });
              }
            } catch (error) {
              console.error('Ошибка при обработке ответа:', error);
              setModal({
                isOpen: true,
                title: 'Ошибка',
                message: 'Ошибка при обработке ответа от сервера',
              });
            }
          } else {
            console.error('Ошибка авторизации: ' + xhr.status);
            console.error('Текст ответа: ' + xhr.responseText);
            setModal({
              isOpen: true,
              title: 'Ошибка авторизации',
              message: xhr.responseText || 'Проверьте правильность введенных данных.',
            });
          }
        }
      };

      xhr.onerror = function () {
        console.error('Проблемы с интернетом');
        setModal({
          isOpen: true,
          title: 'Ошибка',
          message: 'Проблемы с интернетом. Проверьте подключение.',
        });
      };

      xhr.open('POST', '/Auth/login', true);
      xhr.setRequestHeader('Content-Type', 'application/json;charset=UTF-8');
      xhr.send(JSON.stringify(dataUser));
    } catch (error) {
      console.error('Ошибка:', error);
      setModal({
        isOpen: true,
        title: 'Ошибка',
        message: 'Произошла ошибка при попытке входа. Попробуйте позже.',
      });
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
                placeholder="Введите имя пользователя.."
              />
            </div>
          </div>

          <div className="form-group">
            <label className="form-label" htmlFor="password">
              Пароль
            </label>
            <div className="input-container">
              <div className="icon-container">
                <i className="fas fa-lock"></i>
              </div>
              <input
                type="password"
                className="form-input"
                id="password"
                name="password"
                value={formData.password}
                onChange={handleChange}
                required
                placeholder="Введите пароль.."
              />
            </div>
          </div>

          <button type="submit" className="submit-button">
            Войти
          </button>
        </form>
      </div>

      <Modal
        isOpen={modal.isOpen}
        onClose={() => setModal((prev) => ({ ...prev, isOpen: false }))}
        title={modal.title}
        message={modal.message}
      />
    </div>
  );
};

ReactDOM.createRoot(document.getElementById('root')).render(<LoginForm />);
