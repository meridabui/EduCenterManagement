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

function initClassDates() {
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
}

function initClassIndex() {
    const page = document.querySelector('.class-page');
    if (!page) return;
    const filterSubject = document.getElementById('filterSubjectId');
    filterSubject.addEventListener('change', function () {
        document.getElementById('filterTeacherId').value = '';
        this.form.requestSubmit();
    });

    const createForm = document.getElementById("createClassForm");
    const scheduleContainer = document.getElementById("createScheduleContainer");
    const scheduleTemplate = document.getElementById("createScheduleTemplate");
    const addScheduleButton = document.getElementById("addCreateScheduleBtn");
    const scheduleError = document.getElementById("createScheduleError");
    const createSubmit = document.getElementById("createClassSubmit");
    const subjectSelect = document.getElementById("SubjectId");
    const teacherSelect = document.getElementById("TeacherId");
    const startDate = document.getElementById("StartDate");
    const endDate = document.getElementById("EndDate");
    const teachersUrl = page.dataset.teachersUrl;
    const conflictUrl = page.dataset.conflictUrl;
    let conflictRequestId = 0;

    function reindexSchedules() {
        scheduleContainer.querySelectorAll(".schedule-row").forEach((row, index) => {
            row.querySelector(".day-select").name = `Schedules[${index}].DayOfWeek`;
            row.querySelector(".slot-select").name = `Schedules[${index}].TimeSlotId`;
        });
    }

    function validateUniqueDays() {
        const rows = [...scheduleContainer.querySelectorAll(".schedule-row")];
        const days = rows.map(row => row.querySelector(".day-select").value);
        return new Set(days).size === days.length;
    }

    addScheduleButton.addEventListener("click", () => {
        scheduleContainer.appendChild(scheduleTemplate.content.cloneNode(true));
        reindexSchedules();
        scheduleError.style.display = "none";
        checkTeacherConflicts();
    });

    scheduleContainer.addEventListener("click", event => {
        const button = event.target.closest(".remove-schedule");
        if (!button) return;
        if (scheduleContainer.querySelectorAll(".schedule-row").length <= 1) {
            showScheduleError("Lớp phải có ít nhất một lịch học.");
            return;
        }
        button.closest(".schedule-row").remove();
        reindexSchedules();
        checkTeacherConflicts();
    });

    function showScheduleError(message) {
        scheduleError.textContent = message;
        scheduleError.style.display = "block";
    }

    async function loadTeachers(preserveSelection = false) {
        const subjectId = subjectSelect.value;
        const oldValue = preserveSelection ? teacherSelect.value : "";
        teacherSelect.innerHTML = '<option value="">-- Chưa phân công giáo viên --</option>';
        if (!subjectId) return;
        teacherSelect.disabled = true;
        try {
            const response = await fetch(`${teachersUrl}?subjectId=${encodeURIComponent(subjectId)}`);
            if (!response.ok) throw new Error("Không tải được danh sách giáo viên.");
            const data = await response.json();
            data.forEach(teacher => {
                const option = document.createElement("option");
                option.value = teacher.id;
                option.textContent = teacher.fullName;
                if (String(teacher.id) === oldValue) option.selected = true;
                teacherSelect.appendChild(option);
            });
        } catch (error) {
            showScheduleError(error.message || "Không tải được giáo viên.");
        } finally {
            teacherSelect.disabled = false;
        }
        checkTeacherConflicts();
    }

    subjectSelect.addEventListener("change", () => loadTeachers(false));
    [teacherSelect, startDate, endDate].forEach(el => el.addEventListener("change", checkTeacherConflicts));
    scheduleContainer.addEventListener("change", event => {
        if (event.target.matches(".day-select, .slot-select")) checkTeacherConflicts();
    });

    async function checkTeacherConflicts() {
        const requestId = ++conflictRequestId;
        const teacherId = teacherSelect.value;
        const start = startDate.value;
        const end = endDate.value;
        const rows = [...scheduleContainer.querySelectorAll(".schedule-row")];
        reindexSchedules();

        if (!validateUniqueDays()) {
            showScheduleError("Mỗi thứ chỉ được chọn một ca học.");
            createSubmit.disabled = true;
            return;
        }
        if (!teacherId || !start || !end || end < start || rows.some(r => !r.querySelector(".slot-select").value)) {
            scheduleError.style.display = "none";
            createSubmit.disabled = false;
            return;
        }

        createSubmit.disabled = true;
        scheduleError.style.display = "none";
        try {
            const allConflicts = [];
            for (const row of rows) {
                const day = row.querySelector(".day-select").value;
                const slot = row.querySelector(".slot-select").value;
                const query = new URLSearchParams({
                    teacherId, startDate: start, endDate: end,
                    timeSlotId: slot, days: day
                });
                const response = await fetch(`${conflictUrl}?${query.toString()}`);
                const result = await response.json();
                if (response.status === 409 && result.conflicts) allConflicts.push(...result.conflicts);
            }
            if (requestId !== conflictRequestId) return;
            if (allConflicts.length) {
                const unique = [...new Set(allConflicts.map(c =>
                    `Lớp ${c.className} – ${c.dayLabel} – ${c.slotName}`))];
                showScheduleError("Giáo viên trùng lịch: " + unique.join("; "));
                createSubmit.disabled = true;
            } else {
                scheduleError.style.display = "none";
                createSubmit.disabled = false;
            }
        } catch (error) {
            if (requestId !== conflictRequestId) return;
            showScheduleError("Không kiểm tra được lịch giáo viên. Hãy thử lại.");
            createSubmit.disabled = true;
        }
    }

    createForm.addEventListener("submit", event => {
        reindexSchedules();
        const rows = [...scheduleContainer.querySelectorAll(".schedule-row")];
        if (!rows.length || rows.some(r => !r.querySelector(".slot-select").value)) {
            event.preventDefault();
            showScheduleError("Vui lòng chọn thứ và ca học cho tất cả lịch.");
            return;
        }
        if (!validateUniqueDays()) {
            event.preventDefault();
            showScheduleError("Mỗi thứ chỉ được chọn một ca học.");
            return;
        }
        if (endDate.value && startDate.value && endDate.value <= startDate.value) {
            event.preventDefault();
            showScheduleError("Ngày kết thúc phải sau ngày bắt đầu.");
        }
    });

    // Quick assign teacher modal
    const assignModalElement = document.getElementById("assignTeacherModal");
    const assignModal = bootstrap.Modal.getOrCreateInstance(assignModalElement);
    const assignForm = document.getElementById("assignTeacherForm");
    const assignTeacherSelect = document.getElementById("assignTeacherId");
    const assignError = document.getElementById("assignConflict");
    const assignSubmit = document.getElementById("assignTeacherSubmit");

    async function openAssignment(id) {
        document.getElementById("assignClassId").value = id;
        document.getElementById("assignClassSchedule").textContent = "Đang tải lịch học...";
        assignTeacherSelect.innerHTML = '<option value="">-- Chọn giáo viên --</option>';
        assignError.style.display = "none";
        assignSubmit.disabled = true;
        assignModal.show();
        try {
            const response = await fetch(`${page.dataset.assignInfoUrl}?id=${encodeURIComponent(id)}`);
            const data = await response.json();
            if (!response.ok) throw new Error(data.message || "Không tải được thông tin lớp.");
            document.getElementById("assignTeacherTitle").textContent = `Phân công giáo viên – ${data.className}`;
            document.getElementById("assignClassSchedule").textContent = data.schedules.length
                ? data.schedules.map(s => `${s.dayLabel}: ${s.slotName} (${s.startTime}–${s.endTime})`).join("; ")
                : "Chưa có lịch";
            data.teachers.forEach(t => {
                const option = document.createElement("option");
                option.value = t.id;
                option.textContent = t.fullName;
                if (String(t.id) === String(data.currentTeacherId)) option.selected = true;
                assignTeacherSelect.appendChild(option);
            });
            assignSubmit.disabled = false;
        } catch (error) {
            assignError.textContent = error.message;
            assignError.style.display = "block";
        }
    }

    document.querySelectorAll(".quick-assign-btn").forEach(button => {
        button.addEventListener("click", () => openAssignment(button.dataset.classId));
    });

    const requestedClassId = new URLSearchParams(window.location.search).get("classId");
    if (requestedClassId && /^[1-9]\d*$/.test(requestedClassId)) {
        const url = new URL(window.location.href);
        url.searchParams.delete("classId");
        window.history.replaceState(null, "", url);
        openAssignment(requestedClassId);
    }

    assignForm.addEventListener("submit", async event => {
        event.preventDefault();
        assignError.style.display = "none";
        assignSubmit.disabled = true;
        try {
            const response = await fetch(assignForm.action, {
                method: "POST",
                body: new FormData(assignForm),
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });
            const result = await response.json().catch(() => ({}));
            if (!response.ok) {
                let message = result.message || "Không thể phân công giáo viên.";
                if (result.conflicts) {
                    message = "Giáo viên trùng lịch: " + result.conflicts.map(c =>
                        `${c.className} (${c.dayLabel}, ${c.slotName})`).join("; ");
                }
                throw new Error(message);
            }
            window.location.reload();
        } catch (error) {
            assignError.textContent = error.message;
            assignError.style.display = "block";
            assignSubmit.disabled = false;
        }
    });

    // Capacity modal
    const capacityModal = bootstrap.Modal.getOrCreateInstance(document.getElementById("capacityModal"));
    document.querySelectorAll(".capacity-btn").forEach(button => {
        button.addEventListener("click", async () => {
            const id = button.dataset.classId;
            document.getElementById("capacitySubtitle").textContent = "Đang tải sĩ số...";
            document.getElementById("capacityStudents").innerHTML = '<tr><td colspan="4" class="text-center text-muted">Đang tải...</td></tr>';
            document.getElementById("capacityError").textContent = "";
            capacityModal.show();
            try {
                const response = await fetch(`${page.dataset.capacityUrl}?id=${encodeURIComponent(id)}`);
                if (!response.ok) throw new Error("Không tải được thông tin sĩ số.");
                const data = await response.json();
                document.getElementById("capacityTitle").textContent = `Sĩ số lớp ${data.className}`;
                document.getElementById("capacitySubtitle").textContent = `${data.occupied}/${data.max} học sinh · Tỷ lệ lấp đầy ${data.max ? Math.round(data.occupied * 100 / data.max) : 0}%`;
                document.getElementById("capacityConfirmed").textContent = data.confirmed;
                document.getElementById("capacityReserved").textContent = data.reserved;
                document.getElementById("capacityAvailable").textContent = data.available;
                const max = Math.max(Number(data.max) || 0, 1);
                document.getElementById("capacityConfirmedBar").style.width = `${data.confirmed * 100 / max}%`;
                document.getElementById("capacityReservedBar").style.width = `${data.reserved * 100 / max}%`;
                document.getElementById("capacityFreeBar").style.width = `${data.available * 100 / max}%`;
                const tbody = document.getElementById("capacityStudents");
                tbody.replaceChildren();
                if (!data.students || data.students.length === 0) {
                    tbody.innerHTML = '<tr><td colspan="4" class="text-center text-muted">Chưa có học sinh đăng ký.</td></tr>';
                } else {
                    data.students.forEach(student => {
                        const tr = document.createElement("tr");
                        const values = [student.studentName || "—", student.parentId ?? "—", student.reservedUntil ? new Date(student.reservedUntil).toLocaleString("vi-VN") : "—", student.status === "Confirmed" ? "Đã xác nhận" : "Đang giữ chỗ"];
                        values.forEach((value, index) => {
                            const td = document.createElement("td");
                            td.textContent = value;
                            if (index === 3) td.innerHTML = student.status === "Confirmed" ? '<span class="badge bg-success">Đã xác nhận</span>' : '<span class="badge bg-warning text-dark">Đang giữ chỗ</span>';
                            tr.appendChild(td);
                        });
                        tbody.appendChild(tr);
                    });
                }
            } catch (error) {
                document.getElementById("capacityError").textContent = error.message;
            }
        });
    });

    if (page.dataset.openCreate === "true") {
        bootstrap.Modal.getOrCreateInstance(document.getElementById("createClassModal")).show();
    }
}

