(() => {
    'use strict';
    const page = document.querySelector('[data-menu-gallery]');
    if (!page) return;
    const all = selector => [...page.querySelectorAll(selector)];
    const categoryButtons = all('[data-category-index]');
    const categoryPanels = all('[data-category-panel]');
    const groups = all('[data-menu-group]');
    const choices = all('[data-menu-index]');
    const menuPanels = all('[data-menu-panel]');

    const imageDialog = page.querySelector('[data-image-dialog]');
    if (imageDialog && typeof imageDialog.showModal === 'function') {
        const image = imageDialog.querySelector('img');
        const caption = imageDialog.querySelector('p');
        let imageTrigger = null;
        let backdropDown = false;
        const outsideDialog = event => {
            const bounds = imageDialog.getBoundingClientRect();
            return event.clientX < bounds.left || event.clientX > bounds.right
                || event.clientY < bounds.top || event.clientY > bounds.bottom;
        };

        // Delegate so photos revealed by category, group or "show all" work too.
        page.addEventListener('click', event => {
            const trigger = event.target.closest('.mg-zoom-trigger');
            if (!trigger || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
            event.preventDefault();
            imageTrigger = trigger;
            const thumbnail = trigger.querySelector('img');
            // Load the unmodified original URL, never a screenshot or recompressed copy.
            image.src = trigger.href;
            image.alt = thumbnail.alt;
            caption.textContent = thumbnail.alt;
            backdropDown = false;
            imageDialog.showModal();
            document.body.classList.add('menu-image-open');
        });
        image.addEventListener('error', () => {
            const fallback = imageDialog.dataset.fallback;
            if (fallback && image.getAttribute('src') !== fallback) image.src = fallback;
        });
        imageDialog.querySelector('[data-close-image]').addEventListener('click', () => imageDialog.close());
        imageDialog.addEventListener('pointerdown', event => { backdropDown = outsideDialog(event); });
        imageDialog.addEventListener('click', event => {
            if (backdropDown && outsideDialog(event)) imageDialog.close();
            backdropDown = false;
        });
        imageDialog.addEventListener('close', () => {
            document.body.classList.remove('menu-image-open');
            if (imageTrigger?.isConnected) imageTrigger.focus({ preventScroll: true });
            image.removeAttribute('src');
        });
    }

    const showCategory = index => {
        categoryButtons.forEach((button, position) => button.setAttribute('aria-pressed', String(position === index)));
        categoryPanels.forEach((panel, position) => {
            panel.hidden = position !== index;
            const dishes = [...panel.querySelectorAll('[data-custom-dish]')];
            dishes.forEach((dish, dishIndex) => { dish.hidden = dishIndex >= 6; });
            const expand = panel.querySelector('[data-expand-category]');
            expand.hidden = dishes.length <= 6;
            expand.setAttribute('aria-expanded', 'false');
            expand.textContent = `Xem toàn bộ ${dishes.length} món ↓`;
        });
    };
    categoryButtons.forEach((button, index) => button.addEventListener('click', () => showCategory(index)));
    all('[data-expand-category]').forEach(button => button.addEventListener('click', () => {
        const panel = button.closest('[data-category-panel]');
        const expanded = button.getAttribute('aria-expanded') !== 'true';
        const dishes = [...panel.querySelectorAll('[data-custom-dish]')];
        dishes.forEach((dish, index) => { dish.hidden = !expanded && index >= 6; });
        button.setAttribute('aria-expanded', String(expanded));
        button.textContent = expanded ? 'Thu gọn ↑' : `Xem toàn bộ ${dishes.length} món ↓`;
    }));

    const selectMenu = index => {
        const group = Math.floor(index / 6);
        choices.forEach((button, position) => {
            button.hidden = Math.floor(position / 6) !== group;
            button.setAttribute('aria-pressed', String(position === index));
        });
        groups.forEach((button, position) => button.setAttribute('aria-pressed', String(position === group)));
        menuPanels.forEach((panel, position) => { panel.hidden = position !== index; });
        const title = menuPanels[index]?.querySelector('h3');
        const announcement = page.querySelector('[data-menu-announcement]');
        if (announcement && title) announcement.textContent = title.textContent;
    };
    groups.forEach((button, index) => button.addEventListener('click', () => selectMenu(index * 6)));
    choices.forEach((button, index) => button.addEventListener('click', () => selectMenu(index)));

    const wireArrows = (buttons, select) => buttons.forEach((button, index) => button.addEventListener('keydown', event => {
        if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
        event.preventDefault();
        const next = event.key === 'Home' ? 0 : event.key === 'End' ? buttons.length - 1
            : (index + (event.key === 'ArrowRight' ? 1 : -1) + buttons.length) % buttons.length;
        select(next);
        buttons[next].focus({ preventScroll: true });
    }));
    wireArrows(categoryButtons, showCategory);
    wireArrows(groups, index => selectMenu(index * 6));
    wireArrows(choices, selectMenu);

    if (categoryButtons.length) {
        showCategory(0);
        page.querySelector('[data-category-controls]').hidden = false;
    }
    if (choices.length) {
        const requested = choices.findIndex(button => button.dataset.menuId === page.dataset.selectedMenu);
        selectMenu(requested < 0 ? 0 : requested);
        page.querySelector('[data-set-controls]').hidden = false;
    }
    page.classList.add('mg-ready');
})();
