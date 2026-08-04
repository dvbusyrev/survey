(() => {
    if (window.AppPageLifecycle) {
        return;
    }

    const registrations = new Map();
    const mountedControllers = new Map();

    function reportLifecycleError(name, error) {
        console.error(`Ошибка жизненного цикла страницы: ${name}`, error);
    }

    function createScope() {
        const cleanups = new Set();
        let disposed = false;

        function add(cleanup) {
            if (typeof cleanup !== 'function') {
                return cleanup;
            }

            if (disposed) {
                cleanup();
                return cleanup;
            }

            cleanups.add(cleanup);
            return cleanup;
        }

        return {
            add,
            listen(target, type, handler, options) {
                target?.addEventListener?.(type, handler, options);
                return add(() => target?.removeEventListener?.(type, handler, options));
            },
            timeout(callback, delay) {
                const timerId = window.setTimeout(() => {
                    cleanups.delete(cleanup);
                    callback();
                }, delay);
                const cleanup = () => window.clearTimeout(timerId);
                return add(cleanup);
            },
            interval(callback, delay) {
                const timerId = window.setInterval(callback, delay);
                return add(() => window.clearInterval(timerId));
            },
            frame(callback) {
                const frameId = window.requestAnimationFrame(() => {
                    cleanups.delete(cleanup);
                    callback();
                });
                const cleanup = () => window.cancelAnimationFrame(frameId);
                return add(cleanup);
            },
            observe(observer, target, options) {
                observer?.observe?.(target, options);
                return add(() => observer?.disconnect?.());
            },
            dispose() {
                if (disposed) {
                    return;
                }

                disposed = true;
                Array.from(cleanups).reverse().forEach((cleanup) => {
                    try {
                        cleanup();
                    } catch (error) {
                        reportLifecycleError('cleanup', error);
                    }
                });
                cleanups.clear();
            }
        };
    }

    function getMatchingNodes(root, selector) {
        if (!root || !selector) {
            return [];
        }

        const nodes = [];
        if (root.matches?.(selector)) {
            nodes.push(root);
        }

        root.querySelectorAll?.(selector).forEach((node) => nodes.push(node));
        return nodes;
    }

    function mountController(node, name, registration) {
        let controllers = mountedControllers.get(node);
        if (!controllers) {
            controllers = new Map();
            mountedControllers.set(node, controllers);
        }

        if (controllers.has(name)) {
            return;
        }

        const scope = createScope();
        try {
            const cleanup = registration.mount(node, scope);
            controllers.set(name, () => {
                try {
                    cleanup?.();
                } finally {
                    scope.dispose();
                }
            });
        } catch (error) {
            scope.dispose();
            reportLifecycleError(name, error);
        }
    }

    function dispatchPageEvent(node, eventName) {
        try {
            node?.dispatchEvent?.(new CustomEvent(eventName, {
                bubbles: false,
                detail: { node }
            }));
        } catch (error) {
            reportLifecycleError(eventName, error);
        }
    }

    function mount(root = document) {
        registrations.forEach((registration, name) => {
            getMatchingNodes(root, registration.selector).forEach((node) => {
                mountController(node, name, registration);
            });
        });
    }

    function unmount(root = document) {
        Array.from(mountedControllers.entries()).forEach(([node, controllers]) => {
            if (node !== root && !root.contains?.(node)) {
                return;
            }

            dispatchPageEvent(node, 'page:unmount');
            Array.from(controllers.values()).reverse().forEach((dispose) => dispose());
            mountedControllers.delete(node);
        });
    }

    function register(name, selector, mountControllerForNode) {
        const previous = registrations.get(name);
        previous?.unregister?.();

        const registration = {
            selector,
            mount: mountControllerForNode,
            unregister: null
        };
        registration.unregister = () => {
            registrations.delete(name);
            Array.from(mountedControllers.entries()).forEach(([node, controllers]) => {
                const dispose = controllers.get(name);
                if (!dispose) {
                    return;
                }

                dispose();
                controllers.delete(name);
                if (controllers.size === 0) {
                    mountedControllers.delete(node);
                }
            });
        };

        registrations.set(name, registration);
        mount(document);
        return registration.unregister;
    }

    window.AppPageLifecycle = {
        createScope,
        mount,
        unmount,
        register
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => mount(document), { once: true });
    }
})();
