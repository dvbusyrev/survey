// stage12: standalone survey fill page extracted from zapolnenie_anketi.cshtml
const SurveyFillPage = ({ initialData }) => {
            const [answers, setAnswers] = React.useState({});
            const [loading, setLoading] = React.useState(false);
            const [error, setError] = React.useState(null);
            const [isSubmitted, setIsSubmitted] = React.useState(false);

            const handleRatingClick = (questionId, rating) => {
                setError(null);
                setAnswers(prev => ({
                    ...prev,
                    [questionId]: {
                        ...prev[questionId],
                        rating,
                        comment: rating < 5 ? prev[questionId]?.comment || '' : ''
                    }
                }));
            };

            const handleCommentChange = (questionId, comment) => {
                setError(null);
                setAnswers(prev => ({
                    ...prev,
                    [questionId]: {
                        ...prev[questionId],
                        comment
                    }
                }));
            };

            const submitAnswers = async () => {
                try {
                    setLoading(true);
                    setError(null);
                    
                    // Проверяем, что все вопросы заполнены
                    const allQuestionsAnswered = initialData.questions.every(q => {
                        const questionId = q.id || q.Id;
                        const answer = answers[questionId];
                        return answer?.rating && (answer.rating >= 5 || answer.comment);
                    });
                    
                    if (!allQuestionsAnswered) {
                        throw new Error('Пожалуйста, ответьте на все вопросы. Для оценки ниже 5 заполните поле "Ваш комментарий".');
                    }
                    
                    const response = await fetch('/api/submit_answers', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                        },
                        body: JSON.stringify({
                            surveyId: initialData.surveyId,
                            organizationId: initialData.organizationId,
                            answers: Object.entries(answers).map(([questionId, answer]) => ({
                                questionId,
                                rating: answer.rating,
                                comment: answer.comment || ''
                            }))
                        })
                    });

                    if (!response.ok) {
                        const errorData = await response.json();
                        throw new Error(errorData.error || 'Ошибка при отправке ответов');
                    }

                    setIsSubmitted(true);
                    setTimeout(() => {
                        window.location.href = '/survey/thank-you';
                    }, 2000);
                } catch (err) {
                    setError(err.message);
                } finally {
                    setLoading(false);
                }
            };

            const handleBackToList = () => {
                window.location.href = '/survey/list';
            };

            if (isSubmitted) {
                return (
                    <div className="success-message">
                        <svg xmlns="http://www.w3.org/2000/svg" width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                            <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path>
                            <polyline points="22 4 12 14.01 9 11.01"></polyline>
                        </svg>
                        <h2>Анкета успешно заполнена!</h2>
                        <p>Вы будете перенаправлены на страницу благодарности...</p>
                    </div>
                );
            }

            return (
                <div className="page-container">
                    <Header
                        userRole={initialData.userRole}
                        displayName={initialData.displayName}
                        userName={initialData.userName}
                        organizationName={initialData.organizationName}
                    />
                    <div className="admin-container">
                        <Navigation activeTab="answers_tab" userRole={initialData.userRole} userId={initialData.userId} />
                        <div id="content_admin">
                            <div className="survey-fill-container">
                                <div className="note">
                                    <h2>Заполнение анкеты</h2>
                                    <p>Пожалуйста, оцените каждый вопрос по шкале от 1 до 5</p>
                                </div>
                                
                                {error && (
                                    <div className="error-message">
                                        <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                                            <circle cx="12" cy="12" r="10"></circle>
                                            <line x1="12" y1="8" x2="12" y2="12"></line>
                                            <line x1="12" y1="16" x2="12.01" y2="16"></line>
                                        </svg>
                                        {error}
                                    </div>
                                )}
                                
                                {initialData.questions.map((question, index) => {
                                    const questionId = question.id || question.Id || index;
                                    const questionText = question.text || question.Text || `Вопрос ${index + 1}`;
                                    const answer = answers[questionId] || {};
                                    
                                    return (
                                        <div className="question-container" key={questionId}>
                                            <h3>{questionText}</h3>
                                            <div className="rating-buttons">
                                                {[1, 2, 3, 4, 5].map(rating => (
                                                    <button
                                                        key={rating}
                                                        className={`btn_crit ${answer.rating === rating ? 'active' : ''}`}
                                                        onClick={() => handleRatingClick(questionId, rating)}
                                                    >
                                                        {rating}
                                                    </button>
                                                ))}
                                            </div>
                                            {answer.rating > 0 && answer.rating < 5 && (
                                                <div className="comment-container">
                                                    <label className="comment-label">Ваш комментарий</label>
                                                    <textarea 
                                                        placeholder="Напишите комментарий"
                                                        value={answer.comment || ''}
                                                        onChange={(e) => handleCommentChange(questionId, e.target.value)}
                                                    />
                                                </div>
                                            )}
                                        </div>
                                    );
                                })}
                                
                                <div className="submit-container">
                                    <button 
                                        onClick={submitAnswers} 
                                        className="submit-button"
                                        disabled={loading}
                                    >
                                        {loading ? (
                                            <>
                                                <span className="loading-spinner"></span>
                                                Отправка...
                                            </>
                                        ) : 'Отправить ответы'}
                                    </button>
                                </div>
                            </div>
                        </div>
                    </div>
                    <Footer />
                </div>
            );
        };

window.renderStandaloneSurveyFill = function(initialData) {
    const root = ReactDOM.createRoot(document.getElementById('root'));
    root.render(<SurveyFillPage initialData={initialData} />);
};
