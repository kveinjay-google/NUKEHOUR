# Faction Touch Controls Preview Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Generate three original faction-specific touch-control concepts and deliver a locally openable interactive webpage that compares their joystick, five-button command ring, and shortcut bar states.

**Architecture:** Keep generated concept art separate from deterministic interaction geometry. Three raster concept boards establish materials and visual language; one dependency-free HTML page reconstructs the controls with faction tokens, inline icon SVGs for the redesigned left controls, and the existing shortcut glyph atlas for the unchanged quickbar symbols. This plan stops at visual preview; production atlases and responsive game integration receive a separate plan after visual approval.

**Tech Stack:** Built-in `image_gen`, HTML5, CSS custom properties, vanilla JavaScript pointer events, Python `unittest` + Pillow, Playwright/Chromium visual verification.

**Repository constraint:** Do not stage or commit files without explicit user authorization. Every task ends with a verification checkpoint instead of a Git commit.

---

## File Structure

- Create `packaging/tests/test_faction_touch_controls_preview.py` — source and asset contract for the preview.
- Create `design-demos/faction-touch-controls/index.html` — semantic preview shell.
- Create `design-demos/faction-touch-controls/styles.css` — faction materials, geometry, states, and responsive rules.
- Create `design-demos/faction-touch-controls/app.js` — deterministic ring, joystick, comparison, and shortcut-bar interactions.
- Create `design-demos/faction-touch-controls/assets/allies-control-kit.png` — generated Allied concept board.
- Create `design-demos/faction-touch-controls/assets/soviet-control-kit.png` — generated Soviet concept board.
- Create `design-demos/faction-touch-controls/assets/yuri-control-kit.png` — generated Yuri concept board.
- Create `design-demos/faction-touch-controls/screenshots/overview-1440x900.png` — desktop comparison evidence.
- Create `design-demos/faction-touch-controls/screenshots/allies-844x390.png` — compact Allied evidence.
- Create `design-demos/faction-touch-controls/screenshots/soviet-844x390.png` — compact Soviet evidence.
- Create `design-demos/faction-touch-controls/screenshots/yuri-844x390.png` — compact Yuri evidence.

No file under `mods/`, `engine/`, `OpenRA.Mods.RA2/`, or `ios/` is modified by this plan.

### Task 1: Add a Preview Contract Before Creating Assets

**Files:**
- Create: `packaging/tests/test_faction_touch_controls_preview.py`

- [ ] **Step 1: Write the failing asset and page contract**

Create this test with `apply_patch`:

