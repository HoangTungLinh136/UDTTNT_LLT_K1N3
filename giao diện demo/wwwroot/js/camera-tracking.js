// Camera Tracking & Zone Logic using TensorFlow.js (COCO-SSD)

const video = document.getElementById('camera-video');
const canvas = document.getElementById('tracking-canvas');
const ctx = canvas.getContext('2d');
const statusBadge = document.getElementById('ai-status');
const eventsList = document.getElementById('detection-list');

let model = null;
let isZoneMode = false;
let monitoringZones = [];
let currentPolygon = [];
let lastDetectionTime = 0;

// Connect HLS stream (from backend FFmpeg)
if (Hls.isSupported()) {
    const hls = new Hls({
        maxBufferLength: 5,
        maxMaxBufferLength: 10,
        liveSyncDurationCount: 3,
        liveMaxLatencyDurationCount: 5
    });
    // The C# BackgroundService outputs to wwwroot/hls/stream.m3u8
    hls.loadSource('/hls/stream.m3u8');
    hls.attachMedia(video);
    hls.on(Hls.Events.MANIFEST_PARSED, function () {
        video.play().catch(e => console.log("Auto-play prevented", e));
    });
} else if (video.canPlayType('application/vnd.apple.mpegurl')) {
    video.src = '/hls/stream.m3u8';
    video.addEventListener('loadedmetadata', function () {
        video.play().catch(e => console.log("Auto-play prevented", e));
    });
}

// Load AI Model
async function initAI() {
    try {
        model = await cocoSsd.load();
        statusBadge.textContent = "AI Tracking: Active";
        statusBadge.classList.replace('bg-success', 'bg-primary');
        
        // Setup canvas size to match video
        video.addEventListener('play', () => {
            setInterval(detectObjects, 100); // 10 FPS detection
        });
    } catch (err) {
        statusBadge.textContent = "AI Error";
        statusBadge.classList.replace('bg-success', 'bg-danger');
        console.error(err);
    }
}

initAI();

// Resize canvas correctly
function resizeCanvas() {
    canvas.width = video.clientWidth;
    canvas.height = video.clientHeight;
}
window.addEventListener('resize', resizeCanvas);
video.addEventListener('loadeddata', resizeCanvas);

async function detectObjects() {
    if (!model || video.paused || video.ended) return;
    
    // Check if video dimensions are valid before predicting
    if (video.videoWidth === 0 || video.videoHeight === 0) return;

    // We must draw video to an offscreen canvas to scale accurately if needed, 
    // but coco-ssd accepts video elements directly.
    const predictions = await model.detect(video);
    
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    
    // Draw Zones
    drawZones();
    
    // Draw Bounding Boxes and Check Intersections
    predictions.forEach(prediction => {
        // We only care about people for home security
        if (prediction.class !== 'person') return;
        
        // Scale bounding box to canvas size
        const scaleX = canvas.width / video.videoWidth;
        const scaleY = canvas.height / video.videoHeight;
        
        const [x, y, width, height] = prediction.bbox;
        const scaledX = x * scaleX;
        const scaledY = y * scaleY;
        const scaledW = width * scaleX;
        const scaledH = height * scaleY;

        // Draw Box
        ctx.strokeStyle = '#00FF00';
        ctx.lineWidth = 2;
        ctx.strokeRect(scaledX, scaledY, scaledW, scaledH);
        
        // Draw Label
        ctx.fillStyle = '#00FF00';
        ctx.font = '16px Arial';
        ctx.fillText(`Person ${Math.round(prediction.score * 100)}%`, scaledX, scaledY > 20 ? scaledY - 5 : 20);

        // Check if person intersects any monitoring zone
        checkZoneIntersection(scaledX, scaledY, scaledW, scaledH);
    });
}

