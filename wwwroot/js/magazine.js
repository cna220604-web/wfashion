'use strict';

if ('IntersectionObserver' in window && !window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
    const revealTargets = document.querySelectorAll([
        '.magazine-trend-copy > *',
        '.magazine-trend-list-heading > *',
        '.magazine-trend-item > *',
        '.magazine-heading > div > *',
        '.magazine-heading > .magazine-update',
        '.magazine-card-content > *',
        '.magazine-attribution'
    ].join(','));

    if (revealTargets.length > 0) {
        const observer = new IntersectionObserver(entries => {
            entries.forEach(entry => entry.target.classList.toggle('is-scroll-visible', entry.isIntersecting));
        }, { threshold: 0.12 });

        revealTargets.forEach((element, index) => {
            element.classList.add('scroll-reveal');
            element.style.setProperty('--scroll-reveal-offset', index % 2 === 0 ? '-32px' : '32px');
            element.style.setProperty('--scroll-reveal-delay', `${index % 4 * 65}ms`);
            observer.observe(element);
        });
    }
}
