const fashionVideoFeed = document.querySelector('.fashion-video-feed');
if (fashionVideoFeed) {
    const soundPreferences = new WeakMap();
    const updateSoundButton = video => {
        const soundButton = video.closest('.fashion-video-card')?.querySelector('[data-video-sound]');
        if (!soundButton) return;
        soundButton.textContent = video.muted ? 'BẬT TIẾNG' : 'TẮT TIẾNG';
        soundButton.setAttribute('aria-label', video.muted ? 'Bật tiếng video' : 'Tắt tiếng video');
        soundButton.setAttribute('aria-pressed', String(!video.muted));
    };

    fashionVideoFeed.addEventListener('click', event => {
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
