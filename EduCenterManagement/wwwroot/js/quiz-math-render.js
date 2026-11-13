/**
 * EduCenter Management - Quiz Math & Science Renderer (KaTeX + Image Markdown)
 * Hỗ trợ công thức Toán - Lý - Hóa (LaTeX) và hình ảnh minh họa cho đề thi THPTQG
 */

(function (window) {
    'use strict';

    // Tự động kiểm tra và chèn CSS KaTeX nếu trang chưa có
    function ensureKaTeXStyles() {
        if (!document.getElementById('katex-css-runtime')) {
            const link = document.createElement('link');
            link.id = 'katex-css-runtime';
            link.rel = 'stylesheet';
            link.href = '/lib/katex/katex.min.css';
            link.onerror = function () {
                this.href = 'https://cdn.jsdelivr.net/npm/katex@0.16.11/dist/katex.min.css';
            };
            document.head.appendChild(link);
        }

        // Ép ẩn triệt để lớp MathML để không bao giờ bị lặp đôi chữ
        if (!document.getElementById('katex-fix-style')) {
            const style = document.createElement('style');
            style.id = 'katex-fix-style';
            style.textContent = `
                .katex-mathml { display: none !important; }
                .katex { font-size: 1.1em; line-height: 1.2; }
                .katex svg { max-width: 100%; }
            `;
            document.head.appendChild(style);
        }
    }

    ensureKaTeXStyles();

    const QuizMath = {
        /**
         * Render toàn bộ công thức toán học và ảnh markdown trong một container
         * @param {HTMLElement|string} containerEl 
         */
        render: function (containerEl) {
            ensureKaTeXStyles();

            const container = typeof containerEl === 'string'
                ? document.querySelector(containerEl)
                : (containerEl || document.body);

            if (!container) return;

            // 1. Chuyển đổi cú pháp ảnh Markdown ![mota](url) thành thẻ <img> an toàn
            const renderTargets = container.querySelectorAll('.math-render');
            const elements = renderTargets.length > 0 ? renderTargets : [container];

            elements.forEach(function (el) {
                // Chỉ parse markdown image nếu chưa parse
                if (el.innerHTML.indexOf('![') !== -1) {
                    el.innerHTML = el.innerHTML.replace(
                        /!\[([^\]]*)\]\((https?:\/\/[^\s\)\"\'<>]+)\)/gi,
                        '<img src="$2" alt="$1" class="img-fluid rounded my-2 d-block shadow-sm border" style="max-height: 280px; object-fit: contain;" onerror="this.onerror=null;this.parentElement.innerHTML+=\'<span class=\\\'badge bg-danger-subtle text-danger\\\'>[Ảnh lỗi hoặc không tồn tại]</span>\';" />'
                    );
                }
            });

            // 2. Gọi KaTeX auto-render nếu thư viện đã sẵn sàng
            if (typeof renderMathInElement === 'function') {
                try {
                    renderMathInElement(container, {
                        output: 'html', // CHỈ XUẤT HTML: Triệt tiêu 100% hiện tượng lặp đôi chữ MathML
                        delimiters: [
                            { left: '$$', right: '$$', display: true },
                            { left: '$', right: '$', display: false },
                            { left: '\\(', right: '\\)', display: false },
                            { left: '\\[', right: '\\]', display: true }
                        ],
                        throwOnError: false,
                        errorColor: '#dc3545',
                        ignoredTags: ['script', 'noscript', 'style', 'textarea', 'pre', 'code']
                    });
                } catch (e) {
                    console.warn('KaTeX render error:', e);
                }
            }
        },

        /**
         * Chèn một đoạn mã ký hiệu vào vị trí con trỏ của input/textarea
         * @param {HTMLInputElement|HTMLTextAreaElement} input 
         * @param {string} snippet 
         * @param {number} cursorOffsetFromEnd (khoảng cách lùi con trỏ về sau khi chèn)
         */
        insertSnippet: function (input, snippet, cursorOffsetFromEnd) {
            if (!input) return;
            const start = input.selectionStart || 0;
            const end = input.selectionEnd || 0;
            const text = input.value;

            input.value = text.substring(0, start) + snippet + text.substring(end);
            input.focus();

            const newCursor = start + snippet.length - (cursorOffsetFromEnd || 0);
            input.setSelectionRange(newCursor, newCursor);

            // Bắn event input để kích hoạt Live Preview
            input.dispatchEvent(new Event('input', { bubbles: true }));
        },

        /**
         * Tạo thanh công cụ Math Toolbar nút bấm nhanh
         * @param {string} targetContainerId ID của nơi chứa toolbar
         * @param {function} getActiveInputFn Hàm lấy input/textarea đang focus
         */
        createToolbar: function (targetContainerId, getActiveInputFn) {
            const container = document.getElementById(targetContainerId);
            if (!container) return;

            const buttons = [
                // Nhóm định dạng chung
                { label: '$...$', snippet: '$ $', offset: 2, title: 'Bọc trong công thức toán ($)', group: 'math', class: 'btn-dark' },

                // Nhóm Toán
                { label: 'a/b', snippet: '$\\frac{a}{b}$', offset: 4, title: 'Phân số a/b', group: 'math', class: 'btn-outline-primary' },
                { label: '√x', snippet: '$\\sqrt{x}$', offset: 2, title: 'Căn bậc hai', group: 'math', class: 'btn-outline-primary' },
                { label: 'x²', snippet: '$x^{2}$', offset: 2, title: 'Số mũ trên', group: 'math', class: 'btn-outline-primary' },
                { label: 'x₁', snippet: '$x_{1}$', offset: 2, title: 'Chỉ số dưới', group: 'math', class: 'btn-outline-primary' },
                { label: '∫', snippet: '$\\int_{a}^{b} f(x)dx$', offset: 10, title: 'Tích phân', group: 'math', class: 'btn-outline-primary' },
                { label: 'v⃗', snippet: '$\\vec{v}$', offset: 2, title: 'Véc-tơ', group: 'math', class: 'btn-outline-primary' },
                { label: '≤ / ≥', snippet: '$\\le$', offset: 1, title: 'Dấu nhỏ hơn hoặc bằng', group: 'math', class: 'btn-outline-primary' },
                { label: '≠', snippet: '$\\ne$', offset: 1, title: 'Dấu khác', group: 'math', class: 'btn-outline-primary' },
                { label: 'π / α', snippet: '$\\pi$', offset: 1, title: 'Số Pi hoặc ký hiệu Hy Lạp', group: 'math', class: 'btn-outline-primary' },
                { label: '∞', snippet: '$\\infty$', offset: 1, title: 'Vô cực', group: 'math', class: 'btn-outline-primary' },

                // Nhóm Lý
                { label: 'Ω', snippet: '$\\Omega$', offset: 1, title: 'Đơn vị Ôm (Điện trở)', group: 'phy', class: 'btn-outline-info' },
                { label: 'λ', snippet: '$\\lambda$', offset: 1, title: 'Bước sóng Lambda', group: 'phy', class: 'btn-outline-info' },
                { label: 'ω', snippet: '$\\omega$', offset: 1, title: 'Tần số góc Omega', group: 'phy', class: 'btn-outline-info' },
                { label: 'Δt', snippet: '$\\Delta t$', offset: 1, title: 'Độ biến thiên Delta', group: 'phy', class: 'btn-outline-info' },

                // Nhóm Hóa
                { label: '→', snippet: '$\\rightarrow$', offset: 1, title: 'Mũi tên phản ứng', group: 'chem', class: 'btn-outline-success' },
                { label: '⇌', snippet: '$\\rightleftharpoons$', offset: 1, title: 'Phản ứng thuận nghịch', group: 'chem', class: 'btn-outline-success' },
                { label: '↑ / ↓', snippet: '$\\uparrow$', offset: 1, title: 'Khí bay lên / Kết tủa', group: 'chem', class: 'btn-outline-success' },
                { label: 'H₂SO₄', snippet: '$H_{2}SO_{4}$', offset: 1, title: 'Chất hóa học chỉ số dưới', group: 'chem', class: 'btn-outline-success' },
                { label: 'Fe³⁺', snippet: '$Fe^{3+}$', offset: 1, title: 'Ion điện tích', group: 'chem', class: 'btn-outline-success' },

                // Nhóm Ảnh
                { label: '🖼️ Chèn ảnh đồ thị', snippet: '![Đồ thị hàm số](https://link-anh-cua-ban.png)', offset: 1, title: 'Chèn link ảnh minh họa / đồ thị', group: 'media', class: 'btn-outline-danger' }
            ];

            let html = '<div class="d-flex flex-wrap gap-1 p-2 bg-light border rounded mb-2 align-items-center">';
            html += '<small class="fw-bold text-muted me-1"><i class="bi bi-tools me-1"></i>Chèn ký hiệu:</small>';

            buttons.forEach(function (btn) {
                html += `<button type="button" class="btn btn-sm py-0 px-2 ${btn.class} btn-math-insert" data-snippet="${btn.snippet.replace(/"/g, '&quot;')}" data-offset="${btn.offset}" title="${btn.title}">${btn.label}</button>`;
            });

            html += '</div>';
            container.innerHTML = html;

            container.querySelectorAll('.btn-math-insert').forEach(function (btn) {
                btn.addEventListener('click', function (e) {
                    e.preventDefault();
                    const snippet = this.getAttribute('data-snippet');
                    const offset = parseInt(this.getAttribute('data-offset') || '0', 10);
                    const target = getActiveInputFn();
                    if (target) {
                        QuizMath.insertSnippet(target, snippet, offset);
                    }
                });
            });
        }
    };

    window.QuizMath = QuizMath;
})(window);

