namespace Tristinwold.Shared;

/// <summary>
/// The web page a phone opens for remote play. Held sideways, it fills the screen with the game and sends every
/// touch back (the game draws its own on-screen controls). Held upright, the game's picture sits at the top and
/// the bottom of the screen, where the keyboard would be, is a controller: a joystick to walk, a smaller one to
/// turn the camera, the class's moves, and
/// buttons for the map, quests, pack and menu; dragging on the picture looks around, and tapping it works menus.
/// A keyboard button opens the phone's keyboard for names and passwords. On an iPhone, Safari's Share → Add to
/// Home Screen makes it open full screen like an app. A hardware keyboard (an iPad's) also works.
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
            -webkit-user-select: none; user-select: none; -webkit-touch-callout: none; overscroll-behavior: none;
            font-family: -apple-system, system-ui, sans-serif; }
          canvas { position: fixed; inset: 0; width: 100vw; height: 100vh; display: block; }
          #note { position: fixed; inset: 0; display: flex; align-items: center; justify-content: center; text-align: center;
            color: #e8dcc4; font: 17px/1.5 -apple-system, system-ui, sans-serif; padding: 24px; pointer-events: none; }
          #keys { position: fixed; left: max(6px, env(safe-area-inset-left)); top: 50%; transform: translateY(-50%);
            background: rgba(20, 18, 14, .55); color: #e8dcc4; border: 1px solid rgba(232, 220, 196, .35); border-radius: 8px;
            font: 20px -apple-system, system-ui, sans-serif; padding: 6px 8px; touch-action: manipulation; }
          #snd { position: fixed; right: max(6px, env(safe-area-inset-right)); top: max(6px, env(safe-area-inset-top));
            background: rgba(20, 18, 14, .55); color: #e8dcc4; border: 1px solid rgba(232, 220, 196, .35); border-radius: 8px;
            font: 18px -apple-system, system-ui, sans-serif; padding: 4px 8px; touch-action: manipulation; z-index: 2; }
          #probe { position: fixed; top: 0; left: 0; width: 1px; height: env(safe-area-inset-top); visibility: hidden; }
          #type { position: fixed; left: -1000px; top: 0; opacity: 0; width: 10px; height: 10px; font-size: 16px; }

          /* Upright: the controller, below the picture. */
          #pad { display: none; position: fixed; left: 0; right: 0; bottom: 0; top: var(--top, 50vh);
            background: linear-gradient(#1b1813, #0f0d0a); border-top: 1px solid #3a3226; color: #e8dcc4;
            padding: 8px max(10px, env(safe-area-inset-right)) max(10px, env(safe-area-inset-bottom)) max(10px, env(safe-area-inset-left));
            box-sizing: border-box; flex-direction: column; gap: 8px; }
          body.upright #pad { display: flex; }
          body.upright #keys { display: none; }
          /* The top strip: one Menu button on the left that drops down the map, quests, pack and the rest, and the quick keys 1 to 0 beside it. */
          #top { position: relative; display: grid; grid-template-columns: 68px repeat(5, 1fr); grid-template-rows: 40px 40px; gap: 6px; }
          #top button { border-radius: 8px; font-size: 15px; }
          #menu { grid-row: 1 / span 2; font-size: 16px; }
          #drop { display: none; position: absolute; left: 0; bottom: calc(100% + 6px); z-index: 5; grid-template-columns: repeat(2, 128px); gap: 6px;
            background: #1b1813; border: 1px solid #3a3226; border-radius: 12px; padding: 8px; box-shadow: 0 8px 24px rgba(0, 0, 0, .6); }
          #drop.open { display: grid; }
          #drop button { height: 44px; font-size: 16px; }
          .q { display: flex; flex-direction: column; align-items: center; justify-content: center; line-height: 1.05; padding: 0 1px; }
          .q b { font-size: 15px; }
          .q i { font-style: normal; font-size: 10px; opacity: .85; max-width: 100%; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
          .q.empty { opacity: .4; }
          #play { flex: 1; display: flex; align-items: flex-end; justify-content: space-between; gap: 10px; min-height: 0; padding-bottom: 22px; }
          #right { display: flex; flex-direction: column; align-items: center; gap: 20px; }
          .stick { position: relative; aspect-ratio: 1; border-radius: 50%; flex: none;
            background: radial-gradient(circle, rgba(232,220,196,.10), rgba(232,220,196,.04)); border: 2px solid rgba(232,220,196,.25); }
          #stick { width: min(34vw, 136px); }
          #look { width: min(19vw, 74px); }
          .knob { position: absolute; left: 50%; top: 50%; width: 38%; height: 38%; margin: -19% 0 0 -19%; border-radius: 50%;
            background: rgba(255, 210, 122, .55); border: 2px solid #ffd27a; pointer-events: none; }
          #look .knob { background: rgba(154, 200, 255, .5); border-color: #9ac8ff; }
          .stick span { position: absolute; left: 0; right: 0; bottom: -17px; text-align: center; font-size: 11px; opacity: .6; }
          #moves { display: grid; grid-template-columns: repeat(3, min(14.5vw, 58px)); grid-auto-rows: min(14.5vw, 58px); gap: 8px; }
          #pad button { background: rgba(58, 50, 38, .85); color: #e8dcc4; border: 1px solid rgba(232, 220, 196, .3);
            font: 600 14px -apple-system, system-ui, sans-serif; padding: 0 2px; touch-action: none; overflow: hidden; }
          #moves button { border-radius: 50%; line-height: 1.1; font-size: 13px; }
          #moves .big { background: rgba(140, 50, 36, .9); border-color: #ff9a7a; font-size: 15px; }
          #pad button.on { background: #ffd27a; color: #1b1813; }
          #pad button.lit { border: 2px solid #ffd27a; box-shadow: 0 0 10px #ffd27a; }
        </style>
        </head>
        <body>
        <canvas id="screen"></canvas>
        <div id="note">Connecting to the game…</div>
        <button id="keys" aria-label="Keyboard">⌨︎</button>
        <button id="snd" aria-label="Sound">🔇</button>
        <div id="probe"></div>
        <input id="type" autocapitalize="off" autocomplete="off" autocorrect="off" spellcheck="false">
        <div id="pad">
          <div id="top">
            <button id="menu">Menu ▾</button>
            <div id="drop">
              <button data-a="map">Map</button><button data-a="journal">Quests</button><button data-a="pack">Pack</button>
              <button data-a="party">Party</button><button data-a="skills">Skills</button><button data-a="drink">Heal</button>
              <button data-a="rest">Rest</button><button data-a="menu">World</button><button data-a="game_menu">Menu</button>
              <button id="keys2">⌨︎ Type</button>
            </div>
            <button class="q" data-a="quick_1"><b>1</b><i></i></button><button class="q" data-a="quick_2"><b>2</b><i></i></button><button class="q" data-a="quick_3"><b>3</b><i></i></button><button class="q" data-a="quick_4"><b>4</b><i></i></button><button class="q" data-a="quick_5"><b>5</b><i></i></button>
            <button class="q" data-a="quick_6"><b>6</b><i></i></button><button class="q" data-a="quick_7"><b>7</b><i></i></button><button class="q" data-a="quick_8"><b>8</b><i></i></button><button class="q" data-a="quick_9"><b>9</b><i></i></button><button class="q" data-a="quick_0"><b>0</b><i></i></button>
          </div>
          <div id="play">
            <div id="stick" class="stick"><div class="knob"></div><span>move</span></div>
            <div id="right">
              <div id="look" class="stick"><div class="knob"></div><span>camera</span></div>
              <div id="moves">
                <button data-a="ride">Ride</button><button data-a="descend">Dive</button><button data-a="jump">Jump</button>
                <button data-a="interact">Use</button><button data-a="limit">Limit</button><button data-a="cycle_element">Q</button>
                <button data-a="sprint" data-toggle="1">Run</button><button data-a="spell">Spell</button><button data-a="attack" class="big">Strike</button>
              </div>
            </div>
          </div>
        </div>
        <script>
        const key = new URLSearchParams(location.search).get("k") || "";
        function hold(el, e) { try { el.setPointerCapture(e.pointerId); } catch (_) { } }
        const canvas = document.getElementById("screen"), ctx = canvas.getContext("2d");
        const note = document.getElementById("note"), typer = document.getElementById("type");
        let socket = null, frame = { w: 16, h: 9 }, box = { x: 0, y: 0, w: 1, h: 1 }, wait = 500, upright = false, last = null;

        function say(text) { note.textContent = text; note.style.display = text ? "flex" : "none"; }
        function send(o) { if (socket && socket.readyState === 1) socket.send(typeof o === "string" ? o : JSON.stringify(o)); }

        // Sideways, the picture fills the screen. Upright, it sits at the top, full width, and the controller takes the rest.
        function fit() {
          const dpr = window.devicePixelRatio || 1;
          upright = innerHeight > innerWidth;
          document.body.classList.toggle("upright", upright);
          canvas.width = Math.round(innerWidth * dpr); canvas.height = Math.round(innerHeight * dpr);
          if (upright) {
            // The picture is shown a little bigger than the screen is wide, its edges cropped, so what's in it reads larger.
            const top = document.getElementById("probe").getBoundingClientRect().height || 0;
            const w = canvas.width * 1.15, h = w * frame.h / frame.w;
            box = { w, h, x: (canvas.width - w) / 2, y: top * dpr };
            document.documentElement.style.setProperty("--top", `${(box.y + box.h) / dpr}px`);
          } else {
            const s = Math.min(canvas.width / frame.w, canvas.height / frame.h);
            box = { w: frame.w * s, h: frame.h * s, x: (canvas.width - frame.w * s) / 2, y: (canvas.height - frame.h * s) / 2 };
          }
          if (last) ctx.drawImage(last, box.x, box.y, box.w, box.h);
          send({ t: "layout", upright });
        }
        addEventListener("resize", fit); fit();

        function connect() {
          say("Connecting to the game…");
          const proto = location.protocol === "https:" ? "wss:" : "ws:";
          socket = new WebSocket(`${proto}//${location.host}/ws?k=${encodeURIComponent(key)}`);
          socket.binaryType = "arraybuffer";
          socket.onopen = () => { wait = 500; say(""); send({ t: "hello", w: innerWidth, h: innerHeight }); send({ t: "layout", upright }); };
          socket.onmessage = async (e) => {
            if (typeof e.data === "string") {
              const m = JSON.parse(e.data);
              for (const k of [document.getElementById("keys"), document.getElementById("keys2")]) {
                if (m.keyboard === true) k.style.borderColor = "#f0c060";
                if (m.keyboard === false) k.style.borderColor = "";
              }
              if (m.pad) label(m.pad);
              return;
            }
            const head = new Uint8Array(e.data, 0, 1)[0];
            if (head === 0x41) { hear(new Uint8Array(e.data)); return; } // 'A': a moment of the game's sound
            try {
              const image = await createImageBitmap(new Blob([e.data], { type: "image/jpeg" }));
              if (image.width !== frame.w || image.height !== frame.h) { frame = { w: image.width, h: image.height }; fit(); }
              ctx.clearRect(0, 0, canvas.width, canvas.height);
              ctx.drawImage(image, box.x, box.y, box.w, box.h);
              if (last && last.close) last.close();
              last = image;
            } finally { send("drawn"); }
          };
          socket.onclose = () => { say("Lost the game. Reconnecting…"); setTimeout(connect, wait); wait = Math.min(wait * 2, 8000); };
        }
        connect();

        // The game's sound. A phone plays a page's sound only after a tap, so the first touch anywhere wakes it;
        // the speaker button turns it off and on. Each packet is mu-law (a byte a sample), played just after the
        // last, a little ahead, and if it falls behind it skips ahead rather than lagging.
        let audio = null, heardUntil = 0, soundOn = true;
        try { soundOn = localStorage.getItem("tristinwold-sound") !== "off"; } catch (_) {}
        const snd = document.getElementById("snd");
        const MU = new Float32Array(256);
        for (let i = 0; i < 256; i++) {
          const u = ~i & 0xff, exp = (u >> 4) & 7, s = ((((u & 0x0f) << 3) + 0x84) << exp) - 0x84;
          MU[i] = (u & 0x80 ? -s : s) / 32768;
        }
        function showSound() { snd.textContent = soundOn && audio && audio.state === "running" ? "🔊" : "🔇"; }
        function wake() {
          if (!soundOn) return;
          try {
            if (!audio) { audio = new (window.AudioContext || window.webkitAudioContext)(); audio.onstatechange = showSound; }
            if (audio.state !== "running") audio.resume().then(showSound, showSound);
          } catch (_) {}
          showSound();
        }
        document.addEventListener("pointerdown", wake, true);
        document.addEventListener("touchend", wake, true);
        snd.addEventListener("click", (e) => {
          e.stopPropagation();
          soundOn = !(soundOn && audio && audio.state === "running");
          try { localStorage.setItem("tristinwold-sound", soundOn ? "on" : "off"); } catch (_) {}
          if (soundOn) wake(); else if (audio) audio.suspend().then(showSound, showSound);
          showSound();
        });
        function hear(bytes) {
          if (!soundOn || !audio || audio.state !== "running" || bytes.length < 5) return;
          const rate = bytes[1] | (bytes[2] << 8) | (bytes[3] << 16);
          const buffer = audio.createBuffer(1, bytes.length - 4, rate);
          const data = buffer.getChannelData(0);
          for (let i = 4; i < bytes.length; i++) data[i - 4] = MU[bytes[i]];
          const now = audio.currentTime;
          if (heardUntil < now + 0.03 || heardUntil > now + 0.5) heardUntil = now + 0.12;
          const source = audio.createBufferSource();
          source.buffer = buffer;
          source.connect(audio.destination);
          source.start(heardUntil);
          heardUntil += buffer.duration;
        }

        // Touches on the picture (and a mouse, for trying it on a computer), as fractions of the game's picture.
        // Upright, a drag on the picture also turns the view, since the game's own look-pad is put away.
        function at(e) {
          const dpr = window.devicePixelRatio || 1;
          return { x: (e.clientX * dpr - box.x) / box.w, y: (e.clientY * dpr - box.y) / box.h };
        }
        const down = new Map();
        canvas.addEventListener("pointerdown", (e) => { e.preventDefault(); hold(canvas, e); down.set(e.pointerId, [e.clientX, e.clientY]); send({ t: "d", i: e.pointerId, ...at(e) }); });
        canvas.addEventListener("pointermove", (e) => {
          if (!down.has(e.pointerId)) return;
          e.preventDefault();
          send({ t: "m", i: e.pointerId, ...at(e) });
          if (upright) {
            const [px, py] = down.get(e.pointerId);
            send({ t: "look", x: e.clientX - px, y: e.clientY - py });
            down.set(e.pointerId, [e.clientX, e.clientY]);
          }
        });
        for (const ev of ["pointerup", "pointercancel"])
          canvas.addEventListener(ev, (e) => { if (!down.delete(e.pointerId)) return; e.preventDefault(); send({ t: "u", i: e.pointerId, ...at(e) }); });
        for (const ev of ["gesturestart", "gesturechange", "dblclick", "contextmenu"]) document.addEventListener(ev, (e) => e.preventDefault());

        // The controller: buttons press the game's own actions while held (Run stays on until tapped again).
        for (const b of document.querySelectorAll("#pad button[data-a]")) {
          const a = b.dataset.a;
          b.addEventListener("pointerdown", (e) => {
            e.preventDefault(); hold(b, e);
            if (b.dataset.toggle) { const on = !b.classList.contains("on"); b.classList.toggle("on", on); send({ t: "act", a, p: on }); return; }
            b.classList.add("on"); send({ t: "act", a, p: true });
          });
          for (const ev of ["pointerup", "pointercancel"])
            b.addEventListener(ev, (e) => { e.preventDefault(); if (b.closest("#drop")) drop.classList.remove("open"); if (b.dataset.toggle) return; b.classList.remove("on"); send({ t: "act", a, p: false }); });
        }
        // The Menu button drops the map, quests, pack and the rest down over the controller; a tap anywhere else puts them away.
        const menuButton = document.getElementById("menu"), drop = document.getElementById("drop");
        menuButton.addEventListener("pointerdown", (e) => { e.preventDefault(); e.stopPropagation(); drop.classList.toggle("open"); });
        document.addEventListener("pointerdown", (e) => { if (!drop.contains(e.target) && e.target !== menuButton) drop.classList.remove("open"); }, true);

        // The joysticks: how far and which way the thumb is from the middle, -1 to 1 each way. The big one walks;
        // the small one turns the camera, faster the further it's pushed, for as long as it's held.
        function joystick(id, kind) {
          const pad = document.getElementById(id), knob = pad.querySelector(".knob");
          let thumb = null;
          function steer(e) {
            const r = pad.getBoundingClientRect(), radius = r.width / 2;
            let x = (e.clientX - r.left - radius) / (radius * 0.7), y = (e.clientY - r.top - radius) / (radius * 0.7);
            const len = Math.hypot(x, y);
            if (len > 1) { x /= len; y /= len; }
            knob.style.transform = `translate(${x * radius * 0.7}px, ${y * radius * 0.7}px)`;
            send({ t: kind, x: +x.toFixed(2), y: +y.toFixed(2) });
          }
          pad.addEventListener("pointerdown", (e) => { e.preventDefault(); hold(pad, e); thumb = e.pointerId; steer(e); });
          pad.addEventListener("pointermove", (e) => { if (e.pointerId === thumb) { e.preventDefault(); steer(e); } });
          for (const ev of ["pointerup", "pointercancel"])
            pad.addEventListener(ev, (e) => { if (e.pointerId !== thumb) return; thumb = null; knob.style.transform = ""; send({ t: kind, x: 0, y: 0 }); });
        }
        joystick("stick", "stick");
        joystick("look", "pan");

        // The game says what the class's buttons are called now, and which are worth pressing (limit ready, someone to talk to).
        function label(pad) {
          for (const b of document.querySelectorAll("#moves button[data-a]")) {
            const a = b.dataset.a;
            if (pad.labels && pad.labels[a]) b.textContent = pad.labels[a];
            b.classList.toggle("lit", !!(pad.lit && pad.lit[a]));
            // A move that's on a quick key doesn't need a round button as well; the main attack always keeps its own.
            b.style.display = a !== "attack" && (pad.quick || []).some(q => q.n && q.n === b.textContent) ? "none" : "";
          }
          // The quick keys: what's on each, and which are empty.
          for (const q of pad.quick || []) {
            const b = document.querySelector(`#top button[data-a="quick_${q.k}"]`);
            if (!b) continue;
            b.querySelector("i").textContent = q.n || "";
            b.classList.toggle("empty", !!q.e);
          }
        }

        // Typing: the keyboard buttons open the phone's keyboard; letters go to whatever box has focus in the game.
        for (const id of ["keys", "keys2"]) document.getElementById(id).addEventListener("click", () => { drop.classList.remove("open"); typer.value = " "; typer.focus(); });
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
