// ── Scroll Position Restoration ──
// Saves scroll position per URL so the browser back button returns to the same spot.
(function () {
    var key = 'scrollPos_' + location.pathname + location.search;

    // Restore scroll position on pageshow (works with bfcache too)
    window.addEventListener('pageshow', function () {
        var saved = sessionStorage.getItem(key);
        if (saved) {
            // Small delay to let the DOM render before scrolling
            setTimeout(function () { window.scrollTo(0, parseInt(saved, 10)); }, 0);
        }
    });

    // Save scroll position before leaving
    window.addEventListener('beforeunload', function () {
        sessionStorage.setItem(key, window.scrollY.toString());
    });
})();
