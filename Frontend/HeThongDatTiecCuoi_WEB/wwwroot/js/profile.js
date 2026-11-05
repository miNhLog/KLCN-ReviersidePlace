document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('.profile-password-input .toggle-password').forEach(button => {
        button.addEventListener('click', () => {
            const input = button.parentElement?.querySelector('input');
            const icon = button.querySelector('i');
            if (!input || !icon) return;
            const isHidden = input.type === 'password';
            input.type = isHidden ? 'text' : 'password';
            icon.classList.toggle('bi-eye', !isHidden);
            icon.classList.toggle('bi-eye-slash', isHidden);
            button.setAttribute('aria-label', isHidden ? 'Ẩn mật khẩu' : 'Hiện mật khẩu');
        });
    });
});
