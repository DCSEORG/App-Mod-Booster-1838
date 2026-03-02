// site.js - Expense Management general utilities

document.addEventListener('DOMContentLoaded', function () {
    // Auto-dismiss success messages after 5 seconds
    const alerts = document.querySelectorAll('.alert-success');
    alerts.forEach(function (alert) {
        setTimeout(function () {
            alert.style.opacity = '0';
            alert.style.transition = 'opacity 0.5s';
            setTimeout(function () { alert.remove(); }, 500);
        }, 5000);
    });

    // Confirm dangerous actions
    document.querySelectorAll('[data-confirm]').forEach(function (el) {
        el.addEventListener('click', function (e) {
            if (!confirm(el.dataset.confirm)) {
                e.preventDefault();
            }
        });
    });
});
