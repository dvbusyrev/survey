export function getAnswersPageContainer(source) {
    if (source instanceof Element) {
        const closestPage = source.closest('[data-role="survey-answers-page"], [data-page="answers-check"]');
        if (closestPage) {
            return closestPage;
        }
    }

    if (source && typeof source.querySelector === 'function') {
        const nestedPage = source.querySelector('[data-role="survey-answers-page"], [data-page="answers-check"]');
        if (nestedPage) {
            return nestedPage;
        }
    }

    return document.querySelector('[data-role="survey-answers-page"], [data-page="answers-check"]');
}

export function getFillPageContainer(source) {
    if (source instanceof Element) {
        const closestPage = source.closest('[data-role="survey-fill-page"]');
        if (closestPage) {
            return closestPage;
        }
    }

    if (source && typeof source.querySelector === 'function') {
        const nestedPage = source.querySelector('[data-role="survey-fill-page"]');
        if (nestedPage) {
            return nestedPage;
        }
    }

    return document.querySelector('[data-role="survey-fill-page"]');
}

export function applySurveySignedState(source, isSigned, mode = 'answer') {
    const isDraftMode = mode === 'draft';
    const page = isDraftMode
        ? getFillPageContainer(source)
        : getAnswersPageContainer(source);
    if (!page) {
        return;
    }

    if (isDraftMode) {
        page.dataset.isDraftSigned = isSigned ? 'true' : 'false';
    } else {
        page.dataset.isSigned = isSigned ? 'true' : 'false';
    }

    const signatureInfo = page.querySelector('[data-role="signature-info"]');
    const signatureStatus = page.querySelector('[data-role="signature-status"]');
    signatureInfo?.classList.remove('u-hidden', 'is-hidden');

    if (signatureStatus) {
        signatureStatus.textContent = isSigned ? 'Подписана' : 'Нет подписи';
        signatureStatus.classList.toggle('signed', isSigned);
        signatureStatus.classList.toggle('not-signed', !isSigned);
    }

    const signButtons = isDraftMode
        ? new Set([
            ...page.querySelectorAll('[data-role="draft-sign-button"]'),
            ...document.querySelectorAll('[data-role="draft-sign-button"]')
        ])
        : new Set([
            ...page.querySelectorAll('[data-role="sign-button"], [data-role-sign-button="true"]'),
            ...document.querySelectorAll('[data-role="sign-button"][data-survey-id], [data-role-sign-button="true"][data-survey-id]')
        ]);

    signButtons.forEach((signButton) => {
        if (signButton instanceof HTMLButtonElement) {
            signButton.disabled = isSigned;
            signButton.textContent = isSigned ? 'Подписано' : 'Подписать';
        }
    });
}

export function showSurveyError(message) {
    const safeMessage = typeof window.normalizeClientErrorMessage === 'function'
        ? window.normalizeClientErrorMessage(message)
        : message;

    window.AppUi?.notify?.(safeMessage, 'error', { title: 'Ошибка' });
}

export function createSurveyHtmlFragment(html) {
    const range = document.createRange();
    range.selectNode(document.body);
    return range.createContextualFragment(html);
}

export function renderSurveyHostError(host, message) {
    host.replaceChildren();
    showSurveyError(message);
}

export function createSurveyModalFooterButton({ role, text, variant = 'secondary', disabled = false, labelRole = '' }) {
    const button = window.AppUi.createButton({
        variant,
        disabled,
        dataset: { role }
    });

    if (labelRole) {
        const label = window.AppUi.createElement('span', {
            dataset: { role: labelRole },
            text
        });
        button.appendChild(label);
    } else {
        button.textContent = text;
    }

    return button;
}

export function clearSurveyModalFooter(footerHost) {
    footerHost?.replaceChildren();
}

export async function fetchSurveyModalContent(url, fallbackMessage) {
    const response = await fetch(url, {
        headers: {
            'X-Requested-With': 'XMLHttpRequest'
        }
    });
    if (!response.ok) {
        throw new Error(fallbackMessage);
    }

    return response.text();
}
