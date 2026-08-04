(function () {
    if (window.SurveyFilterPopover) {
        return;
    }

    function applyOpenState(instance, isOpen) {
        if (!instance?.state || !instance?.refs?.trigger || !instance?.refs?.popover) {
            return;
        }

        instance.state.isOpen = Boolean(isOpen);
        instance.refs.trigger.setAttribute('aria-expanded', instance.state.isOpen ? 'true' : 'false');

        if (instance.state.isOpen) {
            window.AppCheckboxDropdown?.scheduleListHeightUpdate(instance.refs.popover);
        }
    }

    function setOpen(instance, isOpen) {
        if (instance?.dropdownController?.setOpen && !instance.isSyncingDropdownOpenState) {
            instance.isSyncingDropdownOpenState = true;
            instance.dropdownController.setOpen(Boolean(isOpen));
            instance.isSyncingDropdownOpenState = false;
            return;
        }

        applyOpenState(instance, isOpen);
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

            instance.dropdownController?.destroy?.();
        });
        collection.clear();
    }

    window.SurveyFilterPopover = {
        setOpen,
        applyOpenState,
        cleanupDetachedInstances,
        closeAll,
        containsTarget,
        unbindCollection
    };
})();
