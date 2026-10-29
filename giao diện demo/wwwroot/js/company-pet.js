/* Expressive local companion. Business activity always wins over decorative moods. */
(function () {
    'use strict';
    const MOODS = new Set(['idle','dragging','listening','thinking','talking','notification','sleeping','happy','mini']);
    const LABELS = {
        idle:'Mình sẵn sàng giúp bạn.', dragging:'Đi cùng bạn đến mọi nơi.',
        listening:'Mình đang lắng nghe…', thinking:'Mình đang xử lý yêu cầu…',
        talking:'Mình đang đọc câu trả lời.', notification:'Có thông tin cần bạn chú ý.',
        sleeping:'Mình nghỉ một chút, vẫn luôn ở đây.', happy:'Xong rồi! Mình cùng xem kết quả nhé.',
        mini:'Trợ lý nhỏ, luôn sẵn sàng.'
    };
    window.CompanyPetController = Object.freeze({create(options) {
        const {root,dock,trigger,bubble} = options;
        let preferences = options.preferences || {}, transient = null, sleeping = false;
        let sleepTimer = null, moodTimer = null, frame = null, lastPoint = null, disposed = false;
        let currentMood = '', currentText = '';
        const media = window.matchMedia?.('(prefers-reduced-motion: reduce)');
        const art = () => Array.from(root.querySelectorAll('.company-pet-art'));
        const reduced = () => !!preferences.reduceMotion || !!media?.matches;
        const paused = () => !!document.hidden || disposed;
        const activity = () => {
            const mood = options.getActivity?.() || 'idle';
            return MOODS.has(mood) ? mood : 'idle';
        };
        const copy = document.createElement('span'); copy.className = 'company-pet-bubble-copy';
        const hint = document.createElement('span'); hint.className = 'company-pet-bubble-hint';
        const close = document.createElement('button'); close.className = 'company-bubble-close';
        close.type = 'button'; close.textContent = '×'; close.setAttribute('aria-label','Ẩn lời nhắn của linh vật');
        close.addEventListener('click', e => {e.stopPropagation();bubble.classList.add('bubble-dismissed');});
        bubble.replaceChildren(copy,hint,close);

        function neutralGaze() {
            for (const svg of art()) {
                svg.style.setProperty('--pet-look-x','0px');
                svg.style.setProperty('--pet-look-y','0px');
                svg.style.setProperty('--pet-tilt','0deg');
            }
        }
        function cancelGaze() {
            if (frame !== null) window.cancelAnimationFrame?.(frame);
            frame = null; lastPoint = null; neutralGaze();
        }
        function render() {
            if (disposed) return;
            let mood = activity();
            if (mood === 'idle') mood = options.getError?.() ? 'notification' : transient?.mood ||
                (sleeping ? 'sleeping' : preferences.mini ? 'mini' : 'idle');
            const text = transient?.mood === mood && transient.text ? transient.text : LABELS[mood];
            root.dataset.petState = mood;
            root.classList.toggle('pet-paused',paused());
            root.querySelectorAll('[data-mood-preview]').forEach(button=>button.setAttribute('aria-pressed',String(button.dataset.moodPreview===mood)));
            if (mood !== currentMood || text !== currentText) {
                copy.textContent = text;
                hint.textContent = mood === 'idle' || mood === 'mini' ? 'Kéo mình đến vị trí bạn thích.' : 'Nhấn để mở cuộc trò chuyện.';
                trigger.setAttribute('aria-label','Company AI: '+text+' Nhấn để mở chat, kéo để di chuyển.');
                root.dataset.petLabel = text;
                bubble.classList.remove('bubble-dismissed');
                currentMood=mood;currentText=text;
            }
            if (reduced() || paused() || !['idle','happy','mini'].includes(mood)) cancelGaze();
        }
        function armSleep() {
            clearTimeout(sleepTimer);
            if (disposed || document.hidden) return;
            sleepTimer = setTimeout(() => {
                sleepTimer = null;
                if (activity() === 'idle' && !options.getError?.() && !transient) {sleeping=true;render();}
                else armSleep();
            },60000);
        }
        function wake() {
            if (disposed) return;
            sleeping=false;render();armSleep();
        }
        function playNotificationChime() {
            if (reduced() || paused()) return;
            try {
                const AudioCtx = window.AudioContext || window.webkitAudioContext;
                if (!AudioCtx) return;
                const ctx = new AudioCtx();
                const now = ctx.currentTime;
                // Chuông 2 âm ding-dong trong trẻo (E6 1318Hz -> B6 1975Hz)
                const playTone = (freq, start, duration) => {
                    const osc = ctx.createOscillator();
                    const gain = ctx.createGain();
                    osc.type = 'sine';
                    osc.frequency.setValueAtTime(freq, start);
                    gain.gain.setValueAtTime(0, start);
                    gain.gain.linearRampToValueAtTime(0.18, start + 0.02);
                    gain.gain.exponentialRampToValueAtTime(0.0001, start + duration);
                    osc.connect(gain);
                    gain.connect(ctx.destination);
                    osc.start(start);
                    osc.stop(start + duration);
                };
                playTone(1318.51, now, 0.45);        // E6
                playTone(1975.53, now + 0.12, 0.65); // B6
                setTimeout(() => { ctx.close?.(); }, 1200);
            } catch (_) {}
        }

        function present(mood,text,revertMs) {
            if (disposed || !MOODS.has(mood)) return;
            clearTimeout(moodTimer);moodTimer=null;
            if (mood === 'idle') transient=null;
            else if (['notification','happy'].includes(mood)) {
                transient={mood,text};sleeping=false;
                if (mood === 'happy') playNotificationChime();
                moodTimer=setTimeout(()=>{moodTimer=null;transient=null;render();},revertMs || 4000);
            }
            // Active expressions derive from the application, never a stale callback.
            render();armSleep();
        }
        function preview(mood) {
            if (!MOODS.has(mood) || disposed) return;
            clearTimeout(moodTimer);transient={mood,text:LABELS[mood]};sleeping=false;
            moodTimer=setTimeout(()=>{transient=null;moodTimer=null;render();},4500);
            render();armSleep();
        }
        function refresh() {render();}
        function gaze(e) {
            if (disposed || paused() || reduced() || e.pointerType === 'touch' ||
                !['idle','happy','mini'].includes(root.dataset.petState)) return;
            if (!Number.isFinite(e.clientX) || !Number.isFinite(e.clientY)) return;
            lastPoint={x:e.clientX,y:e.clientY};
            if (frame !== null || !window.requestAnimationFrame) return;
            frame=window.requestAnimationFrame(()=>{
                frame=null;
                if (!lastPoint || paused() || reduced()) return;
                for (const svg of art()) {
                    const rect=svg.getBoundingClientRect();if(!rect.width||!rect.height)continue;
                    const x=Math.max(-3,Math.min(3,(lastPoint.x-rect.left-rect.width/2)/100));
                    const y=Math.max(-2,Math.min(2,(lastPoint.y-rect.top-rect.height/2)/140));
                    svg.style.setProperty('--pet-look-x',x.toFixed(2)+'px');
                    svg.style.setProperty('--pet-look-y',y.toFixed(2)+'px');
                    svg.style.setProperty('--pet-tilt',(x*.6).toFixed(2)+'deg');
                }
            });
        }
        function visibility() {
            cancelGaze();clearTimeout(sleepTimer);sleepTimer=null;
            if (!document.hidden) wake(); else render();
        }
        function configure(next) {
            preferences=next || {};root.classList.toggle('reduce-motion',!!preferences.reduceMotion);
            if (reduced()) cancelGaze();render();
        }
        function motionChanged() {cancelGaze();render();}
        function resetPointer() {cancelGaze();}
        function leavePage(e) {if (e.persisted) {cancelGaze();clearTimeout(sleepTimer);root.classList.add('pet-paused');} else dispose();}
        function returnPage(e) {if(e.persisted)visibility();}
        function dispose() {
            if(disposed)return;disposed=true;clearTimeout(sleepTimer);clearTimeout(moodTimer);cancelGaze();
            root.classList.add('pet-paused');
            document.removeEventListener('pointermove',gaze);document.removeEventListener('pointerleave',resetPointer);
            document.removeEventListener('visibilitychange',visibility);
            window.removeEventListener('pagehide',leavePage);window.removeEventListener('pageshow',returnPage);
            media?.removeEventListener?.('change',motionChanged);
        }
        document.addEventListener('pointermove',gaze,{passive:true});
        document.addEventListener('pointerleave',resetPointer,{passive:true});
        document.addEventListener('visibilitychange',visibility);
        window.addEventListener('pagehide',leavePage);window.addEventListener('pageshow',returnPage);
        media?.addEventListener?.('change',motionChanged);
        configure(preferences);wake();
        return Object.freeze({present,wake,refresh,configure,dispose,preview});
    }});
}());
