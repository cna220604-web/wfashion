if ('serviceWorker' in navigator) {
    window.addEventListener('load', () => {
        navigator.serviceWorker.register('/service-worker.js').catch(error => {
            console.error('W Studio offline support could not be enabled.', error);
        });
    });
}

const installButtons = document.querySelectorAll('[data-install-app]');
if (installButtons.length > 0) {
    let installPrompt;
    window.addEventListener('beforeinstallprompt', event => {
        event.preventDefault();
        installPrompt = event;
        installButtons.forEach(button => { button.hidden = false; });
    });

    installButtons.forEach(button => {
        button.addEventListener('click', async () => {
            if (!installPrompt) return;
            const prompt = installPrompt;
            installButtons.forEach(installButton => { installButton.disabled = true; });
            await prompt.prompt();
            await prompt.userChoice;
            installPrompt = null;
            installButtons.forEach(installButton => {
                installButton.hidden = true;
                installButton.disabled = false;
            });
        });
    });

    window.addEventListener('appinstalled', () => {
        installPrompt = null;
        installButtons.forEach(button => { button.hidden = true; });
    });
}
