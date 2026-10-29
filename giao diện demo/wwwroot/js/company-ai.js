/* One conversation surface, two sizes. No provider credentials or fabricated company data. */
(function () {
    'use strict';
    const root = document.getElementById('ava-assistant');
    if (!root || root.dataset.avaInitialized) return;
    root.dataset.avaInitialized = 'true';
    // Escape any layout stacking/overflow context; the pet is a viewport-level component.
    document.body.appendChild(root);
    const el = id => document.getElementById(id);
    const dock = el('company-pet-dock'), trigger = el('ava-trigger'), drawer = el('ava-drawer');
    const form = el('ava-composer'), input = el('ava-input'), messages = el('ava-messages');
    const content = el('company-content'), welcome = el('company-welcome'), panel = el('company-panel');
    const error = el('ava-error'), status = el('ava-upload-status'), menu = el('company-pet-menu');
    const fileInput = el('ava-file-input'), send = el('ava-send'), latest = el('company-scroll-latest');
    const identity = encodeURIComponent(root.dataset.userId || '');
    const prefix = 'company-ai:v1:' + identity + ':';
    const state = { id: null, log: [], files: [], busy: false, validating: false, xhr: null, panel: 'chat', drag: null, suppressClick: false, follow: true, recorder: null, stream: null, chunks: [], cancelledRecording: false, audio: null, speechLoading: false, historyAbort: null, capabilities: null, capabilityError: '', mode: 'auto', trainingBusy: false, recordingStarting: false, recordingGeneration: 0 };
    const types = { png: 'image/png', jpg: 'image/jpeg', jpeg: 'image/jpeg', webp: 'image/webp', bmp: 'image/bmp', pdf: 'application/pdf', docx: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', xlsx: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', txt: 'text/plain', csv: 'text/csv', json: 'application/json', wav: 'audio/wav', mp3: 'audio/mpeg', m4a: 'audio/mp4', ogg: 'audio/ogg', webm: 'audio/webm' };
    const mimeAliases = { 'image/jpeg': ['image/jpg'], 'text/csv': ['application/vnd.ms-excel'], 'application/json': ['text/json'], 'audio/wav': ['audio/wave', 'audio/x-wav', 'audio/vnd.wave'], 'audio/mpeg': ['audio/mp3'], 'audio/mp4': ['audio/x-m4a'], 'audio/ogg': ['application/ogg'], 'audio/webm': ['video/webm'] };
    const prompts = {
        policy: 'Tôi muốn tìm hiểu quy định nội bộ về ', document: 'Hãy tóm tắt tài liệu đính kèm, nêu các ý chính và điều cần lưu ý.',
        meeting: 'Hãy tóm tắt biên bản cuộc họp sau thành quyết định, công việc cần làm và người phụ trách:\n',
        work: 'Giúp tôi lập kế hoạch công việc cho ', people: 'Thông tin hồ sơ của tôi',
        write: 'Giúp tôi soạn một bản nháp ', data: 'Phân tích bảng dữ liệu đính kèm, giải thích kết quả và nêu giới hạn của dữ liệu.',
        translate: 'Dịch nội dung sau sang tiếng Anh, giữ nguyên ý nghĩa và thuật ngữ:\n',
        image: 'Tạo ảnh poster về ', word: 'Tạo file Word mẫu cho ', excel: 'Tạo file Excel mẫu cho ',
        slides: 'Tạo slide mẫu về ', coding: 'Phân tích repository và giải thích cấu trúc dự án hiện tại.',
        hr: 'Tra cứu thông tin nhân sự trong phạm vi quyền của tôi: ', skills: 'Danh sách skill hiện có của AI service là gì?',
        vision: 'Mô tả hình ảnh đính kèm và trích xuất chữ nhìn thấy. Nêu rõ phần không đọc được.'
    };
    const flowForPrompt = { policy: 'knowledge', people: 'personal', document: 'documents', data: 'data', vision: 'vision', image: 'image', word: 'word', excel: 'excel', slides: 'slides', coding: 'coding', hr: 'hr' };
    function readStore(storage, key, fallback) { try { return JSON.parse(storage.getItem(prefix + key)) ?? fallback; } catch (_) { return fallback; } }
    function writeStore(storage, key, value) { try { storage.setItem(prefix + key, JSON.stringify(value)); return true; } catch (_) { return false; } }
    const preferences = readStore(window.localStorage, 'preferences', { visible: true, mini: false, reduceMotion: false, voice: 'auto' });
    function make(tag, cls, text) { const n = document.createElement(tag); if (cls) n.className = cls; if (text !== undefined) n.textContent = text; return n; }
    function button(text, action, cls = 'company-text-button') { const b = make('button', cls, text); b.type = 'button'; b.addEventListener('click', action); return b; }
    function flowAllowed(id) { return state.capabilities?.flows?.find(f => f.id === id)?.allowed !== false; }
    function resizeComposer() {
        const longDraft = input.value.length > 220 || input.value.split('\n').length > 3;
        form.classList.toggle('is-long-draft', longDraft);
        input.style.height = 'auto';
        if (!input.getClientRects().length) return;
        const styles = window.getComputedStyle(input);
        const minimum = parseFloat(styles.minHeight) || 35;
        const maximum = Math.max(minimum, Math.min(180, drawer.clientHeight * 0.3));
        const desired = Math.max(minimum, input.scrollHeight);
        input.style.maxHeight = maximum + 'px';
        input.style.height = Math.min(desired, maximum) + 'px';
        input.style.overflowY = desired > maximum ? 'auto' : 'hidden';
    }
    function updateComposer() { resizeComposer(); send.disabled = state.busy || state.validating || state.recordingStarting || (!!state.recorder && state.recorder.state === 'recording') || !flowAllowed('chat') || (!input.value.trim() && !state.files.length); input.disabled = state.busy; el('ava-attach').disabled = state.busy || state.validating; el('ava-mic').disabled = state.busy || state.validating || state.recordingStarting || !flowAllowed('transcribe'); petController?.refresh(); }
    function setStatus(text) { status.textContent = text; status.hidden = !text; }
    function clearError() { error.hidden = true; error.replaceChildren(); setPet('idle'); }
    function cancelCurrentOperation() {
        if (state.xhr) {
            try { state.xhr.abort(); } catch (_) { }
            state.xhr = null;
        }
        if (state.recorder?.state === 'recording' || state.recordingStarting) {
            cancelRecording();
        }
        stopSpeech();
        state.busy = false;
        state.validating = false;
        state.trainingBusy = false;
        state.speechLoading = false;
        el('ava-cancel-request').hidden = true;
        clearError();
        setStatus('');
        setPet('idle');
        updateComposer();
    }
    function showError(text, retry) {
        error.replaceChildren(make('span', '', text));
        if (retry) error.appendChild(button('Thử lại', retry));
        if (state.busy || state.validating || state.recordingStarting || state.recorder?.state === 'recording') {
            error.appendChild(button('Hủy thao tác', cancelCurrentOperation, 'company-text-button company-cancel-inline-btn'));
        }
        const dismissBtn = button('×', clearError, 'company-dismiss-btn');
        dismissBtn.setAttribute('title', 'Đóng thông báo');
        dismissBtn.setAttribute('aria-label', 'Đóng thông báo');
        error.appendChild(dismissBtn);
        error.hidden = false;
        setPet('notification');
    }
    function petActivity() {
        if (state.drag?.moved) return 'dragging';
        if (state.recordingStarting || state.recorder?.state === 'recording') return 'listening';
        if (state.busy || state.validating || state.trainingBusy || state.speechLoading) return 'thinking';
        if (state.audio) return 'talking';
        return 'idle';
    }
    const petController = window.CompanyPetController?.create({
        root, dock, trigger, bubble: el('company-pet-bubble'), preferences,
        getActivity: petActivity, getError: () => !error.hidden
    });
    function setPet(mood, text, revert) {
        if (petController) petController.present(mood, text, revert);
        else root.dataset.petState = petActivity() !== 'idle' ? petActivity() : mood;
    }
    function wake() { petController?.wake(); }
    function applyPreferences() {
        dock.hidden = !preferences.visible;
        root.classList.toggle('pet-mini', preferences.mini); root.classList.toggle('reduce-motion', preferences.reduceMotion);
        el('company-ai-reopen').hidden = preferences.visible || drawer.classList.contains('open');
        petController?.configure(preferences);
        writeStore(window.localStorage, 'preferences', preferences);
    }
    function viewport() { return { width: window.innerWidth, height: window.innerHeight }; }
    function positionPet(x, y, persist = true) {
        const v = viewport(), r = dock.getBoundingClientRect();
        const left = Math.max(8, Math.min(Number(x) || 8, Math.max(8, v.width - r.width - 8)));
        const top = Math.max(8, Math.min(Number(y) || 8, Math.max(8, v.height - r.height - 8)));
        dock.style.left = left + 'px'; dock.style.top = top + 'px'; dock.style.right = 'auto'; dock.style.bottom = 'auto';
        // Keep contextual text inside the viewport even when the pet is in a corner.
        const bubble = el('company-pet-bubble'); bubble.style.left = Math.max(95 - left, Math.min(r.width / 2, v.width - 103 - left)) + 'px';
        bubble.style.bottom = top < 95 ? 'auto' : 'calc(100% + 5px)'; bubble.style.top = top < 95 ? 'calc(100% + 5px)' : 'auto';
        if (persist) writeStore(window.localStorage, 'position', { x: left, y: top });
        positionWindow();
    }
    function positionWindow() {
        if (drawer.classList.contains('ava-expanded')) return;
        const v = viewport(), p = dock.getBoundingClientRect(), width = Math.min(470, v.width - 24), height = Math.min(710, v.height - 36);
        let x = p.left > v.width / 2 ? p.left - width - 12 : p.right + 12;
        let y = p.top + p.height - height;
        drawer.style.left = Math.max(12, Math.min(x, v.width - width - 12)) + 'px'; drawer.style.top = Math.max(12, Math.min(y, v.height - height - 12)) + 'px';
    }
    function resetPosition() { preferences.visible = true; preferences.mini = false; applyPreferences(); const v = viewport(); positionPet(v.width - 126, v.height - 300); }
    trigger.addEventListener('pointerdown', e => {
        if (e.button !== 0 || e.isPrimary === false) return;
        wake(); hideMenu();
        const r = dock.getBoundingClientRect();
        state.drag = {
            id: e.pointerId,
            startX: e.clientX,
            startY: e.clientY,
            x: r.left,
            y: r.top,
            moved: false
        };
        try {
            trigger.setPointerCapture(e.pointerId);
        } catch (_) { }
    });

    function handlePointerMove(e) {
        const d = state.drag;
        if (!d || d.id !== e.pointerId) return;
        const dx = e.clientX - d.startX;
        const dy = e.clientY - d.startY;
        if (Math.hypot(dx, dy) > 4) {
            d.moved = true;
            state.suppressClick = true;
        }
        if (d.moved) {
            e.preventDefault();
            setPet('dragging');
            positionPet(d.x + dx, d.y + dy, false);
        }
    }

    function endDrag(e) {
        if (!state.drag || state.drag.id !== e.pointerId) return;
        const wasMoved = state.drag.moved;
        state.drag = null;
        if (wasMoved) {
            state.suppressClick = true;
            setTimeout(() => { state.suppressClick = false; }, 250);
        }
        try {
            if (trigger.hasPointerCapture?.(e.pointerId)) {
                trigger.releasePointerCapture(e.pointerId);
            }
        } catch (_) { }
        const r = dock.getBoundingClientRect();
        positionPet(r.left, r.top, true);
        setPet(state.busy ? 'thinking' : 'idle');
        wake();
    }

    trigger.addEventListener('pointermove', handlePointerMove);
    window.addEventListener('pointermove', handlePointerMove, { passive: false });
    trigger.addEventListener('pointerup', endDrag);
    trigger.addEventListener('pointercancel', endDrag);
    trigger.addEventListener('lostpointercapture', endDrag);
    window.addEventListener('pointerup', endDrag);
    window.addEventListener('pointercancel', endDrag);

    trigger.addEventListener('click', e => {
        if (state.suppressClick) {
            state.suppressClick = false;
            e.preventDefault();
            e.stopPropagation();
            return;
        }
        setOpen(!drawer.classList.contains('open'));
    });
    trigger.addEventListener('keydown', e => {
        const moves = { ArrowLeft: [-20, 0], ArrowRight: [20, 0], ArrowUp: [0, -20], ArrowDown: [0, 20] };
        if (moves[e.key]) { e.preventDefault(); const r = dock.getBoundingClientRect(); positionPet(r.left + moves[e.key][0], r.top + moves[e.key][1]); wake(); }
        if (e.key === 'ContextMenu' || (e.shiftKey && e.key === 'F10')) { e.preventDefault(); showMenu(); }
    });
    trigger.addEventListener('contextmenu', e => { e.preventDefault(); showMenu(); });
    function showMenu() {
        wake(); const v = viewport(), r = dock.getBoundingClientRect(); menu.hidden = false;
        menu.style.left = Math.max(8, Math.min(r.left, v.width - 234)) + 'px';
        menu.style.top = Math.max(8, Math.min(r.top - menu.offsetHeight - 8, v.height - menu.offsetHeight - 8)) + 'px';
        el('company-pet-options').setAttribute('aria-expanded', 'true'); menu.querySelector('button')?.focus();
    }
    function hideMenu() { menu.hidden = true; el('company-pet-options').setAttribute('aria-expanded', 'false'); }
    el('company-pet-options').addEventListener('click', () => menu.hidden ? showMenu() : hideMenu());
    menu.addEventListener('click', e => {
        const a = e.target.closest('[data-pet-action]')?.dataset.petAction; if (!a) return; hideMenu();
        if (a === 'mini') { preferences.mini = !preferences.mini; applyPreferences(); const r = dock.getBoundingClientRect(); positionPet(r.left, r.top); }
        else if (a === 'hide') { preferences.visible = false; applyPreferences(); }
        else if (a === 'chat') { setOpen(true); showPanel('chat'); }
        else if (a === 'note') openPanel('tasks');
        else if (a === 'summary') start('document');
        else if (a === 'translate') start('translate');
        else if (a === 'screenshot') captureScreen();
        else openPanel('help');
    });
    menu.addEventListener('keydown', e => {
        const items = Array.from(menu.querySelectorAll('button'));
        if (e.key === 'ArrowDown' || e.key === 'ArrowUp') { e.preventDefault(); const n = items.indexOf(document.activeElement); items[(n + (e.key === 'ArrowDown' ? 1 : items.length - 1)) % items.length]?.focus(); }
    });
    document.addEventListener('pointerdown', e => { if (!menu.contains(e.target) && !dock.contains(e.target)) hideMenu(); });
    function setOpen(open) {
        drawer.classList.toggle('open', open); drawer.setAttribute('aria-hidden', String(!open)); drawer.inert = !open; trigger.setAttribute('aria-expanded', String(open));
        if (open) { positionWindow(); resizeComposer(); input.focus(); wake(); } else { if (state.recordingStarting || state.recorder?.state === 'recording') cancelRecording(); hideMenu(); drawer.classList.remove('mobile-nav-open'); trigger.focus(); }
        applyPreferences(); saveSession();
    }
    function expand(force) { const value = force ?? !drawer.classList.contains('ava-expanded'); drawer.classList.toggle('ava-expanded', value); el('ava-expand').setAttribute('aria-pressed', String(value)); el('ava-expand').setAttribute('aria-label', value ? 'Thu gọn cửa sổ' : 'Mở rộng không gian'); positionWindow(); resizeComposer(); saveSession(); }
    el('ava-expand').addEventListener('click', () => expand()); el('ava-close').addEventListener('click', () => setOpen(false));
    el('company-ai-reopen').addEventListener('click', () => { setOpen(true); expand(true); showPanel('settings'); });
    el('company-page-open')?.addEventListener('click', () => { setOpen(true); expand(true); });
    el('company-sidebar-toggle').addEventListener('click', () => { const collapsed = drawer.classList.toggle('sidebar-collapsed'); el('company-sidebar-toggle').setAttribute('aria-expanded', String(!collapsed)); el('company-sidebar-toggle').textContent = collapsed ? '›' : '‹'; resizeComposer(); });
    el('company-mobile-nav').addEventListener('click', () => { const open = drawer.classList.toggle('mobile-nav-open'); el('company-mobile-nav').setAttribute('aria-expanded', String(open)); });
    el('company-sidebar-mobile-close').addEventListener('click', () => { drawer.classList.remove('mobile-nav-open'); el('company-mobile-nav').setAttribute('aria-expanded', 'false'); });
    window.addEventListener('resize', () => { hideMenu(); const r = dock.getBoundingClientRect(); positionPet(r.left, r.top); resizeComposer(); });
    document.addEventListener('keydown', e => { if (e.key === 'Escape') { if (!menu.hidden) { hideMenu(); trigger.focus(); } else if (drawer.classList.contains('mobile-nav-open')) { drawer.classList.remove('mobile-nav-open'); el('company-mobile-nav').focus(); } else if (drawer.classList.contains('open')) setOpen(false); } });
    root.addEventListener('pointerdown', wake); root.addEventListener('keydown', wake);

    function saveSession() {
        // Per-user, per-tab cache; server-side owner checks remain authoritative.
        writeStore(window.sessionStorage, 'chat', { id: state.id, log: state.log.slice(-25), expanded: drawer.classList.contains('ava-expanded') });
    }
    function scrollEnd(force = false) { if (state.follow || force) { messages.scrollTop = messages.scrollHeight; latest.hidden = true; } else latest.hidden = false; }
    messages.addEventListener('scroll', () => { state.follow = messages.scrollHeight - messages.scrollTop - messages.clientHeight < 90; if (state.follow) latest.hidden = true; }, { passive: true });
    latest.addEventListener('click', () => { state.follow = true; scrollEnd(true); });
    // Markdown is constructed as DOM. Raw HTML and inline event handlers are never accepted.
    function inline(parent, text) {
        const pattern = /(`[^`]+`|\*\*[^*]+\*\*|\*[^*]+\*|\[[^\]]+\]\([^\s)]+\))/g;
        let cursor = 0;
        for (const match of text.matchAll(pattern)) {
            parent.appendChild(document.createTextNode(text.slice(cursor, match.index))); const part = match[0];
            if (part.startsWith('`')) parent.appendChild(make('code', '', part.slice(1, -1)));
            else if (part.startsWith('**')) parent.appendChild(make('strong', '', part.slice(2, -2)));
            else if (part.startsWith('*')) parent.appendChild(make('em', '', part.slice(1, -1)));
            else {
                const link = /^\[([^\]]+)\]\((.+)\)$/.exec(part);
                let url; try { url = new URL(link[2], location.origin); } catch (_) { url = null; }
                if (url && ['https:', 'http:'].includes(url.protocol)) { const a = make('a', '', link[1]); a.href = url.href; a.target = '_blank'; a.rel = 'noopener noreferrer'; parent.appendChild(a); }
                else parent.appendChild(document.createTextNode(part));
            }
            cursor = match.index + part.length;
        }
        parent.appendChild(document.createTextNode(text.slice(cursor)));
    }
    async function copy(text, b) { try { await navigator.clipboard.writeText(text); b.textContent = 'Đã sao chép'; } catch (_) { b.textContent = 'Không thể sao chép'; } }
    function markdown(target, text) {
        const lines = String(text || '').replace(/\r/g, '').split('\n');
        for (let i = 0; i < lines.length; i++) {
            const line = lines[i];
            if (line.startsWith('```')) {
                const code = []; const lang = line.slice(3).trim(); while (++i < lines.length && !lines[i].startsWith('```')) code.push(lines[i]);
                const card = make('div', 'company-code'), head = make('div', 'company-code-head', lang || 'Mã'); const b = button('Sao chép mã', () => copy(code.join('\n'), b), 'company-code-copy'); head.appendChild(b); card.appendChild(head);
                const pre = make('pre'); pre.appendChild(make('code', '', code.join('\n'))); card.appendChild(pre); target.appendChild(card); continue;
            }
            if (!line.trim()) continue;
            if (line.includes('|') && i + 1 < lines.length && /^\s*\|?\s*:?-{3,}/.test(lines[i + 1])) {
                const wrap = make('div', 'company-table-wrap'), table = make('table'); let row = line; let header = true;
                do { const tr = make('tr'); row.replace(/^\s*\||\|\s*$/g, '').split('|').forEach(cell => { const td = make(header ? 'th' : 'td'); if (header) td.scope = 'col'; inline(td, cell.trim()); tr.appendChild(td); }); table.appendChild(tr); if (header) i++; header = false; row = lines[i + 1] || ''; if (row.includes('|')) i++; else break; } while (true);
                wrap.appendChild(table); target.appendChild(wrap); continue;
            }
            const heading = /^(#{1,3})\s+(.+)$/.exec(line), list = /^\s*([-*]|\d+\.)\s+(.+)$/.exec(line);
            if (list) { const ordered = /\d/.test(list[1]); let ul = target.lastElementChild; if (!ul || ul.tagName !== (ordered ? 'OL' : 'UL')) { ul = make(ordered ? 'ol' : 'ul'); target.appendChild(ul); } const li = make('li'); inline(li, list[2]); ul.appendChild(li); }
            else { const node = make(heading ? 'h' + (heading[1].length + 1) : line.startsWith('> ') ? 'blockquote' : 'p'); inline(node, heading ? heading[2] : line.startsWith('> ') ? line.slice(2) : line); target.appendChild(node); }
        }
    }
    function renderMessage(item) {
        const node = make('article', 'company-message ' + item.role);
        if (item.role === 'assistant') {
            const head = make('div', 'company-message-header'), avatar = make('span', 'company-message-avatar');
            const art = trigger.querySelector('.company-pet-art');
            if (art) avatar.appendChild(art.cloneNode(true));
            head.append(avatar, document.createTextNode('Company AI')); node.appendChild(head);
            const body = make('div', 'company-message-text'); markdown(body, item.text); node.appendChild(body);
            for (const img of item.generatedImages || []) {
                if (!/^data:image\/(png|jpeg|webp);base64,[a-z0-9+/=]+$/i.test(img.dataUrl || '')) continue;
                const image = make('img', 'company-image'); image.src = img.dataUrl; image.alt = img.title || 'Ảnh được tạo theo yêu cầu'; image.addEventListener('load', () => scrollEnd()); node.appendChild(image);
                const a = make('a', 'company-image-download', 'Tải ảnh'); a.href = img.dataUrl; a.download = 'company-ai-image.png'; node.appendChild(a);
            }
            if (item.agentArtifacts?.length) {
                const artifacts = make('div', 'company-artifact-list');
                for (const artifact of item.agentArtifacts) {
                    if (typeof artifact !== 'string') continue;
                    const filename = artifact.split(/[\\/]/).pop();
                    if (!filename || !/\.(docx|xlsx|pptx|pdf|png)$/i.test(filename) || !item.conversationId) continue;
                    const a = make('a', 'company-artifact-link', '↓ ' + filename);
                    a.href = '/api/ai/conversations/' + encodeURIComponent(item.conversationId) + '/artifacts/' + encodeURIComponent(filename);
                    a.download = filename; artifacts.appendChild(a);
                }
                node.appendChild(artifacts);
            }
            const footer = make('div', 'company-message-footer', item.time || ''); const b = button('Sao chép', () => copy(item.text, b)); footer.appendChild(b);
            if (item.canSpeak) { const speak = button('Đọc câu trả lời', () => speakText(item.text, speak)); footer.appendChild(speak); }
            node.appendChild(footer);
        } else node.textContent = item.text;
        messages.appendChild(node); return node;
    }
    function start(key) {
        if (state.busy || state.validating || state.recordingStarting || state.recorder?.state === 'recording') { showError('Hãy hoàn tất hoặc hủy thao tác hiện tại trước khi đổi công cụ.'); return; }
        const flow = flowForPrompt[key];
        if (flow && !flowAllowed(flow)) { openPanel('connections'); return; }
        setOpen(true);
        showPanel('chat');
        state.mode = key;
        el('company-flow-label').textContent = ({ image: 'Tạo hình ảnh', word: 'Tài liệu Word mẫu', excel: 'Bảng tính Excel mẫu', slides: 'Bài thuyết trình mẫu', coding: 'Phân tích mã nguồn', vision: 'Đọc ảnh & OCR', policy: 'Kiến thức nội bộ', hr: 'Tra cứu HRMS' }[key] || 'Tự động chọn luồng') + ' · Shift + Enter xuống dòng';
        input.value = prompts[key] || '';
        input.dispatchEvent(new Event('input'));
        input.focus();
        if (key === 'document' || key === 'data' || key === 'vision') {
            fileInput.click();
        }
    }
    root.querySelectorAll('[data-start]').forEach(b => b.addEventListener('click', () => start(b.dataset.start)));
    root.querySelectorAll('[data-mood-preview]').forEach(item => {
        item.addEventListener('click', () => {
            const mood = item.dataset.moodPreview;
            if (mood) {
                petController?.preview(mood);
                root.querySelectorAll('[data-mood-preview]').forEach(b => {
                    const match = b.dataset.moodPreview === mood;
                    b.setAttribute('aria-pressed', String(match));
                    b.classList.toggle('is-active', match);
                });
            }
        });
    });
    root.querySelectorAll('[data-mood-preview]').forEach(item => {
        item.addEventListener('click', () => {
            const mood = item.dataset.moodPreview;
            petController?.preview(mood);
        });
    });
    function newConversation() {
        if (state.busy || state.validating || state.recordingStarting || state.recorder?.state === 'recording') { showError('Hãy hoàn tất hoặc hủy thao tác hiện tại trước khi tạo hội thoại mới.'); return; }
        state.id = null; state.log = []; messages.replaceChildren(); input.value = ''; clearFiles(); clearError(); setStatus(''); state.mode = 'auto'; el('company-flow-label').textContent = 'Tự động chọn luồng · Shift + Enter xuống dòng'; setPet('idle'); state.follow = true; showPanel('chat'); saveSession(); input.focus(); updateComposer();
    }
    el('ava-new').addEventListener('click', newConversation);
    el('company-new-conversation').addEventListener('click', newConversation);
    const statusLabels = { ready: 'Sẵn sàng', unavailable: 'Chưa sẵn sàng', unknown: 'Chưa xác minh', forbidden: 'Chưa được cấp quyền' };
    function updateConnectionSummary() {
        const ready = state.capabilities?.service?.status === 'ready';
        el('company-connection-dot').dataset.state = ready ? 'ready' : 'offline';
        el('company-service-summary').textContent = state.capabilityError ? 'Chưa kết nối được AI service' : ready ? (state.capabilities.service.fullyOperational ? 'AI service đã kết nối' : 'Đã kết nối · một số khả năng cần kiểm tra') : 'AI service chưa sẵn sàng';
        if (!state.busy) el('company-status').textContent = state.capabilityError ? 'Chưa xác minh kết nối' : ready ? 'Đã kết nối AI service nội bộ' : 'Kiểm tra kết nối AI service';
        root.querySelectorAll('[data-flow]').forEach(b => { const allowed = flowAllowed(b.dataset.flow); b.disabled = !allowed; if (!allowed) b.title = 'Tài khoản chưa được cấp quyền sử dụng.'; });
        updateComposer();
    }
    async function refreshCapabilities() {
        if (state.capabilityLoading) return;
        state.capabilityLoading = true;
        el('company-connection-dot').dataset.state = 'loading';
        try { state.capabilities = await window.CompanyAIService.capabilities(); state.capabilityError = ''; }
        catch (e) { state.capabilityError = e.message; }
        finally { state.capabilityLoading = false; updateConnectionSummary(); if (state.panel === 'connections') connectionPanel(el('company-panel-body')); }
    }
    function connectionPanel(body) {
        body.replaceChildren();
        const toolbar = make('div', 'company-inline-actions'); toolbar.appendChild(button(state.capabilityLoading ? 'Đang kiểm tra…' : 'Kiểm tra lại kết nối', refreshCapabilities)); body.appendChild(toolbar);
        if (state.capabilityError) body.appendChild(make('p', 'company-response-warning', state.capabilityError));
        const caps = state.capabilities;
        if (!caps) { body.appendChild(make('p', 'company-panel-status', 'Chưa lấy được danh sách khả năng. Hãy kiểm tra AI service và thử lại.')); return; }
        body.appendChild(make('p', 'company-panel-status', 'Phạm vi dữ liệu: ' + (caps.user?.dataScope || 'Theo tài khoản') + '. Máy chủ kiểm tra quyền cho từng yêu cầu.'));
        const policy = caps.localOnlyPolicy || caps.service?.localOnlyPolicy;
        if (policy) body.appendChild(make('p', 'company-muted', policy.aiLocalOnly === true && policy.aiAllowPaidApis === false && policy.aiAllowCloudFallback === false ? 'Chế độ cục bộ · API trả phí và chuyển sang cloud đang tắt.' : 'Cấu hình dịch vụ không xác nhận đầy đủ chế độ chỉ chạy cục bộ.'));
        const grid = make('div', 'company-capability-grid');
        for (const flow of caps.flows || []) {
            const card = make('article', 'company-capability-card'); card.dataset.state = flow.status;
            card.append(make('strong', '', flow.title), make('span', 'company-status-tag', statusLabels[flow.status] || 'Chưa xác minh'));
            const starts = { chat: 'work', knowledge: 'policy', personal: 'people', hr: 'hr', documents: 'document', data: 'data', vision: 'vision', image: 'image', word: 'word', excel: 'excel', slides: 'slides', coding: 'coding' };
            if (flow.allowed && starts[flow.id]) card.appendChild(button('Sử dụng', () => start(starts[flow.id])));
            else if (flow.allowed && flow.id === 'training') card.appendChild(button('Bổ sung tri thức', trainingPanel));
            else if (flow.id === 'transcribe') card.appendChild(make('small', 'company-muted', 'Dùng nút micro trong ô chat; bản chép lời có thể sửa trước khi gửi.'));
            else if (flow.id === 'tts') card.appendChild(make('small', 'company-muted', 'Dùng nút Đọc câu trả lời trên phản hồi được hỗ trợ.'));
            grid.appendChild(card);
        }
        body.appendChild(grid);
    }
    function showPanel(name) {
        state.historyAbort?.abort(); state.panel = name; drawer.classList.remove('mobile-nav-open'); el('company-mobile-nav').setAttribute('aria-expanded', 'false');
        root.querySelectorAll('[data-panel]').forEach(b => { const active = b.dataset.panel === name; b.classList.toggle('is-active', active); b.classList.toggle('active', active); if (active) b.setAttribute('aria-current', 'page'); else b.removeAttribute('aria-current'); });
        const chat = name === 'chat'; panel.hidden = chat; messages.hidden = !chat; welcome.hidden = !chat || state.log.length > 0; el('company-composer-area').hidden = !chat; content.scrollTop = 0;
        if (chat) { if (state.log.length) scrollEnd(true); return; }
        latest.hidden = true;
        const body = el('company-panel-body'); body.replaceChildren();
        const headings = { knowledge: ['Kiến thức công ty', 'Hỏi về quy định, chính sách và quy trình. Câu trả lời phụ thuộc tài liệu đã được lập chỉ mục và quyền của bạn.'], documents: ['Tài liệu', 'Đính kèm tài liệu trong hội thoại để đọc và phân tích.'], tools: ['Công cụ cho công việc', 'Chọn một công cụ, mô tả yêu cầu và kiểm tra kết quả trước khi sử dụng.'], tasks: ['Tác vụ của tôi', 'Ghi lại ý tưởng trước khi bắt tay thực hiện.'], history: ['Lịch sử hội thoại', 'Các cuộc trò chuyện của tài khoản hiện tại, được tải từ hệ thống.'], connections: ['Kết nối & khả năng AI', 'Tình trạng dịch vụ và các luồng được tài khoản của bạn sử dụng.'], settings: ['Cài đặt', 'Điều chỉnh người bạn đồng hành theo cách bạn làm việc.'], help: ['Một chút hướng dẫn', 'Company AI luôn ở đây khi bạn cần.'] };
        el('company-panel-title').textContent = headings[name]?.[0] || ''; el('company-panel-description').textContent = headings[name]?.[1] || '';
        if (['knowledge', 'documents', 'tools'].includes(name)) {
            const cards = make('div', 'company-panel-cards'); const choices = name === 'knowledge' ? [['Hỏi quy định nội bộ', 'policy', 'Đặt câu hỏi về chính sách hoặc quy trình.'], ['Thông tin cá nhân', 'people', 'Tra cứu hồ sơ từ dữ liệu được cấp quyền.']] : name === 'documents' ? [['Chọn tài liệu để tóm tắt', 'document', 'PDF, DOCX, TXT · tối đa 20 MiB mỗi lượt.'], ['Phân tích bảng dữ liệu', 'data', 'XLSX, CSV hoặc JSON.']] : [['Lập kế hoạch', 'work', 'Chuẩn bị mục tiêu và các bước thực hiện.'], ['Soạn nội dung', 'write', 'Email, báo cáo và đề xuất.'], ['Tóm tắt cuộc họp', 'meeting', 'Dán biên bản để trích các việc cần làm.'], ['Dịch nhanh', 'translate', 'Dán nội dung và điều chỉnh ngôn ngữ đích.']];
            if (name === 'knowledge') choices.push(['Dữ liệu HRMS', 'hr', 'Tra cứu trong phạm vi tài khoản, có kiểm tra quyền.']);
            if (name === 'documents') choices.push(['Đọc ảnh & OCR', 'vision', 'Nhận diện hình ảnh, trích chữ trong ảnh.']);
            if (name === 'tools') choices.push(['Thiết kế hình ảnh', 'image', 'Tạo poster với công cụ cục bộ.'], ['Tạo Word mẫu', 'word', 'Tạo tệp DOCX, kiểm tra nội dung trước khi sử dụng.'], ['Tạo Excel mẫu', 'excel', 'Bảng tính minh họa; số liệu mẫu không phải dữ liệu nhân sự thật.'], ['Tạo slide mẫu', 'slides', 'Bài thuyết trình có thể tải về.'], ['Phân tích mã nguồn', 'coding', 'Kiểm kê dự án trong phạm vi sandbox.'], ['Kỹ năng hiện có', 'skills', 'Xem các kỹ năng của AI service.']);
            choices.forEach(([title, key, description]) => { const b = button('', () => start(key), 'company-panel-card'); b.disabled = !flowAllowed(flowForPrompt[key]); b.append(make('strong', '', title), make('small', '', description)); if (b.disabled) b.appendChild(make('small', 'company-muted', 'Chưa được cấp quyền')); cards.appendChild(b); }); body.appendChild(cards);
            if (name === 'documents') body.appendChild(make('p', 'company-panel-status', 'Chưa có API danh mục hoặc tìm tệp toàn công ty. Bạn có thể tải tệp vào chat hoặc hỏi về nguồn tri thức đã được kết nối.'));
            if ((name === 'tools' || name === 'knowledge') && flowAllowed('training')) body.appendChild(button('Bổ sung tri thức nội bộ', () => trainingPanel()));
        } else if (name === 'tasks') {
            const label = make('label', 'company-note-label', 'Ghi chú riêng · chỉ lưu trong trình duyệt này'); label.htmlFor = 'company-note'; const note = make('textarea', 'company-note'); note.id = 'company-note'; note.maxLength = 10000; note.placeholder = 'Ý tưởng, những việc cần nhớ…'; note.value = readStore(window.localStorage, 'note', '');
            const saved = make('p', 'company-note-status', 'Chưa đồng bộ với hệ thống quản lý công việc.'); note.addEventListener('input', () => { saved.textContent = writeStore(window.localStorage, 'note', note.value) ? 'Đã lưu riêng trên trình duyệt này.' : 'Không thể lưu trên trình duyệt. Hãy sao chép ghi chú trước khi rời trang.'; }); body.append(label, note, saved);
            const a = make('a', 'company-primary', 'Mở công việc trong HRMS ↗'); a.href = root.dataset.taskUrl || '/MyRequest'; body.appendChild(a);
        } else if (name === 'settings') {
            const settings = make('div', 'company-settings');
            [['visible', 'Hiển thị linh vật', 'Bạn vẫn có thể mở trợ lý khi ẩn linh vật.'], ['mini', 'Chế độ thu nhỏ', 'Một biểu tượng gọn gàng khi cần tập trung.'], ['reduceMotion', 'Giảm chuyển động', 'Hạn chế hoạt ảnh theo sở thích của bạn.']].forEach(([key, title, desc]) => { const row = make('label', 'company-setting'), text = make('span'); text.append(make('strong', '', title), make('small', '', desc)); const c = make('input'); c.type = 'checkbox'; c.checked = preferences[key]; c.addEventListener('change', () => { preferences[key] = c.checked; applyPreferences(); const r = dock.getBoundingClientRect(); positionPet(r.left, r.top); }); row.append(text, c); settings.appendChild(row); });
            const voiceRow = make('label', 'company-setting');
            const vText = make('span');
            vText.append(make('strong', '', 'Giọng đọc AI (TTS)'), make('small', '', 'Sử dụng giọng được cài trên máy. Ngôn ngữ chưa được cài sẽ không thể chọn.'));
            const vSelect = make('select', 'company-note');
            vSelect.style.minHeight = '38px';
            vSelect.style.maxWidth = '210px';
            const localVoices = state.capabilities?.service?.capabilities?.textToSpeech?.voices || [];
            const languages = new Set(localVoices.map(v => v.language));
            [
                ['auto', 'Tự động theo ngôn ngữ'],
                ['vi-VN-HoaiMyNeural', 'Tiếng Việt · giọng trên máy'],
                ['en-US-AriaNeural', 'English · local voice'],
                ['zh-CN-XiaoxiaoNeural', '中文 · local voice']
            ].forEach(([val, label]) => {
                const opt = new Option(label, val);
                const language = val.startsWith('vi-') ? 'vi-VN' : val.startsWith('en-') ? 'en-US' : 'zh-CN';
                opt.disabled = val !== 'auto' && !languages.has(language);
                if (preferences.voice === val && !opt.disabled) opt.selected = true;
                vSelect.add(opt);
            });
            vSelect.addEventListener('change', () => {
                preferences.voice = vSelect.value;
                writeStore(window.localStorage, 'preferences', preferences);
                stopSpeech();
            });
            voiceRow.append(vText, vSelect);
            settings.appendChild(voiceRow);
            const row = make('div', 'company-setting'); row.append(make('strong', '', 'Vị trí linh vật'), button('Về mặc định', resetPosition)); settings.appendChild(row);
            settings.appendChild(button('Xóa bản chat lưu trong tab này', () => { newConversation(); try { window.sessionStorage.removeItem(prefix + 'chat'); } catch (_) { } })); body.appendChild(settings);
            const expressions = make('section', 'company-pet-expression-settings');
            expressions.append(make('h3', '', 'Biểu cảm của linh vật'), make('p', 'company-muted', 'Biểu cảm tự đổi theo chat, micro và âm thanh. Nhấn để xem thử khi trợ lý đang rảnh.'));
            const stage = make('div', 'company-pet-preview-stage');
            const previewArt = trigger.querySelector('.company-pet-art'); if (previewArt) stage.appendChild(previewArt.cloneNode(true));
            expressions.appendChild(stage);
            const previews = make('div', 'company-pet-preview-grid');
            for (const [mood, label] of [['idle', 'Chớp mắt'], ['happy', 'Vui vẻ'], ['thinking', 'Suy nghĩ'], ['listening', 'Lắng nghe'], ['talking', 'Trò chuyện'], ['dragging', 'Di chuyển'], ['notification', 'Thông báo'], ['mini', 'Minimode'], ['sleeping', 'Nghỉ ngơi']]) {
                const b = button(label, () => petController?.preview(mood), 'company-pet-preview-button'); b.dataset.moodPreview = mood;
                previews.appendChild(b);
            }
            expressions.appendChild(previews); body.appendChild(expressions); petController?.refresh();
        } else if (name === 'history') loadHistory(body);
        else if (name === 'connections') { connectionPanel(body); if (!state.capabilities && !state.capabilityLoading) refreshCapabilities(); }
        else { const list = make('ul', 'company-help-list');['Nhấn linh vật để mở hoặc đóng chat.', 'Kéo linh vật bằng chuột hoặc cảm ứng đến bất kỳ vùng nào.', 'Dùng phím mũi tên khi focus linh vật để di chuyển.', 'Chuột phải hoặc nút ba chấm để mở menu.', 'Enter để gửi, Shift + Enter để xuống dòng.', 'Đính kèm tối đa 5 tệp, tổng dung lượng 20 MiB.', 'Hủy yêu cầu chỉ ngừng chờ phản hồi; tác vụ trên máy chủ có thể vẫn tiếp tục.'].forEach(t => list.appendChild(make('li', '', t))); body.appendChild(list); }
    }
    function openPanel(name) { setOpen(true); expand(true); showPanel(name); }
    root.querySelectorAll('[data-panel]').forEach(b => b.addEventListener('click', () => showPanel(b.dataset.panel)));
    async function loadHistory(body) {
        const controller = new AbortController(); state.historyAbort = controller;
        body.appendChild(make('p', 'company-panel-status', 'Đang tải hội thoại…'));
        try {
            const data = await window.CompanyAIService.conversations(controller.signal); if (controller.signal.aborted) return; body.replaceChildren();
            if (!data.length) body.appendChild(make('p', 'company-panel-status', 'Bạn chưa có hội thoại được lưu trên hệ thống.'));
            const list = make('div', 'company-history-list');
            data.forEach(item => {
                const b = button('', async () => {
                    if (state.busy || state.validating || state.recordingStarting || state.recorder?.state === 'recording') { showError('Hãy hoàn tất hoặc hủy thao tác hiện tại trước khi đổi hội thoại.'); return; }
                    b.disabled = true;
                    try { const detail = await window.CompanyAIService.conversation(item.id, controller.signal); if (controller.signal.aborted) return; state.id = item.id; state.log = detail.messages.map(m => ({ role: m.role, text: m.content, time: new Date(m.createdAt).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }), conversationId: item.id, route: m.route, warning: m.warning, verification: m.verification, sources: m.sources || [], canSpeak: m.role === 'assistant' && flowAllowed('tts'), generatedImages: m.attachmentContext?.generatedImages || [], agentArtifacts: m.attachmentContext?.agentArtifacts || [] })); messages.replaceChildren(); state.log.forEach(renderMessage); clearFiles(); clearError(); input.value = ''; showPanel('chat'); saveSession(); updateComposer(); }
                    catch (e) { if (e.name !== 'AbortError') { b.disabled = false; b.appendChild(make('small', '', e.message)); } }
                }, 'company-history-item'); b.append(document.createTextNode(item.title || 'Cuộc trò chuyện'), make('small', '', new Date(item.updatedAt).toLocaleString('vi-VN'))); list.appendChild(b);
            }); body.appendChild(list);
        } catch (e) { if (e.name === 'AbortError') return; body.replaceChildren(make('p', 'company-panel-status', e.message), button('Tải lại', () => loadHistory(body))); }
    }
    function trainingPanel() {
        if (!flowAllowed('training')) { openPanel('connections'); return; }
        openPanel('tools'); const body = el('company-panel-body'); body.replaceChildren(); el('company-panel-title').textContent = 'Bổ sung tri thức';
        const title = make('input', 'company-note'); title.style.minHeight = '44px'; title.maxLength = 160; title.placeholder = 'Tên chủ đề'; title.setAttribute('aria-label', 'Tên chủ đề');
        const text = make('textarea', 'company-note'); text.maxLength = 3500; text.placeholder = 'Nội dung quy trình hoặc kiến thức cần bổ sung'; text.setAttribute('aria-label', 'Nội dung tri thức');
        const feedback = make('p', 'company-note-status', 'Hệ thống kiểm tra quyền ai.skill.manage trước khi lưu. Đây là bổ sung kho tri thức, không huấn luyện trọng số mô hình.');
        const category = make('select', 'company-note'); category.style.minHeight = '44px'; category.setAttribute('aria-label', 'Nhóm tri thức');['Quy trình', 'Chính sách', 'Hướng dẫn công việc'].forEach(x => category.add(new Option(x, x)));
        const b = button('Lưu tri thức', async () => { if (state.trainingBusy) return; if (!title.value.trim() || text.value.trim().length < 5) { feedback.textContent = 'Nhập tên chủ đề và nội dung dài ít nhất 5 ký tự.'; return; } state.trainingBusy = true; updateComposer(); b.disabled = true; title.disabled = text.disabled = category.disabled = true; try { await window.CompanyAIService.trainSkill({ title: title.value, content: text.value, category: category.value }); feedback.textContent = 'Máy chủ đã xác nhận lưu tri thức.'; } catch (e) { feedback.textContent = e.message; } finally { state.trainingBusy = false; updateComposer(); b.disabled = false; title.disabled = text.disabled = category.disabled = false; } }, 'company-primary'); body.append(title, category, text, feedback, b);
    }

    function request(url, body, timeout = 240000, progress) {
        return new Promise((resolve, reject) => {
            const xhr = new XMLHttpRequest(); if (url === '/api/ai/chat') state.xhr = xhr; xhr.open('POST', url, true); xhr.timeout = timeout;
            xhr.setRequestHeader('Content-Type', 'application/json'); xhr.setRequestHeader('RequestVerificationToken', form.querySelector('input[name="__RequestVerificationToken"]')?.value || '');
            if (progress) xhr.upload.onprogress = e => { if (e.lengthComputable) progress(Math.round(e.loaded / e.total * 100)); };
            const done = () => { if (state.xhr === xhr) state.xhr = null; };
            xhr.onload = () => { done(); let data; try { data = JSON.parse(xhr.responseText); } catch (_) { reject(new Error('Máy chủ trả về phản hồi không hợp lệ. Hãy kiểm tra phiên đăng nhập.')); return; } if (xhr.status >= 200 && xhr.status < 300) resolve(data); else { const e = new Error(data.error || data.message || data.detail || 'Yêu cầu chưa thể được xử lý.'); e.status = xhr.status; reject(e); } };
            xhr.onerror = () => { done(); reject(new Error('Không thể kết nối đến máy chủ. Vui lòng thử lại.')); };
            xhr.ontimeout = () => { done(); reject(new Error('Yêu cầu đã hết thời gian. Bạn có thể thử lại.')); };
            xhr.onabort = () => { done(); reject(new Error('Đã hủy chờ phản hồi. Tác vụ trên máy chủ có thể vẫn tiếp tục.')); };
            xhr.send(JSON.stringify(body));
        });
    }
    async function toBase64(file) { const bytes = new Uint8Array(await file.arrayBuffer()); let binary = ''; for (let i = 0; i < bytes.length; i += 32768)binary += String.fromCharCode(...bytes.subarray(i, i + 32768)); return btoa(binary); }
    function clearFiles() { state.files.forEach(f => { if (f.preview) URL.revokeObjectURL(f.preview); }); state.files = []; renderFiles(); }
    function renderFiles() { const box = el('ava-attachments'); box.replaceChildren(); state.files.forEach((item, i) => { const chip = make('div', 'company-attachment'); const duration = Number.isFinite(item.duration) ? ' · ' + Math.floor(item.duration / 60) + ':' + String(Math.ceil(item.duration) % 60).padStart(2, '0') : ''; chip.appendChild(make('span', '', item.file.name + ' · ' + Math.ceil(item.file.size / 1024) + ' KB' + duration)); chip.appendChild(button('×', () => { state.files.splice(i, 1); renderFiles(); })); box.appendChild(chip); }); updateComposer(); }
    function audioDuration(file) { return new Promise(resolve => { const audio = document.createElement('audio'), url = URL.createObjectURL(file); let finished = false; const finish = v => { if (finished) return; finished = true; clearTimeout(timer); audio.pause?.(); audio.removeAttribute('src'); URL.revokeObjectURL(url); resolve(v); }; const timer = setTimeout(() => finish(null), 8000); audio.onloadedmetadata = () => finish(Number.isFinite(audio.duration) ? audio.duration : null); audio.onerror = () => finish(null); audio.src = url; audio.load?.(); }); }
    async function chooseFiles(files) {
        if (state.busy || state.validating) return;
        state.validating = true; updateComposer(); clearError();
        try { let total = state.files.reduce((n, x) => n + x.file.size, 0); for (const file of files) { const type = types[file.name.split('.').pop().toLowerCase()], browser = (file.type || '').split(';')[0].toLowerCase(); if (!type || (browser && !([type, ...(mimeAliases[type] || [])].includes(browser)))) { setStatus('Định dạng tệp không được hỗ trợ hoặc không khớp nội dung khai báo.'); continue; } if (state.files.length >= 5 || total + file.size > 20 * 1024 * 1024) { setStatus('Tối đa 5 tệp, tổng dung lượng không quá 20 MiB.'); continue; } let duration = null; if (type.startsWith('audio/')) { duration = await audioDuration(file); if (!duration || duration > 120) { setStatus('Âm thanh phải đọc được và không vượt giới hạn 2:00.'); continue; } } state.files.push({ file, type, duration }); total += file.size; } } finally { state.validating = false; renderFiles(); }
    }
    el('ava-attach').addEventListener('click', () => fileInput.click());
    el('ava-attach-alt').addEventListener('click', () => { if (!state.busy && !state.validating) fileInput.click(); });
    el('ava-web-toggle').addEventListener('click', () => openPanel('knowledge'));
    fileInput.addEventListener('change', async () => { const files = Array.from(fileInput.files || []); fileInput.value = ''; await chooseFiles(files); });
    form.addEventListener('dragover', e => { e.preventDefault(); form.classList.add('is-dragover'); });
    form.addEventListener('dragleave', () => form.classList.remove('is-dragover'));
    form.addEventListener('drop', async e => { e.preventDefault(); form.classList.remove('is-dragover'); await chooseFiles(Array.from(e.dataTransfer?.files || [])); });
    input.addEventListener('paste', async e => { const files = Array.from(e.clipboardData?.files || []); if (files.length) { e.preventDefault(); await chooseFiles(files); } });
    async function captureScreen() {
        if (state.busy || state.validating) return;
        if (!navigator.mediaDevices?.getDisplayMedia) {
            setOpen(true);
            showPanel('chat');
            showError('Trình duyệt hoặc môi trường chưa hỗ trợ API chụp màn hình trực tiếp. Bạn hãy nhấn nút đính kèm (+) trong khung chat để tải ảnh chụp màn hình lên.');
            return;
        }
        let stream;
        try {
            stream = await navigator.mediaDevices.getDisplayMedia({ video: true, audio: false });
            const video = document.createElement('video');
            video.srcObject = stream;
            video.muted = true;
            await video.play();
            const scale = Math.min(1, 1920 / (video.videoWidth || 1920));
            const canvas = document.createElement('canvas');
            canvas.width = Math.round((video.videoWidth || 1280) * scale);
            canvas.height = Math.round((video.videoHeight || 720) * scale);
            canvas.getContext('2d').drawImage(video, 0, 0, canvas.width, canvas.height);
            const blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/png'));
            if (!blob) throw new Error('Không tạo được ảnh chụp.');
            setOpen(true);
            showPanel('chat');
            await chooseFiles([new File([blob], 'anh-man-hinh.png', { type: 'image/png' })]);
            input.value = 'Phân tích ảnh chụp màn hình đính kèm và giúp tôi với ';
            input.dispatchEvent(new Event('input'));
            input.focus();
            setStatus('Ảnh đã đính kèm. Kiểm tra thông tin trong ảnh trước khi gửi.');
        } catch (e) {
            if (e.name !== 'NotAllowedError') {
                setOpen(true);
                showPanel('chat');
                showError(e.message || 'Không thể chụp màn hình. Hãy đính kèm ảnh thủ công bằng nút dấu cộng.');
            }
        } finally {
            stream?.getTracks().forEach(t => t.stop());
        }
    }
    async function sendText(text, retry) {
        if (state.busy || state.validating || state.recordingStarting || state.recorder?.state === 'recording') return; if (!flowAllowed('chat')) { showError('Tài khoản chưa được cấp quyền chat AI.'); return; } const draft = retry || { text: (text || '').trim(), files: state.files.slice() }; if (!draft.text && !draft.files.length) return;
        setOpen(true); showPanel('chat'); welcome.hidden = true; clearError(); setStatus(''); state.busy = true; state.follow = true; updateComposer(); setPet('thinking'); el('company-status').textContent = 'Đang xử lý yêu cầu…'; el('ava-cancel-request').hidden = false;
        if (!retry) { const item = { role: 'user', text: draft.text + (draft.files.length ? '\n' + draft.files.map(x => '▧ ' + x.file.name).join('\n') : ''), time: new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }) }; state.log.push(item); renderMessage(item); state.files = []; renderFiles(); input.value = ''; resizeComposer(); saveSession(); }
        const loading = make('div', 'company-loading', 'Company AI đang suy nghĩ '); for (let i = 0; i < 3; i++)loading.appendChild(make('span', '', '•')); messages.appendChild(loading); scrollEnd(true);
        try {
            const attachments = await Promise.all(draft.files.map(async x => ({ fileName: x.file.name, contentType: x.type, contentBase64: await toBase64(x.file) })));
            const result = await request('/api/ai/chat', { conversationId: state.id, text: draft.text, attachments }, 240000, p => { if (attachments.length) setStatus('Đang tải tệp… ' + p + '%'); });
            if (!result.answer) throw new Error('Máy chủ chưa trả về nội dung câu trả lời.'); state.id = result.conversationId || state.id;
            const item = { role: 'assistant', text: result.answer, time: new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }), conversationId: state.id, canSpeak: result.canSpeak === true && flowAllowed('tts'), route: result.route, warning: result.warning, verification: result.verification, sources: result.sources?.length ? result.sources : result.citations || [], generatedImages: result.attachmentContext?.generatedImages || [], agentArtifacts: result.attachmentContext?.agentArtifacts || [] }; state.log.push(item); loading.remove(); renderMessage(item); saveSession(); setStatus(''); setPet('happy', 'Câu trả lời đã sẵn sàng.', 4000);
        } catch (e) { loading.remove(); if (e.status === 413 || e.status === 422) { setStatus(e.message); } else showError(e.message, () => sendText(draft.text, draft)); }
        finally { state.busy = false; el('ava-cancel-request').hidden = true; updateConnectionSummary(); scrollEnd(); if (root.dataset.petState === 'thinking') setPet('idle'); wake(); }
    }
    form.addEventListener('submit', e => { e.preventDefault(); sendText(input.value); }); input.addEventListener('input', () => { updateComposer(); }); input.addEventListener('keydown', e => { if (e.key === 'Enter' && !e.shiftKey && !e.isComposing) { e.preventDefault(); sendText(input.value); } });
    el('ava-cancel-request').addEventListener('click', () => state.xhr?.abort());
    function stopSpeech() {
        if (state.audio) { state.audio.onended = null; state.audio.onerror = null; state.audio.pause(); }
        window.speechSynthesis?.cancel();
        state.audio = null; state.utterance = null; state.awaitingPlayback = false;
        if (state.audioUrl) { URL.revokeObjectURL(state.audioUrl); state.audioUrl = null; }
        if (state.speechButton) state.speechButton.textContent = 'Đọc câu trả lời';
        state.speechButton = null; setPet('idle');
    }
    function getActiveVoice() {
        const stored = readStore(window.localStorage, 'preferences', null);
        if (stored && typeof stored.voice === 'string') {
            preferences.voice = stored.voice;
        }
        return preferences.voice || 'auto';
    }
    async function speakText(text, b) {
        if (state.speechLoading) return;
        if (state.awaitingPlayback && state.speechButton === b) {
            try { await state.audio.play(); state.awaitingPlayback = false; b.textContent = 'Dừng đọc'; setPet('talking'); }
            catch (_) { showError('Trình duyệt chặn phát âm thanh. Hãy cho phép âm thanh cho trang này.'); }
            return;
        }
        if (state.audio || state.utterance) {
            const sameButton = state.speechButton === b; stopSpeech(); if (sameButton) return;
        }
        clearError(); state.speechButton = b;
        state.speechLoading = true; updateComposer(); b.disabled = true; b.textContent = 'Đang tạo giọng đọc…';
        const cleanText = text.slice(0, 2500);
        const configuredVoice = getActiveVoice();
        const chosenVoice = configuredVoice && configuredVoice !== 'auto' ? configuredVoice : undefined;
        const controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 80000);
        const finish = () => { if (state.speechButton === b) stopSpeech(); };
        try {
            const result = await window.CompanyAIService.tts({ text: cleanText, voice: chosenVoice }, controller.signal);
            if (!result.audioBase64) throw new Error('Chưa nhận được âm thanh.');
            const bytes = Uint8Array.from(atob(result.audioBase64), c => c.charCodeAt(0));
            const url = URL.createObjectURL(new Blob([bytes], { type: result.mimeType || 'audio/mpeg' }));
            state.audioUrl = url;
            const audio = new Audio(url); state.audio = audio;
            const release = () => URL.revokeObjectURL(url);
            audio.onended = () => { release(); finish(); };
            audio.onerror = () => { release(); finish(); showError('Không giải mã được âm thanh. Vui lòng thử đọc lại.'); };
            try { await audio.play(); b.textContent = 'Dừng đọc'; setPet('talking'); }
            catch (e) {
                if (e.name === 'NotAllowedError') {
                    state.awaitingPlayback = true; b.textContent = 'Bấm để phát âm thanh';
                    setPet('idle');
                } else { release(); throw e; }
            }
        } catch (e) {
            finish();
            if (e.status === 401) {
                showError('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
            } else if (e.status === 403) {
                showError('Tài khoản chưa được cấp quyền sử dụng tính năng giọng nói.');
            } else {
                // Fallback directly to browser speech synthesis
                const ok = fallbackWebSpeech(cleanText, finish, b);
                if (!ok) {
                    showError(e.name === 'AbortError' ? 'Tạo giọng đọc quá thời gian. Vui lòng thử lại.' : (e.message || 'Không thể tổng hợp giọng đọc.'));
                }
            }
        } finally { clearTimeout(timeout); state.speechLoading = false; updateComposer(); b.disabled = false; }
    }
    function fallbackWebSpeech(text, finish, b) {
        if (!window.speechSynthesis || typeof window.SpeechSynthesisUtterance !== 'function') return false;
        try {
            window.speechSynthesis.cancel();
            const userVoice = getActiveVoice();
            let targetLang = 'vi-VN';
            if (userVoice === 'auto') {
                targetLang = /[\u4e00-\u9fff]/.test(text) ? 'zh-CN' : (/[àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđĐ]/i.test(text) ? 'vi-VN' : 'en-US');
            } else {
                targetLang = userVoice.slice(0, 5);
            }

            const utterance = new SpeechSynthesisUtterance(text);
            utterance.lang = targetLang;

            const voices = window.speechSynthesis.getVoices() || [];
            if (voices.length) {
                const langPrefix = targetLang.toLowerCase().slice(0, 2);
                const candidates = voices.filter(v => v.lang.toLowerCase().replace('_', '-').startsWith(langPrefix));
                if (candidates.length) {
                    const voiceKey = userVoice.toLowerCase();
                    const isMale = voiceKey.includes('nam') || voiceKey.includes('guy') || voiceKey.includes('yunxi');
                    const isFemale = voiceKey.includes('my') || voiceKey.includes('aria') || voiceKey.includes('xiao');
                    
                    let matched = null;
                    if (isMale) {
                        matched = candidates.find(v => /nam|male|guy|yunxi|david|george/i.test(v.name));
                    } else if (isFemale) {
                        matched = candidates.find(v => /my|female|aria|zira|jenny|xiaoxiao/i.test(v.name));
                    }
                    if (!matched) {
                        const voiceName = userVoice.replace(/Neural$/, '').slice(6).toLowerCase();
                        matched = candidates.find(v => voiceName && v.name.toLowerCase().replace(/\s/g, '').includes(voiceName));
                    }
                    utterance.voice = matched || candidates[0];
                }
            }

            utterance.rate = 1.0;
            state.utterance = utterance;
            state.speechButton = b;
            utterance.onstart = () => { setPet('talking'); b.textContent = 'Dừng đọc'; };
            utterance.onend = finish;
            utterance.onerror = event => {
                finish();
                if (event.error !== 'canceled' && event.error !== 'interrupted') {
                    showError('Giọng đọc trình duyệt gặp sự cố (' + event.error + ').');
                }
            };
            window.speechSynthesis.speak(utterance);
            return true;
        } catch (_) {
            return false;
        }
    }
    let recordingTimer;
    function stopTracks() { state.stream?.getTracks().forEach(t => t.stop()); state.stream = null; clearTimeout(recordingTimer); el('ava-mic').hidden = false; el('ava-stop-recording').hidden = true; el('ava-cancel-recording').hidden = true; }
    function cancelRecording() {
        state.recordingGeneration++; state.cancelledRecording = true; state.recordingStarting = false;
        if (state.recorder?.state === 'recording') state.recorder.stop();
        stopTracks(); setStatus(''); updateComposer();
    }
    async function record() {
        if (state.busy || state.validating || state.recordingStarting || state.recorder?.state === 'recording') return;
        clearError();
        if (!flowAllowed('transcribe')) { showError('Tài khoản chưa được cấp quyền nhận dạng giọng nói.'); return; }
        if (!navigator.mediaDevices?.getUserMedia || !window.MediaRecorder) { showError('Trình duyệt chưa hỗ trợ ghi âm. Hãy dùng HTTPS hoặc localhost và kiểm tra quyền micro.'); return; }
        const generation = ++state.recordingGeneration;
        state.recordingStarting = true; updateComposer();
        try {
            const audioConstraints = {
                channelCount: 1,
                sampleRate: 16000,
                echoCancellation: true,
                noiseSuppression: true,
                autoGainControl: true
            };
            let stream;
            try {
                stream = await navigator.mediaDevices.getUserMedia({ audio: audioConstraints });
            } catch (_) {
                stream = await navigator.mediaDevices.getUserMedia({ audio: true });
            }
            if (generation !== state.recordingGeneration) { stream.getTracks().forEach(t => t.stop()); return; }
            state.stream = stream;
            const type = ['audio/webm;codecs=opus', 'audio/ogg;codecs=opus', 'audio/mp4'].find(t => MediaRecorder.isTypeSupported(t));
            const recorder = new MediaRecorder(stream, type ? { mimeType: type } : undefined), chunks = [];
            state.recorder = recorder; state.cancelledRecording = false;
            recorder.ondataavailable = e => { if (e.data.size) chunks.push(e.data); };
            recorder.onstop = async () => {
                stopTracks();
                if (state.cancelledRecording || generation !== state.recordingGeneration) { updateComposer(); return; }
                state.validating = true; updateComposer(); setStatus('Đang chuyển lời nói thành văn bản…');
                try {
                    const blob = new Blob(chunks, { type: recorder.mimeType }), signature = new Uint8Array(await blob.slice(0, 4).arrayBuffer());
                    const ogg = signature[0] === 79 && signature[1] === 103;
                    const mime = ogg ? 'audio/ogg' : recorder.mimeType.startsWith('audio/mp4') ? 'audio/mp4' : 'audio/webm';
                    const file = new File([blob], 'cau-hoi.' + (ogg ? 'ogg' : mime === 'audio/mp4' ? 'm4a' : 'webm'), { type: mime });
                    const result = await request('/api/ai/transcribe', { fileName: file.name, mimeType: mime, contentType: mime, contentBase64: await toBase64(file) }, 180000);
                    if (generation === state.recordingGeneration) { input.value = result.transcript || ''; input.dispatchEvent(new Event('input')); input.focus(); setStatus('Bạn có thể sửa nội dung trước khi gửi.'); }
                } catch (e) { showError(e.message); } finally { state.validating = false; updateComposer(); }
            };
            recorder.start(); el('ava-mic').hidden = true; el('ava-stop-recording').hidden = false; el('ava-cancel-recording').hidden = false;
            setStatus('Đang ghi âm · tối đa 2 phút'); recordingTimer = setTimeout(() => { if (recorder.state === 'recording') recorder.stop(); }, 120000);
        } catch (e) { stopTracks(); showError(e.name === 'NotAllowedError' ? 'Micro bị trình duyệt chặn. Hãy cho phép micro trong cài đặt trang rồi thử lại.' : 'Không thể mở micro. Kiểm tra thiết bị và thử lại.', () => record()); }
        finally { if (generation === state.recordingGeneration) state.recordingStarting = false; updateComposer(); }
    }
    el('ava-mic').addEventListener('click', record); el('ava-stop-recording').addEventListener('click', () => { if (state.recorder?.state === 'recording') state.recorder.stop(); }); el('ava-cancel-recording').addEventListener('click', cancelRecording);
    window.addEventListener('pagehide', () => { cancelRecording(); stopSpeech(); state.xhr?.abort(); state.historyAbort?.abort(); });
    // Discard legacy role-keyed chat caches; never restore one user's chat for another user.
    try { for (let i = window.sessionStorage.length - 1; i >= 0; i--) { const key = window.sessionStorage.key(i); if (key?.startsWith('ava-chat-session:')) window.sessionStorage.removeItem(key); } } catch (_) { }
    const saved = readStore(window.sessionStorage, 'chat', null);
    if (saved && Array.isArray(saved.log)) { state.id = typeof saved.id === 'string' ? saved.id : null; state.log = saved.log.filter(x => ['user', 'assistant'].includes(x.role) && typeof x.text === 'string').slice(-25); state.log.forEach(renderMessage); if (saved.expanded) expand(true); }
    applyPreferences(); const pos = readStore(window.localStorage, 'position', null); const v = viewport(); positionPet(pos?.x ?? v.width - 126, pos?.y ?? v.height - 300); showPanel('chat'); updateComposer(); wake();
    if (root.dataset.workspace === 'true') { setOpen(true); expand(true); }
    refreshCapabilities();
    window.AVA = Object.freeze({ send: sendText, open: () => setOpen(true), close: () => setOpen(false), newConversation, openPanel });
}());
