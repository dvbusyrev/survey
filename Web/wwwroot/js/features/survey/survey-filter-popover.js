(function () {
    if (window.SurveyFilterPopover) {
        return;
    }

    function setOpen(instance, isOpen) {
        if (!instance?.state || !instance?.refs?.trigger || !instance?.refs?.popover) {
            return;
        }

        instance.state.isOpen = Boolean(isOpen);
        instance.refs.trigger.setAttribute('aria-expanded', instance.state.isOpen ? 'true' : 'false');
        instance.refs.popover.classList.toggle('is-hidden', !instance.state.isOpen);

        if (instance.state.isOpen) {
            window.AppCheckboxDropdown?.scheduleListHeightUpdate(instance.refs.popover);
        }
    }

    function cleanupDetachedInstances(collections) {
        collections.forEach((collection) => {
            Array.from(collection.entries()).forEach(([root]) => {
                if (!document.contains(root)) {
                    collection.delete(root);
                }
            });
        });
    }

    function closeAll(collections, exceptRoot = null) {
        cleanupDetachedInstances(collections);

        collections.forEach((collection) => {
            collection.forEach((instance, root) => {
                if (root !== exceptRoot) {
                    setOpen(instance, false);
                }
            });
        });
    }

    function containsTarget(collections, target) {
        return collections.some((collection) => Array.from(collection.keys()).some((root) => root.contains(target)));
    }

    function unbindCollection(collection) {
        collection.forEach((instance, root) => {
            if (instance.handlers?.click) {
                root.removeEventListener('click', instance.handlers.click);
            }

            if (instance.handlers?.change) {
                root.removeEventListener('change', instance.handlers.change);
            }
        });
        collection.clear();
    }

    window.SurveyFilterPopover = {
        setOpen,
        cleanupDetachedInstances,
        closeAll,
        containsTarget,
        unbindCollection
    };
})();
