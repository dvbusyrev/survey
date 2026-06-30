(function () {
    const existingController = window.__surveysPageController;
    if (existingController && typeof existingController.destroy === 'function') {
        existingController.destroy();
    }

    const PAGE_SELECTOR = '.app-page[data-page="surveys-list"], .app-page[data-page="surveys-archive"], .app-page[data-page="answers-list"], .app-page[data-page="user-surveys"]';
    const ADMIN_SURVEY_PAGE_SELECTOR = '.app-page[data-page="surveys-list"], .app-page[data-page="surveys-archive"]';
    const WORK_PERIOD_PAGE_SELECTOR = '.app-page[data-page="surveys-list"]';

    let unregisterLifecycle = null;
    const mountedControllers = new Set();
    const mountedControllerByPage = new WeakMap();

    function getPagesFromNode(node) {
        if (node === document || node?.nodeType === Node.DOCUMENT_NODE) {
            return Array.from(document.querySelectorAll(PAGE_SELECTOR));
        }

        if (!(node instanceof Element)) {
            return [];
        }

        const pages = [];
        const ownerPage = node.closest(PAGE_SELECTOR);
        if (ownerPage) {
            pages.push(ownerPage);
        }

        if (node.matches(PAGE_SELECTOR)) {
            pages.push(node);
        }

        node.querySelectorAll(PAGE_SELECTOR).forEach((page) => {
            pages.push(page);
        });

        return Array.from(new Set(pages));
    }

    function createCompositeController(controllers) {
        let isDestroyed = false;
        return {
            destroy() {
                if (isDestroyed) {
                    return;
                }

                isDestroyed = true;
                controllers.slice().reverse().forEach((controller) => controller?.destroy?.());
            }
        };
    }

    function mountPage(page) {
        if (!(page instanceof Element) || !page.matches(PAGE_SELECTOR)) {
            return null;
        }

        const existingController = mountedControllerByPage.get(page);
        if (existingController) {
            return existingController;
        }

        const controllers = [];
        const filtersController = window.SurveyFilters?.mount?.(page);
        if (filtersController) {
            controllers.push(filtersController);
        }

        if (page.matches(WORK_PERIOD_PAGE_SELECTOR)) {
            const workPeriodController = window.SurveyWorkPeriod?.mount?.(page);
            if (workPeriodController) {
                controllers.push(workPeriodController);
            }
        }

        if (page.matches(ADMIN_SURVEY_PAGE_SELECTOR)) {
            const actionsController = window.SurveyAdminList?.mount?.(page);
            if (actionsController) {
                controllers.push(actionsController);
            }
        }

        if (controllers.length === 0) {
            return null;
        }

        let isDestroyed = false;
        const controller = {
            page,
            destroy() {
                if (isDestroyed) {
                    return;
                }

                isDestroyed = true;
                page.removeEventListener('page:unmount', controller.destroy);
                createCompositeController(controllers).destroy();
                mountedControllerByPage.delete(page);
                mountedControllers.delete(controller);
            }
        };

        page.addEventListener('page:unmount', controller.destroy);
        mountedControllerByPage.set(page, controller);
        mountedControllers.add(controller);
        return controller;
    }

    function mount(root = document) {
        const controllers = getPagesFromNode(root)
            .map((page) => mountPage(page))
            .filter(Boolean);

        return createCompositeController(controllers);
    }

    function destroy(root = document) {
        if (root === document || root?.nodeType === Node.DOCUMENT_NODE) {
            Array.from(mountedControllers).forEach((controller) => controller.destroy());
            return;
        }

        if (!(root instanceof Element)) {
            return;
        }

        Array.from(mountedControllers).forEach((controller) => {
            if (controller.page === root || root.contains(controller.page)) {
                controller.destroy();
            }
        });
    }

    window.SurveysPage = {
        mount,
        destroy
    };

    function destroyAll() {
        destroy(document);
        unregisterLifecycle?.();
        unregisterLifecycle = null;
    }

    window.__surveysPageController = {
        destroy: destroyAll
    };

    if (window.AppPageLifecycle?.register) {
        unregisterLifecycle = window.AppPageLifecycle.register(
            'surveys-page',
            PAGE_SELECTOR,
            (page) => mount(page).destroy
        );
        return;
    }

    const mountInitialPages = () => mount(document);
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', mountInitialPages, { once: true });
    } else {
        mountInitialPages();
    }
})();
