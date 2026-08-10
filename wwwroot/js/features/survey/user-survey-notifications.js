export const ANSWER_SUBMITTED_MESSAGE = 'Ответы на анкету успешно отправлены. Анкета перенесена в раздел «Архив анкет».';
export const ANSWER_SUBMISSION_FAILED_MESSAGE = 'Не удалось отправить ответы на анкету.';
export const SURVEY_SIGNED_MESSAGE = 'Анкета успешно подписана.';
export const SURVEY_SIGNING_FAILED_MESSAGE = 'Не удалось подписать анкету.';

const pendingAnswerNotificationKey = 'survey:pending-answer-notification';

function resolveMessage(message, fallbackMessage) {
    const normalizedMessage = String(message || '').trim();
    return normalizedMessage || fallbackMessage;
}

export function notifyAnswerSubmitted(message) {
    window.AppUi?.notify?.(
        resolveMessage(message, ANSWER_SUBMITTED_MESSAGE),
        'success',
        { title: 'Успешно' }
    );
}

export function storePendingAnswerSubmittedNotification(message) {
    try {
        window.sessionStorage.setItem(
            pendingAnswerNotificationKey,
            resolveMessage(message, ANSWER_SUBMITTED_MESSAGE)
        );
        return true;
    } catch (error) {
        return false;
    }
}

export function showPendingAnswerSubmittedNotification() {
    let message = '';

    try {
        message = window.sessionStorage.getItem(pendingAnswerNotificationKey) || '';
        window.sessionStorage.removeItem(pendingAnswerNotificationKey);
    } catch (error) {
        message = '';
    }

    if (message) {
        notifyAnswerSubmitted(message);
    }
}
