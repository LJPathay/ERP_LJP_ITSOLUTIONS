// Small helper to toggle sidebar on mobile
(function(){
    const sidebar = document.querySelector('.sidebar');
    const toggle = document.getElementById('sidebarToggle');
    if (!sidebar || !toggle) return;

    toggle.addEventListener('click', () => {
        sidebar.classList.toggle('show');
    });

    // Click outside sidebar to close on small screens
    document.addEventListener('click', (e) => {
        if (window.innerWidth > 768) return;
        if (!sidebar.classList.contains('show')) return;
        if (e.target.closest('.sidebar') || e.target.closest('#sidebarToggle')) return;
        sidebar.classList.remove('show');
    });

    // Ensure sidebar is visible on larger screens
    window.addEventListener('resize', () => {
        if (window.innerWidth >= 768) {
            sidebar.classList.remove('show');
        }
    });
})();
