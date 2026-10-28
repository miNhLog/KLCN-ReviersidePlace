(() => {
    'use strict';

    document.querySelectorAll('[data-open]').forEach(button => {
        button.addEventListener('click', () => {
            const modal = document.getElementById(button.dataset.open);
            if (modal) modal.hidden = false;
        });
    });

    document.querySelectorAll('.sca-close').forEach(button => {
        button.addEventListener('click', () => {
            const modal = button.closest('.sca-modal');
            if (modal) modal.hidden = true;
        });
    });

    document.querySelectorAll('.sca-modal').forEach(modal => {
        modal.addEventListener('click', event => {
            if (event.target === modal) modal.hidden = true;
        });
    });

    document.addEventListener('keydown', event => {
        if (event.key === 'Escape') {
            document.querySelectorAll('.sca-modal:not([hidden])').forEach(modal => modal.hidden = true);
        }
    });

    document.querySelectorAll('.sca-ajax').forEach(form => {
        form.addEventListener('submit', async event => {
            event.preventDefault();
            if (form.dataset.scheduleLookup === 'true' && form.dataset.scheduleReady !== 'true') return;
            if (!form.reportValidity()) return;
            if (form.querySelector('.danger, .sca-danger') && !confirm('Xác nhận từ chối yêu cầu này?')) return;

            const button = form.querySelector('button[type="submit"], button:not([type])');
            if (button) {
                button.disabled = true;
                button.dataset.text = button.textContent;
                button.textContent = 'Đang xử lý...';
            }

            try {
                const response = await fetch(form.action, {
                    method: 'POST',
                    body: new FormData(form),
                    headers: {
                        'RequestVerificationToken': form.querySelector('[name="__RequestVerificationToken"]')?.value || ''
                    }
                });
                const result = await response.json();
                if (!response.ok || !result.success) {
                    throw new Error(result.message || ({
                        403: 'Bạn không có quyền thao tác yêu cầu này.',
                        409: 'Dữ liệu hoặc lịch sảnh vừa thay đổi.'
                    }[response.status]) || 'Không thể hoàn tất yêu cầu.');
                }
                alert('Đã cập nhật thành công.');
                location.reload();
            } catch (error) {
                alert(error.message);
            } finally {
                if (button) {
                    button.disabled = false;
                    button.textContent = button.dataset.text;
                }
            }
        });
    });
})();