function initClassCreate() {
    const form = document.getElementById('standaloneCreateClassForm');
    if (!form) return;
    const subjectSelect = document.getElementById("SubjectId");
    const teacherSelect = document.getElementById("TeacherId");

    const container = document.getElementById("scheduleContainer");
    const template = document.getElementById("scheduleTemplate");
    const addButton = document.getElementById("addScheduleBtn");
    const validationMessage =
        document.getElementById("scheduleValidation");

    const teachersUrl =
        form.dataset.teachersUrl;

    let nextIndex = container.querySelectorAll(".schedule-row").length;

    async function loadTeachers() {
        const subjectId = subjectSelect.value;

        teacherSelect.innerHTML =
            '<option value="">-- Chưa phân công giáo viên --</option>';

        if (!subjectId) {
            teacherSelect.disabled = true;
            return;
        }

        teacherSelect.disabled = true;

        try {
            const response = await fetch(
                `${teachersUrl}?subjectId=${encodeURIComponent(subjectId)}`,
                {
                    headers: { "Accept": "application/json" }
                }
            );

            if (!response.ok) {
                throw new Error("Không tải được danh sách Teacher.");
            }

            const teachers = await response.json();

            teachers.forEach(function (teacher) {
                const option = document.createElement("option");
                option.value = teacher.id;
                option.textContent = teacher.fullName;
                teacherSelect.appendChild(option);
            });
        } catch (error) {
            console.error(error);
            validationMessage.textContent =
                "Không tải được danh sách Teacher. Vui lòng thử lại.";
        } finally {
            teacherSelect.disabled = false;
        }
    }

    subjectSelect.addEventListener("change", loadTeachers);
    loadTeachers();

    function addScheduleRow() {
        const html = template.innerHTML.replace(
            /__index__/g,
            nextIndex
        );

        container.insertAdjacentHTML("beforeend", html);
        nextIndex++;
        validationMessage.textContent = "";
    }

    addButton.addEventListener("click", addScheduleRow);

    container.addEventListener("click", function (event) {
        const button = event.target.closest(".remove-schedule");

        if (!button) return;

        const rows = container.querySelectorAll(".schedule-row");

        if (rows.length <= 1) {
            validationMessage.textContent =
                "Lớp phải có ít nhất một buổi học.";
            return;
        }

        button.closest(".schedule-row").remove();
        validationMessage.textContent = "";
    });

    // Chặn trùng cùng thứ và cùng khung giờ trên giao diện.
    // Server vẫn kiểm tra lại trước khi lưu.


    form
        .addEventListener("submit", function (event) {
            const rows = Array.from(
                container.querySelectorAll(".schedule-row")
            );

            const seen = new Set();
            let duplicate = false;

            rows.forEach(function (row) {
                const day = row.querySelector(".day-select").value;
                const slot = row.querySelector(".slot-select").value;

                if (!day || !slot) return;

                const key = `${day}-${slot}`;

                if (seen.has(key)) {
                    duplicate = true;
                }

                seen.add(key);
            });

            if (rows.length === 0) {
                duplicate = true;
                validationMessage.textContent =
                    "Vui lòng thêm ít nhất một buổi học.";
            } else if (duplicate) {
                validationMessage.textContent =
                    "Có lịch bị trùng thứ và khung giờ. Vui lòng kiểm tra lại.";
            }

            if (duplicate) {
                event.preventDefault();
            }
        });
}