```python
import hashlib
import unittest
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
PREVIEW = ROOT / "design-demos" / "faction-touch-controls"
INDEX = PREVIEW / "index.html"
STYLES = PREVIEW / "styles.css"
SCRIPT = PREVIEW / "app.js"
FACTIONS = ("allies", "soviet", "yuri")


def preview_source():
    return "\n".join(path.read_text(encoding="utf-8") for path in (INDEX, STYLES, SCRIPT))


class FactionTouchControlsPreviewTest(unittest.TestCase):
    def test_three_distinct_concept_boards_exist(self):
        digests = set()
        for faction in FACTIONS:
            path = PREVIEW / "assets" / f"{faction}-control-kit.png"
            self.assertTrue(path.is_file(), path)
            with Image.open(path) as image:
                self.assertEqual("PNG", image.format, path)
                self.assertGreaterEqual(image.width, 1024, path)
                self.assertGreaterEqual(image.height, 768, path)
                image.verify()
            digests.add(hashlib.sha256(path.read_bytes()).hexdigest())
        self.assertEqual(3, len(digests), "faction concept boards must be visually distinct files")

    def test_preview_exposes_all_factions_sizes_states_and_actions(self):
        source = preview_source()
        for faction in FACTIONS:
            self.assertIn(f'data-faction="{faction}"', source)
            self.assertIn(f"assets/{faction}-control-kit.png", source)
        for size in ("112", "128", "144"):
            self.assertIn(f'data-size="{size}"', source)
        for state in ("normal", "pressed", "active", "disabled"):
            self.assertIn(f'data-state="{state}"', source)
        for action in ("stop", "deploy", "select-type", "force-attack", "return-base"):
            self.assertIn(f'"{action}"', source)

    def test_shortcut_symbols_are_reused_but_left_controls_are_redesigned(self):
        source = preview_source()
        self.assertIn("../../mods/ra2/uibits/ios-commandbar-icons-v2.png", source)
        self.assertIn("../../mods/ra2/uibits/production-x5-icon.png", source)
        self.assertNotIn("ios-joystick-command-icons.png", source)
        self.assertNotIn("ios-viewport-joystick.png", source)
        self.assertIn("action-glyph", source)

    def test_preview_contains_deterministic_ring_and_pointer_interaction(self):
        source = preview_source()
        self.assertIn("const RING_ANGLES = [-150, -115, -80, -45, -10]", source)
        self.assertIn("setPointerCapture", source)
        self.assertIn("Math.hypot", source)
        self.assertIn("renderActionRing", source)
        self.assertIn("toggleQuickbar", source)


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run the contract and confirm the expected RED**

Run:

```bash
python3 -m unittest packaging.tests.test_faction_touch_controls_preview -v
```

Expected: four errors/failures because `design-demos/faction-touch-controls/index.html` and the three generated PNG files do not exist.

- [ ] **Step 3: Confirm the test is isolated from runtime resources**

Run:

```bash
git diff --name-only -- packaging/tests/test_faction_touch_controls_preview.py mods engine ios OpenRA.Mods.RA2
```

Expected: only `packaging/tests/test_faction_touch_controls_preview.py` appears among new work from this plan; pre-existing dirty runtime files remain untouched.

### Task 2: Generate the Three Faction Concept Boards

**Files:**
- Create: `design-demos/faction-touch-controls/assets/allies-control-kit.png`
- Create: `design-demos/faction-touch-controls/assets/soviet-control-kit.png`
- Create: `design-demos/faction-touch-controls/assets/yuri-control-kit.png`

- [ ] **Step 1: Generate the Allied concept board with the built-in image tool**

Call `image_gen` once with no reference image and this prompt:

```text
Use case: ui-mockup
Asset type: high-fidelity game HUD control-kit concept board
Primary request: Design one original ALLIED faction mobile RTS touch-control kit, viewed perfectly front-on with orthographic UI presentation. Include one large circular virtual joystick base and thumb, five separate command buttons for stop, deploy, select same type, force attack, and return to base, one long empty expandable shortcut bar with nine equal slots, and one collapse/expand control. Show small normal, pressed, active, and disabled state examples without repeating the whole kit.
Scene/backdrop: transparent background if possible; otherwise a flat neutral charcoal studio board with generous separation between every component.
Style/medium: premium hand-painted game UI, aerospace precision, deep navy composite, cool silver aluminum, ceramic white markings, restrained cyan status lights, tiny amber danger accent.
Composition/framing: clean asset-sheet grid, every component isolated, front-facing, no perspective, no overlap, consistent scale, safe margins.
Materials/textures: matte aerospace composite, machined aluminum, fine instrument etching, subtle glass only on indicators.
Constraints: strong silhouettes at small size; no text; no numbers; no faction logo; no company logo; no characters; no weapons outside the five semantic icons; no watermark; no purple gradient; no generic sci-fi hologram panel.
Avoid: perspective mockup, floating phones, labels, random extra buttons, inconsistent lighting, illegible micro-detail.
```

Inspect the result with `view_image`. Accept only if all requested component groups are visibly separate and no text/logo/watermark appears. Copy the accepted generated file into `design-demos/faction-touch-controls/assets/allies-control-kit.png` using `ditto`.

- [ ] **Step 2: Generate the Soviet concept board independently**

Call `image_gen` again with no prior generated image included:

```text
Use case: ui-mockup
Asset type: high-fidelity game HUD control-kit concept board
Primary request: Design one original SOVIET faction mobile RTS touch-control kit, viewed perfectly front-on with orthographic UI presentation. Include one large circular virtual joystick base and thumb, five separate command buttons for stop, deploy, select same type, force attack, and return to base, one long empty expandable shortcut bar with nine equal slots, and one collapse/expand control. Show small normal, pressed, active, and disabled state examples without repeating the whole kit.
Scene/backdrop: transparent background if possible; otherwise a flat neutral charcoal studio board with generous separation between every component.
Style/medium: premium hand-painted game UI, heavy industrial military construction, dark gunmetal and black steel, worn deep-red enamel, thick octagonal framing, restrained bolts, amber warning lamps.
Composition/framing: clean asset-sheet grid, every component isolated, front-facing, no perspective, no overlap, consistent scale, safe margins.
Materials/textures: stamped steel, armored ribs, recessed mechanical switches, believable wear confined to edges.
Constraints: strong silhouettes at small size; no text; no numbers; no faction logo; no company logo; no characters; no weapons outside the five semantic icons; no watermark; button feedback must feel mechanically deep rather than neon.
Avoid: steampunk gears everywhere, excessive rust, perspective mockup, labels, random extra buttons, illegible micro-detail.
```

Inspect with `view_image`, then copy the accepted output to `design-demos/faction-touch-controls/assets/soviet-control-kit.png`.

- [ ] **Step 3: Generate the Yuri concept board independently**

Call `image_gen` a third time with no prior generated image included:

```text
Use case: ui-mockup
Asset type: high-fidelity game HUD control-kit concept board
Primary request: Design one original YURI faction mobile RTS touch-control kit, viewed perfectly front-on with orthographic UI presentation. Include one large circular virtual joystick base and thumb, five separate command buttons for stop, deploy, select same type, force attack, and return to base, one long empty expandable shortcut bar with nine equal slots, and one collapse/expand control. Show small normal, pressed, active, and disabled state examples without repeating the whole kit.
Scene/backdrop: transparent background if possible; otherwise a flat neutral charcoal studio board with generous separation between every component.
Style/medium: premium hand-painted game UI, psychic biotech and obsidian crystal, near-black violet material, dark silver structure, asymmetric segmented rings, localized magenta energy and very small toxic-green status accents.
Composition/framing: clean asset-sheet grid, every component isolated, front-facing, no perspective, no overlap, consistent scale, safe margins.
Materials/textures: polished obsidian, faceted mineral crystal, semi-organic armor seams, subtle psychic filaments localized to active areas.
Constraints: strong silhouettes at small size; no text; no numbers; no faction logo; no company logo; no characters; no weapons outside the five semantic icons; no watermark; purple is a faction material color, not a full-canvas SaaS gradient.
Avoid: generic cyberpunk neon, magic fantasy runes, tentacles, perspective mockup, labels, random extra buttons, illegible micro-detail.
```

Inspect with `view_image`, then copy the accepted output to `design-demos/faction-touch-controls/assets/yuri-control-kit.png`.

- [ ] **Step 4: Run the image-only contract**

Run:

```bash
python3 -m unittest packaging.tests.test_faction_touch_controls_preview.FactionTouchControlsPreviewTest.test_three_distinct_concept_boards_exist -v
```

Expected: `OK`; all three PNGs decode, are at least 1024×768, and have distinct byte hashes.

### Task 3: Build the Dependency-Free Interactive Preview

**Files:**
- Create: `design-demos/faction-touch-controls/index.html`
- Create: `design-demos/faction-touch-controls/styles.css`
- Create: `design-demos/faction-touch-controls/app.js`

- [ ] **Step 1: Create the semantic page shell and faction controls**

Use `apply_patch` to create `index.html` with this complete shell:

```html
<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
  <title>三阵营触控控制皮肤预览</title>
  <link rel="stylesheet" href="styles.css">
