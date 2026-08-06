(() => {
  const BUTTON_ID = "simprinter-print-button";

  function handlePrintClick(btn, getText) {
    const text = getText();
    if (!text) {
      btn.textContent = "Nothing to print";
      setTimeout(() => (btn.textContent = "🖨 Print"), 2000);
      return;
    }

    // SimBrief's raw output already starts with its own title line (e.g.
    // "TAKEOFF PERFORMANCE"), so nothing extra is prepended here - doing so
    // just printed the title twice.
    btn.textContent = "Printing...";

    browser.runtime
      .sendMessage({ type: "simprinter-print", text })
      .then((response) => {
        btn.textContent = response && response.ok ? "✅ Sent" : "❌ Failed";
        if (response && !response.ok) console.warn("SimPrinter:", response.error);
      })
      .catch(() => {
        btn.textContent = "❌ Failed";
      })
      .finally(() => {
        setTimeout(() => (btn.textContent = "🖨 Print"), 2500);
      });
  }

  // Case 1: the per-flight Takeoff/Landing Performance popup (flight briefing page).
  //
  // SimBrief's "tlr" (takeoff/landing reference) dialog is reused for both calculators and
  // may leave a stale, hidden copy in the DOM after switching between them - querying "the
  // first .tlr-output in the page" can silently find the wrong (invisible) one and skip
  // injecting into the one actually on screen. Every candidate is checked for visibility, and
  // the dialog/result element are captured in a closure so the click handler never has to
  // re-look-up by id later either. The panel's own "Copy" button also sits inside a
  // fixed-height, overflow:hidden container - anything appended after it there gets clipped
  // invisible, so the button lives in the <h3> header row instead, which is never clipped.
  function injectPopupButtons() {
    document.querySelectorAll(".tlr-output").forEach((container) => {
      const dialog = container.closest(".ui-dialog");
      if (dialog && dialog.offsetParent === null) return; // stale hidden copy

      const h3 = container.querySelector("h3");
      if (!h3 || h3.querySelector(`#${BUTTON_ID}`)) return;

      const resultEl = container.querySelector("#message-tlr-result");
      if (!resultEl || !resultEl.textContent.trim()) return; // wait for a real calculation

      const infoLink = h3.querySelector("span.right");
      const btn = document.createElement("span");
      btn.id = BUTTON_ID;
      btn.className = "right";
      btn.textContent = "🖨 Print";
      btn.style.cssText = "cursor:pointer;margin-right:14px;";
      btn.addEventListener("click", (e) => {
        e.preventDefault();
        e.stopPropagation();
        handlePrintClick(btn, () => resultEl.textContent.trim());
      });

      if (infoLink) h3.insertBefore(btn, infoLink);
      else h3.appendChild(btn);
    });
  }

  // Case 2: the standalone Performance & Tools page (dispatch.simbrief.com/tools).
  //
  // Its "Raw Output" view has its own independent header (with its own Formatted/Raw
  // toggle), completely separate from the Formatted view's header - the two are just shown
  // and hidden with jQuery slideUp/slideDown. Takeoff and Landing each get their own
  // .performance-results-raw container, so both are handled by the same loop.
  function injectToolsPageButtons() {
    document.querySelectorAll(".performance-results-raw").forEach((container) => {
      const h1 = container.querySelector("h1");
      if (!h1 || h1.querySelector(`#${BUTTON_ID}`)) return;

      const textEl = container.querySelector(".textbox");
      if (!textEl || !textEl.textContent.trim()) return;

      const btn = document.createElement("a");
      btn.id = BUTTON_ID;
      btn.className = "dual-toggle";
      btn.style.cssText = "cursor:pointer;";
      btn.textContent = "🖨 Print";
      btn.addEventListener("click", (e) => {
        e.preventDefault();
        e.stopPropagation();
        handlePrintClick(btn, () => textEl.textContent.trim());
      });

      h1.appendChild(btn);
    });
  }

  function tryInject() {
    injectPopupButtons();
    injectToolsPageButtons();
  }

  new MutationObserver(tryInject).observe(document.body, {
    childList: true,
    subtree: true,
    characterData: true,
  });

  tryInject();
})();
