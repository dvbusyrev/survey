function renderFooter(host) {
    const template = document.getElementById('footer-template');
    if (!host || !template?.content?.firstElementChild) {
        return null;
    }

    const existingFooter = host.querySelector(':scope > footer');
    if (existingFooter) {
        return () => {};
    }

    const footer = template.content.firstElementChild.cloneNode(true);
    host.appendChild(footer);
    return () => {
        footer.remove();
    };
}

window.mountFooter = function mountFooter(host) {
    return renderFooter(host);
};
