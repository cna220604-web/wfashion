const fashionVideoFeed = document.querySelector('.fashion-video-feed');
if (fashionVideoFeed && 'IntersectionObserver' in window) {
    const videoObserver = new IntersectionObserver(entries => {
        entries.forEach(entry => {
            const video = entry.target;
            if (entry.isIntersecting) {
                video.play().catch(() => { video.controls = true; });
            } else {
                video.pause();
            }
        });
    }, { threshold: 0.7 });

    fashionVideoFeed.querySelectorAll('.fashion-video-player').forEach(video => videoObserver.observe(video));
}
