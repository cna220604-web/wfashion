'use strict';

const userManagement = document.querySelector('[data-admin-user-management]');
if (userManagement) {
    const groupButtons = [...userManagement.querySelectorAll('[data-user-group-tab]')];
    const groupPanels = [...userManagement.querySelectorAll('[data-user-group-panel]')];

    groupButtons.forEach(button => {
        button.addEventListener('click', () => {
            const selectedGroup = button.dataset.userGroupTab;
            groupButtons.forEach(groupButton => {
                const isSelected = groupButton === button;
                groupButton.classList.toggle('is-active', isSelected);
                groupButton.setAttribute('aria-pressed', String(isSelected));
            });
            groupPanels.forEach(panel => {
                panel.hidden = panel.dataset.userGroupPanel !== selectedGroup;
            });
        });
    });

    groupPanels.forEach(panel => {
        const searchInput = panel.querySelector('[data-user-search]');
        const rows = [...panel.querySelectorAll('[data-user-row]')];
        const noResults = panel.querySelector('[data-user-no-results]');
        if (!searchInput) return;

        searchInput.addEventListener('input', () => {
            const query = searchInput.value.trim().toLocaleLowerCase('vi');
            let visibleCount = 0;
            rows.forEach(row => {
                const searchText = (row.dataset.userSearchText || row.textContent || '').toLocaleLowerCase('vi');
                const matches = searchText.includes(query);
                row.hidden = !matches;
                if (matches) visibleCount++;
            });
            if (noResults) noResults.hidden = visibleCount > 0;
        });
    });
}