// Zone Logic
function toggleZoneMode() {
    isZoneMode = !isZoneMode;
    if (isZoneMode) {
        canvas.style.pointerEvents = 'auto'; // allow clicking on canvas
        currentPolygon = [];
        alert("Click on the video to draw a zone polygon. Double click to finish.");
    } else {
        canvas.style.pointerEvents = 'none';
        currentPolygon = [];
    }
}

canvas.addEventListener('click', (e) => {
    if (!isZoneMode) return;
    const rect = canvas.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const y = e.clientY - rect.top;
    currentPolygon.push({x, y});
});

canvas.addEventListener('dblclick', () => {
    if (!isZoneMode || currentPolygon.length < 3) return;
    monitoringZones.push(currentPolygon);
    currentPolygon = [];
    isZoneMode = false;
    canvas.style.pointerEvents = 'none';
    alert("Zone saved!");
});

function drawZones() {
    ctx.fillStyle = 'rgba(255, 0, 0, 0.2)';
    ctx.strokeStyle = 'red';
    ctx.lineWidth = 2;
    
    // Draw saved zones
    monitoringZones.forEach(zone => {
        ctx.beginPath();
        ctx.moveTo(zone[0].x, zone[0].y);
        for(let i=1; i<zone.length; i++) {
            ctx.lineTo(zone[i].x, zone[i].y);
        }
        ctx.closePath();
        ctx.fill();
        ctx.stroke();
    });
    
    // Draw current drawing zone
    if (currentPolygon.length > 0) {
        ctx.beginPath();
        ctx.moveTo(currentPolygon[0].x, currentPolygon[0].y);
        for(let i=1; i<currentPolygon.length; i++) {
            ctx.lineTo(currentPolygon[i].x, currentPolygon[i].y);
        }
        ctx.stroke();
    }
}

function checkZoneIntersection(x, y, w, h) {
    if (monitoringZones.length === 0) return;
    
    // Simple centroid check for intersection
    const centerX = x + w/2;
    const centerY = y + h/2;
    
    let inZone = false;
    monitoringZones.forEach((zone, index) => {
        if (isPointInPolygon({x: centerX, y: centerY}, zone)) {
            inZone = true;
            triggerAlert(`Zone ${index + 1}`);
        }
    });
}

function isPointInPolygon(point, vs) {
    let x = point.x, y = point.y;
    let inside = false;
    for (let i = 0, j = vs.length - 1; i < vs.length; j = i++) {
        let xi = vs[i].x, yi = vs[i].y;
        let xj = vs[j].x, yj = vs[j].y;
        let intersect = ((yi > y) != (yj > y)) && (x < (xj - xi) * (y - yi) / (yj - yi) + xi);
        if (intersect) inside = !inside;
    }
    return inside;
}

function triggerAlert(zoneName) {
    const now = Date.now();
    if (now - lastDetectionTime < 10000) return; // Only alert every 10s max
    lastDetectionTime = now;
    
    // Send API Request
    fetch('/api/CameraApi/log-detection', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': document.querySelector('#cameraCsrf input[name="__RequestVerificationToken"]').value },
        body: JSON.stringify({ zoneName: zoneName })
    })
    .then(res => res.json())
    .then(data => {
        if (data.success) {
            const noEvents = document.getElementById('no-events-text');
            if (noEvents) noEvents.remove();
            
            const li = document.createElement('li');
            li.className = 'list-group-item text-danger fw-bold';
            const timeString = new Date().toLocaleTimeString();
            const icon = document.createElement('i');
            icon.className = 'fa fa-exclamation-triangle';
            icon.setAttribute('aria-hidden', 'true');
            li.append(icon, document.createTextNode(` Person detected in ${String(zoneName ?? '')} at ${timeString}`));
            eventsList.prepend(li);
        }
    })
    .catch(console.error);
}

function sendPtz(command) {
    fetch('/api/CameraApi/ptz', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': document.querySelector('#cameraCsrf input[name="__RequestVerificationToken"]').value },
        body: JSON.stringify({ command: command })
    });
}
