// Web Worker: kører jsQR analyse off main thread
importScripts('https://cdn.jsdelivr.net/npm/jsqr@1.4.0/dist/jsQR.min.js');

self.onmessage = function (e) {
    const { data, width, height } = e.data;
    const code = jsQR(data, width, height, { inversionAttempts: 'attemptBoth' });
    self.postMessage(code ? code.data : null);
};
