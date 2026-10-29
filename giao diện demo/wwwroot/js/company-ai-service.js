/* Same-origin adapter. Identity and permissions are always resolved by ASP.NET. */
(function () {
    'use strict';
    async function request(path, options) {
        const response = await fetch(path, { credentials: 'same-origin', ...options,
            headers: { Accept: 'application/json', ...options?.headers } });
        const payload = await response.json().catch(() => null);
        if (!response.ok || response.redirected) {
            const message = response.status === 401 || response.redirected
                ? 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.'
                : response.status === 403 ? 'Tài khoản của bạn chưa được cấp quyền cho chức năng này.'
                : payload?.error || (response.status === 404 ? 'Không tìm thấy hội thoại hoặc bạn không có quyền truy cập.'
                    : 'Chưa thể kết nối với AI service. Vui lòng thử lại.');
            const error = new Error(message);
            error.status = response.status;
            error.code = payload?.code || '';
            throw error;
        }
        if (payload === null) throw new Error('Máy chủ trả về dữ liệu không hợp lệ.');
        return payload;
    }
    const read = (path, signal) => request(path, { signal });
    const write = (path, payload, signal) => request(path, { method: 'POST', signal,
        headers: { 'Content-Type': 'application/json',
            RequestVerificationToken: document.querySelector('input[name="__RequestVerificationToken"]')?.value || '' },
        body: JSON.stringify(payload) });
    window.CompanyAIService = Object.freeze({
        capabilities: signal => read('/api/ai/capabilities', signal),
        conversations: signal => read('/api/ai/conversations', signal),
        conversation: (id, signal) => read('/api/ai/conversations/' + encodeURIComponent(id), signal),
        artifactUrl: (conversationId, artifactPath) => '/api/ai/conversations/' + encodeURIComponent(conversationId) + '/artifacts/' + encodeURIComponent(String(artifactPath).split(/[\\/]/).pop()),
        chat: (payload, signal) => write('/api/ai/chat', payload, signal),
        transcribe: (attachment, signal) => write('/api/ai/transcribe', attachment, signal),
        tts: (payload, signal) => write('/api/ai/tts', payload, signal),
        trainSkill: (payload, signal) => write('/api/ai/train-skill', payload, signal)
    });
}());
