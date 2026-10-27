/**
 * HRMS Notification Sound Manager
 * Sử dụng Web Audio API để phát âm thanh thông báo
 * Không cần file .mp3 bên ngoài → không bao giờ lỗi 404
 *
 * localStorage key: "hrms_sound_enabled" ("true" | "false")
 * sessionStorage key: "hrms_played_ids" (JSON array of notification IDs)
 */

window.HRMS_Sound = (function () {

    var audioCtx = null;

    // ============================================================
    // KHỞI TẠO WEB AUDIO CONTEXT (lazy)
    // ============================================================
    function getAudioContext() {
        try {
            if (!audioCtx) {
                var AudioContext = window.AudioContext || window.webkitAudioContext;
                if (AudioContext) {
                    audioCtx = new AudioContext();
                }
            }
            return audioCtx;
        } catch (e) {
            return null;
        }
    }

    // ============================================================
    // PHÁT ÂM THANH "DING" NHẸ NHÀNG
    // Thiết kế: 2 nốt nhạc ngắn (notification bell effect)
    // ============================================================
    function playDing() {
        var ctx = getAudioContext();
        if (!ctx) return;

        try {
            // Resume context nếu bị suspended (browser autoplay policy)
            if (ctx.state === 'suspended') {
                ctx.resume().then(function () {
                    _createDingSound(ctx);
                }).catch(function () { /* ignore */ });
            } else {
                _createDingSound(ctx);
            }
        } catch (e) {
            /* Không crash nếu Web Audio API bị chặn */
        }
    }

    function _createDingSound(ctx) {
        try {
            var now = ctx.currentTime;

            // === NỐT 1: E5 (659 Hz) ===
            var osc1 = ctx.createOscillator();
            var gain1 = ctx.createGain();
            osc1.type = 'sine';
            osc1.frequency.setValueAtTime(659, now);
            osc1.frequency.exponentialRampToValueAtTime(500, now + 0.3);
            gain1.gain.setValueAtTime(0, now);
            gain1.gain.linearRampToValueAtTime(0.35, now + 0.01);
            gain1.gain.exponentialRampToValueAtTime(0.001, now + 0.5);
            osc1.connect(gain1);
            gain1.connect(ctx.destination);
            osc1.start(now);
            osc1.stop(now + 0.5);

            // === NỐT 2: B5 (988 Hz) - nhẹ hơn, phát sau 0.12s ===
            var osc2 = ctx.createOscillator();
            var gain2 = ctx.createGain();
            osc2.type = 'sine';
            osc2.frequency.setValueAtTime(988, now + 0.12);
            osc2.frequency.exponentialRampToValueAtTime(750, now + 0.45);
            gain2.gain.setValueAtTime(0, now + 0.12);
            gain2.gain.linearRampToValueAtTime(0.25, now + 0.13);
            gain2.gain.exponentialRampToValueAtTime(0.001, now + 0.6);
            osc2.connect(gain2);
            gain2.connect(ctx.destination);
            osc2.start(now + 0.12);
            osc2.stop(now + 0.6);

        } catch (e) {
            /* Không crash */
        }
    }

    // ============================================================
    // THEO DÕI NOTIFICATION ĐÃ PHÁT (DÙNG sessionStorage)
    // Để không phát lại sau khi refresh trang
    // ============================================================
    function getPlayedIds() {
        try {
            var raw = sessionStorage.getItem('hrms_played_ids');
            return raw ? JSON.parse(raw) : [];
        } catch (e) {
            return [];
        }
    }

    function markAsPlayed(notifId) {
        try {
            var ids = getPlayedIds();
            if (!ids.includes(notifId)) {
                ids.push(notifId);
                // Giữ tối đa 200 IDs để không tốn bộ nhớ
                if (ids.length > 200) ids = ids.slice(-200);
                sessionStorage.setItem('hrms_played_ids', JSON.stringify(ids));
            }
        } catch (e) { /* ignore */ }
    }

    function hasBeenPlayed(notifId) {
        try {
            return getPlayedIds().includes(notifId);
        } catch (e) {
            return false;
        }
    }

    // ============================================================
    // HÀM CHÍNH: PHÁT ÂM THANH CHO NOTIFICATION MỚI
    // Gọi từ SignalR handler khi nhận notification
    // notifId: ID định danh notification (để track đã phát chưa)
    // ============================================================
    function playForNotification(notifId) {
        // Nếu đã phát âm thanh này rồi → bỏ qua
        if (notifId && hasBeenPlayed(notifId)) return;

        // Phát âm thanh
        playDing();

        // Đánh dấu đã phát
        if (notifId) markAsPlayed(notifId);
    }

    // PUBLIC API
    // ============================================================
    return {
        play: playDing,
        playForNotification: playForNotification,
        hasBeenPlayed: hasBeenPlayed,
        markAsPlayed: markAsPlayed
    };

})();
