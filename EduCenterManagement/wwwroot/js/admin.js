document.querySelector('.admin-menu')?.addEventListener('click', function () {
    const open = document.body.classList.toggle('admin-nav-open');
    this.setAttribute('aria-expanded', String(open));
});
document.addEventListener('keydown', function (event) {
    if (event.key === 'Escape') {
        document.body.classList.remove('admin-nav-open');
        document.querySelector('.admin-menu')?.setAttribute('aria-expanded', 'false');
    }
});
