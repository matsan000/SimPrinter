const SIMPRINTER_URL = "http://127.0.0.1:39901/print-text";

browser.runtime.onMessage.addListener((message) => {
  if (!message || message.type !== "simprinter-print") return;

  return fetch(SIMPRINTER_URL, {
    method: "POST",
    headers: { "Content-Type": "text/plain" },
    body: message.text,
  })
    .then((res) => {
      if (res.ok) return { ok: true };
      return { ok: false, error: `SimPrinter returned HTTP ${res.status}` };
    })
    .catch(() => ({
      ok: false,
      error: "Could not reach SimPrinter - make sure it's running with " +
        "\"Allow the SimPrinter browser extension to print\" enabled in Settings.",
    }));
});
