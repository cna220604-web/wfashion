const fashionVideoFeed = document.querySelector('.fashion-video-feed');
if (fashionVideoFeed) {
    const soundPreferences = new WeakMap();
    const videoReturnStorageKey = 'studio.fashion-video-return';
    const updateSoundButton = video => {
        const soundButton = video.closest('.fashion-video-card')?.querySelector('[data-video-sound]');
        if (!soundButton) return;
        soundButton.textContent = video.muted ? 'BẬT TIẾNG' : 'TẮT TIẾNG';
        soundButton.setAttribute('aria-label', video.muted ? 'Bật tiếng video' : 'Tắt tiếng video');
        soundButton.setAttribute('aria-pressed', String(!video.muted));
    };

    fashionVideoFeed.addEventListener('click', event => {
        const productLink = event.target.closest('.fashion-video-product');
        if (productLink && event.button === 0 && !event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey) {
            const card = productLink.closest('.fashion-video-card');
            const video = card?.querySelector('.fashion-video-player');
            const cards = [...fashionVideoFeed.querySelectorAll('.fashion-video-card')];
            const productId = new URL(productLink.href, window.location.href).searchParams.get('product');
            if (card && video && productId) {
                try {
                    sessionStorage.setItem(videoReturnStorageKey, JSON.stringify({
                        productId,
                        index: cards.indexOf(card),
                        currentTime: Number.isFinite(video.currentTime) ? video.currentTime : 0,
                        muted: video.muted
                    }));
                } catch {
                    // Navigation still works if browser storage is unavailable.
                }
            }
            return;
        }

        const soundButton = event.target.closest('[data-video-sound]');
        if (!soundButton) return;

        const video = soundButton.closest('.fashion-video-card')?.querySelector('.fashion-video-player');
        if (!video) return;

        video.muted = !video.muted;
        soundPreferences.set(video, video.muted);
        if (video.paused) video.play().catch(() => { video.controls = true; });
    });

    fashionVideoFeed.querySelectorAll('.fashion-video-player').forEach(video => {
        const card = video.closest('.fashion-video-card');
        const updateOrientation = () => {
            if (!card || !video.videoWidth || !video.videoHeight) return;
            card.classList.toggle('is-portrait', video.videoHeight >= video.videoWidth);
            card.classList.toggle('is-landscape', video.videoWidth > video.videoHeight);
        };
        video.addEventListener('loadedmetadata', updateOrientation, { once: true });
        if (video.readyState >= HTMLMediaElement.HAVE_METADATA) updateOrientation();
        video.addEventListener('volumechange', () => updateSoundButton(video));
        updateSoundButton(video);
    });

    const isVideoReturn = new URLSearchParams(window.location.search).get('resume') === '1';
    if (isVideoReturn) {
        try {
            const savedPosition = JSON.parse(sessionStorage.getItem(videoReturnStorageKey) || 'null');
            sessionStorage.removeItem(videoReturnStorageKey);
            if (savedPosition && Number.isInteger(savedPosition.index)) {
                const cards = [...fashionVideoFeed.querySelectorAll('.fashion-video-card')];
                const card = cards[savedPosition.index];
                const video = card?.querySelector('.fashion-video-player');
                if (card && video) {
                    card.scrollIntoView({ block: 'start', behavior: 'instant' });
                    video.muted = savedPosition.muted === true;
                    soundPreferences.set(video, video.muted);
                    const restorePlaybackTime = () => {
                        if (Number.isFinite(savedPosition.currentTime) && savedPosition.currentTime > 0) {
                            video.currentTime = Math.min(savedPosition.currentTime, Number.isFinite(video.duration) ? video.duration : savedPosition.currentTime);
                        }
                    };
                    if (video.readyState >= HTMLMediaElement.HAVE_METADATA) restorePlaybackTime();
                    else video.addEventListener('loadedmetadata', restorePlaybackTime, { once: true });
                }
            }
        } catch {
            sessionStorage.removeItem(videoReturnStorageKey);
        }
    }

    if ('IntersectionObserver' in window) {
        const videoObserver = new IntersectionObserver(entries => {
            entries.forEach(entry => {
                const video = entry.target;
                if (entry.isIntersecting) {
                    video.muted = soundPreferences.get(video) ?? false;
                    video.play().catch(() => {
                        if (soundPreferences.has(video)) {
                            video.controls = true;
                            return;
                        }
                        video.muted = true;
                        updateSoundButton(video);
                        video.play().catch(() => { video.controls = true; });
                    });
                } else {
                    video.pause();
                }
            });
        }, { threshold: 0.7 });

        fashionVideoFeed.querySelectorAll('.fashion-video-player').forEach(video => videoObserver.observe(video));
    }
}
