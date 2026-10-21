/**
 * Riverside Palace - VietQR PayOS Deposit Modal Handler
 * Real-time Polling & Confetti Celebration
 */

const RiversidePayment = (function () {
    let pollingInterval = null;
    let countdownInterval = null;
    let currentOrderCode = null;
    let redirectTimer = null;

    function formatVnd(amount) {
        return Number(amount || 0).toLocaleString('vi-VN') + ' đ';
    }

    function copyToClipboard(text, btnElement) {
        if (!text) return;
        navigator.clipboard.writeText(text).then(() => {
            const originalText = btnElement.innerText;
            btnElement.innerText = '✓ Đã chép';
            btnElement.style.background = '#10B981';
            btnElement.style.color = '#FFFFFF';
            setTimeout(() => {
                btnElement.innerText = originalText;
                btnElement.style.background = '';
                btnElement.style.color = '';
            }, 1800);
        }).catch(err => {
            console.error('Không thể sao chép:', err);
        });
    }

    function openDepositModal(bookingId, bookingCode, phoneNumber, totalEstimate) {
        const modalBackdrop = document.getElementById('rpPayModalBackdrop');
        if (!modalBackdrop) return;

        // Reset view states
        document.getElementById('rpPayInitialContent').style.display = 'block';
        document.getElementById('rpPaySuccessContent').style.display = 'none';
        document.getElementById('rpPayQrLoading').style.display = 'flex';
        document.getElementById('rpPayQrImg').style.display = 'none';
        document.getElementById('rpPayStatusText').innerText = 'Đang khởi tạo mã VietQR từ PayOS...';

        // Điền trước thông tin cơ bản
        document.getElementById('rpPayBookingCode').innerText = bookingCode || ('#' + bookingId);
        const estTotal = Number(totalEstimate || 0);
        const depositEst = Math.round(estTotal * 0.10);
        document.getElementById('rpPayDepositVal').innerText = formatVnd(depositEst);

        // Mở modal
        modalBackdrop.classList.add('is-open');
        document.body.style.overflow = 'hidden';

        // Gọi Backend tạo link cọc
        fetch('/Payment/CreateDeposit', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: JSON.stringify({
                bookingId: parseInt(bookingId),
                bookingCode: bookingCode,
                phoneNumber: phoneNumber
            })
        })
        .then(res => res.json())
        .then(result => {
            if (!result.success || !result.data) {
                alert(result.message || 'Không thể tạo mã VietQR thanh toán cọc lúc này.');
                closeDepositModal();
                return;
            }

            const data = result.data;
            currentOrderCode = data.orderCode;

            // Hiển thị thông tin thực tế từ PayOS
            document.getElementById('rpPayDepositVal').innerText = formatVnd(data.businessDeposit);
            document.getElementById('rpPayActualAmount').innerText = formatVnd(data.actualAmount);
            document.getElementById('rpPayAccountName').innerText = data.accountName || 'CONG TY CP RIVERSIDE PALACE';
            document.getElementById('rpPayAccountNumber').innerText = data.accountNumber || '--';
            document.getElementById('rpPayBankName').innerText = data.bankName || data.bankBin || 'VietinBank';
            document.getElementById('rpPayDescription').innerText = data.description || ('RSP ' + bookingId);

            // Gắn sự kiện copy
            const copyAccBtn = document.getElementById('rpPayCopyAccBtn');
            if (copyAccBtn) {
                copyAccBtn.onclick = function() { copyToClipboard(data.accountNumber, copyAccBtn); };
            }
            const copyDescBtn = document.getElementById('rpPayCopyDescBtn');
            if (copyDescBtn) {
                copyDescBtn.onclick = function() { copyToClipboard(data.description, copyDescBtn); };
            }

            // Hiển thị ảnh QR
            const qrImg = document.getElementById('rpPayQrImg');
            if (qrImg) {
                qrImg.src = data.qrCode;
                qrImg.onload = function() {
                    document.getElementById('rpPayQrLoading').style.display = 'none';
                    qrImg.style.display = 'block';
                };
            }

            // Đồng hồ đếm ngược 10 phút
            startCountdown(10 * 60);

            // Bắt đầu Polling kiểm tra trạng thái mỗi 2 giây
            startPolling(data.orderCode, bookingCode);
        })
        .catch(err => {
            console.error('Lỗi gọi API create deposit:', err);
            alert('Không thể kết nối đến máy chủ thanh toán.');
            closeDepositModal();
        });
    }

    function startCountdown(durationSeconds) {
        clearInterval(countdownInterval);
        let remain = durationSeconds;
        const timerEl = document.getElementById('rpPayCountdownVal');

        function update() {
            if (remain <= 0) {
                clearInterval(countdownInterval);
                clearInterval(pollingInterval);
                if (timerEl) timerEl.innerText = '00:00 (Hết hạn)';
                document.getElementById('rpPayStatusText').innerText = 'Giao dịch đã hết thời gian hiệu lực.';
                return;
            }
            const m = Math.floor(remain / 60);
            const s = remain % 60;
            if (timerEl) {
                timerEl.innerText = `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
            }
            remain--;
        }

        update();
        countdownInterval = setInterval(update, 1000);
    }

    function startPolling(orderCode, bookingCode) {
        clearInterval(pollingInterval);
        document.getElementById('rpPayStatusText').innerText = 'Đang chờ hệ thống ngân hàng xác nhận giao dịch...';

        pollingInterval = setInterval(() => {
            if (!orderCode) return;

            fetch(`/Payment/CheckStatus?orderCode=${orderCode}`)
                .then(res => res.json())
                .then(result => {
                    if (result.success && result.data) {
                        const status = (result.data.status || '').toUpperCase();
                        if (status === 'PAID') {
                            onPaymentSuccess(result.data, bookingCode);
                        } else if (status === 'CANCELLED') {
                            clearInterval(pollingInterval);
                            clearInterval(countdownInterval);
                            document.getElementById('rpPayStatusText').innerText = 'Giao dịch đã bị hủy.';
                        }
                    }
                })
                .catch(err => {
                    console.warn('Lỗi polling trạng thái thanh toán:', err);
                });
        }, 2000);
    }

    function onPaymentSuccess(data, bookingCode) {
        clearInterval(pollingInterval);
        clearInterval(countdownInterval);

        // Đổi giao diện sang trạng thái thành công
        document.getElementById('rpPayInitialContent').style.display = 'none';
        document.getElementById('rpPaySuccessContent').style.display = 'block';
        document.getElementById('rpPaySuccessAmount').innerText = formatVnd(data.businessDeposit);
        document.getElementById('rpPaySuccessBookingCode').innerText = bookingCode || data.bookingCode || 'Đơn tiệc của bạn';

        // Kích hoạt pháo hoa chúc mừng Confetti
        fireConfetti();

        // Tự động reload lại trang sau 3.5 giây để cập nhật trạng thái đơn tiệc và lịch sảnh
        redirectTimer = setTimeout(() => {
            window.location.reload();
        }, 3500);
    }

    function fireConfetti() {
        if (typeof confetti === 'function') {
            confetti({
                particleCount: 100,
                spread: 70,
                origin: { y: 0.6 }
            });
            setTimeout(() => {
                confetti({
                    particleCount: 60,
                    angle: 60,
                    spread: 55,
                    origin: { x: 0 }
                });
                confetti({
                    particleCount: 60,
                    angle: 120,
                    spread: 55,
                    origin: { x: 1 }
                });
            }, 300);
        }
    }

    function closeDepositModal() {
        clearInterval(pollingInterval);
        clearInterval(countdownInterval);
        clearTimeout(redirectTimer);
        currentOrderCode = null;

        const modalBackdrop = document.getElementById('rpPayModalBackdrop');
        if (modalBackdrop) {
            modalBackdrop.classList.remove('is-open');
        }
        document.body.style.overflow = '';
    }

    return {
        open: openDepositModal,
        close: closeDepositModal
    };
})();

// Khởi tạo sự kiện khi DOM sẵn sàng
document.addEventListener('DOMContentLoaded', function () {
    // Tự động kiểm tra URL query param ?autoDeposit=true
    const urlParams = new URLSearchParams(window.location.search);
    if (urlParams.get('autoDeposit') === 'true') {
        const depositBtn = document.getElementById('btnOpenDepositModal');
        if (depositBtn) {
            depositBtn.click();
        }
    }
});
