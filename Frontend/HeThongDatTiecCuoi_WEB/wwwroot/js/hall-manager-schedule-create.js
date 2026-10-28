(() => {
    'use strict';
    const form = document.getElementById('hmCreateForm');
    if (!form) return;
    const $ = id => document.getElementById(id);
    const booking = $('hmBooking'), hall = $('hmNewHall'), day = $('hmNewDate'), shift = $('hmNewShift');
    const submit = $('hmCreateSubmit'), status = $('hmAvailabilityStatus'), summary = $('hmBookingSummary');
    const exception = form.elements.namedItem('ExceptionReason');
    const dateKey = value => String(value).slice(0, 10);
    const fmt = value => dateKey(value).split('-').reverse().join('/');
    const capacity = h => h.minimumCapacity == null ? `tối đa ${h.maximumCapacity} khách (không giới hạn tối thiểu)` : `${h.minimumCapacity}–${h.maximumCapacity} khách`;
    let options = null, halls = [], checkedKey = '', revision = 0, optionsRevision = 0;
    let pending = null;
    const key = () => [booking.value, day.value, shift.value].join('|');
    const option = (value, text, disabled = false) => { const item = document.createElement('option'); item.value = value; item.textContent = text; item.disabled = disabled; return item; };
    const selectedBooking = () => options?.bookings.find(b => String(b.bookingId) === booking.value);
    function ready(value) { form.dataset.scheduleReady = String(value); submit.disabled = !value; }
    function clearHalls(message) {
        revision++; pending?.abort(); pending = null; checkedKey = ''; halls = [];
        hall.replaceChildren(option('', 'Chọn tiệc, ngày và ca để xem sảnh trống')); hall.disabled = true;
        $('hmUnavailableHalls').hidden = true; $('hmUnavailableHalls').textContent = '';
        status.textContent = message; ready(false);
    }
    async function get(url, signal) {
        const response = await fetch(url, { headers: { Accept: 'application/json' }, cache: 'no-store', signal });
        if (response.status === 401 || response.redirected) throw new Error('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
        if (!response.headers.get('content-type')?.includes('json')) throw new Error('Không đọc được dữ liệu từ máy chủ. Vui lòng thử lại.');
        const body = await response.json();
        if (!response.ok || !body.success) throw new Error(body.message || 'Không thể tải dữ liệu. Vui lòng thử lại.');
        return body.data;
    }
    function updateSummary() {
        const item = selectedBooking();
        summary.hidden = !item;
        summary.textContent = item ? `${item.bookingCode} · ${item.customerName} — Lịch hiện tại: ${fmt(item.currentDate)} · ${item.currentShift} · ${item.currentHallName} · ${item.guestCount} khách` : '';
        const days = item ? (Date.parse(dateKey(item.currentDate) + 'T00:00:00Z') - Date.parse(dateKey(options.today) + 'T00:00:00Z')) / 86400000 : Infinity;
        exception.required = Boolean(item && days < options.exceptionThresholdDays);
        exception.placeholder = exception.required ? `Bắt buộc giải trình vì còn dưới ${options.exceptionThresholdDays} ngày đến tiệc.` : 'Nhập giải trình nếu có ngoại lệ.';
    }
    function renderBookings() {
        const selected = booking.value;
        const query = $('hmBookingSearch').value.trim().toLocaleLowerCase('vi');
        const matches = (options?.bookings || []).filter(b => `${b.bookingCode} ${b.customerName}`.toLocaleLowerCase('vi').includes(query));
        booking.replaceChildren(option('', matches.length ? 'Chọn mã tiệc và khách hàng' : 'Không có tiệc phù hợp, đủ điều kiện đổi lịch'));
        matches.forEach(b => booking.appendChild(option(String(b.bookingId), `${b.bookingCode} · ${b.customerName} · ${fmt(b.currentDate)} · ${b.currentHallName}`)));
        booking.disabled = !matches.length;
        booking.value = matches.some(b => String(b.bookingId) === selected) ? selected : '';
        if (booking.value !== selected) { clearHalls('Chọn lại tiệc để kiểm tra lịch trống.'); updateSummary(); }
    }
    async function loadOptions() {
        const request = ++optionsRevision;
        options = null; booking.disabled = true; booking.replaceChildren(option('', 'Đang tải danh sách tiệc…'));
        summary.hidden = true; exception.required = false;
        clearHalls('Đang tải các tiệc thuộc sảnh phụ trách…'); $('hmRetryOptions').hidden = true;
        try {
            const data = await get(form.dataset.optionsUrl);
            if (request !== optionsRevision) return;
            options = data; day.min = dateKey(data.minimumNewDate);
            renderBookings(); updateSummary();
            status.textContent = data.bookings.length ? 'Chọn tiệc, ngày và ca để kiểm tra lịch trống.' : 'Không có tiệc đủ điều kiện đổi lịch trong sảnh phụ trách; tiệc đã có yêu cầu đang xử lý sẽ không xuất hiện.';
        } catch (error) {
            if (request !== optionsRevision) return;
            booking.replaceChildren(option('', 'Không tải được danh sách tiệc'));
            status.textContent = error.message; $('hmRetryOptions').hidden = false;
        }
    }
    async function checkAvailability() {
        clearHalls('Chọn tiệc, ngày và ca để kiểm tra lịch trống.');
        if (!selectedBooking() || !day.value || !shift.value) return;
        if (!day.checkValidity()) { status.textContent = `Ngày mới phải từ ${fmt(day.min)} trở đi.`; return; }
        const request = revision, requestKey = key();
        pending = new AbortController();
        status.textContent = 'Đang kiểm tra lịch và sức chứa sảnh…';
        hall.replaceChildren(option('', 'Đang kiểm tra lịch trống…'));
        try {
            const url = new URL(form.dataset.availabilityUrl, location.href);
            url.search = new URLSearchParams({ bookingId: booking.value, date: day.value, shift: shift.value });
            const result = await get(url, pending.signal);
            if (request !== revision || requestKey !== key()) return;
            halls = result; checkedKey = requestKey;
            const count = halls.filter(h => h.available).length;
            hall.replaceChildren(option('', count ? 'Chọn sảnh còn trống' : 'Không có sảnh phù hợp còn trống'));
            halls.forEach(h => hall.appendChild(option(String(h.hallId), `${h.hallName} · ${capacity(h)}${h.available ? ' · Còn trống, phù hợp số khách' : ' · ' + h.unavailableReason}`, !h.available)));
            const unavailable = halls.filter(h => !h.available);
            $('hmUnavailableHalls').hidden = !unavailable.length;
            $('hmUnavailableHalls').textContent = unavailable.map(h => `${h.hallName} (${capacity(h)}): ${h.unavailableReason}`).join('\n');
            hall.disabled = !count;
            status.textContent = count ? `${count} sảnh còn trống, phù hợp sức chứa cho ${fmt(day.value)} · ${shift.value}. Chọn sảnh để đối chiếu trước khi gửi.` : 'Không có sảnh phù hợp còn trống. Các lựa chọn bên dưới ghi rõ lý do; hãy đổi ngày hoặc ca.';
        } catch (error) {
            if (request !== revision || error.name === 'AbortError') return;
            hall.replaceChildren(option('', 'Không kiểm tra được lịch trống'));
            status.textContent = error.message;
        }
    }
    booking.addEventListener('change', () => { updateSummary(); checkAvailability(); });
    day.addEventListener('change', checkAvailability); shift.addEventListener('change', checkAvailability);
    day.addEventListener('input', () => clearHalls('Ngày mới đã thay đổi; cần kiểm tra lại lịch trống.'));
    $('hmBookingSearch').addEventListener('input', renderBookings);
    hall.addEventListener('change', () => {
        const selected = halls.find(h => String(h.hallId) === hall.value && h.available);
        const valid = Boolean(selected && checkedKey === key()); ready(valid);
        if (valid) status.textContent = `Lịch đề xuất: ${fmt(day.value)} · ${shift.value} · ${selected.hallName} · sức chứa ${capacity(selected)}. Tiệc có ${selectedBooking().guestCount} khách, phù hợp sức chứa sảnh. Lịch hiện còn trống, chưa giữ chỗ.`;
    });
    $('hmCheckAvailability').addEventListener('click', checkAvailability);
    $('hmRetryOptions').addEventListener('click', loadOptions);
    form.addEventListener('submit', event => {
        if (checkedKey !== key() || !halls.some(h => String(h.hallId) === hall.value && h.available)) {
            event.preventDefault(); event.stopImmediatePropagation(); ready(false);
            status.textContent = 'Vui lòng chọn sảnh từ kết quả kiểm tra lịch trống của đúng tiệc, ngày và ca đang chọn.';
        }
    }, true);
    document.querySelectorAll('[data-open="hmCreate"]').forEach(button => button.addEventListener('click', loadOptions));
    ready(false);
})();
