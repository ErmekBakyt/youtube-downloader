// YTDownloader - Main JavaScript Module (UI + i18n + Progress Bar)
// NOTE: This is UI-side logic only (state management, localization, progress visualization).

'use strict';

let currentVideoUrl = '';
let selectedFormatId = '';
let videoData = null;

// ---------- i18n helpers ----------
function t(key, fallback) {
    if (window.i18n && typeof window.i18n[key] === 'string') return window.i18n[key];
    return fallback ?? key;
}

// ---------- MAIN FETCH FUNCTION ----------
async function fetchVideoInfo() {
    const urlInput = document.getElementById('urlInput');
    const url = (urlInput?.value || '').trim();

    if (!url) {
        shakeInput();
        return;
    }

    // Basic URL validation on client side
    if (!isYoutubeUrl(url)) {
        showError(`${t('invalidUrl', 'Please enter a valid URL.')}\n${t('exampleUrl', 'Example: https://www.youtube.com/watch?v=...')}`);
        return;
    }

    currentVideoUrl = url;
    showLoading();

    try {
        const token = getAntiForgeryToken();
        const response = await fetch('/Home/GetVideoInfo', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-CSRF-TOKEN': token
            },
            body: JSON.stringify({ url }),
            credentials: 'same-origin'
        });

        if (!response.ok) {
            throw new Error(`${t('serverError', 'Server error')}: ${response.status}`);
        }

        const result = await response.json();

        if (!result.success) {
            showError(result.errorMessage || t('fetchFailed', 'Failed to fetch video info.'));
            return;
        }

        videoData = result.data;
        renderVideoInfo(videoData);

    } catch (error) {
        if (error?.name === 'AbortError') return;
        showError(`${t('fetchFailed', 'Failed to fetch video info.')}: ${error.message}`);
    }
}

// ---------- RENDER VIDEO INFO ----------
function renderVideoInfo(data) {
    document.getElementById('videoThumbnail').src = data.thumbnailUrl;
    document.getElementById('videoThumbnail').alt = data.title || t('thumbnailAlt', 'Video Thumbnail');

    document.getElementById('videoTitle').textContent = data.title || '';
    document.getElementById('videoAuthor').textContent = data.author || '';
    document.getElementById('videoViews').textContent = data.viewCount || '';
    document.getElementById('videoDuration').textContent = data.duration || '';

    renderFormats(data.formats);

    hideLoading();
    document.getElementById('featuresSection')?.classList.add('hidden');
    document.getElementById('videoSection')?.classList.remove('hidden');
}

// ---------- RENDER FORMAT OPTIONS ----------
function renderFormats(formats) {
    const grid = document.getElementById('formatGrid');
    grid.innerHTML = '';

    if (!formats || formats.length === 0) {
        grid.innerHTML = `<p style="color: var(--text-muted); font-size: 0.85rem;">${t('noFormats', 'No formats found.')}</p>`;
        return;
    }

    formats.forEach((format) => {
        const option = document.createElement('label');
        option.className = 'format-option';

        const isAudio = format.extension === 'mp3' || format.resolution === 'Audio Only';
        const icon = isAudio ? '🎵' : getQualityIcon(format.resolution);

        const formatIdEsc = escapeHtml(format.formatId);
        const labelEsc = escapeHtml(format.label);

        option.innerHTML = `
      <input type="radio"
             name="videoFormat"
             value="${formatIdEsc}"
             onchange="selectFormat('${formatIdEsc}', '${labelEsc}')" />
      <div class="format-label">
        ${format.isRecommended ? `<span class="recommended-badge">✓ ${t('recommended', 'Recommended')}</span>` : ''}
        <div class="format-quality">${icon} ${escapeHtml(format.resolution !== 'Audio Only' ? format.resolution : t('audioLabel', 'Audio'))}</div>
        <div class="format-details">${escapeHtml((format.extension || '').toUpperCase())} ${isAudio ? '' : '• ' + escapeHtml(format.quality || '')}</div>
        ${format.fileSize ? `<div class="format-size">~ ${escapeHtml(format.fileSize)}</div>` : ''}
      </div>
    `;

        grid.appendChild(option);
    });
}