function initClassEdit() {
    const pageForm = document.getElementById('editClassForm');
    if (!pageForm) return;
    const container = document.getElementById('scheduleContainer');
    const template = document.getElementById('scheduleTemplate');
    const addButton = document.getElementById('addScheduleBtn');
    const form = document.getElementById('editClassForm');
    const error = document.getElementById('scheduleError');
    let nextIndex = container.querySelectorAll('.schedule-row').length;

    function reindexRows() {
        container.querySelectorAll('.schedule-row').forEach((row, index) => {
            row.querySelector('.day-select').name = `Schedules[${index}].DayOfWeek`;
            row.querySelector('.slot-select').name = `Schedules[${index}].TimeSlotId`;
        });
        nextIndex = container.querySelectorAll('.schedule-row').length;
    }

    addButton.addEventListener('click', function () {
        const fragment = template.content.cloneNode(true);
        container.appendChild(fragment);
        reindexRows();
        error.textContent = '';
    });

    container.addEventListener('click', function (event) {
        const button = event.target.closest('.remove-schedule');
        if (!button) return;
        if (container.querySelectorAll('.schedule-row').length <= 1) {
            error.textContent = 'Lớp phải có ít nhất một lịch học.';
            return;
        }
        button.closest('.schedule-row').remove();
        reindexRows();
    });

    form.addEventListener('submit', function (event) {
        reindexRows();
        const rows = [...container.querySelectorAll('.schedule-row')];
        const days = rows.map(r => r.querySelector('.day-select').value);
        const duplicate = new Set(days).size !== days.length;
        const incomplete = rows.some(r => !r.querySelector('.day-select').value || !r.querySelector('.slot-select').value);
        if (rows.length === 0 || duplicate || incomplete) {
            event.preventDefault();
            error.textContent = duplicate
                ? 'Mỗi thứ chỉ được chọn một lần.'
                : 'Vui lòng chọn thứ và ca học cho từng lịch.';
        }
    });

    const subjectSelect = document.getElementById('SubjectId');
    const teacherSelect = document.getElementById('TeacherId');
    const teachersUrl = pageForm.dataset.teachersUrl;
    async function loadTeachers(preserve) {
        const selected = preserve ? teacherSelect.value : '';
        teacherSelect.innerHTML = '<option value="">-- Chưa phân công giáo viên --</option>';
        if (!subjectSelect.value) return;
        try {
            const response = await fetch(`${teachersUrl}?subjectId=${encodeURIComponent(subjectSelect.value)}`);
            if (!response.ok) throw new Error('Không tải được giáo viên.');
            const teachers = await response.json();
            teachers.forEach(t => {
                const option = new Option(t.fullName, t.id, false, String(t.id) === selected);
                teacherSelect.add(option);
            });
        } catch (e) { console.error(e); }
    }
    subjectSelect.addEventListener('change', () => loadTeachers(false));
}

function initAdminConfirmations() {
    document.querySelectorAll('form[data-confirm]').forEach(function (form) {
        form.addEventListener('submit', function (event) {
            if (!window.confirm(form.dataset.confirm)) event.preventDefault();
        });
    });
}

document.addEventListener('DOMContentLoaded', function () {
    initAdminConfirmations();
    initClassDates();
    initClassIndex();
    initClassCreate();
    initClassEdit();
});
