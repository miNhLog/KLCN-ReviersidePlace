(() => {
    const labels = ['Chưa đánh giá', 'Rất chưa hài lòng', 'Chưa hài lòng', 'Bình thường', 'Hài lòng', 'Rất hài lòng'];
    document.querySelectorAll('[data-rating-group]').forEach(group => {
        const inputs = [...group.querySelectorAll('.review-star-input')];
        const stars = [...group.querySelectorAll('.review-star')];
        const output = group.querySelector('.review-rating-label');
        const selected = () => Number(inputs.find(input => input.checked)?.value || 0);
        const paint = (value, preview = false) => {
            stars.forEach((star, index) => {
                star.classList.toggle('is-selected', !preview && index < value);
                star.classList.toggle('is-preview', preview && index < value);
            });
            if (output) output.textContent = value ? `${value}/5 · ${labels[value]}` : (group.classList.contains('review-stars-large') ? 'Chọn số sao' : labels[0]);
        };
        paint(selected());
        inputs.forEach(input => input.addEventListener('change', () => paint(selected())));
        stars.forEach((star, index) => {
            star.addEventListener('pointerenter', () => paint(index + 1, true));
            star.addEventListener('pointerleave', () => paint(selected()));
        });
        group.addEventListener('pointerleave', () => paint(selected()));
    });
    const comment = document.getElementById('Comment');
    const counter = document.getElementById('commentCounter');
    const updateCounter = () => { if (counter) counter.textContent = `${comment?.value.length || 0}/1000`; };
    comment?.addEventListener('input', updateCounter);
    updateCounter();
    document.getElementById('weddingReviewForm')?.addEventListener('submit', event => {
        if (!event.currentTarget.checkValidity()) return;
        const button = event.currentTarget.querySelector('.review-submit');
        if (!button) return;
        button.disabled = true;
        button.querySelector('strong').textContent = 'Đang gửi đánh giá...';
        button.querySelector('small').textContent = 'Vui lòng chờ trong giây lát';
    });
})();
