function renderFooter(host) {
    const template = document.getElementById('footer-template');
    if (!host || !template?.content?.firstElementChild) {
        return null;
    }

    host.innerHTML = '';
    host.appendChild(template.content.firstElementChild.cloneNode(true));
    return () => {
        host.innerHTML = '';
    };
}

window.mountFooter = function mountFooter(host) {
    return renderFooter(host);
};