</head>
<body data-faction="allies" data-size="128" data-state="normal">
  <header class="review-header">
    <div><p class="eyebrow">NUCLEAR CRISIS · TOUCH CONTROL STUDY</p><h1>三阵营触控控制系统</h1></div>
    <nav class="segmented" aria-label="阵营">
      <button data-faction="allies" aria-pressed="true">盟军</button>
      <button data-faction="soviet" aria-pressed="false">苏军</button>
      <button data-faction="yuri" aria-pressed="false">尤里</button>
      <button data-faction="compare" aria-pressed="false">并排比较</button>
    </nav>
  </header>
  <main>
    <section class="stage" aria-label="交互式 HUD 预览">
      <div class="battlefield" id="battlefield">
        <div class="control-cluster" id="controlCluster">
          <div class="joystick" id="joystick" role="application" aria-label="虚拟摇杆">
            <div class="joystick-track"></div><div class="joystick-thumb" id="joystickThumb"></div>
          </div>
          <div class="action-ring" id="actionRing" aria-label="左下快捷操作"></div>
        </div>
        <div class="quickbar" id="quickbar" aria-label="快捷按键栏"></div>
      </div>
      <aside class="review-controls">
        <div class="control-group size-control"><button data-size="112">小</button><button data-size="128">中</button><button data-size="144">大</button></div>
        <div class="control-group state-control"><button data-state="normal">正常</button><button data-state="pressed">按下</button><button data-state="active">激活</button><button data-state="disabled">禁用</button></div>
        <button class="quickbar-toggle" type="button">收起快捷栏</button>
      </aside>
    </section>
    <section class="concept-board"><img id="conceptImage" src="assets/allies-control-kit.png" alt="当前阵营控制套件概念板"></section>
    <section class="comparison" id="comparison" hidden></section>
  </main>
  <script src="app.js"></script>