function getQualityIcon(resolution) {
    const height = parseInt(resolution, 10);
    if (Number.isFinite(height)) {
        if (height >= 2160) return '🔷';
        if (height >= 1080) return '🎬';
        if (height >= 720) return '📺';
        return '📱';
    }
    return '🎬';
}

// ---------- FORMAT SELECTION ----------
function selectFormat(formatId, label) {
    selectedFormatId = formatId;

    const btn = document.getElementById('downloadBtn');
    const btnText = document.getElementById('downloadBtnText');
    const hint = document.querySelector('.download-hint');

    btn.disabled = false;
    btnText.textContent = t('download', 'Download');
    if (hint) hint.style.opacity = '0';
}

// ---------- DOWNLOAD (progress bar) ----------
async function downloadVideo() {
    if (!selectedFormatId || !currentVideoUrl) return;

    const btn = document.getElementById('downloadBtn');
    const btnText = document.getElementById('downloadBtnText');
    const progressDiv = document.getElementById('downloadProgress');
    const fill = document.getElementById('progressFill');     // must exist in HTML
    const progressText = document.getElementById('progressText');

    // Disable button & show progress
    btn.disabled = true;
    if (btnText) btnText.textContent = t('preparing', 'Preparing...');
    if (progressDiv) progressDiv.classList.remove('hidden');
    if (fill) fill.style.width = '0%';
    if (progressText) progressText.textContent = t('downloadingWait', 'Downloading, please wait...');

    try {
        const token = getAntiForgeryToken();
        const response = await fetch('/Home/Download', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-CSRF-TOKEN': token
            },
            body: JSON.stringify({
                url: currentVideoUrl,
                formatId: selectedFormatId
            }),
            credentials: 'same-origin'
        });

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(errorText || `${t('downloadError', 'Download error')}: ${response.status}`);
        }

        // Filename
        const disposition = response.headers.get('Content-Disposition');
        let filename = 'video.mp4';
        if (disposition && disposition.includes('filename=')) {
            const match = disposition.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/);
            if (match) filename = decodeURIComponent(match[1].replace(/['"]/g, ''));
        }

        // Progress: works best when server sends Content-Length
        const lengthHeader = response.headers.get('Content-Length');
        const total = lengthHeader ? parseInt(lengthHeader, 10) : 0;

        // Stream read
        if (!response.body || !response.body.getReader) {
            // Fallback: no streaming support
            const blob = await response.blob();
            triggerDownload(blob, filename);
            finishDownloadUi(btn, btnText, progressDiv);
            return;
        }

        const reader = response.body.getReader();
        let received = 0;

        const chunks = [];
        while (true) {
            const { done, value } = await reader.read();
            if (done) break;
            chunks.push(value);
            received += value.length;

            if (total > 0) {
                const percent = Math.min(100, Math.round((received / total) * 100));
                if (fill) fill.style.width = `${percent}%`;
                if (progressText) progressText.textContent = `${percent}%`;
            } else {
                // Unknown total: show MB
                if (progressText) progressText.textContent = `${formatMb(received)} MB`;
            }
        }

        const mime = response.headers.get('Content-Type') || 'application/octet-stream';
        const blob = new Blob(chunks, { type: mime });
        triggerDownload(blob, filename);

        finishDownloadUi(btn, btnText, progressDiv);

    } catch (error) {
        if (progressDiv) progressDiv.classList.add('hidden');
        btn.disabled = false;
        if (btnText) btnText.textContent = t('retry', 'Try Again');

        alert(`${t('downloadError', 'Download error')}: ${error.message}`);
    }
}

function triggerDownload(blob, filename) {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
}

function finishDownloadUi(btn, btnText, progressDiv) {
    if (btnText) btnText.textContent = `✓ ${t('downloaded', 'Downloaded!')}`;
    if (progressDiv) progressDiv.classList.add('hidden');
    btn.disabled = false;

    setTimeout(() => {
        if (btnText) btnText.textContent = t('download', 'Download');
    }, 2500);
}

