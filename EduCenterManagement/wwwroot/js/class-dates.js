document.addEventListener("DOMContentLoaded", () => {
    document.querySelectorAll("form[data-class-dates]").forEach(form => {
        const start = form.querySelector('[name="StartDate"]');
        const end = form.querySelector('[name="EndDate"]');
        const minStart = form.dataset.minStart || start.min;

        function showError(input, message) {
            input.setCustomValidity(message);
            input.setAttribute("aria-invalid", String(Boolean(message)));
            const error = form.querySelector(`[data-valmsg-for="${input.name}"]`);
            if (error) error.textContent = message;
        }

        function validateDates() {
            let minEnd = minStart;
            if (start.value) {
                // Work in UTC to add a calendar day without timezone/DST shifts.
                const next = new Date(`${start.value}T00:00:00Z`);
                next.setUTCDate(next.getUTCDate() + 1);
                if (!Number.isNaN(next.getTime())) minEnd = next.toISOString().slice(0, 10);
            }
            end.min = minEnd;
            start.min = minStart;
            const startError = start.value && start.value < minStart
                ? "Ngày bắt đầu phải sau hôm nay (giờ Việt Nam)." : "";
            const endError = start.value && end.value && end.value <= start.value
                ? "Ngày kết thúc phải sau ngày bắt đầu." : "";
            showError(start, startError);
            showError(end, endError);
            return !startError && !endError;
        }

        [start, end].forEach(input => input.addEventListener("input", validateDates));
        [start, end].forEach(input => input.addEventListener("change", validateDates));
        form.addEventListener("submit", event => {
            if (!validateDates()) {
                event.preventDefault();
                form.reportValidity();
            }
        }, true);
        form.addEventListener("reset", () => setTimeout(validateDates, 0));
        validateDates();
    });
});
