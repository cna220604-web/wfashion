// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

document.querySelectorAll('[data-product-search-panel]').forEach(panel => {
    const form = panel.querySelector('[data-product-search-form]');
    const input = panel.querySelector('[data-product-search]');
    const rows = [...panel.querySelectorAll('[data-product-search-row]')];
    const emptyRow = panel.querySelector('[data-product-search-empty]');
    const count = panel.querySelector('[data-product-search-count]');
    if (!form || !input || !emptyRow || !count) return;

    const normalize = value => value.normalize('NFD')
        .replace(/[\u0300-\u036f]/g, '')
        .replace(/đ/gi, 'd')
        .toLocaleLowerCase('vi');
    const searchableRows = rows.map(row => ({
        row,
        text: normalize(row.dataset.search || row.textContent || '')
    }));

    const filterProducts = () => {
        const terms = normalize(input.value.trim()).split(/\s+/).filter(Boolean);
        let visibleCount = 0;
        searchableRows.forEach(({ row, text }) => {
            const matches = terms.every(term => text.includes(term));
            row.hidden = !matches;
            if (matches) visibleCount++;
        });
        emptyRow.hidden = visibleCount > 0;
        count.textContent = `${visibleCount} / ${rows.length} mặt hàng`;
    };

    form.addEventListener('submit', event => {
        event.preventDefault();
        filterProducts();
    });
    input.addEventListener('input', filterProducts);
    filterProducts();
});

document.querySelectorAll('[data-profile-form]').forEach(form => {
    const fields = form.querySelector('[data-profile-fields]');
    const editButton = form.querySelector('[data-profile-edit]');
    const actions = form.querySelector('[data-profile-actions]');
    const cancelButton = form.querySelector('[data-profile-cancel]');
    if (!fields || !editButton || !actions || !cancelButton) return;

    const setEditing = editing => {
        fields.disabled = !editing;
        editButton.hidden = editing;
        actions.hidden = !editing;
        form.dataset.editing = String(editing);
    };

    setEditing(form.dataset.editing === 'true');
    editButton.addEventListener('click', () => {
        setEditing(true);
        form.querySelector('[name="Profile.DisplayName"]')?.focus();
    });
    cancelButton.addEventListener('click', () => {
        window.location.assign(window.location.pathname + window.location.search);
    });
});

document.addEventListener('click', event => {
    const closeButton = event.target.closest('[data-close-admin-edit]');
    if (closeButton) closeButton.closest('details')?.removeAttribute('open');
});

document.querySelectorAll('[data-avatar-trigger]').forEach(button => {
    const fileInput = button.closest('.profile-avatar-row')?.querySelector('[data-avatar-input]');
    if (!fileInput) return;

    let previewUrl;
    button.addEventListener('click', () => fileInput.click());
    fileInput.addEventListener('change', () => {
        const file = fileInput.files?.[0];
        if (!file || !file.type.startsWith('image/')) return;

        if (previewUrl) URL.revokeObjectURL(previewUrl);
        previewUrl = URL.createObjectURL(file);
        const currentPreview = button.querySelector('[data-avatar-preview]');
        const preview = document.createElement('img');
        preview.className = 'profile-avatar';
        preview.dataset.avatarPreview = '';
        preview.alt = 'Ảnh đại diện mới';
        preview.src = previewUrl;
        currentPreview.replaceWith(preview);
    });
    window.addEventListener('pagehide', () => {
        if (previewUrl) URL.revokeObjectURL(previewUrl);
    }, { once: true });
});
