namespace Tristinwold.Shared;

/// <summary>
/// The web page a phone opens for remote play. It fills the screen with the game, sends every
/// touch back (the game draws its own on-screen controls, so the page needs none), and has a
/// keyboard button for typing names and passwords. On an iPhone, Safari's Share → Add to Home
/// Screen makes it open full screen like an app. A hardware keyboard (an iPad's) also works.
/// </summary>
public static class RemotePage
{
    public const string Html = """
        <!doctype html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no, viewport-fit=cover">
        <meta name="apple-mobile-web-app-capable" content="yes">
        <meta name="mobile-web-app-capable" content="yes">
        <meta name="apple-mobile-web-app-status-bar-style" content="black-translucent">
        <meta name="apple-mobile-web-app-title" content="Tristinwold">
        <title>Tristinwold</title>
        <style>
          html, body { margin: 0; height: 100%; background: #000; overflow: hidden; touch-action: none;
            -webkit-user-select: none; user-select: none; -webkit-touch-callout: none; overscroll-behavior: none; }
          canvas { position: fixed; inset: 0; width: 100vw; height: 100vh; display: block; }
          #note { position: fixed; inset: 0; display: flex; align-items: center; justify-content: center; text-align: center;
            color: #e8dcc4; font: 17px/1.5 -apple-system, system-ui, sans-serif; padding: 24px; pointer-events: none; }
          #keys { position: fixed; left: max(6px, env(safe-area-inset-left)); top: 50%; transform: translateY(-50%);
            background: rgba(20, 18, 14, .55); color: #e8dcc4; border: 1px solid rgba(232, 220, 196, .35); border-radius: 8px;
            font: 20px -apple-system, system-ui, sans-serif; padding: 6px 8px; touch-action: manipulation; }
          #type { position: fixed; left: -1000px; top: 0; opacity: 0; width: 10px; height: 10px; font-size: 16px; }
          @media (orientation: portrait) and (pointer: coarse) { #turn { display: flex !important; } }
          #turn { display: none; position: fixed; inset: 0; background: #000; color: #e8dcc4; align-items: center; justify-content: center;
            font: 18px -apple-system, system-ui, sans-serif; text-align: center; padding: 24px; z-index: 5; }
        </style>
        </head>
        <body>
        <canvas id="screen"></canvas>
        <div id="note">Connecting to the game…</div>
        <button id="keys" aria-label="Keyboard">⌨︎</button>
        <input id="type" autocapitalize="off" autocomplete="off" autocorrect="off" spellcheck="false">
        <div id="turn">Turn your phone sideways to play.</div>
        <script>
        const key = new URLSearchParams(location.search).get("k") || "";
        const canvas = document.getElementById("screen"), ctx = canvas.getContext("2d");
        const note = document.getElementById("note"), typer = document.getElementById("type");
        let socket = null, frame = { w: 16, h: 9 }, box = { x: 0, y: 0, w: 1, h: 1 }, wait = 500;

        function say(text) { note.textContent = text; note.style.display = text ? "flex" : "none"; }
        function send(o) { if (socket && socket.readyState === 1) socket.send(typeof o === "string" ? o : JSON.stringify(o)); }

        function fit() {
          const dpr = window.devicePixelRatio || 1;
          canvas.width = Math.round(innerWidth * dpr); canvas.height = Math.round(innerHeight * dpr);
          const s = Math.min(canvas.width / frame.w, canvas.height / frame.h);
          box = { w: frame.w * s, h: frame.h * s, x: (canvas.width - frame.w * s) / 2, y: (canvas.height - frame.h * s) / 2 };
        }
        addEventListener("resize", fit); fit();

        function connect() {
          say("Connecting to the game…");
          const proto = location.protocol === "https:" ? "wss:" : "ws:";
          socket = new WebSocket(`${proto}//${location.host}/ws?k=${encodeURIComponent(key)}`);
          socket.binaryType = "blob";
          socket.onopen = () => { wait = 500; say(""); send({ t: "hello", w: innerWidth, h: innerHeight }); };
          socket.onmessage = async (e) => {
            if (typeof e.data === "string") {
              const m = JSON.parse(e.data);
              if (m.keyboard === true) document.getElementById("keys").style.borderColor = "#f0c060";
              if (m.keyboard === false) document.getElementById("keys").style.borderColor = "";
              return;
            }
            try {
              const image = await createImageBitmap(e.data);
              if (image.width !== frame.w || image.height !== frame.h) { frame = { w: image.width, h: image.height }; fit(); }
              ctx.drawImage(image, box.x, box.y, box.w, box.h);
              image.close && image.close();
            } finally { send("drawn"); }
          };
          socket.onclose = () => { say("Lost the game. Reconnecting…"); setTimeout(connect, wait); wait = Math.min(wait * 2, 8000); };
        }
        connect();

        // Touches (and a mouse, for trying it on a computer), as fractions of the game's picture.
        function at(e) {
          const dpr = window.devicePixelRatio || 1;
          return { x: (e.clientX * dpr - box.x) / box.w, y: (e.clientY * dpr - box.y) / box.h };
        }
        const down = new Set();
        canvas.addEventListener("pointerdown", (e) => { e.preventDefault(); canvas.setPointerCapture(e.pointerId); down.add(e.pointerId); send({ t: "d", i: e.pointerId, ...at(e) }); });
        canvas.addEventListener("pointermove", (e) => { if (!down.has(e.pointerId)) return; e.preventDefault(); send({ t: "m", i: e.pointerId, ...at(e) }); });
        for (const ev of ["pointerup", "pointercancel"])
          canvas.addEventListener(ev, (e) => { if (!down.delete(e.pointerId)) return; e.preventDefault(); send({ t: "u", i: e.pointerId, ...at(e) }); });
        for (const ev of ["gesturestart", "gesturechange", "dblclick", "contextmenu"]) document.addEventListener(ev, (e) => e.preventDefault());

        // Typing: the keyboard button opens the phone's keyboard; letters go to whatever box has focus in the game.
        document.getElementById("keys").addEventListener("click", () => { typer.value = " "; typer.focus(); });
        typer.addEventListener("input", (e) => {
          if (e.inputType === "deleteContentBackward") send({ t: "key", k: "Backspace" });
          else if (e.data) send({ t: "text", s: e.data });
          typer.value = " ";
        });
        typer.addEventListener("keydown", (e) => {
          if (e.key === "Enter") { send({ t: "key", k: "Enter" }); typer.blur(); }
          if (e.key === "Backspace" && typer.value === "") send({ t: "key", k: "Backspace" });
        });

        // A hardware keyboard plays the game directly (WASD, space, E, M...).
        const names = { Space: "Space", ShiftLeft: "Shift", ShiftRight: "Shift", Escape: "Escape", Enter: "Enter", Tab: "Tab",
          Backspace: "Backspace", ArrowUp: "Up", ArrowDown: "Down", ArrowLeft: "Left", ArrowRight: "Right", ControlLeft: "Ctrl", AltLeft: "Alt" };
        function godotKey(code) { return names[code] || (code.startsWith("Key") ? code.slice(3) : code.startsWith("Digit") ? code.slice(5) : code.startsWith("F") ? code : null); }
        for (const [ev, pressed] of [["keydown", true], ["keyup", false]])
          addEventListener(ev, (e) => {
            if (document.activeElement === typer || e.repeat) return;
            const k = godotKey(e.code);
            if (!k) return;
            e.preventDefault();
            send({ t: "press", k, p: pressed });
          });
        </script>
        </body>
        </html>
        """;
}
