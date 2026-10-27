(function () {
    'use strict';

    const root = document.getElementById('ava-assistant');
    if (!root || root.dataset.avaInitialized === 'true') return;
    root.dataset.avaInitialized = 'true';

    const el = (id) => document.getElementById(id);
    const trigger = el('ava-trigger'), drawer = el('ava-drawer'), close = el('ava-close');
    const fresh = el('ava-new'), form = el('ava-composer'), input = el('ava-input');
    const send = el('ava-send'), messages = el('ava-messages'), error = el('ava-error');
    const fileInput = el('ava-file-input'), attachButton = el('ava-attach');
    const attachmentList = el('ava-attachments'), uploadStatus = el('ava-upload-status');
    const cancelRequestButton = el('ava-cancel-request');
    const micButton = el('ava-mic'), stopRecordingButton = el('ava-stop-recording'), cancelRecordingButton = el('ava-cancel-recording');
    let conversationId = null, lastFailedDraft = null, inFlight = false, activeOperation = null;
    let selectedFiles = [], activeXhr = null, recorder = null, mediaStream = null, recordingChunks = [], recordingTimer = null, fileValidationCount = 0;
    const MAX_BYTES = 20 * 1024 * 1024, MAX_FILES = 5, MAX_AUDIO_SECONDS = 120;
    const types = {
        '.png':'image/png','.jpg':'image/jpeg','.jpeg':'image/jpeg','.webp':'image/webp','.bmp':'image/bmp',
        '.pdf':'application/pdf','.docx':'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
        '.xlsx':'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet','.txt':'text/plain','.csv':'text/csv','.json':'application/json',
        '.wav':'audio/wav','.mp3':'audio/mpeg','.m4a':'audio/mp4','.ogg':'audio/ogg','.webm':'audio/webm'
    };
    const mimeAliases = {
        'image/jpeg':['image/jpg'], 'application/json':['text/json'], 'text/csv':['application/vnd.ms-excel'],
        'audio/wav':['audio/x-wav','audio/wave','audio/vnd.wave'], 'audio/mpeg':['audio/mp3'],
        'audio/mp4':['audio/x-m4a'], 'audio/ogg':['application/ogg'], 'audio/webm':['video/webm']
    };

    function setOpen(open) {
        drawer.classList.toggle('open', open); drawer.setAttribute('aria-hidden', String(!open));
        trigger.setAttribute('aria-expanded', String(open)); if (open) setTimeout(() => input.focus(), 120);
    }

    const actorKey = encodeURIComponent(String(root.dataset.avaActor || 'authenticated').trim().toLowerCase());
    const positionStorageKey = 'ava-trigger-position:v1:' + actorKey;
    let dragState = null, suppressTriggerClick = false;

    function viewportSize() {
        return {
            width: Math.max(document.documentElement?.clientWidth || 0, window.innerWidth || 0),
            height: Math.max(document.documentElement?.clientHeight || 0, window.innerHeight || 0)
        };
    }
    function clamp(value, minimum, maximum) {
        return Math.min(Math.max(value, minimum), Math.max(minimum, maximum));
    }
    function updateDrawerAnchor(left, top, width, height) {
        const viewport = viewportSize();
        root.classList.toggle('ava-anchor-left', left + width / 2 <= viewport.width / 2);
        root.classList.toggle('ava-anchor-top', top + height / 2 <= viewport.height / 2);
    }
    function applyTriggerPosition(left, top) {
        const viewport = viewportSize();
        const bounds = trigger.getBoundingClientRect();
        const width = bounds.width || trigger.offsetWidth || 58;
        const height = bounds.height || trigger.offsetHeight || 58;
        const margin = 8;
        const safeLeft = clamp(left, margin, viewport.width - width - margin);
        const safeTop = clamp(top, margin, viewport.height - height - margin);
        root.classList.add('ava-positioned');
        root.style.left = safeLeft + 'px';
        root.style.top = safeTop + 'px';
        root.style.right = 'auto';
        root.style.bottom = 'auto';
        updateDrawerAnchor(safeLeft, safeTop, width, height);
        return { left: safeLeft, top: safeTop, width, height, viewport };
    }
    function saveTriggerPosition() {
        const bounds = trigger.getBoundingClientRect();
        const viewport = viewportSize();
        const horizontalRange = Math.max(1, viewport.width - bounds.width - 16);
        const verticalRange = Math.max(1, viewport.height - bounds.height - 16);
        const value = {
            x: clamp((bounds.left - 8) / horizontalRange, 0, 1),
            y: clamp((bounds.top - 8) / verticalRange, 0, 1)
        };
        try { window.localStorage?.setItem(positionStorageKey, JSON.stringify(value)); } catch (_) { /* Storage may be disabled. */ }
    }
    function restoreTriggerPosition() {
        let saved = null;
        try { saved = JSON.parse(window.localStorage?.getItem(positionStorageKey) || 'null'); } catch (_) { /* Ignore invalid or unavailable storage. */ }
        if (!saved || !Number.isFinite(saved.x) || !Number.isFinite(saved.y)) return;
        const viewport = viewportSize();
        const bounds = trigger.getBoundingClientRect();
        const width = bounds.width || trigger.offsetWidth || 58;
        const height = bounds.height || trigger.offsetHeight || 58;
        applyTriggerPosition(8 + clamp(saved.x, 0, 1) * Math.max(1, viewport.width - width - 16), 8 + clamp(saved.y, 0, 1) * Math.max(1, viewport.height - height - 16));
    }
    function beginTriggerDrag(event) {
        if (event.button !== undefined && event.button !== 0) return;
        const bounds = trigger.getBoundingClientRect();
        dragState = { pointerId: event.pointerId, startX: event.clientX, startY: event.clientY, left: bounds.left, top: bounds.top, moved: false };
        trigger.setPointerCapture?.(event.pointerId);
    }
    function moveTrigger(event) {
        if (!dragState || (dragState.pointerId !== undefined && event.pointerId !== dragState.pointerId)) return;
        const deltaX = event.clientX - dragState.startX;
        const deltaY = event.clientY - dragState.startY;
        if (!dragState.moved && Math.hypot(deltaX, deltaY) < 5) return;
        dragState.moved = true;
        suppressTriggerClick = true;
        root.classList.add('ava-dragging');
        applyTriggerPosition(dragState.left + deltaX, dragState.top + deltaY);
        event.preventDefault?.();
    }
    function endTriggerDrag(event) {
        if (!dragState || (dragState.pointerId !== undefined && event.pointerId !== dragState.pointerId)) return;
        const moved = dragState.moved;
        trigger.releasePointerCapture?.(event.pointerId);
        dragState = null;
        root.classList.remove('ava-dragging');
        if (moved) saveTriggerPosition();
    }

    trigger.addEventListener('pointerdown', beginTriggerDrag);
    trigger.addEventListener('pointermove', moveTrigger);
    trigger.addEventListener('pointerup', endTriggerDrag);
    trigger.addEventListener('pointercancel', endTriggerDrag);
    window.addEventListener('resize', () => {
        if (!root.classList.contains('ava-positioned')) return;
        const bounds = trigger.getBoundingClientRect();
        applyTriggerPosition(bounds.left, bounds.top);
        saveTriggerPosition();
    });
    restoreTriggerPosition();

    // AVA_IMAGE_LIGHTBOX
    let avaImageLightbox = null;
    let avaPreviousBodyOverflow = '';

    function closeImagePreview() {
        if (!avaImageLightbox) return;

        avaImageLightbox.hidden = true;
        avaImageLightbox.style.display = 'none';

        const previewImage =
            avaImageLightbox.querySelector('img');

        if (previewImage) {
            previewImage.removeAttribute('src');
        }

        document.body.style.overflow =
            avaPreviousBodyOverflow;
    }

    function openImagePreview(sourceImage) {
        if (!sourceImage || !sourceImage.src) return;

        if (!avaImageLightbox) {
            const overlay =
                document.createElement('div');

            overlay.id = 'ava-image-lightbox';
            overlay.hidden = true;

            overlay.setAttribute(
                'role',
                'dialog'
            );

            overlay.setAttribute(
                'aria-modal',
                'true'
            );

            overlay.setAttribute(
                'aria-label',
                'Xem ảnh đính kèm'
            );

            Object.assign(
                overlay.style,
                {
                    position: 'fixed',
                    inset: '0',
                    zIndex: '2147483647',
                    background:
                        'rgba(0, 0, 0, 0.88)',
                    display: 'none',
                    alignItems: 'center',
                    justifyContent: 'center',
                    padding: '24px',
                    boxSizing: 'border-box',
                    cursor: 'zoom-out'
                }
            );

            const image =
                document.createElement('img');

            image.alt = '';

            Object.assign(
                image.style,
                {
                    display: 'block',
                    maxWidth: '95vw',
                    maxHeight: '92vh',
                    width: 'auto',
                    height: 'auto',
                    objectFit: 'contain',
                    borderRadius: '10px',
                    boxShadow:
                        '0 20px 70px rgba(0,0,0,.55)',
                    cursor: 'default'
                }
            );

            image.addEventListener(
                'click',
                event => {
                    event.stopPropagation();
                }
            );

            const closeButton =
                document.createElement('button');

            closeButton.type = 'button';
            closeButton.textContent = '×';

            closeButton.setAttribute(
                'aria-label',
                'Đóng ảnh'
            );

            Object.assign(
                closeButton.style,
                {
                    position: 'fixed',
                    top: '16px',
                    right: '20px',
                    zIndex: '2147483647',
                    width: '44px',
                    height: '44px',
                    border: '0',
                    borderRadius: '50%',
                    background:
                        'rgba(255,255,255,.15)',
                    color: '#fff',
                    fontSize: '32px',
                    lineHeight: '40px',
                    cursor: 'pointer'
                }
            );

            closeButton.addEventListener(
                'click',
                event => {
                    event.stopPropagation();
                    closeImagePreview();
                }
            );

            overlay.addEventListener(
                'click',
                closeImagePreview
            );

            overlay.appendChild(image);
            overlay.appendChild(closeButton);

            document.body.appendChild(
                overlay
            );

            avaImageLightbox = overlay;

            document.addEventListener(
                'keydown',
                event => {
                    if (
                        event.key === 'Escape' &&
                        avaImageLightbox &&
                        !avaImageLightbox.hidden
                    ) {
                        closeImagePreview();
                    }
                }
            );
        }

        const previewImage =
            avaImageLightbox.querySelector('img');

        previewImage.src =
            sourceImage.currentSrc ||
            sourceImage.src;

        previewImage.alt =
            sourceImage.alt ||
            'Ảnh đính kèm';

        avaPreviousBodyOverflow =
            document.body.style.overflow;

        document.body.style.overflow =
            'hidden';

        avaImageLightbox.hidden = false;
        avaImageLightbox.style.display =
            'flex';
    }

    function enableImagePreview(root) {
        if (!root) return;

        root.addEventListener(
            'click',
            event => {
                const image =
                    event.target.closest?.('img');

                if (
                    !image ||
                    !root.contains(image)
                ) {
                    return;
                }

                event.preventDefault();
                event.stopPropagation();

                openImagePreview(image);
            }
        );

        root.addEventListener(
            'mouseover',
            event => {
                const image =
                    event.target.closest?.('img');

                if (
                    image &&
                    root.contains(image)
                ) {
                    image.style.cursor =
                        'zoom-in';

                    image.title =
                        'Bấm để phóng to ảnh';
                }
            }
        );
    }

    enableImagePreview(
        attachmentList
    );

    enableImagePreview(
        messages
    );

    function addMessage(role, text, extraClass) {
        const node = document.createElement('div'); node.className = 'ava-message ' + role + (extraClass ? ' ' + extraClass : '');
        node.textContent = text; messages.appendChild(node); messages.scrollTop = messages.scrollHeight; return node;
    }
    function addSpeechControl(node, text, canSpeak) {
        if (!canSpeak || !node || !text || !window.speechSynthesis || typeof window.SpeechSynthesisUtterance !== 'function') return;
        const button = document.createElement('button');
        button.type = 'button'; button.className = 'ava-speak'; button.title = 'Đọc câu trả lời';
        button.setAttribute('aria-label', 'Đọc câu trả lời của AVA');
        button.innerHTML = '<i class="fa-solid fa-volume-high" aria-hidden="true"></i>';
        button.addEventListener('click', () => {
            if (button.classList.contains('speaking')) {
                window.speechSynthesis.cancel();
                button.classList.remove('speaking');
                button.setAttribute('aria-label', 'Đọc câu trả lời của AVA');
                return;
            }
            window.speechSynthesis.cancel();
            const utterance = new window.SpeechSynthesisUtterance(text);
            utterance.lang = 'vi-VN'; utterance.rate = 1;
            const reset = () => { button.classList.remove('speaking'); button.setAttribute('aria-label', 'Đọc câu trả lời của AVA'); };
            utterance.onstart = () => { button.classList.add('speaking'); button.setAttribute('aria-label', 'Dừng đọc câu trả lời'); };
            utterance.onend = reset; utterance.onerror = reset;
            window.speechSynthesis.speak(utterance);
        });
        node.appendChild(button);
    }
    function addUserMessage(text, files) {
        const node = document.createElement('div'); node.className = 'ava-message user';
        if (text) { const body = document.createElement('div'); body.textContent = text; node.appendChild(body); }
        (files || []).forEach(item => {
            const card = document.createElement('div'); card.className = 'ava-attachment ava-attachment-sent';
            if (item.contentType.startsWith('image/') && item.previewUrl) {
                const image = document.createElement('img'); image.src = item.previewUrl; image.alt = item.file.name; card.appendChild(image);
            }
            const name = document.createElement('span'); name.className = 'ava-attachment-name'; name.textContent = item.file.name; card.appendChild(name);
            node.appendChild(card);
        });
        messages.appendChild(node); messages.scrollTop = messages.scrollHeight; return node;
    }
    function clearError() { error.hidden = true; error.textContent = ''; error.classList.remove('ava-error-with-action'); }
    function showError(text, retryDraft) {
        error.classList.remove('ava-error-with-action');
        error.textContent = retryDraft ? text + ' Nhấn để thử lại.' : text;
        error.hidden = false;
        lastFailedDraft = retryDraft || null;
    }
    function showActionError(text, actionLabel, action) {
        error.replaceChildren();
        error.classList.add('ava-error-with-action');
        const message = document.createElement('span');
        message.textContent = text;
        error.appendChild(message);
        if (actionLabel && action) {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'ava-error-action';
            button.textContent = actionLabel;
            button.addEventListener('click', event => {
                event.stopPropagation?.();
                action();
            });
            error.appendChild(button);
        }
        error.hidden = false;
        lastFailedDraft = null;
    }
    function updateComposerState() {
        send.disabled = inFlight || fileValidationCount > 0 || (!input.value.trim() && !selectedFiles.some(item => item.state === 'READY'));
    }
    function setStatus(text, percent, kind) {
        uploadStatus.hidden = !text;
        uploadStatus.classList.toggle('ava-validation-notice', kind === 'validation');
        uploadStatus.replaceChildren();
        if (!text) return;
        uploadStatus.textContent = text;
        if (percent !== undefined) {
            const bar = document.createElement('div'), fill = document.createElement('span');
            bar.className = 'ava-upload-progress'; fill.style.width = Math.max(0, Math.min(100, percent)) + '%';
            bar.appendChild(fill); uploadStatus.appendChild(bar);
        }
    }
    function showValidationNotice(text) {
        clearError();
        lastFailedDraft = null;
        setStatus(text, undefined, 'validation');
    }
    function clearSelectedFiles() {
        selectedFiles.forEach(item => { if (item.previewUrl) URL.revokeObjectURL(item.previewUrl); });
        selectedFiles = []; renderAttachments();
    }
    function renderAttachments() {
        attachmentList.replaceChildren();
        selectedFiles.forEach((item, index) => {
            const chip = document.createElement('div'); chip.className = 'ava-attachment';
            if (item.contentType.startsWith('image/') && item.previewUrl) {
                const image = document.createElement('img'); image.src = item.previewUrl; image.alt = 'Xem trước ' + item.file.name; chip.appendChild(image);
            }
            const name = document.createElement('span'); name.className = 'ava-attachment-name';
            const duration = Number.isFinite(item.durationSeconds) ? ' · ' + formatDuration(item.durationSeconds) : '';
            name.textContent = item.file.name + ' · ' + Math.ceil(item.file.size / 1024) + ' KB' + duration + ' · ' + item.state; chip.appendChild(name);
            const remove = document.createElement('button'); remove.type = 'button'; remove.className = 'ava-attachment-remove';
            remove.setAttribute('aria-label', 'Xóa ' + item.file.name); remove.textContent = '×';
            remove.addEventListener('click', () => { if (item.previewUrl) URL.revokeObjectURL(item.previewUrl); selectedFiles.splice(index, 1); renderAttachments(); });
            chip.appendChild(remove); attachmentList.appendChild(chip);
        });
    
        // AVA_MULTIMODAL_SEND_STATE
        updateComposerState();
}
    function formatDuration(seconds) {
        const rounded = Math.max(0, Math.ceil(seconds));
        const minutes = Math.floor(rounded / 60);
        return minutes + ':' + String(rounded % 60).padStart(2, '0');
    }
    function readAudioDuration(file) {
        return new Promise(resolve => {
            const audio = document.createElement('audio');
            const objectUrl = URL.createObjectURL(file);
            let finished = false;
            const finish = value => {
                if (finished) return;
                finished = true;
                clearTimeout(timeout);
                audio.onloadedmetadata = audio.ondurationchange = audio.onerror = null;
                audio.pause?.();
                audio.removeAttribute?.('src');
                URL.revokeObjectURL(objectUrl);
                resolve(Number.isFinite(value) && value > 0 ? value : null);
            };
            const timeout = setTimeout(() => finish(null), 8000);
            const inspect = () => {
                if (Number.isFinite(audio.duration) && audio.duration > 0) finish(audio.duration);
            };
            audio.preload = 'metadata';
            audio.onloadedmetadata = inspect;
            audio.ondurationchange = inspect;
            audio.onerror = () => finish(null);
            audio.src = objectUrl;
            audio.load?.();
        });
    }
    async function chooseFiles(files) {
        const incoming = Array.from(files || []);
        if (selectedFiles.length + incoming.length > MAX_FILES) { showValidationNotice('Không thể tải tệp: AVA hỗ trợ tối đa 5 tệp mỗi lượt.'); return; }
        let total = selectedFiles.reduce((sum, item) => sum + item.file.size, 0);
        const notices = [];
        for (const file of incoming) {
            const extension = '.' + (file.name.split('.').pop() || '').toLowerCase();
            const contentType = types[extension];
            const browserType = (file.type || '').split(';', 1)[0].toLowerCase();
            const acceptedTypes = [contentType, ...(mimeAliases[contentType] || [])];
            if (!contentType || (browserType && !acceptedTypes.includes(browserType))) {
                notices.push(`Không thể tải “${file.name}”: định dạng tệp không được hỗ trợ hoặc không đúng loại.`);
                continue;
            }
            if (file.size > MAX_BYTES || total + file.size > MAX_BYTES) {
                notices.push(`Không thể tải “${file.name}”: tổng dung lượng tệp mỗi lượt không được vượt quá 20 MiB.`);
                continue;
            }
            let durationSeconds = null;
            if (contentType.startsWith('audio/')) {
                setStatus(`Đang kiểm tra thời lượng “${file.name}”…`);
                durationSeconds = await readAudioDuration(file);
                if (durationSeconds === null) {
                    notices.push(`Không thể tải “${file.name}”: trình duyệt không đọc được thời lượng âm thanh. Hãy chọn tệp MP3, WAV, M4A, OGG hoặc WebM hợp lệ.`);
                    continue;
                }
                if (durationSeconds > MAX_AUDIO_SECONDS) {
                    notices.push(`Không thể tải “${file.name}”: âm thanh dài ${formatDuration(durationSeconds)}, vượt giới hạn 2:00.`);
                    continue;
                }
            }
            total += file.size;
            selectedFiles.push({ file, contentType, durationSeconds, previewUrl: contentType.startsWith('image/') ? URL.createObjectURL(file) : null, state: 'READY' });
        }
        renderAttachments();
        if (notices.length) showValidationNotice(notices.join(' '));
        else { clearError(); setStatus(''); }
    }
    function toBase64(file) {
        return file.arrayBuffer().then(buffer => {
            const bytes = new Uint8Array(buffer); let binary = '';
            for (let offset = 0; offset < bytes.length; offset += 0x8000) binary += String.fromCharCode(...bytes.subarray(offset, Math.min(offset + 0x8000, bytes.length)));
            return btoa(binary);
        });
    }
    function requestJson(url, body, onProgress, timeoutMs) {
        return new Promise((resolve, reject) => {
            const xhr = new XMLHttpRequest(); activeXhr = xhr; cancelRequestButton.hidden = false; xhr.open('POST', url, true); xhr.timeout = timeoutMs || 180000;
            xhr.withCredentials = true;
            xhr.setRequestHeader('Content-Type', 'application/json');
            xhr.setRequestHeader('RequestVerificationToken', form.querySelector('input[name="__RequestVerificationToken"]')?.value || '');
            xhr.setRequestHeader('X-Requested-With', 'XMLHttpRequest');
            xhr.upload.onprogress = event => { if (event.lengthComputable && onProgress) onProgress(Math.round(event.loaded * 100 / event.total)); };
            xhr.onload = () => { activeXhr = null; cancelRequestButton.hidden = true; let payload = {}; try { payload = JSON.parse(xhr.responseText || '{}'); } catch (_) { /* bounded generic error below */ }
                if (xhr.status >= 200 && xhr.status < 300) resolve(payload); else {
                    const requestError = new Error(payload.error || payload.detail || 'AVA hiện chưa sẵn sàng. Vui lòng thử lại.');
                    requestError.code = payload.code || '';
                    reject(requestError);
                } };
            xhr.onerror = () => { activeXhr = null; cancelRequestButton.hidden = true; reject(new Error('Không thể kết nối đến AVA. Vui lòng thử lại.')); };
            xhr.ontimeout = () => { activeXhr = null; cancelRequestButton.hidden = true; reject(new Error('Yêu cầu đã hết thời gian. Tệp vẫn được giữ để bạn thử lại.')); };
            xhr.onabort = () => { activeXhr = null; cancelRequestButton.hidden = true; reject(new Error('Yêu cầu đã được hủy.')); };
            xhr.send(JSON.stringify(body));
        });
    }
    function resetMessages() {
        if (window.speechSynthesis) window.speechSynthesis.cancel();
        messages.replaceChildren(); const greeting = document.createElement('div'); greeting.className = 'ava-empty-state';
        greeting.textContent = 'Chào bạn! Tôi là AVA. Bạn cần hỗ trợ gì về HRMS hôm nay?'; messages.appendChild(greeting); clearError(); setStatus('');
    }
    async function sendText(text, retryDraft) {
        const value = String(text || '').trim();
        if ((!value && selectedFiles.length === 0 && !retryDraft) || inFlight) return;
        const draft = retryDraft || { text: value, files: selectedFiles.slice(), userNode: null };
        if (!draft.userNode) draft.userNode = addUserMessage(draft.text, draft.files);
        draft.userNode.classList.remove('failed');
        inFlight = true; activeOperation = { cancelled: false }; clearError(); cancelRequestButton.hidden = false; updateComposerState();
        if (!retryDraft) { input.value = ''; input.style.height = 'auto'; }
        const loading = addMessage('assistant', 'Đang chuẩn bị tệp…', 'loading');
        send.disabled = input.disabled = attachButton.disabled = micButton.disabled = true;
        try {
            const attachments = [];
            for (let i = 0; i < draft.files.length; i++) {
                draft.files[i].state = 'ENCODING'; loading.textContent = `Đang đọc tệp ${i + 1}/${draft.files.length}…`; renderAttachments();
                const contentBase64 = await toBase64(draft.files[i].file);
                if (activeOperation.cancelled) throw new Error('Đã hủy chuẩn bị tệp.');
                draft.files[i].state = 'READY';
                attachments.push({ fileName: draft.files[i].file.name, contentType: draft.files[i].contentType, contentBase64 });
            }
            draft.files.forEach(item => { item.state = 'SENDING'; });
            if (!retryDraft) { selectedFiles = []; renderAttachments(); }
            loading.textContent = 'AVA đang xử lý…'; setStatus(attachments.length ? 'Đang tải tệp lên…' : 'AVA đang xử lý…', 0);
            const payload = await requestJson('/api/ai/chat', { conversationId, text: draft.text, attachments }, percent => setStatus('Đang tải tệp lên… ' + percent + '%', percent), 240000);
            loading.remove();
            if (typeof payload.answer !== 'string' || !payload.answer.trim()) throw new Error('AVA trả về dữ liệu không hợp lệ.');
            draft.files.forEach(item => { item.state = 'SENT'; });
            conversationId = payload.conversationId || conversationId;
            const answerNode = addMessage('assistant', payload.answer);
            addSpeechControl(answerNode, payload.answer, payload.canSpeak === true);
            lastFailedDraft = null; setStatus('');
        } catch (err) {
            draft.files.forEach(item => { item.state = activeOperation?.cancelled ? 'CANCELLED' : 'FAILED'; });
            loading.remove();
            if (['AUDIO_DURATION_LIMIT_EXCEEDED', 'ATTACHMENT_TOO_LARGE', 'TOO_MANY_ATTACHMENTS'].includes(err.code)) {
                draft.userNode.classList.remove('failed');
                showValidationNotice(err.message || 'Tệp không đáp ứng giới hạn tải lên của AVA.');
            } else {
                draft.userNode.classList.add('failed'); showError(err.message || 'AVA hiện chưa sẵn sàng. Vui lòng thử lại.', draft); setStatus('');
            }
        } finally {
            inFlight = false; activeOperation = null; cancelRequestButton.hidden = true; input.disabled = false; attachButton.disabled = false; micButton.disabled = false; updateComposerState(); input.focus();
        }
    }
    function stopMicrophoneTracks() { if (mediaStream) mediaStream.getTracks().forEach(track => track.stop()); mediaStream = null; }
    async function detectRecordedAudioContainer(blob) {
        const bytes = new Uint8Array(await blob.slice(0, 64).arrayBuffer());
        const ascii = (offset, value) => value.split('').every((char, index) => bytes[offset + index] === char.charCodeAt(0));
        if (bytes.length >= 12 && ascii(0, 'RIFF') && ascii(8, 'WAVE')) return { mimeType: 'audio/wav', extension: '.wav' };
        if (bytes.length >= 4 && bytes[0] === 0x1a && bytes[1] === 0x45 && bytes[2] === 0xdf && bytes[3] === 0xa3) return { mimeType: 'audio/webm', extension: '.webm' };
        if (bytes.length >= 4 && ascii(0, 'OggS')) return { mimeType: 'audio/ogg', extension: '.ogg' };
        if (bytes.length >= 8 && ascii(4, 'ftyp')) return { mimeType: 'audio/mp4', extension: '.m4a' };
        if ((bytes.length >= 3 && ascii(0, 'ID3')) || (bytes.length >= 2 && bytes[0] === 0xff && (bytes[1] & 0xe0) === 0xe0)) return { mimeType: 'audio/mpeg', extension: '.mp3' };
        return null;
    }
    async function getMicrophonePermissionState() {
        if (!navigator.permissions?.query) return 'unknown';
        try {
            const status = await navigator.permissions.query({ name: 'microphone' });
            return status?.state || 'unknown';
        } catch (_) {
            // Safari and some older browsers do not expose microphone through
            // the Permissions API; getUserMedia remains the source of truth.
            return 'unknown';
        }
    }
    async function showMicrophoneAccessError(cause) {
        const policy = document.permissionsPolicy || document.featurePolicy;
        if (policy?.allowsFeature && !policy.allowsFeature('microphone')) {
            showActionError('Microphone đang bị chính sách của trang chặn. Hãy mở HRMS trực tiếp, không qua iframe, rồi thử lại.', 'Thử lại', beginRecording);
            return;
        }
        if (window.isSecureContext === false) {
            showActionError('Trình duyệt chỉ cho dùng microphone trên HTTPS hoặc localhost. Hãy mở HRMS bằng địa chỉ localhost/127.0.0.1 rồi thử lại.', 'Thử lại', beginRecording);
            return;
        }

        const permission = await getMicrophonePermissionState();
        if (cause?.name === 'NotAllowedError' || cause?.name === 'SecurityError') {
            if (permission === 'denied') {
                showActionError('Microphone đang bị trình duyệt chặn. Nhấn biểu tượng ổ khóa/thông tin cạnh thanh địa chỉ → Microphone → Cho phép, tải lại trang rồi thử lại.', 'Kiểm tra lại', beginRecording);
            } else {
                showActionError('AVA cần quyền microphone. Nhấn “Thử cấp quyền” rồi chọn Cho phép trong hộp thoại của trình duyệt.', 'Thử cấp quyền', beginRecording);
            }
            return;
        }
        if (cause?.name === 'NotFoundError' || cause?.name === 'DevicesNotFoundError') {
            showError('Không tìm thấy microphone. Hãy kết nối hoặc bật thiết bị thu âm rồi thử lại.');
            return;
        }
        if (cause?.name === 'NotReadableError' || cause?.name === 'TrackStartError') {
            showActionError('Microphone đang được ứng dụng khác sử dụng hoặc bị Windows chặn. Hãy đóng ứng dụng ghi âm khác và bật quyền Microphone cho ứng dụng máy tính trong Windows Settings.', 'Thử lại', beginRecording);
            return;
        }
        if (cause?.name === 'OverconstrainedError') {
            showError('Microphone hiện tại không đáp ứng cấu hình ghi âm. Hãy chọn thiết bị đầu vào khác trong trình duyệt.');
            return;
        }
        showActionError('Không thể truy cập microphone. Hãy kiểm tra thiết bị và quyền riêng tư của trình duyệt/Windows.', 'Thử lại', beginRecording);
    }
    async function beginRecording() {
        clearError();
        if (!navigator.mediaDevices?.getUserMedia || !window.MediaRecorder) { showError('Trình duyệt không hỗ trợ ghi âm. Hãy dùng phiên bản Edge, Chrome, Firefox hoặc Safari mới.'); return; }
        try {
            mediaStream = await navigator.mediaDevices.getUserMedia({ audio: true }); recordingChunks = [];
            const preferredRecordingType = typeof MediaRecorder.isTypeSupported === 'function'
                ? ['audio/webm;codecs=opus', 'audio/mp4', 'audio/ogg;codecs=opus']
                    .find(type => MediaRecorder.isTypeSupported(type))
                : undefined;
            const options = preferredRecordingType ? { mimeType: preferredRecordingType } : undefined;
            recorder = new MediaRecorder(mediaStream, options); recorder.ondataavailable = event => { if (event.data.size) recordingChunks.push(event.data); };
            recorder.onstop = async () => {
                if (recordingTimer) clearTimeout(recordingTimer); recordingTimer = null;
                const blob = new Blob(recordingChunks, { type: recorder.mimeType || 'audio/webm' }); stopMicrophoneTracks();
                micButton.hidden = false; stopRecordingButton.hidden = true; cancelRecordingButton.hidden = true;
                if (blob.size > MAX_BYTES) { showError('Bản ghi vượt quá 20 MiB.'); return; }
                const detectedContainer = await detectRecordedAudioContainer(blob);
                if (!detectedContainer) { showError('Bản ghi không chứa dữ liệu âm thanh hợp lệ. Vui lòng ghi lại và nói ít nhất một giây.'); return; }
                const file = new File([blob], 'ava-recording' + detectedContainer.extension, { type: detectedContainer.mimeType });
                micButton.disabled = true; cancelRecordingButton.hidden = false;
                cancelRecordingButton.title = 'Hủy nhận dạng'; cancelRecordingButton.setAttribute('aria-label', 'Hủy nhận dạng');
                setStatus('Đang nhận dạng tiếng Việt bằng STT local…');
                try {
                    const contentBase64 = await toBase64(file);
                    const result = await requestJson('/api/ai/transcribe', { fileName: file.name, contentType: file.type, contentBase64 }, null, 180000);
                    input.value = result.transcript || '';
            input.dispatchEvent(new Event('input', { bubbles: true }));
            input.focus(); input.dispatchEvent(new Event('input', { bubbles: true })); input.focus();
                    if (!input.value) showError('Không nhận dạng được lời nói. Bạn có thể nhập câu hỏi bằng tay.');
                    else { clearError(); setStatus('Bản chép lời đã sẵn sàng — bạn có thể sửa rồi gửi.'); }
                } catch (err) { showError(err.message || 'STT local chưa sẵn sàng.'); setStatus(''); }
                finally { micButton.disabled = false; cancelRecordingButton.hidden = true; }
            };
            recorder.onerror = () => { stopMicrophoneTracks(); micButton.hidden = false; stopRecordingButton.hidden = true; cancelRecordingButton.hidden = true; showError('Ghi âm gặp lỗi.'); setStatus(''); };
            recorder.start(250); micButton.hidden = true; stopRecordingButton.hidden = false; cancelRecordingButton.hidden = false; setStatus('Đang ghi âm… tối đa 120 giây.');
            recordingTimer = setTimeout(() => { if (recorder?.state === 'recording') recorder.stop(); }, 120000);
        } catch (err) { stopMicrophoneTracks(); await showMicrophoneAccessError(err); }
    }
    function cancelRecording() {
        if (recordingTimer) clearTimeout(recordingTimer); recordingTimer = null;
        if (activeXhr && micButton.disabled) { activeXhr.abort(); return; }
        if (recorder?.state === 'recording') { recorder.onstop = null; recorder.stop(); }
        stopMicrophoneTracks(); micButton.hidden = false; stopRecordingButton.hidden = true; cancelRecordingButton.hidden = true; setStatus('Đã hủy ghi âm.');
    }

    trigger.addEventListener('click', event => {
        if (suppressTriggerClick) { suppressTriggerClick = false; event.preventDefault(); return; }
        setOpen(!drawer.classList.contains('open'));
    });
    close.addEventListener('click', () => setOpen(false));
    fresh.addEventListener('click', () => { conversationId = null; lastFailedDraft = null; clearSelectedFiles(); resetMessages(); input.focus(); updateComposerState(); });
    attachButton.addEventListener('click', () => fileInput.click());
    fileInput.addEventListener('change', async () => {
        const files = Array.from(fileInput.files || []);
        fileInput.value = '';
        fileValidationCount++;
        updateComposerState();
        try { await chooseFiles(files); }
        finally { fileValidationCount--; updateComposerState(); }
    });
    micButton.addEventListener('click', beginRecording);
    stopRecordingButton.addEventListener('click', () => { if (recorder?.state === 'recording') recorder.stop(); });
    cancelRecordingButton.addEventListener('click', cancelRecording);
    cancelRequestButton.addEventListener('click', () => { if (activeOperation) activeOperation.cancelled = true; if (activeXhr) activeXhr.abort(); });
    form.addEventListener('submit', event => { event.preventDefault(); sendText(input.value); });
    input.addEventListener('input', () => { input.style.height = 'auto'; input.style.height = Math.min(input.scrollHeight, 120) + 'px'; updateComposerState(); });
    input.addEventListener('keydown', event => { if (event.key === 'Enter' && !event.shiftKey) { event.preventDefault(); form.requestSubmit(); } });
    error.addEventListener('click', () => { if (lastFailedDraft && !inFlight) sendText(lastFailedDraft.text, lastFailedDraft); });
    window.addEventListener('pagehide', () => { if (activeXhr) activeXhr.abort(); cancelRecording(); clearSelectedFiles(); });

    window.AVA = window.AVA || {};
    window.AVA.open = () => setOpen(true); window.AVA.close = () => setOpen(false);
    window.AVA.toggle = () => setOpen(!drawer.classList.contains('open'));
    window.AVA.send = text => { setOpen(true); return sendText(text); };
    window.AVA.newConversation = () => { conversationId = null; lastFailedDraft = null; clearSelectedFiles(); resetMessages(); updateComposerState(); };
    window.AVA.loadConversation = id => { conversationId = id || null; lastFailedDraft = null; clearSelectedFiles(); setOpen(true); resetMessages(); updateComposerState(); };
    updateComposerState();
})();
