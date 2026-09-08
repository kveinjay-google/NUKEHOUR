const RING_ANGLES = [-150, -115, -80, -45, -10];
const RING_RADIUS_RATIO = 127 / 128;
const COMPACT_MEDIA_QUERY = "(max-width: 900px), (max-height: 500px)";
const ACTIONS = ["stop", "deploy", "select-type", "force-attack", "return-base"];
const FACTIONS = ["allies", "soviet", "yuri"];
const FACTION_LABELS = { allies: "盟军", soviet: "苏军", yuri: "尤里" };
const SIZE_LABELS = { "112": "小号", "128": "中号", "144": "大号" };
const STATE_LABELS = { normal: "正常状态", pressed: "按下状态", active: "持续激活预览", disabled: "禁用状态" };
const ACTION_LABELS = {
  "stop": "停止", "deploy": "部署", "select-type": "选择同类",
  "force-attack": "强制攻击", "return-base": "返回基地"
};
const CONCEPT_IMAGES = Object.freeze({ allies: "assets/allies-control-kit.png", soviet: "assets/soviet-control-kit.png", yuri: "assets/yuri-control-kit.png" });
const SHORTCUTS = [
  { action: "group-1", label: "编队一", spriteX: 3 },
  { action: "group-2", label: "编队二", spriteX: 35 },
  { action: "group-3", label: "编队三", spriteX: 67 },
  { action: "group-4", label: "编队四", spriteX: 99 },
  { action: "group-5", label: "编队五", spriteX: 131 },
  { action: "production-x5", label: "五倍生产", image: "../../mods/ra2/uibits/production-x5-icon.png" },
  { action: "attack-move", label: "攻击移动", spriteX: 163 },
  { action: "scatter", label: "分散", spriteX: 195 },
  { action: "queue-orders", label: "队列命令", spriteX: 227 }
];
const GLYPHS = {
  "stop": '<svg class="action-glyph" aria-hidden="true" viewBox="0 0 32 32"><path vector-effect="non-scaling-stroke" d="M9 5h14l4 4v14l-4 4H9l-4-4V9z"/><path vector-effect="non-scaling-stroke" d="M11 11h10v10H11z"/></svg>',
  "deploy": '<svg class="action-glyph" aria-hidden="true" viewBox="0 0 32 32"><path vector-effect="non-scaling-stroke" d="M16 4v17M9 14l7 7 7-7M6 27h20M9 23v4M23 23v4"/></svg>',
  "select-type": '<svg class="action-glyph" aria-hidden="true" viewBox="0 0 32 32"><circle vector-effect="non-scaling-stroke" cx="10" cy="16" r="4"/><circle vector-effect="non-scaling-stroke" cx="22" cy="10" r="4"/><circle vector-effect="non-scaling-stroke" cx="22" cy="23" r="4"/><path vector-effect="non-scaling-stroke" d="M14 15l4-3M14 18l4 3"/></svg>',
  "force-attack": '<svg class="action-glyph" aria-hidden="true" viewBox="0 0 32 32"><circle vector-effect="non-scaling-stroke" cx="16" cy="16" r="10"/><circle vector-effect="non-scaling-stroke" cx="16" cy="16" r="4"/><path vector-effect="non-scaling-stroke" d="M16 2v7M16 23v7M2 16h7M23 16h7"/></svg>',
  "return-base": '<svg class="action-glyph" aria-hidden="true" viewBox="0 0 32 32"><path vector-effect="non-scaling-stroke" d="M8 14 16 7l8 7v13H8zM13 27v-7h6v7M4 9v7h7"/><path vector-effect="non-scaling-stroke" d="m4 16 5-5"/></svg>'
};

const body = document.body, ring = document.getElementById("actionRing");
const joystick = document.getElementById("joystick"), thumb = document.getElementById("joystickThumb");
const quickbar = document.getElementById("quickbar"), quickbarToggle = document.querySelector(".quickbar-toggle");
const stage = document.getElementById("stage"), conceptPanel = document.getElementById("conceptPanel");
const conceptImage = document.getElementById("conceptImage"), conceptTitle = document.getElementById("conceptTitle");
const comparison = document.getElementById("comparison"), interactionStatus = document.getElementById("interactionStatus");
const joystickValue = document.getElementById("joystickValue");
const compactViewport = window.matchMedia(COMPACT_MEDIA_QUERY);

let activeFaction = "allies", forceAttackActive = false, activePointerId = null;
let keyboardX = 0, keyboardY = 0;

function ringRadius(joystickSize) {
  return Math.round(joystickSize * RING_RADIUS_RATIO);
}

