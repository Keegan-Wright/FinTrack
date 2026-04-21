window.setupBackgroundSyncEvent = (dotNetHelper, url) => {
    const eventSource = new EventSource(url);
    eventSource.addEventListener('BackgroundBankingSyncComplete', (event) => {
        dotNetHelper.invokeMethodAsync('OnBackgroundSyncComplete');
    });

    window.addEventListener('unload', () => {
        eventSource.close();
    });
};
