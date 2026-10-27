// Client state regression only; this does not certify real browser E2E.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import { test } from 'node:test';

const source = fs.readFileSync(new URL('../../giao diện demo/wwwroot/js/ava-assistant.js', import.meta.url), 'utf8');

function setup(options = {}) {
    const nodes = new Map(), requests = [], recorders = [], storage = new Map();
    class Element {
        constructor() {
            this.listeners = {}; this.children = []; this.dataset = {}; this.style = {};
            this.classList = { toggle() {}, add() {}, remove() {}, contains() { return false; } };
            this.hidden = false; this.disabled = false; this.value = ''; this.textContent = '';
        }
        addEventListener(name, fn) { this.listeners[name] = fn; }
        dispatchEvent(event) { return this.listeners[event.type]?.(event); }
        setAttribute(name, value) { this[name] = value; }
        removeAttribute(name) { delete this[name]; }
        appendChild(child) { child.parent = this; this.children.push(child); }
        replaceChildren() { this.children = []; }
        remove() { if (this.parent) this.parent.children = this.parent.children.filter(x => x !== this); }
        querySelector() { return { value: 'synthetic-token' }; }
        getBoundingClientRect() {
            const isTrigger = this.id === 'ava-trigger';
            return {
                left: Number.parseFloat(this.parent?.style?.left ?? this.style.left) || (isTrigger ? 918 : 0),
                top: Number.parseFloat(this.parent?.style?.top ?? this.style.top) || (isTrigger ? 718 : 0),
                width: isTrigger ? 58 : 0,
                height: isTrigger ? 58 : 0
            };
        }
        setPointerCapture() {}
        releasePointerCapture() {}
        focus() {}
    }
    const get = id => {
        if (!nodes.has(id)) { const node = new Element(); node.id = id; nodes.set(id, node); }
        return nodes.get(id);
    };
    class Xhr {
        constructor() { this.upload = {}; requests.push(this); }
        open(method, url) { this.url = url; }
        setRequestHeader() {}
        send(body) { this.body = JSON.parse(body); }
        complete(status, payload) { this.status = status; this.responseText = JSON.stringify(payload); this.onload(); }
        abort() { this.onabort(); }
    }
    class Recorder {
        static isTypeSupported() { return true; }
        constructor() { this.mimeType = 'audio/webm'; recorders.push(this); }
        start() { this.state = 'recording'; }
        stop() {
            this.state = 'inactive';
            this.ondataavailable?.({ data: new Blob([new Uint8Array([0x1a, 0x45, 0xdf, 0xa3, 0x01, 0x00])], { type: this.mimeType }) });
            this.onstop?.();
        }
    }
    class LocalFile extends Blob { constructor(parts, name, options) { super(parts, options); this.name = name; } }
    get('ava-trigger').parent = get('ava-assistant');
    get('ava-assistant').dataset.avaActor = options.actor || 'Employee';
    const window = {
        MediaRecorder: Recorder, isSecureContext: true, innerWidth: 1000, innerHeight: 800, addEventListener() {},
        localStorage: {
            getItem: key => storage.get(key) ?? null,
            setItem: (key, value) => storage.set(key, value)
        }
    };
    const navigator = {
        permissions: options.permissionState
            ? { query: async () => ({ state: options.permissionState }) }
            : undefined,
        mediaDevices: {
            getUserMedia: options.getUserMedia
                || (async () => ({ getTracks: () => [{ stop() {} }] }))
        }
    };
    vm.runInNewContext(source, {
        window, document: { getElementById: get, createElement: tag => {
            const node = new Element();
            if (tag === 'audio') {
                node.duration = options.audioDuration ?? 60;
                node.load = () => Promise.resolve().then(() => node.onloadedmetadata?.());
                node.pause = () => {};
            }
            return node;
        } },
        navigator,
        MediaRecorder: Recorder, XMLHttpRequest: Xhr, Blob, File: LocalFile,
        URL: { createObjectURL: () => 'blob:synthetic', revokeObjectURL() {} },
        Event: class { constructor(type) { this.type = type; } },
        Uint8Array, btoa: value => Buffer.from(value, 'binary').toString('base64'),
        setTimeout: () => 1, clearTimeout() {}
    });
    const tick = () => new Promise(resolve => setImmediate(resolve));
    const selectFile = () => {
        get('ava-file-input').files = [new LocalFile(['attachment test'], 'test.txt', { type: 'text/plain' })];
        return get('ava-file-input').dispatchEvent({ type: 'change' });
    };
    const selectAudio = (size = 1024) => {
        get('ava-file-input').files = [new LocalFile([new Uint8Array(size)], 'voice.mp3', { type: 'audio/mpeg' })];
        return get('ava-file-input').dispatchEvent({ type: 'change' });
    };
    return { get, window, requests, recorders, storage, tick, selectFile, selectAudio };
}

test('denied microphone permission shows recovery guidance without starting STT', async () => {
    const denied = new Error('Permission denied');
    denied.name = 'NotAllowedError';
    const s = setup({
        permissionState: 'denied',
        getUserMedia: async () => { throw denied; }
    });
    await s.get('ava-mic').dispatchEvent({ type: 'click' });
    assert.equal(s.get('ava-error').hidden, false);
    assert.match(s.get('ava-error').children[0].textContent, /bị trình duyệt chặn/);
    assert.equal(s.get('ava-error').children[1].textContent, 'Kiểm tra lại');
});