function updateStatus(message) {
  interactionStatus.textContent = message || `${FACTION_LABELS[activeFaction]}，${SIZE_LABELS[body.dataset.size]}，${STATE_LABELS[body.dataset.state]}`;
}
function syncForceAttackState() {
  const forceButton = ring.querySelector('[data-action="force-attack"]');
  if (!forceButton)
    return;
  forceButton.classList.toggle("is-active", forceAttackActive);
  forceButton.setAttribute("aria-pressed", String(forceAttackActive));
}
function activateAction(button) {
  if (body.dataset.state === "disabled")
    return;
  if (button.dataset.action === "force-attack")
    forceAttackActive = !forceAttackActive;
  else
    forceAttackActive = false;
  syncForceAttackState();
  const suffix = button.dataset.action === "force-attack" && forceAttackActive ? "，持续模式已开启" : "";
  updateStatus(`${ACTION_LABELS[button.dataset.action]}${suffix}`);
}
function renderActionRing() {
  const size = Number(body.dataset.size);
  const radius = ringRadius(size);
  document.documentElement.style.setProperty("--joy-size", `${size}px`);
  document.documentElement.style.setProperty("--ring-radius", `${radius}px`);
  ring.replaceChildren();
  ACTIONS.forEach((action, index) => {
    const angle = RING_ANGLES[index] * Math.PI / 180;
    const button = document.createElement("button");
    button.type = "button";
    button.className = "action-button";
    button.dataset.action = action;
    button.setAttribute("aria-label", ACTION_LABELS[action]);
    button.disabled = body.dataset.state === "disabled"; button.setAttribute("aria-disabled", String(button.disabled));
    if (action === "force-attack")
      button.setAttribute("aria-pressed", String(forceAttackActive));
    button.style.setProperty("--action-x", `${Math.cos(angle) * radius}px`);
    button.style.setProperty("--action-y", `${Math.sin(angle) * radius}px`);
    button.innerHTML = `<span class="action-face">${GLYPHS[action]}</span>`;
    button.addEventListener("click", () => activateAction(button));
    ring.append(button);
  });
  syncForceAttackState();
}
function renderQuickbar() {
  quickbar.replaceChildren();
  SHORTCUTS.forEach(shortcut => {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "shortcut-slot";
    button.dataset.shortcut = shortcut.action;
    button.setAttribute("aria-label", shortcut.label);
    button.disabled = body.dataset.state === "disabled"; button.setAttribute("aria-disabled", String(button.disabled));
    if (shortcut.image) {
      const image = document.createElement("img");
      image.src = shortcut.image;
      image.alt = "";
      image.width = 32;
      image.height = 32;
      button.append(image);
    } else {
      const glyph = document.createElement("span");
      glyph.className = "shortcut-glyph";
      glyph.setAttribute("aria-hidden", "true");
      glyph.style.setProperty("--sprite-x", `-${shortcut.spriteX}px`);
      button.append(glyph);
    }
    quickbar.append(button);
  });
}
function setJoystickOffset(offsetX, offsetY, announce = false) {
  const distance = Math.hypot(offsetX, offsetY);
  const bounds = joystick.getBoundingClientRect();
  const travelLimit = bounds.width * 0.27;
  const scale = distance > travelLimit ? travelLimit / distance : 1;
  keyboardX = offsetX * scale;
  keyboardY = offsetY * scale;
  thumb.style.setProperty("--thumb-x", `${keyboardX}px`);
  thumb.style.setProperty("--thumb-y", `${keyboardY}px`);
  const centered = Math.hypot(keyboardX, keyboardY) < 0.5;
  const value = centered ? "摇杆位于中心" : `摇杆偏移 ${Math.round(keyboardX)}，${Math.round(keyboardY)}`;
  joystickValue.textContent = value;
  joystick.setAttribute("aria-label", `可拖动虚拟摇杆，${value}`);
  if (announce)
    updateStatus(value);
}
function resetJoystick(announce = false) {
  if (activePointerId !== null && joystick.hasPointerCapture(activePointerId))
    joystick.releasePointerCapture(activePointerId);
  activePointerId = null;
  joystick.classList.remove("is-dragging");
  setJoystickOffset(0, 0, announce);
}

function moveJoystick(pointerX, pointerY) {
  const bounds = joystick.getBoundingClientRect();
  setJoystickOffset(pointerX - (bounds.left + bounds.width / 2), pointerY - (bounds.top + bounds.height / 2));
}

function applyPreviewState(state) {
  body.dataset.state = state;
  document.querySelectorAll(".state-controls [data-state]").forEach(button => {
    button.setAttribute("aria-pressed", String(button.dataset.state === state));
  });
  const disabled = state === "disabled";
  joystick.setAttribute("aria-disabled", String(disabled));
  ring.querySelectorAll(".action-button").forEach(button => {
    button.disabled = disabled;
    button.setAttribute("aria-disabled", String(disabled));
  });
  quickbar.querySelectorAll(".shortcut-slot").forEach(button => {
    button.disabled = disabled;
    button.setAttribute("aria-disabled", String(disabled));
  });
  if (disabled)
    resetJoystick();
  updateStatus();
}

function selectSize(size) {
  body.dataset.size = size;
  document.querySelectorAll(".size-controls [data-size]").forEach(button => {
    button.setAttribute("aria-pressed", String(button.dataset.size === size));
  });
  resetJoystick();
  renderActionRing();
  updateStatus();
}

