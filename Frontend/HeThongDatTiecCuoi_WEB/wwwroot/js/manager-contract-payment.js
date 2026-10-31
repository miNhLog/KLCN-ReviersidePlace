(() => {
    const root = document.querySelector('[data-page-root]');
    if (!root) return;

    const base = '/quan-ly/hop-dong-thanh-toan';
    const token = document.querySelector('#cpAntiforgery input[name="__RequestVerificationToken"]')?.value || '';
    const state = { contractPage: 1, paymentPage: 1, paymentsLoaded: false, selectedBooking: null, currentContract: null, confirmCallback: null, payosPollTimer: null, currentPayosOrder: null, currentPayosContractId: null, payosCheckoutUrl: null };
    const byId = id => document.getElementById(id);
    const money = value => `${Number(value || 0).toLocaleString('vi-VN')} đ`;
    const date = value => value ? new Date(value).toLocaleDateString('vi-VN') : '—';
    const percentage = value => Number(value || 0).toLocaleString('vi-VN', { maximumFractionDigits: 1 });
    const createNode = (tag, className, text) => {
        const element = document.createElement(tag);
        if (className) element.className = className;
        if (text !== undefined && text !== null) element.textContent = text;
        return element;
    };
    const iconNode = className => {
        const icon = createNode('i', className);
        icon.setAttribute('aria-hidden', 'true');
        return icon;
    };

    async function request(url, options = {}) {
        const headers = { ...(options.headers || {}) };
        if (options.body !== undefined) headers['Content-Type'] = 'application/json';
        if (options.method && options.method !== 'GET') headers.RequestVerificationToken = token;
        const response = await fetch(url, { ...options, headers });
        let payload = null;
        try { payload = await response.json(); } catch { /* PDF and empty bodies are not used here. */ }
        if (!response.ok || payload?.success === false) throw new Error(payload?.message || 'Yêu cầu không thành công.');
        return payload?.data ?? payload;
    }

    function showToast(message, isError = false) {
        if (isError) window.RiversideToast?.error(message);
        else window.RiversideToast?.success(message);
    }

    function statusBadge(code, name) {
        const normalized = String(code || '').toLowerCase();
        const css = ['draft', 'active', 'completed', 'cancelled', 'pending', 'success', 'failed'].includes(normalized) ? normalized : 'draft';
        return createNode('span', `cp-badge ${css}`, name || code || '—');
    }

    function queryString(values) {
        const params = new URLSearchParams();
        Object.entries(values).forEach(([key, value]) => {
            if (value !== null && value !== undefined && String(value).trim() !== '' && value !== 'ALL') params.set(key, value);
        });
        return params.toString();
    }

    async function loadContracts(page = 1) {
        state.contractPage = page;
        try {
            const data = await request(`${base}/contracts?${queryString({
                keyword: byId('contractKeyword').value,
                status: byId('contractStatus').value,
                fromDate: byId('contractFromDate').value,
                toDate: byId('contractToDate').value,
                page
            })}`);
            renderContractRows(data.items || []);
            byId('kpiTotal').textContent = data.statistics.totalContracts;
            byId('kpiActive').textContent = data.statistics.activeContracts;
            byId('kpiAwaiting').textContent = data.statistics.awaitingPaymentContracts;
            byId('kpiRevenue').querySelector('.cp-kpi-revenue-value').textContent = Number(data.statistics.totalCollectedRevenue || 0).toLocaleString('vi-VN');
            updateKpiRatios(data.statistics);
            byId('contractPageInfo').textContent = `Trang ${Math.max(1, data.page)} / ${Math.max(1, data.totalPages)} · ${data.totalItems} hợp đồng`;
            renderPages(byId('contractPages'), data.page, data.totalPages, loadContracts);
        } catch (error) { showToast(error.message, true); }
    }

    function updateKpiRatios(statistics) {
        const total = Math.max(0, Number(statistics.totalContracts || 0));
        const setRatio = (value, barId, labelId) => {
            const ratio = total > 0 ? Math.max(0, Math.min(100, Math.round(Number(value || 0) * 100 / total))) : 0;
            byId(barId).style.width = `${ratio}%`;
            byId(labelId).textContent = `${ratio}%`;
        };
        setRatio(statistics.activeContracts, 'kpiActiveBar', 'kpiActivePercent');
        setRatio(statistics.awaitingPaymentContracts, 'kpiAwaitingBar', 'kpiAwaitingPercent');
    }

    function renderContractRows(items) {
        const body = byId('contractRows');
        body.replaceChildren();
        if (!items.length) {
            const row = body.insertRow();
            const cell = row.insertCell();
            cell.colSpan = 8; cell.className = 'cp-empty'; cell.textContent = 'Chưa có hợp đồng phù hợp.';
            return;
        }
        items.forEach(item => {
            const row = body.insertRow();
            row.dataset.contractId = item.contractId;
            row.addEventListener('click', () => openDrawer(item.contractId));
            const codeCell = row.insertCell(); codeCell.append(createNode('span', 'cp-code', item.contractCode));
            const customer = row.insertCell(); customer.className = 'cp-customer'; customer.append(createNode('strong', '', item.customerName), createNode('small', '', item.customerPhone));
            const booking = row.insertCell(); booking.append(createNode('span', 'cp-code', item.bookingCode));
            row.insertCell().textContent = date(item.createdDate);
            const total = row.insertCell(); total.className = 'cp-money'; total.textContent = money(item.totalValue);
            row.insertCell().append(statusBadge(item.statusCode, item.statusName));
            const progressCell = row.insertCell();
            const progress = createNode('div', 'cp-progress');
            const track = createNode('div', 'cp-progress-track');
            const bar = createNode('div', 'cp-progress-bar');
            bar.style.width = `${Math.max(0, Math.min(100, Number(item.paymentPercentage || 0)))}%`;
            track.append(bar);
            const meta = createNode('div', 'cp-progress-meta');
            meta.append(createNode('span', '', `Đã thu ${money(item.paidAmount)}`), createNode('b', '', `${percentage(item.paymentPercentage)}%`));
            progress.append(track, meta);
            if (item.hasPaymentInconsistency) progress.append(createNode('div', 'cp-warning', 'Thanh toán vượt giá trị'));
            progressCell.append(progress);
            const actions = row.insertCell();
            const view = createNode('button', 'cp-icon-btn'); view.type = 'button'; view.title = 'Xem chi tiết';
            const icon = createNode('i', 'fa-regular fa-eye'); view.append(icon);
            view.addEventListener('click', event => { event.stopPropagation(); openDrawer(item.contractId); });
            actions.append(view);
        });
    }

    async function loadPayments(page = 1) {
        state.paymentPage = page;
        try {
            const data = await request(`${base}/payments?${queryString({
                keyword: byId('paymentKeyword').value,
                type: byId('paymentType').value,
                status: byId('paymentStatus').value,
                fromDate: byId('paymentFromDate').value,
                toDate: byId('paymentToDate').value,
                page
            })}`);
            state.paymentsLoaded = true;
            renderPaymentRows(data.items || []);
            byId('paymentPageInfo').textContent = `Trang ${Math.max(1, data.page)} / ${Math.max(1, data.totalPages)} · ${data.totalItems} giao dịch · Chỉ đọc`;
            renderPages(byId('paymentPages'), data.page, data.totalPages, loadPayments);
        } catch (error) { showToast(error.message, true); }
    }

    function renderPaymentRows(items) {
        const body = byId('paymentRows'); body.replaceChildren();
        if (!items.length) {
            const row = body.insertRow(); const cell = row.insertCell(); cell.colSpan = 9; cell.className = 'cp-empty'; cell.textContent = 'Chưa có giao dịch phù hợp.'; return;
        }
        items.forEach(item => {
            const row = body.insertRow(); row.addEventListener('click', () => openDrawer(item.contractId, 'payments'));
            row.insertCell().append(createNode('span', 'cp-code', item.transactionCode || `#${item.paymentId}`));
            row.insertCell().append(createNode('span', 'cp-code', item.contractCode));
            row.insertCell().append(createNode('span', 'cp-code', item.bookingCode));
            const customer = row.insertCell(); customer.className = 'cp-customer'; customer.append(createNode('strong', '', item.customerName), createNode('small', '', item.customerPhone));
            row.insertCell().textContent = item.paymentType;
            const amount = row.insertCell(); amount.className = 'cp-money'; amount.textContent = money(item.amount);
            row.insertCell().textContent = date(item.paymentDate);
            row.insertCell().textContent = item.paymentMethod || '—';
            row.insertCell().append(statusBadge(item.statusCode, item.statusName));
        });
    }

    function renderPages(container, current, total, callback) {
        container.replaceChildren(); total = Math.max(1, total || 1); current = Math.max(1, current || 1);
        const add = (label, page, disabled, active = false) => {
            const button = createNode('button', `cp-page-btn${active ? ' active' : ''}`, label);
            button.type = 'button'; button.disabled = disabled; if (!disabled) button.addEventListener('click', () => callback(page)); container.append(button);
        };
        add('‹', current - 1, current === 1);
        const first = Math.max(1, Math.min(current - 2, total - 4));
        const last = Math.min(total, first + 4);
        for (let page = first; page <= last; page++) add(String(page), page, false, page === current);
        add('›', current + 1, current === total);
    }

    async function openDrawer(contractId, targetTab = 'general') {
        byId('drawerOverlay').classList.add('open'); byId('contractDrawer').classList.add('open');
        byId('drawerLoading').classList.remove('cp-hidden');
        ['drawerGeneral', 'drawerBooking', 'drawerPayments', 'drawerHistory'].forEach(id => byId(id).replaceChildren());
        try {
            const detail = await request(`${base}/contracts/${contractId}`);
            state.currentContract = detail;
            renderDrawer(detail);
            switchDrawerTab(targetTab);
        } catch (error) { showToast(error.message, true); closeDrawer(); }
    }

    function renderDrawer(detail) {
        byId('drawerLoading').classList.add('cp-hidden');
        byId('drawerCode').textContent = detail.contractCode;
        byId('drawerMeta').textContent = `Ngày lập ${date(detail.createdDate)}`;
        const status = byId('drawerStatus'); status.replaceChildren(statusBadge(detail.statusCode, detail.statusName));

        const general = byId('drawerGeneral');
        general.append(contractOverview(detail));
        general.append(detailCard('Thông tin hợp đồng và khách hàng', [
            ['Mã hợp đồng', detail.contractCode], ['Mã booking', detail.booking.bookingCode],
            ['Khách hàng', detail.booking.customerName], ['Số điện thoại', detail.booking.customerPhone],
            ['Email', detail.booking.customerEmail || 'Chưa cung cấp'], ['Ngày lập', date(detail.createdDate)]
        ], 'fa-regular fa-address-card'));
        const contractText = createNode('div', 'cp-detail-text-grid');
        contractText.append(textCard('Nội dung hợp đồng', detail.contractContent, 'fa-regular fa-file-lines'), textCard('Điều khoản thanh toán', detail.paymentTerms, 'fa-solid fa-scale-balanced'));
        general.append(contractText);

        const booking = byId('drawerBooking');
        booking.append(detailCard('Booking liên quan', [
            ['Mã booking', detail.booking.bookingCode], ['Ngày / ca', `${date(detail.booking.eventDate)} · ${detail.booking.shift}`],
            ['Sảnh', detail.booking.hallName], ['Số khách', String(detail.booking.guestCount)], ['Thực đơn', detail.booking.menuName || 'Chưa chọn'],
            ['Trang trí', detail.booking.decorationPackageName || 'Chưa chọn'], ['Dịch vụ', detail.booking.services?.length ? detail.booking.services.join(', ') : 'Không có'],
            ['Dự toán', money(detail.booking.estimatedTotal)], ['Trạng thái booking', detail.booking.statusName]
        ], 'fa-regular fa-calendar-check'));
        const bookingLink = createNode('a', 'cp-secondary cp-detail-link', 'Xem chi tiết Booking'); bookingLink.href = '/AdminBooking';
        bookingLink.prepend(createNode('i', 'fa-solid fa-arrow-up-right-from-square')); booking.append(bookingLink);

        renderDrawerPayments(detail);
        renderHistory(detail.history || []);
        renderDrawerActions(detail);
    }

    function contractOverview(detail) {
        const overview = createNode('section', 'cp-contract-overview');
        const heading = createNode('div', 'cp-contract-overview-head');
        const title = createNode('div'); title.append(createNode('span', '', 'TỔNG QUAN TÀI CHÍNH'), createNode('strong', '', `${percentage(detail.paymentPercentage)}% đã thanh toán`));
        const progress = createNode('div', 'cp-contract-overview-progress'); const bar = createNode('span');
        bar.style.width = `${Math.max(0, Math.min(100, Number(detail.paymentPercentage || 0)))}%`; progress.append(bar); heading.append(title, progress);
        const metrics = createNode('div', 'cp-contract-metrics');
        [
            ['Tổng giá trị', money(detail.totalValue), 'fa-solid fa-file-invoice-dollar', 'navy'],
            ['Đã thanh toán', money(detail.paidAmount), 'fa-solid fa-circle-check', 'green'],
            ['Còn phải thu', money(detail.remainingBalance), 'fa-regular fa-clock', detail.remainingBalance > 0 ? 'amber' : 'green']
        ].forEach(([label, value, icon, tone]) => {
            const item = createNode('div', `cp-contract-metric ${tone}`); const iconBox = createNode('span', 'cp-contract-metric-icon');
            iconBox.append(iconNode(icon)); const copy = createNode('span', 'cp-contract-metric-copy'); copy.append(createNode('small', '', label), createNode('strong', '', value)); item.append(iconBox, copy); metrics.append(item);
        });
        overview.append(heading, metrics); return overview;
    }

    function detailCard(title, pairs, iconClass = 'fa-regular fa-rectangle-list') {
        const card = createNode('article', 'cp-detail-card');
        const heading = createNode('header', 'cp-detail-card-head'); const icon = createNode('span'); icon.append(iconNode(iconClass)); heading.append(icon, createNode('h3', '', title)); card.append(heading);
        const list = createNode('dl', 'cp-detail-grid');
        pairs.forEach(([label, value]) => { list.append(createNode('dt', '', label), createNode('dd', '', value ?? '—')); });
        card.append(list); return card;
    }

    function textCard(title, text, iconClass = 'fa-regular fa-file-lines') {
        const card = createNode('article', 'cp-detail-card cp-detail-text-card');
        const heading = createNode('header', 'cp-detail-card-head'); const icon = createNode('span'); icon.append(iconNode(iconClass)); heading.append(icon, createNode('h3', '', title));
        const content = createNode('p', `cp-pre${text ? '' : ' cp-detail-empty-copy'}`, text || 'Chưa có nội dung.'); card.append(heading, content); return card;
    }

    function renderDrawerPayments(detail) {
        const section = byId('drawerPayments');
        const summary = createNode('article', 'cp-detail-card cp-payment-detail-card');
        const summaryHead = createNode('header', 'cp-detail-card-head'); const summaryIcon = createNode('span'); summaryIcon.append(iconNode('fa-solid fa-chart-line'));
        summaryHead.append(summaryIcon, createNode('h3', '', 'Tiến độ thanh toán'), createNode('b', 'cp-payment-percentage', `${percentage(detail.paymentPercentage)}%`)); summary.append(summaryHead);
        const track = createNode('div', 'cp-progress-track'); const bar = createNode('div', 'cp-progress-bar');
        bar.style.width = `${Math.max(0, Math.min(100, Number(detail.paymentPercentage || 0)))}%`; track.append(bar); summary.append(track);
        const values = createNode('div', 'cp-payment-summary');
        [['Đã thanh toán', money(detail.paidAmount)], ['Còn lại', money(detail.remainingBalance)], ['Tổng giá trị', money(detail.totalValue)]].forEach(([label, value]) => {
            const item = createNode('div'); item.append(createNode('span', '', label), createNode('strong', '', value)); values.append(item);
        }); summary.append(values);
        if (detail.hasPaymentInconsistency) summary.append(createNode('p', 'cp-warning', 'Tổng thanh toán vượt giá trị hợp đồng. Vui lòng kiểm tra dữ liệu.'));
        else if (detail.statusCode === 'ACTIVE' && detail.remainingBalance > 0) {
            summary.append(createNode('p', 'cp-warning', `Còn ${money(detail.remainingBalance)} chưa thanh toán.`));
            const payBtn = actionButton('Ghi nhận thanh toán', 'cp-primary', () => openRecordPaymentModal(detail));
            payBtn.style.marginTop = '10px';
            summary.append(payBtn);
        }
        section.append(summary);
        const history = createNode('article', 'cp-detail-card');
        const historyHead = createNode('header', 'cp-detail-card-head'); const historyIcon = createNode('span'); historyIcon.append(iconNode('fa-solid fa-receipt'));
        historyHead.append(historyIcon, createNode('h3', '', 'Lịch sử giao dịch'), createNode('small', 'cp-readonly-badge', 'Chỉ đọc')); history.append(historyHead);
        if (!detail.payments?.length) history.append(createNode('p', 'cp-pre', 'Chưa phát sinh giao dịch thanh toán.'));
        else detail.payments.forEach(payment => history.append(detailCard(`${payment.paymentType} · ${money(payment.amount)}`, [
            ['Mã giao dịch', payment.transactionCode || `#${payment.paymentId}`], ['Ngày', date(payment.paymentDate)],
            ['Phương thức', payment.paymentMethod || '—'], ['Trạng thái', payment.statusName]
        ])));
        section.append(history);
    }

    function renderHistory(items) {
        const section = byId('drawerHistory'); const card = createNode('article', 'cp-detail-card');
        const cardHead = createNode('header', 'cp-detail-card-head'); const cardIcon = createNode('span'); cardIcon.append(iconNode('fa-solid fa-clock-rotate-left'));
        cardHead.append(cardIcon, createNode('h3', '', 'Lịch sử nghiệp vụ')); card.append(cardHead);
        const timeline = createNode('div', 'cp-timeline');
        if (!items.length) timeline.append(createNode('p', 'cp-pre', 'Chưa có lịch sử.'));
        const names = { CONTRACT_CREATED: 'Tạo hợp đồng', CONTRACT_UPDATED: 'Cập nhật bản nháp', CONTRACT_ACTIVATED: 'Kích hoạt hợp đồng', CONTRACT_COMPLETED: 'Hoàn tất hợp đồng', CONTRACT_CANCELLED: 'Hủy hợp đồng' };
        items.forEach(item => { const event = createNode('div', 'cp-timeline-item'); event.append(createNode('strong', '', names[item.action] || item.action), createNode('span', '', `${new Date(item.timestamp).toLocaleString('vi-VN')} · ${item.notes || ''}`)); timeline.append(event); });
        card.append(timeline); section.append(card);
    }

    function renderDrawerActions(detail) {
        const actions = byId('drawerActions'); actions.replaceChildren();
        const pdf = createNode('a', 'cp-secondary', 'In / Tải PDF'); pdf.href = `${base}/contracts/${detail.contractId}/pdf`; pdf.target = '_blank';
        const pdfIcon = iconNode('fa-solid fa-print'); pdf.prepend(pdfIcon); actions.append(pdf);
        if (detail.statusCode === 'DRAFT') {
            actions.append(actionButton('Sửa hợp đồng', 'cp-secondary', () => openEdit(detail), 'fa-regular fa-pen-to-square'), actionButton('Kích hoạt', 'cp-primary', () => confirmActivate(detail), 'fa-solid fa-bolt'), actionButton('Hủy hợp đồng', 'cp-danger', () => confirmCancel(detail), 'fa-regular fa-trash-can'));
        } else if (detail.statusCode === 'ACTIVE') {
            if (detail.remainingBalance > 0) {
                actions.append(actionButton('Thu tiền / PayOS', 'cp-primary', () => openRecordPaymentModal(detail), 'fa-solid fa-qrcode'));
            }
            if (!detail.hasPaymentInconsistency && Number(detail.paidAmount) === Number(detail.totalValue)) actions.append(actionButton('Hoàn tất hợp đồng', 'cp-primary', () => confirmComplete(detail), 'fa-solid fa-circle-check'));
            actions.append(actionButton('Hủy hợp đồng', 'cp-danger', () => confirmCancel(detail), 'fa-regular fa-trash-can'));
        }
    }

    function actionButton(label, className, handler, iconClass = '') { const button = createNode('button', className, label); button.type = 'button'; if (iconClass) button.prepend(iconNode(iconClass)); button.addEventListener('click', handler); return button; }
    function closeDrawer() { byId('drawerOverlay').classList.remove('open'); byId('contractDrawer').classList.remove('open'); state.currentContract = null; }
    function switchDrawerTab(tab) {
        document.querySelectorAll('[data-drawer-tab]').forEach(button => button.classList.toggle('active', button.dataset.drawerTab === tab));
        const map = { general: 'drawerGeneral', booking: 'drawerBooking', payments: 'drawerPayments', history: 'drawerHistory' };
        Object.entries(map).forEach(([key, id]) => byId(id).classList.toggle('cp-hidden', key !== tab));
    }

    async function openCreateModal() {
        byId('createFormError').classList.add('cp-hidden');
        byId('createFormError').textContent = '';
        openModal('createModal');
        await loadEligibleBookings();
    }
    async function loadEligibleBookings(keyword = '') {
        const container = byId('eligibleBookings'); container.replaceChildren(createNode('div', 'cp-empty', 'Đang tải booking đủ điều kiện...'));
        try {
            const bookings = await request(`${base}/eligible-bookings?${queryString({ keyword })}`);
            container.replaceChildren();
            if (!bookings.length) { container.append(createNode('div', 'cp-empty', 'Không có booking đã xác nhận chưa có hợp đồng.')); return; }
            bookings.forEach(booking => {
                const button = createNode('button', 'cp-booking-option'); button.type = 'button'; button.dataset.bookingId = booking.bookingId;
                const icon = createNode('span', 'cp-booking-option-icon'); icon.setAttribute('aria-hidden', 'true'); icon.innerHTML = '<i class="fa-regular fa-calendar-check"></i>';
                const copy = createNode('span', 'cp-booking-option-copy');
                const heading = createNode('span', 'cp-booking-option-heading');
                heading.append(createNode('strong', '', booking.bookingCode), createNode('b', '', booking.customerName));
                const meta = createNode('span', 'cp-booking-option-meta');
                meta.append(createNode('span', '', booking.customerPhone), createNode('span', '', date(booking.eventDate)), createNode('span', '', booking.hallName));
                copy.append(heading, meta);
                const check = createNode('span', 'cp-booking-option-check'); check.setAttribute('aria-hidden', 'true'); check.innerHTML = '<i class="fa-solid fa-check"></i>';
                button.append(icon, copy, check);
                button.addEventListener('click', () => selectBooking(booking, button)); container.append(button);
            });
            const initialId = Number(root.dataset.initialBookingId || 0);
            if (initialId) { const found = bookings.find(item => item.bookingId === initialId); const button = container.querySelector(`[data-booking-id="${initialId}"]`); if (found && button) selectBooking(found, button); }
        } catch (error) { container.replaceChildren(createNode('div', 'cp-empty', error.message)); }
    }

    function selectBooking(booking, button) {
        state.selectedBooking = booking;
        document.querySelectorAll('.cp-booking-option').forEach(item => item.classList.toggle('selected', item === button));
        const summary = byId('selectedBookingSummary'); summary.classList.remove('cp-hidden'); summary.replaceChildren();
        const summaryHead = createNode('div', 'cp-booking-summary-head');
        const summaryTitle = createNode('div', ''); summaryTitle.append(createNode('span', '', 'BOOKING ĐÃ CHỌN'), createNode('strong', '', booking.bookingCode));
        const summaryBadge = createNode('span', 'cp-booking-summary-badge', 'Đã xác nhận');
        summaryHead.append(summaryTitle, summaryBadge);
        const summaryGrid = createNode('dl', 'cp-booking-summary-grid');
        [
            ['Khách hàng', booking.customerName],
            ['Liên hệ', `${booking.customerPhone} · ${booking.customerEmail || 'Chưa có email'}`],
            ['Thời gian', `${date(booking.eventDate)} · ${booking.shift}`],
            ['Sảnh tiệc', `${booking.hallName} · ${booking.guestCount} khách`],
            ['Thực đơn', booking.menuName || 'Chưa chọn'],
            ['Trang trí', booking.decorationPackageName || 'Chưa chọn']
        ].forEach(([label, value]) => summaryGrid.append(createNode('dt', '', label), createNode('dd', '', value)));
        summary.append(summaryHead, summaryGrid);
        byId('createFormError').classList.add('cp-hidden');
        byId('createTotalValue').value = booking.estimatedTotal ?? 0;
        byId('createContent').value = booking.defaultContractContent || '';
        byId('createTerms').value = booking.defaultPaymentTerms || '';
    }

    async function saveDraft() {
        const error = byId('createFormError');
        const saveButton = byId('saveDraft');
        const totalValue = Number(byId('createTotalValue').value || 0);
        const showCreateError = (message, target) => {
            const icon = createNode('i', 'fa-solid fa-circle-exclamation'); icon.setAttribute('aria-hidden', 'true');
            error.replaceChildren(icon, createNode('span', '', message));
            error.classList.remove('cp-hidden');
            target?.focus();
        };
        if (!state.selectedBooking) return showCreateError('Vui lòng chọn một booking đã xác nhận trước khi tạo hợp đồng.', byId('eligibleKeyword'));
        if (totalValue <= 0) return showCreateError('Tổng giá trị hợp đồng phải lớn hơn 0 đồng.', byId('createTotalValue'));
        error.classList.add('cp-hidden');
        saveButton.disabled = true;
        saveButton.innerHTML = '<i class="fa-solid fa-spinner fa-spin" aria-hidden="true"></i><span>Đang lưu...</span>';
        try {
            const result = await request(`${base}/contracts`, { method: 'POST', body: JSON.stringify({ bookingId: state.selectedBooking.bookingId, totalValue, contractContent: byId('createContent').value, paymentTerms: byId('createTerms').value }) });
            closeModal('createModal'); showToast(result.message || 'Đã lưu hợp đồng bản nháp.'); state.selectedBooking = null; root.dataset.initialBookingId = ''; await loadContracts(1); await openDrawer(result.contractId);
        } catch (requestError) {
            showCreateError(requestError.message || 'Không thể lưu hợp đồng. Vui lòng thử lại.');
        } finally {
            saveButton.disabled = false;
            saveButton.innerHTML = '<i class="fa-regular fa-floppy-disk" aria-hidden="true"></i><span>Lưu bản nháp</span>';
        }
    }

    function openEdit(detail) { byId('editTotalValue').value = detail.totalValue; byId('editContent').value = detail.contractContent; byId('editTerms').value = detail.paymentTerms; openModal('editModal'); }
    async function saveEdit() {
        if (!state.currentContract) return;
        try {
            const result = await request(`${base}/contracts/${state.currentContract.contractId}`, { method: 'PUT', body: JSON.stringify({ totalValue: Number(byId('editTotalValue').value || 0), contractContent: byId('editContent').value, paymentTerms: byId('editTerms').value }) });
            closeModal('editModal'); showToast(result.message); await loadContracts(state.contractPage); await openDrawer(state.currentContract.contractId);
        } catch (error) { showToast(error.message, true); }
    }

    function showConfirm(title, message, callback, danger = false) { byId('confirmTitle').textContent = title; byId('confirmMessage').textContent = message; state.confirmCallback = callback; byId('confirmAction').className = danger ? 'cp-danger' : 'cp-primary'; openModal('confirmModal'); }
    function confirmActivate(detail) { showConfirm('Kích hoạt hợp đồng', 'Khi kích hoạt, hợp đồng sẽ không thể chỉnh sửa nội dung hoặc tổng giá trị. Bạn có chắc chắn muốn tiếp tục?', () => runCommand(detail.contractId, 'activate')); }
    function confirmComplete(detail) { showConfirm('Hoàn tất hợp đồng', `Khách hàng đã thanh toán đủ ${money(detail.totalValue)}. Xác nhận hoàn tất hợp đồng?`, () => runCommand(detail.contractId, 'complete')); }
    function confirmCancel(detail) { const warning = detail.paidAmount > 0 ? ' Hợp đồng đã phát sinh thanh toán thành công. Việc hủy không xóa hoặc hoàn tiền các giao dịch đã ghi nhận.' : ''; showConfirm('Hủy hợp đồng', `Bạn có chắc chắn muốn hủy ${detail.contractCode}? Booking không bị hủy.${warning}`, () => runCommand(detail.contractId, 'cancel'), true); }
    async function runCommand(id, command) { closeModal('confirmModal'); try { const result = await request(`${base}/contracts/${id}/${command}`, { method: 'POST' }); showToast(result.message); await loadContracts(state.contractPage); await openDrawer(id); } catch (error) { showToast(error.message, true); } }


    function openRecordPaymentModal(detail) {
        state.currentContract = detail;
        byId('recordPaymentType').value = 'Thanh toán đợt';
        byId('recordPaymentMethod').value = 'CASH';
        const defaultAmt = Math.max(0, Math.min(detail.remainingBalance, Math.round(detail.totalValue * 0.5)));
        byId('recordPaymentAmount').value = defaultAmt || detail.remainingBalance;
        byId('recordPaymentNote').value = '';
        updatePaymentHelperText();
        openModal('recordPaymentModal');
    }

    function updatePaymentHelperText() {
        const detail = state.currentContract;
        if (!detail) return;
        const type = byId('recordPaymentType').value;
        const method = byId('recordPaymentMethod').value;
        const helper = byId('paymentHelperMsg');
        if (method === 'CASH') {
            helper.textContent = `Thu tiền mặt tại quầy cho ${type}. Giao dịch sẽ được ghi nhận ngay và cập nhật vào tiến độ hợp đồng.`;
        } else {
            helper.textContent = `Hệ thống sẽ tạo mã QR VietQR PayOS cho ${type}. Quản lý cho khách quét mã hoặc sao chép liên kết thanh toán.`;
        }
    }

    async function submitPayment() {
        const detail = state.currentContract;
        if (!detail) return;
        const paymentType = byId('recordPaymentType').value;
        const paymentMethod = byId('recordPaymentMethod').value;
        const amount = Number(byId('recordPaymentAmount').value || 0);
        const note = byId('recordPaymentNote').value?.trim();

        if (amount <= 0) return showToast('Vui lòng nhập số tiền hợp lệ.', true);
        if (amount > detail.remainingBalance) {
            return showToast(`Số tiền thanh toán (${money(amount)}) không được vượt quá số dư còn lại (${money(detail.remainingBalance)}).`, true);
        }

        const submitBtn = byId('btnSubmitPayment');
        submitBtn.disabled = true;

        try {
            if (paymentMethod === 'CASH') {
                const res = await request(`${base}/payments/record-cash`, {
                    method: 'POST',
                    body: JSON.stringify({
                        contractId: detail.contractId,
                        amount: amount,
                        paymentType: paymentType,
                        notes: note
                    })
                });
                closeModal('recordPaymentModal');
                showToast(res.message || 'Ghi nhận thanh toán tiền mặt thành công!');
                await loadContracts(state.contractPage);
                await openDrawer(detail.contractId, 'payments');
                if (state.paymentsLoaded) await loadPayments(state.paymentPage);
            } else if (paymentMethod === 'VIETQR_PAYOS') {
                const res = await request(`${base}/payments/create-payos-link`, {
                    method: 'POST',
                    body: JSON.stringify({
                        contractId: detail.contractId,
                        amount: amount,
                        paymentType: paymentType,
                        description: note
                    })
                });
                closeModal('recordPaymentModal');
                openPayosModal(res, detail.contractId);
            }
        } catch (err) {
            showToast(err.message, true);
        } finally {
            submitBtn.disabled = false;
        }
    }

    function copyToClipboard(text, btnElement) {
        if (!text) return;
        navigator.clipboard.writeText(String(text)).then(() => {
            if (btnElement) {
                const originalHtml = btnElement.innerHTML;
                btnElement.innerHTML = '<i class="fa-solid fa-check"></i> Đã chép';
                btnElement.style.background = '#10B981';
                btnElement.style.color = '#FFFFFF';
                setTimeout(() => {
                    btnElement.innerHTML = originalHtml;
                    btnElement.style.background = '';
                    btnElement.style.color = '';
                }, 1800);
            }
            showToast('Đã sao chép vào bộ nhớ tạm.');
        }).catch(err => {
            console.error('Không thể sao chép:', err);
        });
    }

    function resolveQrImageUrl(qrRaw) {
        if (!qrRaw) return '';
        const trimmed = String(qrRaw).trim();
        if (trimmed.startsWith('http://') || trimmed.startsWith('https://') || trimmed.startsWith('data:image/')) {
            return trimmed;
        }
        return `https://api.qrserver.com/v1/create-qr-code/?size=300x300&data=${encodeURIComponent(trimmed)}`;
    }

    function getQrFallbackUrl(qrRaw) {
        if (!qrRaw) return '';
        const trimmed = String(qrRaw).trim();
        return `https://quickchart.io/qr?size=300&text=${encodeURIComponent(trimmed)}`;
    }

    function resolveBankName(bankBin, rawBankName) {
        if (rawBankName && isNaN(rawBankName)) return rawBankName;
        const binMap = {
            '970422': 'MB Bank (Quân Đội)',
            '970415': 'VietinBank (Công Thương)',
            '970436': 'Vietcombank (Ngoại Thương)',
            '970407': 'Techcombank (Kỹ Thương)',
            '970418': 'BIDV (Đầu Tư & Phát Triển)',
            '970405': 'Agribank (Nông Nghiệp)'
        };
        return binMap[String(bankBin || rawBankName).trim()] || (rawBankName || 'Ngân hàng Quân Đội (MB Bank)');
    }

    function startPayosCountdown(seconds = 600) {
        stopPayosCountdown();
        let remain = seconds;
        const el = byId('payosCountdownVal');
        function tick() {
            if (remain <= 0) {
                stopPayosCountdown();
                if (el) el.textContent = '00:00 (Hết hạn)';
                return;
            }
            const m = Math.floor(remain / 60);
            const s = remain % 60;
            if (el) el.textContent = `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
            remain--;
        }
        tick();
        state.payosCountdownTimer = setInterval(tick, 1000);
    }

    function stopPayosCountdown() {
        if (state.payosCountdownTimer) {
            clearInterval(state.payosCountdownTimer);
            state.payosCountdownTimer = null;
        }
    }

    function openPayosModal(res, contractId) {
        const orderCode = res.orderCode || res.orderId || '';
        const amt = res.businessAmount || res.amount || res.actualAmount || 0;
        state.currentPayosOrder = orderCode;
        state.currentPayosContractId = contractId;
        state.payosCheckoutUrl = res.checkoutUrl;

        // Reset views
        const initView = byId('payosInitialContent');
        const successView = byId('payosSuccessContent');
        if (initView) initView.style.display = 'block';
        if (successView) successView.style.display = 'none';

        const loadingEl = byId('payosQrLoading');
        const errorEl = byId('payosQrError');
        const qrImg = byId('payosQrImg');
        if (loadingEl) loadingEl.style.display = 'flex';
        if (errorEl) errorEl.style.display = 'none';
        if (qrImg) qrImg.style.display = 'none';

        // Bind data
        const cCode = (state.currentContract && state.currentContract.contractCode) || ('HD-' + contractId);
        if (byId('payosContractCodeDisplay')) byId('payosContractCodeDisplay').textContent = cCode;
        if (byId('payosAmountDisplay')) byId('payosAmountDisplay').textContent = money(amt);

        // Demo banner
        const demoBanner = byId('payosDemoBanner');
        if (demoBanner) {
            if (res.isDemoMode) {
                demoBanner.style.display = 'flex';
                const actualEl = byId('payosActualAmountDisplay');
                if (actualEl) actualEl.textContent = money(res.actualAmount || 2000);
            } else {
                demoBanner.style.display = 'none';
            }
        }

        // Details
        if (byId('payosBankName')) byId('payosBankName').textContent = resolveBankName(res.bankBin, res.bankName);
        if (byId('payosAccountName')) byId('payosAccountName').textContent = res.accountName || 'CONG TY CP RIVERSIDE PALACE';
        if (byId('payosAccountNo')) byId('payosAccountNo').textContent = res.accountNumber || '--';
        if (byId('payosTransferDesc')) byId('payosTransferDesc').textContent = res.description || ('HD' + contractId);
        if (byId('payosOrderCode')) byId('payosOrderCode').textContent = String(orderCode);

        // Status alert
        const alertEl = byId('payosStatusAlert');
        if (alertEl) {
            alertEl.innerHTML = '<div class="rp-pay-spinner"></div><span id="payosStatusText">Đang chờ khách hàng quét mã và hệ thống ngân hàng xác nhận...</span>';
            alertEl.style.background = '#F8FAFC';
            alertEl.style.color = '#1E40AF';
        }

        // Copy buttons
        const copyAccBtn = byId('payosCopyAccBtn');
        if (copyAccBtn) {
            copyAccBtn.onclick = () => copyToClipboard(res.accountNumber, copyAccBtn);
        }
        const copyDescBtn = byId('payosCopyDescBtn');
        if (copyDescBtn) {
            copyDescBtn.onclick = () => copyToClipboard(res.description, copyDescBtn);
        }

        // Real VietQR image resolver
        const rawQr = res.qrCode || '';
        let qrFallbackAttempted = false;
        if (qrImg) {
            qrImg.onload = () => {
                if (loadingEl) loadingEl.style.display = 'none';
                if (errorEl) errorEl.style.display = 'none';
                qrImg.style.display = 'block';
            };
            qrImg.onerror = () => {
                if (!qrFallbackAttempted && rawQr && !rawQr.startsWith('http://') && !rawQr.startsWith('https://')) {
                    qrFallbackAttempted = true;
                    qrImg.src = getQrFallbackUrl(rawQr);
                    return;
                }
                if (loadingEl) loadingEl.style.display = 'none';
                if (errorEl) errorEl.style.display = 'flex';
            };
            qrImg.src = resolveQrImageUrl(rawQr);
        }

        // Timer & Polling
        startPayosCountdown(600);
        openModal('payosModal');
        startPayosPolling(res.orderCode, contractId);
    }

    function onPayosSuccess(contractId, amt) {
        stopPayosPolling();
        stopPayosCountdown();

        const initView = byId('payosInitialContent');
        const successView = byId('payosSuccessContent');
        if (initView) initView.style.display = 'none';
        if (successView) {
            successView.style.display = 'block';
            const successAmt = byId('payosSuccessAmount');
            if (successAmt) successAmt.textContent = money(amt);
        }

        showToast('Khách hàng đã thanh toán PayOS thành công!');

        // Refresh background state
        loadContracts(state.contractPage);
        openDrawer(contractId, 'payments');
        if (state.paymentsLoaded) loadPayments(state.paymentPage);
    }

    function startPayosPolling(orderCode, contractId) {
        stopPayosPolling();
        state.payosPollTimer = setInterval(async () => {
            try {
                const checkRes = await request(`${base}/payments/check-payos/${orderCode}`);
                if (checkRes.isPaid || checkRes.status === 'PAID') {
                    onPayosSuccess(contractId, checkRes.businessDeposit || checkRes.actualAmount || 0);
                }
            } catch (e) {
                // Ignore background polling errors
            }
        }, 3000);
    }

    function stopPayosPolling() {
        if (state.payosPollTimer) {
            clearInterval(state.payosPollTimer);
            state.payosPollTimer = null;
        }
    }

    async function checkPayosManual() {
        if (!state.currentPayosOrder) return;
        try {
            const checkRes = await request(`${base}/payments/check-payos/${state.currentPayosOrder}`);
            if (checkRes.isPaid || checkRes.status === 'PAID') {
                onPayosSuccess(state.currentPayosContractId, checkRes.businessDeposit || checkRes.actualAmount || 0);
            } else {
                showToast(`Trạng thái giao dịch: ${checkRes.status || 'PENDING'}`);
            }
        } catch (e) {
            showToast(e.message, true);
        }
    }

    function openModal(id) { byId(id).classList.add('open'); }
    function closeModal(id) {
        byId(id).classList.remove('open');
        if (id === 'confirmModal') state.confirmCallback = null;
        if (id === 'payosModal') {
            stopPayosPolling();
            stopPayosCountdown();
        }
    }
    function debounce(handler, delay = 350) { let timer; return (...args) => { clearTimeout(timer); timer = setTimeout(() => handler(...args), delay); }; }

    document.querySelectorAll('[data-main-tab]').forEach(button => button.addEventListener('click', () => {
        document.querySelectorAll('[data-main-tab]').forEach(item => item.classList.toggle('active', item === button));
        const payments = button.dataset.mainTab === 'payments'; byId('contractTab').classList.toggle('cp-hidden', payments); byId('paymentTab').classList.toggle('cp-hidden', !payments); if (payments && !state.paymentsLoaded) loadPayments(1);
    }));
    document.querySelectorAll('[data-drawer-tab]').forEach(button => button.addEventListener('click', () => switchDrawerTab(button.dataset.drawerTab)));
    document.querySelectorAll('[data-close-modal]').forEach(button => button.addEventListener('click', () => closeModal(button.dataset.closeModal)));
    [byId('contractStatus'), byId('contractFromDate'), byId('contractToDate')].forEach(element => element.addEventListener('change', () => loadContracts(1)));
    byId('contractKeyword').addEventListener('input', debounce(() => loadContracts(1)));
    [byId('paymentType'), byId('paymentStatus'), byId('paymentFromDate'), byId('paymentToDate')].forEach(element => element.addEventListener('change', () => loadPayments(1)));
    byId('paymentKeyword').addEventListener('input', debounce(() => loadPayments(1)));
    byId('eligibleKeyword').addEventListener('input', debounce(event => loadEligibleBookings(event.target.value)));
    byId('openCreateContract').addEventListener('click', openCreateModal); byId('saveDraft').addEventListener('click', saveDraft); byId('saveEdit').addEventListener('click', saveEdit);
    byId('recordPaymentType')?.addEventListener('change', updatePaymentHelperText);
    byId('recordPaymentMethod')?.addEventListener('change', updatePaymentHelperText);
    byId('btnFill50')?.addEventListener('click', () => { if (state.currentContract) byId('recordPaymentAmount').value = Math.max(0, Math.min(state.currentContract.remainingBalance, Math.round(state.currentContract.totalValue * 0.5))); });
    byId('btnFillRemaining')?.addEventListener('click', () => { if (state.currentContract) byId('recordPaymentAmount').value = state.currentContract.remainingBalance; });
    byId('btnSubmitPayment')?.addEventListener('click', submitPayment);
    byId('btnCheckPayosNow')?.addEventListener('click', checkPayosManual);
    byId('btnCopyPayosLink')?.addEventListener('click', () => { if (state.payosCheckoutUrl) { navigator.clipboard.writeText(state.payosCheckoutUrl); showToast('Đã chép link thanh toán!'); } });
    byId('closeDrawer').addEventListener('click', closeDrawer); byId('drawerOverlay').addEventListener('click', closeDrawer);
    byId('confirmAction').addEventListener('click', () => { const callback = state.confirmCallback; if (callback) callback(); });
    document.addEventListener('keydown', event => { if (event.key === 'Escape') { closeDrawer(); document.querySelectorAll('.cp-modal.open').forEach(modal => closeModal(modal.id)); } });

    loadContracts(1);
    if (root.dataset.openCreate === 'true' || root.dataset.initialBookingId) openCreateModal();
    else if (Number(root.dataset.initialContractId || 0)) openDrawer(Number(root.dataset.initialContractId));
})();