test('AI icon can be dragged inside the viewport and stores position per actor', () => {
    const s = setup({ actor: 'Employee' });
    const trigger = s.get('ava-trigger');
    trigger.dispatchEvent({ type: 'pointerdown', button: 0, pointerId: 7, clientX: 947, clientY: 747 });
    trigger.dispatchEvent({ type: 'pointermove', pointerId: 7, clientX: 400, clientY: 300, preventDefault() {} });
    trigger.dispatchEvent({ type: 'pointerup', pointerId: 7 });
    assert.equal(s.get('ava-assistant').style.left, '371px');
    assert.equal(s.get('ava-assistant').style.top, '271px');
    assert.equal([...s.storage.keys()][0], 'ava-trigger-position:v1:employee');
    trigger.dispatchEvent({ type: 'click', preventDefault() {} });
    assert.notEqual(trigger['aria-expanded'], 'true', 'Dragging must not accidentally open AVA');
});

test('successful chat clears sent attachments', async () => {
    const s = setup(); await s.selectFile();
    const pending = s.window.AVA.send('summarize');
    await s.tick();
    assert.equal(s.requests[0].body.attachments.length, 1);
    s.requests[0].complete(200, { answer: 'done', conversationId: 'synthetic' });
    await pending;
    assert.equal(s.get('ava-attachments').children.length, 0);
    assert.equal(s.get('ava-send').disabled, true, 'Empty composer must not send');
    s.get('ava-input').value = 'next question';
    s.get('ava-input').dispatchEvent({ type: 'input' });
    assert.equal(s.get('ava-send').disabled, false);
});

for (const failure of ['http', 'timeout', 'abort']) {
    test(failure + ' preserves the failed draft for retry and restores controls', async () => {
        const s = setup(); await s.selectFile();
        const pending = s.window.AVA.send('summarize');
        await s.tick();
        if (failure === 'http') s.requests[0].complete(503, { error: 'provider unavailable' });
        else if (failure === 'timeout') s.requests[0].ontimeout();
        else s.requests[0].abort();
        await pending;
        assert.equal(s.get('ava-attachments').children.length, 0, 'F2 moves sent files into the message draft');
        assert.equal(s.get('ava-error').hidden, false);
        for (const id of ['ava-input', 'ava-mic', 'ava-attach']) assert.equal(s.get(id).disabled, false);
        assert.equal(s.get('ava-messages').children.some(x => x.className?.includes('loading')), false);
        s.get('ava-error').dispatchEvent({ type: 'click' });
        await s.tick();
        assert.equal(s.requests[1].body.attachments.length, 1, 'Retry must reuse the attachment');
        assert.equal(s.requests[1].body.attachments[0].fileName, 'test.txt');
        s.requests[1].complete(200, { answer: 'retry succeeded' });
        await s.tick();
    });
}

test('audio longer than 120 seconds is rejected before it enters the prompt', async () => {
    const s = setup({ audioDuration: 121 });
    await s.selectAudio();
    assert.equal(s.get('ava-attachments').children.length, 0);
    assert.equal(s.get('ava-send').disabled, true);
    assert.equal(s.get('ava-error').hidden, true, 'Validation notice must not use the red system error');
    assert.match(s.get('ava-upload-status').textContent, /vượt giới hạn 2:00/);
    assert.equal(s.requests.length, 0);
});

test('audio within 120 seconds is accepted and sent to AVA', async () => {
    const s = setup({ audioDuration: 119 });
    await s.selectAudio();
    assert.equal(s.get('ava-attachments').children.length, 1);
    assert.match(s.get('ava-attachments').children[0].children[0].textContent, /1:59/);
    const pending = s.window.AVA.send('Hãy phân tích nội dung âm thanh');
    await s.tick();
    assert.equal(s.requests[0].body.attachments.length, 1);
    assert.equal(s.requests[0].body.attachments[0].contentType, 'audio/mpeg');
    s.requests[0].complete(200, { answer: 'Đã phân tích âm thanh.' });
    await pending;
});

test('oversized file is rejected before upload without a red system error', async () => {
    const s = setup();
    await s.selectAudio(20 * 1024 * 1024 + 1);
    assert.equal(s.get('ava-attachments').children.length, 0);
    assert.equal(s.get('ava-error').hidden, true);
    assert.match(s.get('ava-upload-status').textContent, /20 MiB/);
    assert.equal(s.requests.length, 0);
});

test('server-side upload limit fallback hides machine code and does not offer retry', async () => {
    const s = setup();
    await s.selectFile();
    const pending = s.window.AVA.send('Phân tích tệp');
    await s.tick();
    s.requests[0].complete(422, { error: 'Âm thanh phải dài không quá 120 giây.', code: 'AUDIO_DURATION_LIMIT_EXCEEDED' });
    await pending;
    assert.equal(s.get('ava-error').hidden, true);
    assert.match(s.get('ava-upload-status').textContent, /không quá 120 giây/);
    assert.doesNotMatch(s.get('ava-upload-status').textContent, /AUDIO_DURATION_LIMIT_EXCEEDED/);
});

test('Stop submits recording to STT and leaves transcript editable without auto-send', async () => {
    const s = setup();
    await s.get('ava-mic').dispatchEvent({ type: 'click' });
    await s.get('ava-stop-recording').dispatchEvent({ type: 'click' });
    await s.tick();
    assert.equal(s.requests.length, 1, 'Stop must send the recorded blob for transcription');
    assert.equal(s.requests[0].url, '/api/ai/transcribe');
    s.requests[0].complete(200, { transcript: 'editable question' });
    await s.tick();
    assert.equal(s.get('ava-input').value, 'editable question');
    assert.equal(s.get('ava-input').disabled, false);
    assert.equal(s.get('ava-send').disabled, false);
    assert.equal(s.requests.length, 1, 'STT must not auto-send chat');
});