</body>
</html>
```

- [ ] **Step 2: Implement faction tokens and control geometry**

Create `styles.css` with these complete faction tokens, geometry, feedback states, and compact rules:

```css
* { box-sizing:border-box; }
:root {
  color-scheme:dark; --joy-size:128px; --button-size:56px; --ring-radius:127px;
  --safe-top:max(12px, env(safe-area-inset-top)); --safe-right:max(12px, env(safe-area-inset-right));
  --safe-bottom:max(12px, env(safe-area-inset-bottom)); --safe-left:max(12px, env(safe-area-inset-left));
  font-family:Inter, ui-sans-serif, system-ui, -apple-system, sans-serif;
}
body { margin:0; min-height:100vh; color:#f5f1e8; background:#080a0d; }
button { min-width:44px; min-height:44px; color:inherit; font:inherit; cursor:pointer; }
body[data-faction="allies"] { --metal:#8ba4b5; --shell:#13283a; --accent:#8fe7ff; --danger:#f3a23f; --shape:18px; --grain:linear-gradient(145deg,#a7bac6,#405768 34%,#14293a 36% 74%,#7993a6); }
body[data-faction="soviet"] { --metal:#716b64; --shell:#241a19; --accent:#f0a43c; --danger:#d93428; --shape:8px; --grain:linear-gradient(145deg,#878077,#342f2b 28%,#211717 30% 72%,#7a241f); }
body[data-faction="yuri"] { --metal:#756780; --shell:#160f1d; --accent:#cf56dc; --danger:#9dcc62; --shape:22px 8px 22px 8px; --grain:linear-gradient(145deg,#786782,#2a1735 30%,#110b18 32% 72%,#56245d); }
.review-header { min-height:96px; display:flex; align-items:end; justify-content:space-between; gap:24px; padding:20px 28px; border-bottom:1px solid #282d33; background:#0d1014; }
.eyebrow { margin:0 0 6px; color:#9199a2; font:600 11px/1.2 ui-monospace, monospace; letter-spacing:.16em; }
h1 { margin:0; font-size:clamp(24px,3vw,42px); line-height:1; }
.segmented,.control-group { display:flex; gap:6px; flex-wrap:wrap; }
.segmented button,.control-group button,.quickbar-toggle { border:1px solid #3b4148; border-radius:6px; padding:8px 14px; background:#161a1f; }
.segmented button[aria-pressed="true"],.control-group button[aria-pressed="true"] { border-color:var(--accent); color:var(--accent); background:color-mix(in srgb,var(--accent) 14%,#111); }
main { display:grid; grid-template-columns:minmax(0,1fr) minmax(280px,34vw); min-height:calc(100vh - 96px); }
.stage { position:relative; min-width:0; background:#090c10; }
.battlefield { position:relative; min-height:620px; overflow:hidden; isolation:isolate; background:radial-gradient(circle at 45% 48%,#28352e 0 2%,transparent 22%),linear-gradient(135deg,#243128,#111a18 58%,#101418); }
.battlefield::before { content:""; position:absolute; inset:0; opacity:.25; background-image:linear-gradient(#fff1 1px,transparent 1px),linear-gradient(90deg,#fff1 1px,transparent 1px); background-size:32px 32px; }
.control-cluster { position:absolute; z-index:2; left:calc(var(--safe-left) + 168px); bottom:calc(var(--safe-bottom) + 74px); width:var(--joy-size); height:var(--joy-size); }
.joystick { position:absolute; inset:0; width:var(--joy-size); height:var(--joy-size); border:4px solid var(--metal); border-radius:50%; touch-action:none; background:var(--grain); box-shadow:0 10px 28px #000c,inset 0 0 0 7px #0008,inset 0 0 24px var(--accent); }
body[data-faction="soviet"] .joystick { border-radius:24%; clip-path:polygon(24% 0,76% 0,100% 24%,100% 76%,76% 100%,24% 100%,0 76%,0 24%); }
body[data-faction="yuri"] .joystick { border-radius:46% 54% 38% 62%/58% 40% 60% 42%; transform:rotate(-8deg); }
.joystick-track { position:absolute; inset:18%; border:2px solid color-mix(in srgb,var(--accent) 62%,transparent); border-radius:inherit; }
.joystick-thumb { position:absolute; left:50%; top:50%; width:42%; aspect-ratio:1; translate:-50% -50%; border:3px solid var(--metal); border-radius:50%; background:radial-gradient(circle at 35% 28%,#fff8,var(--accent) 7%,var(--shell) 42%,#050608 78%); box-shadow:0 7px 14px #000b,0 0 12px color-mix(in srgb,var(--accent) 48%,transparent); }
.action-ring { position:absolute; inset:0; overflow:visible; pointer-events:none; }
.action-button { position:absolute; width:var(--button-size); height:var(--button-size); padding:0; border:0; background:none; pointer-events:auto; touch-action:manipulation; }
.action-face { display:grid; place-items:center; width:100%; height:100%; color:#f7f2e8; border:3px solid var(--metal); border-radius:var(--shape); background:var(--grain); box-shadow:0 7px 12px #000b,inset 0 0 0 2px #0007; transition:filter 90ms,translate 90ms,box-shadow 90ms; }
body[data-faction="soviet"] .action-face { clip-path:polygon(16% 0,84% 0,100% 16%,100% 84%,84% 100%,16% 100%,0 84%,0 16%); }
body[data-faction="yuri"] .action-face { border-radius:28% 54% 32% 48%; }
.action-glyph { width:30px; height:30px; fill:none; stroke:currentColor; stroke-width:2.2; stroke-linecap:round; stroke-linejoin:round; }
.action-button:hover .action-face { color:var(--accent); filter:brightness(1.16); }
.action-button:active .action-face,.action-face[data-preview-state="pressed"] { translate:0 3px; filter:brightness(.78); box-shadow:0 2px 5px #000d,inset 0 5px 12px #000a; }
.action-button.is-active .action-face,.action-face[data-preview-state="active"] { color:var(--accent); border-color:var(--accent); box-shadow:0 0 18px color-mix(in srgb,var(--accent) 68%,transparent),inset 0 0 12px color-mix(in srgb,var(--accent) 35%,transparent); }
.action-face[data-preview-state="disabled"] { opacity:.38; filter:grayscale(.8); }
.quickbar { position:absolute; z-index:3; left:50%; bottom:var(--safe-bottom); translate:-50% 0; display:flex; gap:5px; padding:8px 12px; min-height:60px; border:3px solid var(--metal); border-radius:var(--shape); background:var(--grain); box-shadow:0 8px 24px #000c,inset 0 0 0 2px #0008; }
.quickbar.is-collapsed .shortcut-slot:nth-child(n+3) { display:none; }
.shortcut-slot { display:grid; place-items:center; width:44px; height:44px; border:1px solid color-mix(in srgb,var(--metal) 72%,#000); background:#08090bdd; }
.shortcut-glyph { width:26px; height:26px; background-image:url("../../mods/ra2/uibits/ios-commandbar-icons-v2.png"); background-repeat:no-repeat; background-position:calc(var(--sprite-x) * -1px) -3px; image-rendering:auto; }
.shortcut-slot img { width:32px; height:32px; object-fit:contain; }
.review-controls { position:absolute; z-index:4; top:16px; right:16px; width:min(240px,42%); display:grid; gap:10px; padding:12px; border:1px solid #394149; background:#090c10e8; backdrop-filter:blur(10px); }
.concept-board { min-width:0; padding:18px; border-left:1px solid #282d33; background:#101318; }
.concept-board img { display:block; width:100%; height:auto; max-height:calc(100vh - 132px); object-fit:contain; }
.comparison { grid-column:1/-1; display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:14px; padding:18px; background:#0b0e12; }
.comparison[hidden] { display:none; }
.comparison-card { min-width:0; padding:14px; border:1px solid #353b42; background:#11151a; }
.comparison-card img { display:block; width:100%; aspect-ratio:4/3; object-fit:contain; background:#090b0e; }
.comparison-card h2 { margin:12px 0 0; font-size:18px; }
@media (max-width:900px) {
  .review-header { min-height:64px; align-items:center; padding:8px 12px; }
  .eyebrow,.concept-board,.comparison { display:none !important; }
  h1 { font-size:18px; }
  main { display:block; min-height:calc(100vh - 64px); }
  .battlefield { min-height:calc(100vh - 64px); }
  .review-controls { top:8px; right:var(--safe-right); width:auto; max-width:48%; grid-template-columns:repeat(2,auto); }
  .state-control { display:none; }
  .control-cluster { left:calc(var(--safe-left) + var(--ring-radius) - 12px); bottom:calc(var(--safe-bottom) + 76px); }
  .quickbar { max-width:calc(100vw - var(--safe-left) - var(--safe-right) - 16px); overflow:hidden; }
}
@media (max-height:500px) {
  .review-header { position:absolute; z-index:8; left:0; right:0; background:linear-gradient(#080a0df2,transparent); border:0; pointer-events:none; }
  .review-header nav { pointer-events:auto; }
  main,.battlefield { min-height:100vh; }
  .review-controls { top:58px; }
}
```

- [ ] **Step 3: Implement deterministic ring, joystick dragging, states, and quickbar**

Create `app.js` with the complete deterministic interaction code below. The left-control glyphs are new inline vector primitives; the quickbar continues to crop the existing runtime atlas without recoloring or reordering it.

```js
const RING_ANGLES = [-150, -115, -80, -45, -10];
const ACTIONS = ["stop", "deploy", "select-type", "force-attack", "return-base"];
const FACTIONS = ["allies", "soviet", "yuri"];
const FACTION_LABELS = { allies:"盟军", soviet:"苏军", yuri:"尤里" };
const CONCEPT_IMAGES = {
  allies:"assets/allies-control-kit.png",
  soviet:"assets/soviet-control-kit.png",
  yuri:"assets/yuri-control-kit.png"
};
const SHORTCUTS = [
  ["group-1", 3], ["group-2", 35], ["group-3", 67], ["group-4", 99],
  ["group-5", 131], ["production-x5", null], ["attack-move", 163],
  ["scatter", 195], ["queue-orders", 227]
];
const GLYPHS = {
  "stop": '<svg class="action-glyph" aria-hidden="true" viewBox="0 0 32 32"><path vector-effect="non-scaling-stroke" d="M9 5h14l4 4v14l-4 4H9l-4-4V9z"/><path vector-effect="non-scaling-stroke" d="M11 11h10v10H11z"/></svg>',
  "deploy": '<svg class="action-glyph" aria-hidden="true" viewBox="0 0 32 32"><path vector-effect="non-scaling-stroke" d="M16 4v18M9 15l7 7 7-7M6 27h20"/></svg>',
  "select-type": '<svg class="action-glyph" aria-hidden="true" viewBox="0 0 32 32"><path vector-effect="non-scaling-stroke" d="M6 12V6h6M20 6h6v6M26 20v6h-6M12 26H6v-6"/><circle vector-effect="non-scaling-stroke" cx="16" cy="16" r="5"/></svg>',
  "force-attack": '<svg class="action-glyph" aria-hidden="true" viewBox="0 0 32 32"><circle vector-effect="non-scaling-stroke" cx="16" cy="16" r="10"/><circle vector-effect="non-scaling-stroke" cx="16" cy="16" r="4"/><path vector-effect="non-scaling-stroke" d="M16 2v7M16 23v7M2 16h7M23 16h7"/></svg>',
  "return-base": '<svg class="action-glyph" aria-hidden="true" viewBox="0 0 32 32"><path vector-effect="non-scaling-stroke" d="M4 15 16 5l12 10M8 13v14h16V13M12 27v-8h8v8"/><path vector-effect="non-scaling-stroke" d="M5 8v6h6"/></svg>'
};

const body = document.body;
const ring = document.getElementById("actionRing");
const joystick = document.getElementById("joystick");
const thumb = document.getElementById("joystickThumb");
const quickbar = document.getElementById("quickbar");
const comparison = document.getElementById("comparison");
const conceptImage = document.getElementById("conceptImage");

function ringRadius(joystickSize) {
  return Math.round(joystickSize * 0.68 + 40);
}

function renderActionRing() {
  const size = Number(body.dataset.size);
  const radius = ringRadius(size);
  document.documentElement.style.setProperty("--joy-size", `${size}px`);
  document.documentElement.style.setProperty("--ring-radius", `${radius}px`);
  ring.replaceChildren();
  ACTIONS.forEach((action, index) => {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "action-button";
    button.dataset.action = action;
    button.setAttribute("aria-label", action);
    button.innerHTML = `<span class="action-face">${GLYPHS[action]}</span>`;
    const radians = RING_ANGLES[index] * Math.PI / 180;
    button.style.left = `${size / 2 - 28}px`;
    button.style.top = `${size / 2 - 28}px`;
    button.style.transform = `translate(${Math.cos(radians) * radius}px, ${Math.sin(radians) * radius}px)`;
    button.addEventListener("click", () => activateAction(button));
    ring.append(button);
  });
  applyPreviewState(body.dataset.state);
}

function clampJoystick(pointerX, pointerY) {
  const box = joystick.getBoundingClientRect();
  const dx = pointerX - (box.left + box.width / 2);
  const dy = pointerY - (box.top + box.height / 2);
  const distance = Math.hypot(dx, dy);
  const limit = box.width * 50 / 112;
  const scale = distance > limit ? limit / distance : 1;
  thumb.style.transform = `translate(${dx * scale}px, ${dy * scale}px)`;
}

function toggleQuickbar() {
  quickbar.classList.toggle("is-collapsed");
  document.querySelector(".quickbar-toggle").textContent = quickbar.classList.contains("is-collapsed") ? "展开快捷栏" : "收起快捷栏";
}

function activateAction(button) {
  const force = ring.querySelector('[data-action="force-attack"]');
  if (button === force)
    force.classList.toggle("is-active");
  else
    force?.classList.remove("is-active");
}

function applyPreviewState(state) {
  body.dataset.state = state;
  ring.querySelectorAll(".action-face").forEach(face => {
    face.dataset.previewState = state;
  });
  document.querySelectorAll("[data-state]").forEach(button => {
    button.setAttribute("aria-pressed", String(button.dataset.state === state));
  });
}

function renderQuickbar() {
  quickbar.replaceChildren();
  SHORTCUTS.forEach(([name, spriteX]) => {
    const slot = document.createElement("button");
    slot.type = "button";
    slot.className = "shortcut-slot";
    slot.setAttribute("aria-label", name);
    if (name === "production-x5") {
      const image = document.createElement("img");
      image.src = "../../mods/ra2/uibits/production-x5-icon.png";
      image.alt = "X5";
      slot.append(image);
    } else {
      const glyph = document.createElement("span");
      glyph.className = "shortcut-glyph";
      glyph.style.setProperty("--sprite-x", spriteX);
      slot.append(glyph);
    }
    quickbar.append(slot);
  });
}

function showFaction(faction) {
  if (!FACTIONS.includes(faction)) return;
  body.dataset.faction = faction;
  conceptImage.src = CONCEPT_IMAGES[faction];
  document.querySelector(".stage").hidden = false;
  document.querySelector(".concept-board").hidden = false;
  comparison.hidden = true;
  document.querySelectorAll("[data-faction]").forEach(button => {
    button.setAttribute("aria-pressed", String(button.dataset.faction === faction));
  });
}

function renderComparison() {
  if (matchMedia("(max-width:900px)").matches) return;
  comparison.replaceChildren();
  FACTIONS.forEach(faction => {
    const card = document.createElement("article");
    card.className = "comparison-card";
    card.dataset.faction = faction;
    card.innerHTML = `<img src="${CONCEPT_IMAGES[faction]}" alt="${FACTION_LABELS[faction]}控制套件"><h2>${FACTION_LABELS[faction]}</h2>`;
    comparison.append(card);
  });
  document.querySelector(".stage").hidden = true;
  document.querySelector(".concept-board").hidden = true;
  comparison.hidden = false;
  document.querySelectorAll("[data-faction]").forEach(button => {
    button.setAttribute("aria-pressed", String(button.dataset.faction === "compare"));
  });
}

document.querySelectorAll("nav [data-faction]").forEach(button => {
  button.addEventListener("click", () => button.dataset.faction === "compare" ? renderComparison() : showFaction(button.dataset.faction));
});
document.querySelectorAll("[data-size]").forEach(button => {
  button.addEventListener("click", () => {
    body.dataset.size = button.dataset.size;
    document.querySelectorAll("[data-size]").forEach(item => item.setAttribute("aria-pressed", String(item === button)));
    renderActionRing();
  });
});
document.querySelectorAll("[data-state]").forEach(button => button.addEventListener("click", () => applyPreviewState(button.dataset.state)));
document.querySelector(".quickbar-toggle").addEventListener("click", toggleQuickbar);

let joystickPointer = null;
joystick.addEventListener("pointerdown", event => {
  joystickPointer = event.pointerId;
  joystick.setPointerCapture(event.pointerId);
  clampJoystick(event.clientX, event.clientY);
});
joystick.addEventListener("pointermove", event => {
  if (event.pointerId === joystickPointer) clampJoystick(event.clientX, event.clientY);
});
function releaseJoystick(event) {
  if (event.pointerId !== joystickPointer) return;
  joystickPointer = null;
  thumb.style.transform = "translate(0,0)";
}
joystick.addEventListener("pointerup", releaseJoystick);
joystick.addEventListener("pointercancel", releaseJoystick);

renderQuickbar();
renderActionRing();
showFaction("allies");
```

- [ ] **Step 4: Add the comparison mode and responsive rules**

Comparison mode renders three `.comparison-card` elements with the concept board and compact control reconstruction for each faction. At widths below 900px, hide comparison mode and keep a single active faction. At 844×390, keep the control cluster and quickbar fully within `env(safe-area-inset-*)`; reduce review chrome before shrinking the HUD. The preview must preserve 44px minimum controls even when the page around it scrolls.

- [ ] **Step 5: Run the complete source contract to reach GREEN**

Run:

```bash
python3 -m unittest packaging.tests.test_faction_touch_controls_preview -v
```

Expected: four tests pass.

### Task 4: Verify Interaction, Responsive Layout, and Visual Quality

**Files:**
- Create: `design-demos/faction-touch-controls/screenshots/overview-1440x900.png`
- Create: `design-demos/faction-touch-controls/screenshots/allies-844x390.png`
- Create: `design-demos/faction-touch-controls/screenshots/soviet-844x390.png`
- Create: `design-demos/faction-touch-controls/screenshots/yuri-844x390.png`
- Modify only if verification fails: `design-demos/faction-touch-controls/index.html`
- Modify only if verification fails: `design-demos/faction-touch-controls/styles.css`
- Modify only if verification fails: `design-demos/faction-touch-controls/app.js`

- [ ] **Step 1: Run static HTML verification**

Run:

```bash
python3 /Users/kevin/.codex/skills/huashu-design/scripts/verify.py design-demos/faction-touch-controls/index.html --viewports 1440x900,844x390,1133x744 --output design-demos/faction-touch-controls/screenshots
```

Expected: page loads in all three viewports, captures screenshots, and reports no JavaScript console or page errors.

- [ ] **Step 2: Perform real click and drag checks in Chromium**

Open the file through the Playwright skill wrapper. Exercise this exact sequence:

1. Select Soviet; assert `body[data-faction="soviet"]` and the Soviet concept image are active.
2. Select Yuri; assert the Yuri palette and concept image are active.
3. Select sizes 112, 128, 144; assert the joystick width and ring radius increase monotonically.
4. Drag the joystick beyond its base; assert the thumb remains clamped inside the circular travel radius.
5. Press each of the five command buttons; assert only one button renders Pressed at a time.
6. Toggle force attack Active, then click another action target; assert Active clears.
7. Collapse and expand the quickbar; assert the glyph order does not change.
8. Enter comparison mode at 1440×900; assert exactly three faction cards are visible.

Expected: every assertion succeeds and `pageerror` remains empty.

- [ ] **Step 3: Inspect screenshots visually**

Use `view_image` on the overview and all three compact screenshots. Reject and revise the page if any of the following occur:

- action-button centers do not visibly form one circular arc;
- faction silhouettes are distinguishable only by color;
- existing quickbar glyphs are blurred, stretched, recolored beyond recognition, or reordered;
- labels or controls overlap at 844×390;
- generated boards contain text, logos, watermarks, perspective distortion, or missing component groups;
- Pressed and Active look identical;
- Yuri becomes a generic full-screen purple gradient;
- Soviet texture obscures glyphs or Allied lighting becomes a generic cyan hologram.

- [ ] **Step 4: Run final checks and report the exact preview path**

Run:

```bash
python3 -m unittest packaging.tests.test_faction_touch_controls_preview -v
git diff --check -- packaging/tests/test_faction_touch_controls_preview.py design-demos/faction-touch-controls docs/superpowers/specs/2026-08-24-faction-touch-controls-preview-design.md docs/superpowers/plans/2026-08-24-faction-touch-controls-preview.md
git status --short -- packaging/tests/test_faction_touch_controls_preview.py design-demos/faction-touch-controls docs/superpowers/specs/2026-08-24-faction-touch-controls-preview-design.md docs/superpowers/plans/2026-08-24-faction-touch-controls-preview.md
```

Expected: tests pass, diff check is clean, and the status list contains only the two documents, one test, the preview HTML/CSS/JavaScript, three concept PNGs, and screenshot evidence from this phase.

Open `design-demos/faction-touch-controls/index.html` in the browser and provide the absolute clickable path plus the overview screenshot to the user. Ask for visual approval before creating the production runtime-integration plan.
