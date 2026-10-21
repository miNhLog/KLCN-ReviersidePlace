/**
 * Riverside Palace - VietQR PayOS Deposit Modal Handler
 * Real-time Polling, EMVCo QR Resolver, Robust Error & Timeout Handling, Confetti Celebration
 */

const RiversidePayment = (function () {
    let pollingInterval = null;
    let countdownInterval = null;
    let currentOrderCode = null;
    let redirectTimer = null;
    let qrTimeoutTimer = null;
    let lastBookingParams = null;
    let rawQrCode = null;
    let qrFallbackAttempted = false;

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

    /**
     * Chuyển đổi mã QR từ PayOS (chuỗi EMVCo hoặc URL) thành URL hình ảnh hiển thị được trên thẻ <img>
     */
    function resolveQrImageUrl(qrRaw) {
        if (!qrRaw) return '';
        const trimmed = String(qrRaw).trim();
        // Nếu đã là URL ảnh trực tiếp hoặc data:image
        if (trimmed.startsWith('http://') || trimmed.startsWith('https://') || trimmed.startsWith('data:image/')) {
            return trimmed;
        }
        // Chuỗi EMVCo VietQR chuẩn PayOS (bắt đầu bằng 000201...) -> Bọc qua API tạo QR chuẩn
        return `https://api.qrserver.com/v1/create-qr-code/?size=300x300&data=${encodeURIComponent(trimmed)}`;
    }

    /**
     * URL dự phòng nếu api.qrserver.com gặp sự cố mạng
     */
    function getQrFallbackUrl(qrRaw) {
        if (!qrRaw) return '';
        const trimmed = String(qrRaw).trim();
        return `https://quickchart.io/qr?size=300&text=${encodeURIComponent(trimmed)}`;
    }

    /**
     * Hiển thị giao diện báo lỗi và cho phép người dùng bấm Thử lại
     */
    function showQrError(errorMessage) {
        clearTimeout(qrTimeoutTimer);

        const loadingEl = document.getElementById('rpPayQrLoading');
        if (loadingEl) loadingEl.style.display = 'none';

        const qrImg = document.getElementById('rpPayQrImg');
        if (qrImg) qrImg.style.display = 'none';

        const errorEl = document.getElementById('rpPayQrError');
        if (errorEl) errorEl.style.display = 'flex';

        const errorMsgEl = document.getElementById('rpPayQrErrorMsg');
        if (errorMsgEl) {
            errorMsgEl.innerText = errorMessage || 'Không thể tải mã QR thanh toán. Vui lòng bấm thử lại hoặc liên hệ quản trị viên.';
        }

        const statusText = document.getElementById('rpPayStatusText');
        if (statusText) {
            statusText.innerText = errorMessage || 'Lỗi tải mã QR thanh toán.';
        }

        const retryBtn = document.getElementById('rpPayQrRetryBtn');
        if (retryBtn) {
            retryBtn.onclick = function () {
                if (lastBookingParams) {
                    openDepositModal(
                        lastBookingParams.bookingId,
                        lastBookingParams.bookingCode,
                        lastBookingParams.phoneNumber,
                        lastBookingParams.totalEstimate
                    );
                }
            };
        }
    }

    async function openDepositModal(bookingId, bookingCode, phoneNumber, totalEstimate) {
        const modalBackdrop = document.getElementById('rpPayModalBackdrop');
        if (!modalBackdrop) return;

        // Lưu thông số gần nhất để phục vụ nút Thử lại (Retry)
        lastBookingParams = {
            bookingId: bookingId,
            bookingCode: bookingCode,
            phoneNumber: phoneNumber,
            totalEstimate: totalEstimate
        };
        rawQrCode = null;
        qrFallbackAttempted = false;

        // Dọn dẹp các timer cũ
        clearInterval(pollingInterval);
        clearInterval(countdownInterval);
        clearTimeout(redirectTimer);
        clearTimeout(qrTimeoutTimer);

        // Reset trạng thái view
        document.getElementById('rpPayInitialContent').style.display = 'block';
        document.getElementById('rpPaySuccessContent').style.display = 'none';
        
        const loadingEl = document.getElementById('rpPayQrLoading');
        if (loadingEl) loadingEl.style.display = 'flex';
        const loadingText = document.getElementById('rpPayQrLoadingText');
        if (loadingText) loadingText.innerText = 'Đang khởi tạo mã VietQR từ PayOS...';

        const errorEl = document.getElementById('rpPayQrError');
        if (errorEl) errorEl.style.display = 'none';

        const qrImg = document.getElementById('rpPayQrImg');
        if (qrImg) {
            qrImg.style.display = 'none';
            qrImg.onload = null;
            qrImg.onerror = null;
            qrImg.removeAttribute('src');
        }

        document.getElementById('rpPayStatusText').innerText = 'Đang khởi tạo mã VietQR từ PayOS...';

        // Điền trước thông tin cơ bản
        document.getElementById('rpPayBookingCode').innerText = bookingCode || ('#' + bookingId);
        const estTotal = Number(totalEstimate || 0);
        const depositEst = Math.round(estTotal * 0.10);
        document.getElementById('rpPayDepositVal').innerText = formatVnd(depositEst);

        // Mở modal backdrop
        modalBackdrop.classList.add('is-open');
        document.body.style.overflow = 'hidden';

        // Cơ chế Timeout an toàn: tối đa 15 giây nếu không tải được QR thì hiển thị nút Thử lại
        qrTimeoutTimer = setTimeout(() => {
            console.warn('Hết hạn chờ tải mã VietQR (15 giây).');
            showQrError('Quá thời gian tải mã QR thanh toán (15 giây). Vui lòng bấm "Thử lại" để làm mới.');
        }, 15000);

        try {
            // Gọi Backend tạo link cọc
            const response = await fetch('/Payment/CreateDeposit', {
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
            });

            if (!response.ok) {
                throw new Error(`Máy chủ phản hồi lỗi (${response.status}): ${response.statusText}`);
            }

            const result = await response.json();
            if (!result.success || !result.data) {
                throw new Error(result.message || 'Không thể tạo mã VietQR thanh toán cọc lúc này.');
            }

            const data = result.data;
            currentOrderCode = data.orderCode;
            rawQrCode = data.qrCode || '';

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
                copyAccBtn.onclick = function () { copyToClipboard(data.accountNumber, copyAccBtn); };
            }
            const copyDescBtn = document.getElementById('rpPayCopyDescBtn');
            if (copyDescBtn) {
                copyDescBtn.onclick = function () { copyToClipboard(data.description, copyDescBtn); };
            }

            // Xử lý hiển thị ảnh QR với onload, onerror và fallback
            if (qrImg) {
                const targetQrUrl = resolveQrImageUrl(rawQrCode);

                qrImg.onload = function () {
                    // Xóa timeout khi ảnh tải thành công
                    clearTimeout(qrTimeoutTimer);
                    if (loadingEl) loadingEl.style.display = 'none';
                    if (errorEl) errorEl.style.display = 'none';
                    qrImg.style.display = 'block';
                    document.getElementById('rpPayStatusText').innerText = 'Đang chờ hệ thống ngân hàng xác nhận giao dịch...';
                };

                qrImg.onerror = function () {
                    console.warn('Lỗi khi tải ảnh QR từ:', qrImg.src);
                    // Thử fallback sang QuickChart 1 lần nếu chuỗi ban đầu là EMVCo
                    if (!qrFallbackAttempted && rawQrCode && !rawQrCode.startsWith('http://') && !rawQrCode.startsWith('https://')) {
                        qrFallbackAttempted = true;
                        console.info('Thử fallback sang QuickChart QR API...');
                        qrImg.src = getQrFallbackUrl(rawQrCode);
                        return;
                    }
                    clearTimeout(qrTimeoutTimer);
                    showQrError('Không thể tải mã QR thanh toán. Vui lòng bấm thử lại hoặc liên hệ quản trị viên.');
                };

                // Bắt đầu tải ảnh QR
                qrImg.src = targetQrUrl;
            }

            // Đồng hồ đếm ngược 10 phút
            startCountdown(10 * 60);

            // Bắt đầu Polling kiểm tra trạng thái mỗi 2 giây
            startPolling(data.orderCode, bookingCode);

        } catch (err) {
            console.error('Lỗi quy trình tạo thanh toán cọc:', err);
            clearInterval(countdownInterval);
            clearInterval(pollingInterval);
            clearTimeout(qrTimeoutTimer);
            showQrError(err.message || 'Không thể kết nối đến máy chủ thanh toán.');
        }
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
        clearTimeout(qrTimeoutTimer);

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
        clearTimeout(qrTimeoutTimer);
        currentOrderCode = null;
        rawQrCode = null;
        qrFallbackAttempted = false;

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
