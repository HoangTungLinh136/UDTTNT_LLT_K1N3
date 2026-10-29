(() => {
    'use strict';
    const dashboard = document.getElementById('manager-dashboard');
    if (!dashboard) return;
    dashboard.querySelectorAll('[data-dashboard-filter]').forEach(form => {
        form.addEventListener('submit', () => {
            dashboard.setAttribute('aria-busy', 'true');
            dashboard.querySelectorAll('.md-loading').forEach(status => { status.hidden = false; });
        });
    });
    window.addEventListener('pageshow', () => {
        dashboard.removeAttribute('aria-busy');
        dashboard.querySelectorAll('.md-loading').forEach(status => { status.hidden = true; });
    });
})();