function formatMb(bytes) {
    return Math.round((bytes / (1024 * 1024)) * 10) / 10;
}

// ---------- STATE MANAGEMENT ----------
function showLoading() {
    document.getElementById('loadingSection')?.classList.remove('hidden');
    document.getElementById('errorSection')?.classList.add('hidden');
    document.getElementById('videoSection')?.classList.add('hidden');

    const fetchBtn = document.getElementById('fetchBtn');
    fetchBtn.disabled = true;
    fetchBtn.querySelector('.btn-text').textContent = t('analyzing', 'Analyzing...');
}

function hideLoading() {
    document.getElementById('loadingSection')?.classList.add('hidden');
    const fetchBtn = document.getElementById('fetchBtn');
    fetchBtn.disabled = false;
    fetchBtn.querySelector('.btn-text').textContent = t('analyze', 'Analyze');
}

function showError(message) {
    hideLoading();
    document.getElementById('errorMessage').textContent = message;
    document.getElementById('errorSection')?.classList.remove('hidden');
    document.getElementById('videoSection')?.classList.add('hidden');
}

function resetState() {
    selectedFormatId = '';
    videoData = null;

    const urlInput = document.getElementById('urlInput');
    if (urlInput) urlInput.value = '';

    document.getElementById('videoSection')?.classList.add('hidden');
    document.getElementById('errorSection')?.classList.add('hidden');
    document.getElementById('loadingSection')?.classList.add('hidden');
    document.getElementById('featuresSection')?.classList.remove('hidden');

    const btn = document.getElementById('downloadBtn');
    btn.disabled = true;
    btn.style.background = '';

    const btnText = document.getElementById('downloadBtnText');
    if (btnText) btnText.textContent = t('qualitySelect', 'Select Quality');

    const hint = document.querySelector('.download-hint');
    if (hint) hint.style.opacity = '';

    document.getElementById('downloadProgress')?.classList.add('hidden');

    urlInput?.focus();
}

// ---------- UTILITIES ----------
function isYoutubeUrl(url) {
    return /^(https?:\/\/)?(www\.)?(youtube\.com|youtu\.be)\/.+/.test(url);
}

function getAntiForgeryToken() {
    const input = document.querySelector('#antiForgeryForm input[name="__RequestVerificationToken"]');
    return input ? input.value : '';
}

function escapeHtml(str) {
    if (!str) return '';
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

function shakeInput() {
    const input = document.querySelector('.search-input-wrapper');
    if (!input) return;

    input.style.animation = 'none';
    // reflow
    // eslint-disable-next-line no-unused-expressions
    input.offsetHeight;
    input.style.animation = 'shake 0.4s cubic-bezier(0.36, 0.07, 0.19, 0.97)';

    if (!document.querySelector('#shake-keyframes')) {
        const style = document.createElement('style');
        style.id = 'shake-keyframes';
        style.textContent = `
      @keyframes shake {
        10%, 90% { transform: translateX(-2px); }
        20%, 80% { transform: translateX(4px); }
        30%, 50%, 70% { transform: translateX(-6px); }
        40%, 60% { transform: translateX(6px); }
      }
    `;
        document.head.appendChild(style);
    }

    input.style.borderColor = 'var(--accent)';
    setTimeout(() => { input.style.borderColor = ''; }, 1500);
}

// ---------- EVENT LISTENERS ----------
document.addEventListener('DOMContentLoaded', () => {
    const urlInput = document.getElementById('urlInput');
    if (!urlInput) return;

    // Enter key to fetch
    urlInput.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') fetchVideoInfo();
    });

    // Paste handler - auto fetch
    urlInput.addEventListener('paste', () => {
        setTimeout(() => {
            const pastedText = urlInput.value.trim();
            if (isYoutubeUrl(pastedText)) {
                setTimeout(fetchVideoInfo, 300);
            }
        }, 100);
    });

    urlInput.focus();
});