function selectFaction(faction) {
  if (faction === "compare" && compactViewport.matches)
    faction = activeFaction;
  const comparing = faction === "compare";
  if (!comparing)
    activeFaction = faction;
  body.dataset.faction = faction;
  stage.hidden = comparing;
  conceptPanel.hidden = comparing;
  comparison.hidden = !comparing;
  document.querySelectorAll(".faction-button").forEach(button => {
    button.setAttribute("aria-pressed", String(button.dataset.faction === faction));
  });

  if (!comparing) {
    conceptImage.src = CONCEPT_IMAGES[faction];
    conceptImage.alt = `${FACTION_LABELS[faction]}触控控制套件概念板`;
    conceptTitle.textContent = `${FACTION_LABELS[faction]}控制套件`;
    resetJoystick();
    updateStatus();
  } else {
    updateStatus("三阵营并排比较");
  }
}

function renderComparison() {
  comparison.replaceChildren();
  FACTIONS.forEach(faction => {
    const card = document.createElement("article");
    card.className = "comparison-card";
    card.dataset.cardFaction = faction;
    const image = document.createElement("img");
    image.src = CONCEPT_IMAGES[faction];
    image.alt = `${FACTION_LABELS[faction]}触控控制套件概念板`;
    const heading = document.createElement("h2");
    heading.textContent = FACTION_LABELS[faction];
    card.append(image, heading);
    comparison.append(card);
  });
}

function toggleQuickbar() {
  const collapsed = quickbar.classList.toggle("is-collapsed");
  quickbarToggle.textContent = collapsed ? "展开快捷栏" : "收起快捷栏";
  quickbarToggle.setAttribute("aria-expanded", String(!collapsed));
  updateStatus(collapsed ? "快捷栏已收起" : "快捷栏已展开，符号顺序保持不变");
}

function syncCompactMode(event) {
  const compact = event.matches;
  const compareButton = document.querySelector('.faction-button[data-faction="compare"]');
  const closingComparison = compact && body.dataset.faction === "compare";
  const restoreFactionFocus = compact && document.activeElement === compareButton;
  compareButton.disabled = compact;
  compareButton.setAttribute("aria-disabled", String(compact));
  if (closingComparison)
    selectFaction(activeFaction);
  if (restoreFactionFocus)
    requestAnimationFrame(() => {
      requestAnimationFrame(() => {
        document.querySelector(`.faction-button[data-faction="${activeFaction}"]`).focus();
      });
    });
}

joystick.addEventListener("pointerdown", event => {
  if (body.dataset.state === "disabled" || activePointerId !== null ||
      (event.pointerType === "mouse" && event.button !== 0))
    return;
  activePointerId = event.pointerId;
  joystick.setPointerCapture(event.pointerId);
  joystick.classList.add("is-dragging");
  moveJoystick(event.clientX, event.clientY);
});

joystick.addEventListener("pointermove", event => {
  if (event.pointerId === activePointerId)
    moveJoystick(event.clientX, event.clientY);
});

function endJoystickPointer(event) {
  if (event.pointerId !== activePointerId)
    return;
  resetJoystick();
}

function handleLostPointerCapture(event) {
  if (event.pointerId === activePointerId)
    resetJoystick();
}

function handleJoystickKeydown(event) {
  if (body.dataset.state === "disabled")
    return;
  if (event.key === "Home" || event.key === "Escape") {
    event.preventDefault();
    resetJoystick(true);
    return;
  }
  if (activePointerId !== null)
    return;
  const step = Number(body.dataset.size) * 0.08;
  const offsets = {
    "ArrowUp": [0, -step], "ArrowDown": [0, step],
    "ArrowLeft": [-step, 0], "ArrowRight": [step, 0]
  };
  const offset = offsets[event.key];
  if (!offset)
    return;
  event.preventDefault();
  setJoystickOffset(keyboardX + offset[0], keyboardY + offset[1], true);
}

joystick.addEventListener("pointerup", endJoystickPointer);
joystick.addEventListener("pointercancel", endJoystickPointer);
joystick.addEventListener("lostpointercapture", handleLostPointerCapture);
joystick.addEventListener("keydown", handleJoystickKeydown);

document.querySelectorAll(".faction-button").forEach(button => {
  button.addEventListener("click", () => selectFaction(button.dataset.faction));
});

document.querySelectorAll(".size-controls [data-size]").forEach(button => {
  button.addEventListener("click", () => selectSize(button.dataset.size));
});

document.querySelectorAll(".state-controls [data-state]").forEach(button => {
  button.addEventListener("click", () => applyPreviewState(button.dataset.state));
});

quickbarToggle.addEventListener("click", toggleQuickbar);
compactViewport.addEventListener("change", syncCompactMode);

renderQuickbar();
renderActionRing();
renderComparison();
applyPreviewState("normal");
syncCompactMode(compactViewport);
updateStatus();
