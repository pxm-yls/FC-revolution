// ============================================================
// FC Revolution LAN Pad - app.js
// WebRTC 串流 + 响应式布局 + 横竖屏适配
// ============================================================

var BINDABLE_BUTTONS = ['Up', 'Down', 'Left', 'Right', 'Y', 'X', 'B', 'A', 'Select', 'Start'];
var RESERVED_BUTTONS = ['L1', 'R1', 'L2', 'R2', 'L3', 'R3'];
var ALL_BUTTONS = BINDABLE_BUTTONS.concat(RESERVED_BUTTONS);
var DISPATCHED_BUTTONS = ['Up', 'Down', 'Left', 'Right', 'B', 'A', 'Select', 'Start'];
var DIRECT_BUTTON_TO_ACTION = {
  Up: 'Up',
  Down: 'Down',
  Left: 'Left',
  Right: 'Right',
  B: 'B',
  A: 'A',
  Select: 'Select',
  Start: 'Start'
};
var TURBO_BUTTON_TO_ACTION = {
  X: 'A',
  Y: 'B'
};
var BUTTON_LABELS = {
  Up: '上',
  Down: '下',
  Left: '左',
  Right: '右',
  Y: 'Y',
  X: 'X',
  B: 'B',
  A: 'A',
  Select: 'Select',
  Start: 'Start',
  L1: 'L1',
  R1: 'R1',
  L2: 'L2',
  R2: 'R2',
  L3: 'L3',
  R3: 'R3'
};
var DEFAULT_KEY_BINDINGS = {
  Up: 'ArrowUp',
  Down: 'ArrowDown',
  Left: 'ArrowLeft',
  Right: 'ArrowRight',
  Y: 'KeyA',
  X: 'KeyS',
  B: 'KeyZ',
  A: 'KeyX',
  Select: 'Space',
  Start: 'Enter'
};
var KEYBIND_STORAGE_KEY = 'fc-revolution-webpad-keybinds-v1';
var INPUT_WATCHDOG_INTERVAL_MS = 250;
var CONTROLLER_CHROME_HIDE_DELAY_MS = 2000;
var DEFAULT_TURBO_HZ = 12;
var TURBO_TOGGLE_INTERVAL_MS = Math.max(30, Math.round(1000 / (DEFAULT_TURBO_HZ * 2)));

// --- 状态管理 ---
var state = {
  selectedSessionId: null,
  selectedSessionName: '',
  selectedRomPath: null,
  selectedRomName: '',
  controlSocket: null,
  streamWs: null,
  heartbeatTimer: null,
  listRefreshTimer: null,
  sidePreviewRefreshTimer: null,
  romPage: 1,
  sessionPage: 1,
  romsPerPage: 10,
  sessionsPerPage: 10,
  sessionPreviewVersion: 0,
  previewRefreshVersion: 0,
  allRoms: [],
  allSessions: [],
  isMobile: false,
  isTablet: false,
  isLandscape: false,
  streamStats: { fps: 0, latency: 0, frameCount: 0, lastFrameTime: 0 },
  controlClaimPromise: null,
  claimedPortId: null,
  claimedSessionId: null,
  keyBindings: {},
  keybindPanelOpen: false,
  capturingButton: null,
  activeButtonSources: {},
  dispatchedButtonStates: {},
  turboTimer: null,
  turboPulseOn: true,
  mobileSelectionScaleFrame: 0,
  inputWatchdogTimer: null,
  selectionToolbarMoreOpen: false,
  controllerChromeVisible: true,
  controllerChromeHideTimer: null
};

// --- DOM 元素引用 ---
var dom = {
  topbar: document.querySelector('.topbar'),
  statusBar: document.getElementById('statusBar'),
  playerSelect: document.getElementById('playerSelect'),
  clientName: document.getElementById('clientName'),
  selectionTitle: document.getElementById('selectionTitle'),
  selectionMeta: document.getElementById('selectionMeta'),
  entryUrl: document.getElementById('entryUrl'),
  controllerTitle: document.getElementById('controllerTitle'),
  controllerMeta: document.getElementById('controllerMeta'),
  selectionView: document.getElementById('selectionView'),
  controllerView: document.getElementById('controllerView'),
  selectionStreamCanvas: document.getElementById('selectionStreamCanvas'),
  selectionPreviewEmpty: document.getElementById('selectionPreviewEmpty'),
  selectionToolbar: document.getElementById('selectionToolbar'),
  selectionToolbarDesktopMount: document.getElementById('selectionToolbarDesktopMount'),
  selectionToolbarMobileMount: document.getElementById('selectionToolbarMobileMount'),
  selectionToolbarMoreButton: document.getElementById('selectionToolbarMoreButton'),
  selectionToolbarMore: document.getElementById('selectionToolbarMore'),
  romPaginationInfo: document.getElementById('romPaginationInfo'),
  romPrevPageButton: document.getElementById('romPrevPageButton'),
  romNextPageButton: document.getElementById('romNextPageButton'),
  sessionPaginationInfo: document.getElementById('sessionPaginationInfo'),
  sessionPrevPageButton: document.getElementById('sessionPrevPageButton'),
  sessionNextPageButton: document.getElementById('sessionNextPageButton'),
  romPanel: document.getElementById('romPanel'),
  sessionPanel: document.getElementById('sessionPanel'),
  contentGrid: document.getElementById('contentGrid'),
  toggleRomPanelButton: document.getElementById('toggleRomPanelButton'),
  toggleSessionPanelButton: document.getElementById('toggleSessionPanelButton'),
  mobileSelection: document.getElementById('mobileSelection'),
  mobileSelectionScaler: document.getElementById('mobileSelectionScaler'),
  desktopLayout: document.querySelector('.desktop-layout'),
  mobileRoms: document.getElementById('mobileRoms'),
  mobileSessions: document.getElementById('mobileSessions'),
  mobileRomPaginationInfo: document.getElementById('mobileRomPaginationInfo'),
  mobileRomPrevPageButton: document.getElementById('mobileRomPrevPageButton'),
  mobileRomNextPageButton: document.getElementById('mobileRomNextPageButton'),
  mobileSessionPaginationInfo: document.getElementById('mobileSessionPaginationInfo'),
  mobileSessionPrevPageButton: document.getElementById('mobileSessionPrevPageButton'),
  mobileSessionNextPageButton: document.getElementById('mobileSessionNextPageButton'),
  mobileStreamCanvas: document.getElementById('mobileStreamCanvas'),
  mobilePreviewEmpty: document.getElementById('mobilePreviewEmpty'),
  mobileSelectionTitle: document.getElementById('mobileSelectionTitle'),
  mobileSelectionMeta: document.getElementById('mobileSelectionMeta'),
  controllerLayout: document.getElementById('controllerLayout'),
  controllerHeader: document.getElementById('controllerHeader'),
  controllerChromeButton: document.getElementById('controllerChromeButton'),
  controllerStream: document.getElementById('controllerStream'),
  keybindPanelButton: document.getElementById('keybindPanelButton'),
  keybindPanel: document.getElementById('keybindPanel'),
  closeKeybindPanelButton: document.getElementById('closeKeybindPanelButton'),
  resetKeybindsButton: document.getElementById('resetKeybindsButton'),
  keybindHint: document.getElementById('keybindHint'),
  keybindSummary: document.getElementById('keybindSummary'),
  keybindGrid: document.getElementById('keybindGrid'),
  streamCanvas: document.getElementById('streamCanvas'),
  streamPlaceholder: document.getElementById('streamPlaceholder'),
  streamFps: document.getElementById('streamFps'),
  streamLatency: document.getElementById('streamLatency'),
  fullscreenButton: document.getElementById('fullscreenButton')
};

// --- 工具函数 ---
function setStatus(text) { dom.statusBar.textContent = text || ''; }
function getPlayer() { return Number(dom.playerSelect.value); }
function playerLabel() { return getPlayer() === 0 ? '1P' : '2P'; }
function getPortIdForPlayer(player) { return player === 0 ? 'p1' : player === 1 ? 'p2' : null; }
function getSelectedPortId() { return getPortIdForPlayer(getPlayer()); }
function wsUrl() { return (location.protocol === 'https:' ? 'wss://' : 'ws://') + location.host + '/ws'; }
function sessionPreviewUrl(sessionId, tick) { return '/api/sessions/' + sessionId + '/preview?v=' + (tick || state.sessionPreviewVersion); }
function romPreviewUrl(romPath) { return '/api/roms/preview?romPath=' + encodeURIComponent(romPath); }
function getRomPageCount() { return Math.max(1, Math.ceil(state.allRoms.length / state.romsPerPage)); }
function getSessionPageCount() { return Math.max(1, Math.ceil(state.allSessions.length / state.sessionsPerPage)); }
function getFullscreenElement() { return document.fullscreenElement || document.webkitFullscreenElement || null; }

function requestNativeFullscreen(element) {
  if (!element)
    return Promise.reject(new Error('当前页面无法进入全屏'));

  var request = element.requestFullscreen || element.webkitRequestFullscreen;
  if (!request)
    return Promise.reject(new Error('当前浏览器不支持原生全屏'));

  var result = request.call(element);
  return result && typeof result.then === 'function' ? result : Promise.resolve();
}

function exitNativeFullscreen() {
  var exit = document.exitFullscreen || document.webkitExitFullscreen;
  if (!exit)
    return Promise.resolve();

  var result = exit.call(document);
  return result && typeof result.then === 'function' ? result : Promise.resolve();
}

function getSessionById(sessionId) {
  for (var i = 0; i < state.allSessions.length; i += 1) {
    if (state.allSessions[i].sessionId === sessionId) return state.allSessions[i];
  }
  return null;
}

function cloneDefaultKeyBindings() {
  var bindings = {};
  BINDABLE_BUTTONS.forEach(function(button) {
    bindings[button] = DEFAULT_KEY_BINDINGS[button] || null;
  });
  return bindings;
}

function createActiveButtonSourceMap() {
  var map = {};
  ALL_BUTTONS.forEach(function(button) {
    map[button] = new Set();
  });
  return map;
}

function createDispatchedButtonStateMap() {
  var map = {};
  DISPATCHED_BUTTONS.forEach(function(button) {
    map[button] = false;
  });
  return map;
}

function ensureActiveButtonSourceMap() {
  if (!state.activeButtonSources || typeof state.activeButtonSources !== 'object')
    state.activeButtonSources = createActiveButtonSourceMap();

  ALL_BUTTONS.forEach(function(button) {
    if (!(state.activeButtonSources[button] instanceof Set))
      state.activeButtonSources[button] = new Set();
  });
}

function ensureDispatchedButtonStateMap() {
  if (!state.dispatchedButtonStates || typeof state.dispatchedButtonStates !== 'object')
    state.dispatchedButtonStates = createDispatchedButtonStateMap();

  DISPATCHED_BUTTONS.forEach(function(button) {
    if (typeof state.dispatchedButtonStates[button] !== 'boolean')
      state.dispatchedButtonStates[button] = false;
  });
}

function normalizeKeyBindings(raw) {
  var bindings = cloneDefaultKeyBindings();
  if (!raw || typeof raw !== 'object')
    return bindings;

  BINDABLE_BUTTONS.forEach(function(button) {
    var value = raw[button];
    if (typeof value === 'string' && value.trim())
      bindings[button] = value;
    else if (value === null)
      bindings[button] = null;
  });

  return bindings;
}

function loadKeyBindings() {
  try {
    var raw = localStorage.getItem(KEYBIND_STORAGE_KEY);
    state.keyBindings = normalizeKeyBindings(raw ? JSON.parse(raw) : null);
  } catch (_error) {
    state.keyBindings = cloneDefaultKeyBindings();
  }
}

function saveKeyBindings() {
  try {
    localStorage.setItem(KEYBIND_STORAGE_KEY, JSON.stringify(state.keyBindings));
  } catch (_error) {
  }
}

function getButtonLabel(button) {
  return BUTTON_LABELS[button] || button;
}

function describeButton(button) {
  switch (button) {
    case 'Up': return '角色向上';
    case 'Down': return '角色向下';
    case 'Left': return '角色向左';
    case 'Right': return '角色向右';
    case 'Y': return 'B 连发键';
    case 'X': return 'A 连发键';
    case 'A': return '主动作键';
    case 'B': return '副动作键';
    case 'Select': return '选择 / 菜单';
    case 'Start': return '开始 / 暂停';
    default: return '网页手柄按键';
  }
}

function formatKeyCode(code) {
  if (!code)
    return '未设置';

  switch (code) {
    case 'ArrowUp': return 'Up';
    case 'ArrowDown': return 'Down';
    case 'ArrowLeft': return 'Left';
    case 'ArrowRight': return 'Right';
    case 'Space': return 'Space';
    case 'Enter': return 'Enter';
    case 'Escape': return 'Esc';
    case 'ShiftLeft': return 'L Shift';
    case 'ShiftRight': return 'R Shift';
    case 'ControlLeft': return 'L Ctrl';
    case 'ControlRight': return 'R Ctrl';
    case 'AltLeft': return 'L Alt';
    case 'AltRight': return 'R Alt';
    case 'Backspace': return 'Backspace';
    case 'Tab': return 'Tab';
  }

  if (code.indexOf('Key') === 0)
    return code.slice(3).toUpperCase();
  if (code.indexOf('Digit') === 0)
    return code.slice(5);
  if (code.indexOf('Numpad') === 0)
    return 'Num ' + code.slice(6);

  return code;
}

function getButtonForKeyCode(code) {
  if (!code)
    return null;

  for (var i = 0; i < BINDABLE_BUTTONS.length; i += 1) {
    var button = BINDABLE_BUTTONS[i];
    if (state.keyBindings[button] === code)
      return button;
  }

  return null;
}

function isControllerViewVisible() {
  return dom.controllerView && !dom.controllerView.classList.contains('hidden');
}

function hasActiveControlClaim() {
  return state.controlSocket != null || state.controlClaimPromise != null || state.claimedPortId != null;
}

function isEditableTarget(target) {
  if (!target)
    return false;

  var tagName = target.tagName ? target.tagName.toLowerCase() : '';
  return tagName === 'input' || tagName === 'textarea' || tagName === 'select' || !!target.isContentEditable;
}

function updateButtonVisual(button, active) {
  document.querySelectorAll('[data-button="' + button + '"]').forEach(function(element) {
    element.classList.toggle('pressed', !!active);
  });
}

function isLogicalButtonActive(button) {
  ensureActiveButtonSourceMap();
  var sources = state.activeButtonSources[button];
  return !!(sources && sources.size > 0);
}

function hasTurboButtonsActive() {
  return isLogicalButtonActive('X') || isLogicalButtonActive('Y');
}

function stopTurboLoop(resetPulse) {
  if (state.turboTimer) {
    clearInterval(state.turboTimer);
    state.turboTimer = null;
  }

  if (resetPulse !== false)
    state.turboPulseOn = true;
}

function syncTurboLoop() {
  if (!hasTurboButtonsActive()) {
    stopTurboLoop();
    return;
  }

  if (state.turboTimer)
    return;

  state.turboPulseOn = true;
  state.turboTimer = setInterval(function() {
    state.turboPulseOn = !state.turboPulseOn;
    syncDispatchedButtonState('A');
    syncDispatchedButtonState('B');
  }, TURBO_TOGGLE_INTERVAL_MS);
}

function computeDispatchedButtonState(button) {
  switch (button) {
    case 'A':
      return isLogicalButtonActive('A') || (isLogicalButtonActive('X') && state.turboPulseOn);
    case 'B':
      return isLogicalButtonActive('B') || (isLogicalButtonActive('Y') && state.turboPulseOn);
    default:
      return isLogicalButtonActive(button);
  }
}

function syncDispatchedButtonState(button) {
  ensureDispatchedButtonStateMap();
  var nextState = computeDispatchedButtonState(button);
  if (state.dispatchedButtonStates[button] === nextState)
    return null;

  state.dispatchedButtonStates[button] = nextState;
  return sendButton(button, nextState);
}

function syncDispatchedStateForLogicalButton(button) {
  var dispatchedButton = TURBO_BUTTON_TO_ACTION[button] || DIRECT_BUTTON_TO_ACTION[button] || null;
  if (!dispatchedButton || DISPATCHED_BUTTONS.indexOf(dispatchedButton) < 0)
    return null;

  return syncDispatchedButtonState(dispatchedButton);
}

function resetDispatchedButtonStates() {
  ensureDispatchedButtonStateMap();
  DISPATCHED_BUTTONS.forEach(function(button) {
    state.dispatchedButtonStates[button] = false;
  });
}

function resetActiveButtonSources() {
  ensureActiveButtonSourceMap();
  ALL_BUTTONS.forEach(function(button) {
    state.activeButtonSources[button].clear();
    updateButtonVisual(button, false);
  });
  stopTurboLoop();
  resetDispatchedButtonStates();
}

function clearClaimedControlState() {
  state.claimedPortId = null;
  state.claimedSessionId = null;
}

function clearTextSelection() {
  if (!window.getSelection)
    return;

  var selection = window.getSelection();
  if (selection && selection.rangeCount > 0)
    selection.removeAllRanges();
}

function setSelectionToolbarMoreOpen(open) {
  state.selectionToolbarMoreOpen = !!open;

  if (dom.selectionToolbarMore)
    dom.selectionToolbarMore.classList.toggle('hidden', !state.selectionToolbarMoreOpen);

  if (dom.selectionToolbarMoreButton)
    dom.selectionToolbarMoreButton.setAttribute('aria-expanded', state.selectionToolbarMoreOpen ? 'true' : 'false');
}

function relocateSelectionToolbar() {
  if (!dom.selectionToolbar)
    return;

  var useMobileMount = state.isMobile && !state.isLandscape;
  var target = useMobileMount ? dom.selectionToolbarMobileMount : dom.selectionToolbarDesktopMount;
  if (!target || dom.selectionToolbar.parentElement === target)
    return;

  target.appendChild(dom.selectionToolbar);
}

function clearControllerChromeHideTimer() {
  if (state.controllerChromeHideTimer) {
    clearTimeout(state.controllerChromeHideTimer);
    state.controllerChromeHideTimer = null;
  }
}

function setControllerChromeVisible(visible) {
  state.controllerChromeVisible = !!visible;

  if (dom.controllerHeader)
    dom.controllerHeader.classList.toggle('is-hidden', !state.controllerChromeVisible);

  if (dom.controllerChromeButton) {
    dom.controllerChromeButton.classList.toggle('hidden', state.controllerChromeVisible);
    dom.controllerChromeButton.setAttribute('aria-expanded', state.controllerChromeVisible ? 'true' : 'false');
  }
}

function scheduleControllerChromeHide() {
  clearControllerChromeHideTimer();
  if (!isControllerViewVisible() || state.keybindPanelOpen)
    return;

  state.controllerChromeHideTimer = setTimeout(function() {
    if (!state.keybindPanelOpen)
      setControllerChromeVisible(false);
  }, CONTROLLER_CHROME_HIDE_DELAY_MS);
}

function revealControllerChrome() {
  setControllerChromeVisible(true);
  scheduleControllerChromeHide();
}

function preventDefaultIfCancelable(event) {
  if (event && event.cancelable)
    event.preventDefault();
}

function getPointerSourceId(pointerId) {
  return 'pointer:' + (pointerId != null ? pointerId : 'primary');
}

function isSourceActive(sourceId) {
  ensureActiveButtonSourceMap();
  return ALL_BUTTONS.some(function(button) {
    var sources = state.activeButtonSources[button];
    return sources && sources.has(sourceId);
  });
}

function hasActiveButtons() {
  ensureActiveButtonSourceMap();
  return ALL_BUTTONS.some(function(button) {
    var sources = state.activeButtonSources[button];
    return sources && sources.size > 0;
  });
}

function updateFullscreenButton() {
  if (!dom.fullscreenButton)
    return;

  var isFullscreen = !!getFullscreenElement();
  dom.fullscreenButton.textContent = isFullscreen ? '退出全屏' : '全屏';
  dom.fullscreenButton.title = isFullscreen ? '退出网页全屏' : '进入网页全屏';
}

function setMobileSelectionScale(scale, widthPx) {
  if (!dom.mobileSelection || !dom.mobileSelectionScaler)
    return;

  var normalized = Math.max(0.35, Math.min(1, scale || 1));
  dom.mobileSelection.style.setProperty('--mobile-selection-scale', normalized.toFixed(4));
  dom.mobileSelection.classList.toggle('is-scaled', normalized < 0.999);
  dom.mobileSelectionScaler.style.width = widthPx ? Math.max(widthPx, dom.mobileSelection.clientWidth || 0) + 'px' : '';
}

function updateMobileSelectionScale() {
  state.mobileSelectionScaleFrame = 0;

  if (!dom.mobileSelection || !dom.mobileSelectionScaler)
    return;

  if (!state.isMobile || state.isLandscape || dom.selectionView.classList.contains('hidden')) {
    setMobileSelectionScale(1, null);
    return;
  }

  var containerWidth = dom.mobileSelection.clientWidth;
  var containerHeight = dom.mobileSelection.clientHeight;
  if (!containerWidth || !containerHeight)
    return;

  setMobileSelectionScale(1, containerWidth);
  var contentHeight = dom.mobileSelectionScaler.scrollHeight;
  if (!contentHeight)
    return;

  var scale = Math.min(containerHeight / contentHeight, 1);
  var compensatedWidth = containerWidth / Math.max(scale, 0.35);
  setMobileSelectionScale(scale, compensatedWidth);

  var visualHeight = dom.mobileSelectionScaler.scrollHeight * scale;
  if (visualHeight > containerHeight + 1) {
    scale = Math.max(0.35, scale * (containerHeight / visualHeight));
    compensatedWidth = containerWidth / scale;
    setMobileSelectionScale(scale, compensatedWidth);
  }
}

function scheduleMobileSelectionScale() {
  if (state.mobileSelectionScaleFrame)
    cancelAnimationFrame(state.mobileSelectionScaleFrame);

  state.mobileSelectionScaleFrame = requestAnimationFrame(updateMobileSelectionScale);
}

async function releaseAllActiveButtons() {
  ensureActiveButtonSourceMap();
  ensureDispatchedButtonStateMap();
  var pending = [];

  ALL_BUTTONS.forEach(function(button) {
    var sources = state.activeButtonSources[button];
    if (!sources || sources.size === 0)
      return;

    sources.clear();
    updateButtonVisual(button, false);
  });

  stopTurboLoop();
  DISPATCHED_BUTTONS.forEach(function(button) {
    var syncPromise = syncDispatchedButtonState(button);
    if (syncPromise)
      pending.push(syncPromise);
  });

  if (pending.length > 0)
    await Promise.all(pending).catch(function() { });

  clearTextSelection();
}

function setButtonSourceState(button, sourceId, pressed) {
  ensureActiveButtonSourceMap();
  var sources = state.activeButtonSources[button];
  if (!sources)
    return;

  var wasActive = sources.size > 0;
  if (pressed)
    sources.add(sourceId);
  else
    sources.delete(sourceId);

  var isActive = sources.size > 0;
  updateButtonVisual(button, isActive);
  if (wasActive === isActive)
    return;

  syncTurboLoop();
  syncDispatchedStateForLogicalButton(button);
}

function releasePointerSource(pointerId) {
  var sourceId = getPointerSourceId(pointerId);
  ALL_BUTTONS.forEach(function(button) {
    setButtonSourceState(button, sourceId, false);
  });
}

function startInputWatchdog() {
  stopInputWatchdog();
  state.inputWatchdogTimer = setInterval(function() {
    if (!hasActiveButtons())
      return;

    var lostFocus = typeof document.hasFocus === 'function' && !document.hasFocus();
    if (document.hidden || lostFocus || !isControllerViewVisible())
      releaseAllActiveButtons().catch(function() { });
  }, INPUT_WATCHDOG_INTERVAL_MS);
}

function stopInputWatchdog() {
  if (state.inputWatchdogTimer) {
    clearInterval(state.inputWatchdogTimer);
    state.inputWatchdogTimer = null;
  }
}

function updateKeybindHint() {
  if (!dom.keybindHint)
    return;

  if (state.capturingButton) {
    dom.keybindHint.textContent = '正在录入 ' + getButtonLabel(state.capturingButton) + '，按下任意键完成；按 Escape 清除当前绑定。';
    return;
  }

  dom.keybindHint.textContent = '点击某个键位后，按下键盘上的目标键；按 Escape 可清除当前绑定。';
}

function createKeycap(label) {
  var keycap = document.createElement('span');
  keycap.className = 'keycap';
  keycap.textContent = label;
  return keycap;
}

function renderKeybindSummary() {
  if (!dom.keybindSummary)
    return;

  dom.keybindSummary.innerHTML = '';
  var chipLine = document.createElement('div');
  chipLine.className = 'keybind-chip-line';

  BINDABLE_BUTTONS.forEach(function(button) {
    var chip = document.createElement('div');
    chip.className = 'keybind-chip';

    var label = document.createElement('span');
    label.className = 'keybind-chip-label';
    label.textContent = getButtonLabel(button);

    chip.appendChild(label);
    chip.appendChild(createKeycap(formatKeyCode(state.keyBindings[button])));
    chipLine.appendChild(chip);
  });

  dom.keybindSummary.appendChild(chipLine);
}

function cancelKeyCapture(shouldRender) {
  state.capturingButton = null;
  updateKeybindHint();
  if (shouldRender !== false)
    renderKeybindUi();
}

function setKeyBinding(button, code) {
  if (BINDABLE_BUTTONS.indexOf(button) < 0)
    return;

  if (code) {
    BINDABLE_BUTTONS.forEach(function(candidate) {
      if (candidate !== button && state.keyBindings[candidate] === code)
        state.keyBindings[candidate] = null;
    });
  }

  state.keyBindings[button] = code || null;
  saveKeyBindings();
  renderKeybindUi();
}

function beginKeyCapture(button) {
  if (state.capturingButton === button) {
    cancelKeyCapture();
    return;
  }

  state.capturingButton = button;
  updateKeybindHint();
  renderKeybindUi();
}

function renderKeybindUi() {
  if (!dom.keybindGrid)
    return;

  renderKeybindSummary();
  dom.keybindGrid.innerHTML = '';

  BINDABLE_BUTTONS.forEach(function(button) {
    var row = document.createElement('div');
    row.className = 'keybind-row' + (state.capturingButton === button ? ' is-capturing' : '');

    var copy = document.createElement('div');
    copy.className = 'keybind-copy';

    var title = document.createElement('p');
    title.className = 'keybind-title';
    title.textContent = getButtonLabel(button);

    var description = document.createElement('p');
    description.className = 'keybind-desc';
    description.textContent = describeButton(button);

    copy.appendChild(title);
    copy.appendChild(description);

    var captureButton = document.createElement('button');
    captureButton.type = 'button';
    captureButton.className = 'keybind-capture';

    if (state.capturingButton === button) {
      captureButton.classList.add('is-capturing');
      captureButton.textContent = '按下任意键...';
    } else {
      var currentCode = state.keyBindings[button];
      captureButton.classList.toggle('is-empty', !currentCode);
      captureButton.textContent = currentCode ? formatKeyCode(currentCode) : '点击绑定';
    }

    captureButton.onclick = function() {
      beginKeyCapture(button);
    };

    var clearButton = document.createElement('button');
    clearButton.type = 'button';
    clearButton.textContent = '清除';
    clearButton.disabled = !state.keyBindings[button];
    clearButton.onclick = function() {
      setKeyBinding(button, null);
      if (state.capturingButton === button)
        cancelKeyCapture();
      setStatus('已清除 ' + getButtonLabel(button) + ' 的键盘映射');
    };

    row.appendChild(copy);
    row.appendChild(captureButton);
    row.appendChild(clearButton);
    dom.keybindGrid.appendChild(row);
  });

  if (dom.keybindPanelButton) {
    dom.keybindPanelButton.classList.toggle('is-active', state.keybindPanelOpen);
    dom.keybindPanelButton.textContent = state.keybindPanelOpen ? '关闭键位' : '键位设置';
    dom.keybindPanelButton.title = state.keybindPanelOpen ? '关闭键盘键位面板' : '打开键盘键位面板';
  }
}

function setKeybindPanelOpen(open) {
  state.keybindPanelOpen = !!open;
  if (dom.keybindPanel) {
    dom.keybindPanel.classList.toggle('hidden', !state.keybindPanelOpen);
    dom.keybindPanel.setAttribute('aria-hidden', state.keybindPanelOpen ? 'false' : 'true');
  }

  if (state.keybindPanelOpen) {
    releaseAllActiveButtons().catch(function() { });
    setControllerChromeVisible(true);
    clearControllerChromeHideTimer();
  } else {
    cancelKeyCapture(false);
    if (isControllerViewVisible())
      revealControllerChrome();
  }

  updateKeybindHint();
  renderKeybindUi();
}

function handleKeyboardKeydown(event) {
  if (state.capturingButton) {
    event.preventDefault();
    event.stopPropagation();
    if (event.repeat)
      return;

    var capturedButton = state.capturingButton;
    if (event.code === 'Escape') {
      setKeyBinding(capturedButton, null);
      cancelKeyCapture();
      setStatus('已清除 ' + getButtonLabel(capturedButton) + ' 的键盘映射');
      return;
    }

    setKeyBinding(capturedButton, event.code);
    cancelKeyCapture();
    setStatus('已将 ' + getButtonLabel(capturedButton) + ' 绑定为 ' + formatKeyCode(event.code));
    return;
  }

  if (!isControllerViewVisible())
    return;

  if (state.keybindPanelOpen) {
    if (event.code === 'Escape') {
      event.preventDefault();
      setKeybindPanelOpen(false);
    }
    return;
  }

  if (isEditableTarget(event.target) || event.repeat)
    return;

  var button = getButtonForKeyCode(event.code);
  if (!button)
    return;

  event.preventDefault();
  setButtonSourceState(button, 'key:' + event.code, true);
}

function handleKeyboardKeyup(event) {
  if (state.capturingButton) {
    event.preventDefault();
    event.stopPropagation();
    return;
  }

  if (!isControllerViewVisible() || state.keybindPanelOpen)
    return;

  var button = getButtonForKeyCode(event.code);
  if (!button)
    return;

  event.preventDefault();
  setButtonSourceState(button, 'key:' + event.code, false);
}

// --- 设备检测与布局管理 ---
function detectDevice() {
  var ua = navigator.userAgent;
  var w = window.innerWidth;
  var h = window.innerHeight;
  var isTabletUA = /iPad/i.test(ua) || (/Macintosh/i.test(ua) && navigator.maxTouchPoints > 1);
  var isTabletSize = w >= 769 && w <= 1024;
  state.isTablet = isTabletUA || (isTabletSize && navigator.maxTouchPoints > 1);
  state.isMobile = !state.isTablet && (
    /Android|webOS|iPhone|iPod|BlackBerry|IEMobile|Opera Mini/i.test(ua) || w <= 768
  );
  state.isLandscape = w > h;
  return { isMobile: state.isMobile, isTablet: state.isTablet, isLandscape: state.isLandscape };
}

function updateLayout() {
  detectDevice();
  var usePortraitMobileLayout = state.isMobile && !state.isLandscape;
  document.documentElement.classList.toggle('is-mobile-no-select', state.isMobile);

  if (usePortraitMobileLayout) {
    dom.desktopLayout.style.display = 'none';
    dom.mobileSelection.style.display = 'grid';
    document.body.classList.add('is-mobile');
    document.body.classList.remove('is-desktop', 'is-tablet');
  } else if (state.isTablet) {
    dom.desktopLayout.style.display = 'grid';
    dom.mobileSelection.style.display = 'none';
    document.body.classList.add('is-tablet');
    document.body.classList.remove('is-mobile', 'is-desktop');
  } else {
    dom.desktopLayout.style.display = 'grid';
    dom.mobileSelection.style.display = 'none';
    document.body.classList.add('is-desktop');
    document.body.classList.remove('is-mobile', 'is-tablet');
  }

  if (state.isLandscape) {
    document.body.classList.add('is-landscape');
    document.body.classList.remove('is-portrait');
  } else {
    document.body.classList.add('is-portrait');
    document.body.classList.remove('is-landscape');
  }

  // 控制器视图布局类（保留兼容，CSS 已通过媒体查询处理横屏）
  if (dom.controllerView && !dom.controllerView.classList.contains('hidden')) {
    if (state.isMobile && state.isLandscape) {
      dom.controllerLayout.classList.add('landscape-mode');
    } else {
      dom.controllerLayout.classList.remove('landscape-mode');
    }
  }

  relocateSelectionToolbar();
  updateFullscreenButton();
  scheduleMobileSelectionScale();
}

// 监听横竖屏变化
function setupOrientationListener() {
  var mediaQuery = window.matchMedia('(orientation: landscape)');
  
  function handleOrientationChange(e) {
    state.isLandscape = e.matches;
    updateLayout();
  }

  if (mediaQuery.addEventListener) {
    mediaQuery.addEventListener('change', handleOrientationChange);
  } else {
    mediaQuery.addListener(handleOrientationChange);
  }

  window.addEventListener('resize', function() {
    detectDevice();
    updateLayout();
  });

  if (window.visualViewport) {
    window.visualViewport.addEventListener('resize', function() {
      updateLayout();
    });
  }
}

function shouldSuppressMobileSelection(target) {
  return state.isMobile && !isEditableTarget(target);
}

// --- 视图切换 ---
function updateSelectionSummary() {
  if (state.selectedSessionId) {
    dom.selectionTitle.textContent = state.selectedSessionName || '已选择游戏窗口';
    dom.selectionMeta.textContent = '当前将接管 ' + playerLabel() + '。画面为该窗口实时串流。';
    dom.mobileSelectionTitle.textContent = state.selectedSessionName || '已选择游戏窗口';
    dom.mobileSelectionMeta.textContent = '当前控制 ' + playerLabel();
    // 视频可见性由 StreamManager._connect / stop() 管理，此处只设置连接中的提示文本
    if (!StreamManager._ws) {
      dom.selectionPreviewEmpty.classList.remove('hidden');
      dom.selectionPreviewEmpty.textContent = '正在建立串流连接...';
      dom.mobilePreviewEmpty.classList.remove('hidden');
      dom.mobilePreviewEmpty.textContent = '串流连接中...';
    }
    scheduleMobileSelectionScale();
    return;
  }

  if (state.selectedRomPath) {
    dom.selectionTitle.textContent = state.selectedRomName || '待启动游戏';
    dom.selectionMeta.textContent = '开始控制时会自动启动该游戏窗口。';
    dom.selectionPreviewEmpty.classList.remove('hidden');
    dom.selectionPreviewEmpty.textContent = '点击"开始控制"后会启动该游戏，并在这里显示实时画面。';
    dom.mobileSelectionTitle.textContent = state.selectedRomName || '待启动游戏';
    dom.mobileSelectionMeta.textContent = '开始控制后自动启动';
    dom.mobilePreviewEmpty.classList.remove('hidden');
    dom.mobilePreviewEmpty.textContent = '开始控制后自动启动';
    scheduleMobileSelectionScale();
    return;
  }

  dom.selectionTitle.textContent = '未选择游戏窗口';
  dom.selectionMeta.textContent = '请先从左侧启动游戏，或从右侧接入已经打开的窗口。';
  dom.selectionPreviewEmpty.classList.remove('hidden');
  dom.selectionPreviewEmpty.textContent = '启动或选择一个游戏窗口后，这里会实时显示游戏画面。';
  dom.mobileSelectionTitle.textContent = '未选择游戏窗口';
  dom.mobileSelectionMeta.textContent = '先选择一个游戏';
  dom.mobilePreviewEmpty.classList.remove('hidden');
  dom.mobilePreviewEmpty.textContent = '先选择一个游戏窗口';
  scheduleMobileSelectionScale();
}

function updateControllerSummary() {
  dom.controllerTitle.textContent = state.selectedSessionName || '等待接入';
  dom.controllerMeta.textContent = state.selectedSessionId
    ? ('正在控制 ' + playerLabel() + ' / 会话 ' + state.selectedSessionId)
    : '当前未接入任何会话';
}

function showSelectionView() {
  document.body.classList.remove('controller-mode');
  dom.selectionView.classList.remove('hidden');
  dom.controllerView.classList.add('hidden');
  setSelectionToolbarMoreOpen(false);
  clearControllerChromeHideTimer();
  setControllerChromeVisible(true);
  if (state.keybindPanelOpen)
    setKeybindPanelOpen(false);
  updateSelectionSummary();
  updateLayout();
  startAutoRefresh(); // 返回选台时恢复自动刷新
  // 有 session 时保留串流（用于预览），无 session 时停止
  if (!state.selectedSessionId) {
    stopStream();
  }
  scheduleMobileSelectionScale();
}

function showControllerView() {
  document.body.classList.add('controller-mode');
  dom.selectionView.classList.add('hidden');
  dom.controllerView.classList.remove('hidden');
  setSelectionToolbarMoreOpen(false);
  updateControllerSummary();
  renderKeybindUi();
  updateLayout();
  revealControllerChrome();
  stopAutoRefresh(); // 进入控制器视图时停止所有轮询，防止内存泄漏
}

// --- WebSocket 串流管理（JPEG 视频 + PCM16 音频）---
var StreamManager = {
  _headerSize: 12,
  _ws: null,
  _audioCtx: null,
  _nextPlayTime: 0,
  statsInterval: null,
  currentSessionId: null,
  _retryCount: 0,
  _maxRetries: 3,
  _retryTimer: null,
  _frameCount: 0,
  _videoDecodeInFlight: false,
  _latestVideoPayload: null,
  _latestVideoSequence: -1,
  _latestVideoWidth: 0,
  _latestVideoHeight: 0,

  async start(sessionId) {
    this._fullStop();
    this._retryCount = 0;
    this._frameCount = 0;
    this.currentSessionId = sessionId;
    setStatus('正在建立串流连接...');
    try {
      this._connect(sessionId);
    } catch (error) {
      console.error('[Stream] 连接失败:', error);
      setStatus('串流连接失败: ' + error.message);
    }
  },

  _connect(sessionId) {
    var self = this;
    this._cleanupConn();

    var protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
    var url = protocol + '//' + location.host + '/api/sessions/' + sessionId + '/stream/ws';
    var ws = new WebSocket(url);
    ws.binaryType = 'arraybuffer';
    this._ws = ws;

    ws.onopen = function() {
      console.log('[Stream] WebSocket 已连接');
      self._retryCount = 0;
      setStatus('串流已连接');

      // 显示 canvas，隐藏占位符
      dom.streamPlaceholder.classList.add('hidden');
      dom.streamCanvas.classList.remove('hidden');
      dom.selectionStreamCanvas.classList.remove('hidden');
      dom.selectionPreviewEmpty.classList.add('hidden');
      dom.mobileStreamCanvas.classList.remove('hidden');
      dom.mobilePreviewEmpty.classList.add('hidden');

      self._startStats();
      scheduleMobileSelectionScale();
    };

    ws.onmessage = function(event) {
      if (self._ws !== ws) return;
      var data = new Uint8Array(event.data);
      if (data.length < self._headerSize) return;
      var msgType = data[0];
      var version = data[1];
      var codec = data[2];
      var sequence = self._readInt32Le(data, 4);
      var metadata = self._readInt32Le(data, 8);
      var payload = data.subarray(self._headerSize);
      if (version !== 0x01) return;

      if (msgType === 0x01) {
        self._frameCount++;
        if (codec !== 0x01) return;
        var width = (metadata >>> 16) & 0xFFFF;
        var height = metadata & 0xFFFF;
        self._queueVideoFrame(payload, sequence, width, height);
      } else if (msgType === 0x02) {
        if (codec !== 0x01) return;
        self._playAudio(payload, metadata);
      }
    };

    ws.onclose = function() {
      console.log('[Stream] WebSocket 已关闭');
      if (self._ws === ws && self.currentSessionId === sessionId) {
        self._scheduleRetry(sessionId);
      }
    };

    ws.onerror = function() {
      console.error('[Stream] WebSocket 错误');
    };
  },

  _drawFrame(bitmap) {
    // 绘制到所有 canvas 元素
    var canvases = [dom.streamCanvas, dom.selectionStreamCanvas, dom.mobileStreamCanvas];
    for (var i = 0; i < canvases.length; i++) {
      var c = canvases[i];
      if (c.width !== bitmap.width || c.height !== bitmap.height) {
        c.width = bitmap.width;
        c.height = bitmap.height;
      }
      var ctx = c.getContext('2d');
      ctx.drawImage(bitmap, 0, 0);
    }
  },

  _queueVideoFrame(payload, sequence, width, height) {
    if (sequence <= this._latestVideoSequence) return;
    this._latestVideoSequence = sequence;
    this._latestVideoPayload = payload;
    this._latestVideoWidth = width;
    this._latestVideoHeight = height;
    this._drainVideoQueue();
  },

  _drainVideoQueue() {
    var self = this;
    if (self._videoDecodeInFlight || !self._latestVideoPayload) return;

    var payload = self._latestVideoPayload;
    self._latestVideoPayload = null;
    self._videoDecodeInFlight = true;

    var blob = new Blob([payload], { type: 'image/jpeg' });
    createImageBitmap(blob).then(function(bitmap) {
      self._drawFrame(bitmap);
      bitmap.close();
    }).catch(function() {}).finally(function() {
      self._videoDecodeInFlight = false;
      if (self._latestVideoPayload) self._drainVideoQueue();
    });
  },

  _playAudio(pcm16Le, sampleRate) {
    if (!this._audioCtx) {
      try {
        this._audioCtx = new (window.AudioContext || window.webkitAudioContext)({ sampleRate: sampleRate || 48000 });
        this._nextPlayTime = this._audioCtx.currentTime;
      } catch (e) { return; }
    }
    var ctx = this._audioCtx;
    var sampleCount = pcm16Le.length / 2;
    var buffer = ctx.createBuffer(1, sampleCount, sampleRate || 48000);
    var channel = buffer.getChannelData(0);
    for (var i = 0; i < sampleCount; i++) {
      var lo = pcm16Le[i * 2];
      var hi = pcm16Le[i * 2 + 1];
      var sample = (hi << 8) | lo;
      if (sample >= 0x8000) sample -= 0x10000;
      channel[i] = sample / 32768.0;
    }
    var source = ctx.createBufferSource();
    source.buffer = buffer;
    source.connect(ctx.destination);
    var now = ctx.currentTime;
    if (this._nextPlayTime < now) this._nextPlayTime = now;
    source.start(this._nextPlayTime);
    this._nextPlayTime += buffer.duration;
  },

  _readInt32Le(buffer, offset) {
    var view = new DataView(buffer.buffer, buffer.byteOffset, buffer.byteLength);
    return view.getInt32(offset, true);
  },

  _scheduleRetry(sessionId) {
    var self = this;
    self._cleanupConn();
    if (self._retryCount >= self._maxRetries) {
      setStatus('串流连接失败，已达最大重试次数，请返回后重新进入');
      return;
    }
    self._retryCount++;
    setStatus('连接断开，3s 后重连 (' + self._retryCount + '/' + self._maxRetries + ')...');
    if (self._retryTimer) clearTimeout(self._retryTimer);
    self._retryTimer = setTimeout(function() {
      self._retryTimer = null;
      if (self.currentSessionId === sessionId) {
        try {
          self._connect(sessionId);
        } catch (error) {
          console.error('[Stream] 重连失败:', error);
          self._scheduleRetry(sessionId);
        }
      }
    }, 3000);
  },

  _cleanupConn() {
    if (this.statsInterval) { clearInterval(this.statsInterval); this.statsInterval = null; }
    this._videoDecodeInFlight = false;
    this._latestVideoPayload = null;
    this._latestVideoSequence = -1;
    this._latestVideoWidth = 0;
    this._latestVideoHeight = 0;
    if (this._ws) {
      try { this._ws.close(); } catch (e) {}
      this._ws = null;
    }
  },

  _fullStop() {
    if (this._retryTimer) { clearTimeout(this._retryTimer); this._retryTimer = null; }
    this._cleanupConn();
    this._nextPlayTime = 0;
    if (this._audioCtx) {
      try { this._audioCtx.close(); } catch (e) {}
      this._audioCtx = null;
    }
  },

  stop() {
    this._fullStop();
    this.currentSessionId = null;
    this._retryCount = 0;
    this._frameCount = 0;
    // 控制器视图
    dom.streamCanvas.classList.add('hidden');
    dom.streamPlaceholder.classList.remove('hidden');
    // 选台预览
    dom.selectionStreamCanvas.classList.add('hidden');
    dom.selectionPreviewEmpty.classList.remove('hidden');
    // 移动端预览
    dom.mobileStreamCanvas.classList.add('hidden');
    dom.mobilePreviewEmpty.classList.remove('hidden');
    dom.streamFps.textContent = '-- FPS';
    dom.streamLatency.textContent = '-- ms';
    state.streamStats = { fps: 0, latency: 0, frameCount: 0, lastFrameTime: 0 };
    scheduleMobileSelectionScale();
  },

  _startStats() {
    var self = this;
    var lastFrames = 0;
    var lastTime = Date.now();

    this.statsInterval = setInterval(function() {
      var now = Date.now();
      var elapsed = (now - lastTime) / 1000;
      if (elapsed > 0 && lastFrames > 0) {
        var fps = Math.round((self._frameCount - lastFrames) / elapsed);
        dom.streamFps.textContent = fps + ' FPS';
        state.streamStats.fps = fps;
      }
      lastFrames = self._frameCount;
      lastTime = now;
      // WebSocket 无 jitter 指标，显示帧计数
      dom.streamLatency.textContent = self._frameCount + ' frames';
    }, 1000);
  }
};

function stopStream() {
  StreamManager.stop();
}

function startStream(sessionId) {
  StreamManager.start(sessionId);
}

// --- 渲染函数 ---
function markRowSelected(row, active) {
  if (active) row.classList.add('selected');
  else row.classList.remove('selected');
}

function badge(text, extraClass) {
  var span = document.createElement('span');
  span.className = 'badge' + (extraClass ? ' ' + extraClass : '');
  span.textContent = text;
  return span;
}

function createMediaFallback(text) {
  var placeholder = document.createElement('div');
  placeholder.className = 'card-placeholder';
  placeholder.textContent = text;
  return placeholder;
}

function formatPlayerSource(label, source) {
  return label + ' ' + (source === 1 ? '网页控制' : '本地控制');
}

// 渲染桌面端 ROM 列表
function renderRoms() {
  var root = document.getElementById('roms');
  root.innerHTML = '';

  var pageCount = getRomPageCount();
  if (state.romPage > pageCount) state.romPage = pageCount;
  if (state.romPage < 1) state.romPage = 1;

  var pageStart = (state.romPage - 1) * state.romsPerPage;
  var pageItems = state.allRoms.slice(pageStart, pageStart + state.romsPerPage);

  pageItems.forEach(function(rom) {
    var card = document.createElement('article');
    card.className = 'rom-card';
    markRowSelected(card, state.selectedRomPath === rom.path && !state.selectedSessionId);

    var media = document.createElement('div');
    media.className = 'card-media';

    if (rom.hasPreview !== false) {
      var video = document.createElement('video');
      video.className = 'card-video';
      video.autoplay = true;
      video.loop = true;
      video.muted = true;
      video.playsInline = true;
      video.preload = 'metadata';
      video.src = romPreviewUrl(rom.path);
      video.onerror = function() {
        media.innerHTML = '';
        media.appendChild(createMediaFallback('当前没有预览动画'));
      };
      video.onloadeddata = function() {
        var playPromise = video.play();
        if (playPromise && typeof playPromise.catch === 'function')
          playPromise.catch(function() { });
      };
      media.appendChild(video);
    } else {
      media.appendChild(createMediaFallback('当前没有预览动画'));
    }

    var body = document.createElement('div');
    body.className = 'card-body';

    var title = document.createElement('h4');
    title.className = 'card-title';
    title.textContent = rom.displayName;

    var meta = document.createElement('div');
    meta.className = 'card-meta';
    meta.textContent = rom.isLoaded ? '该游戏当前已有打开中的窗口。' : '点击后将在主程序中打开一个新的游戏窗口。';

    var badges = document.createElement('div');
    badges.className = 'badge-line';
    badges.appendChild(badge(rom.isLoaded ? '运行中' : '待启动', rom.isLoaded ? 'gold' : ''));

    var actions = document.createElement('div');
    actions.className = 'card-actions';

    var button = document.createElement('button');
    button.className = 'primary';
    button.textContent = '选择并启动';
    button.onclick = async function() {
      try {
        await launchRom(rom);
      } catch (error) {
        setStatus(error.message);
      }
    };

    actions.appendChild(button);
    body.appendChild(title);
    body.appendChild(meta);
    body.appendChild(badges);
    body.appendChild(actions);
    card.appendChild(media);
    card.appendChild(body);
    root.appendChild(card);
  });

  dom.romPaginationInfo.textContent = '第 ' + state.romPage + ' / ' + pageCount + ' 页';
  dom.romPrevPageButton.disabled = state.romPage <= 1;
  dom.romNextPageButton.disabled = state.romPage >= pageCount;

  // 同时渲染移动端 ROM 列表
  renderMobileRoms();
}

// 渲染移动端 ROM 列表
function renderMobileRoms() {
  var root = dom.mobileRoms;
  root.innerHTML = '';
  root.scrollLeft = 0;

  var pageCount = getRomPageCount();
  if (state.romPage > pageCount) state.romPage = pageCount;
  if (state.romPage < 1) state.romPage = 1;

  var pageStart = (state.romPage - 1) * state.romsPerPage;
  var pageItems = state.allRoms.slice(pageStart, pageStart + state.romsPerPage);

  pageItems.forEach(function(rom) {
    var card = document.createElement('article');
    card.className = 'mobile-rom-card';
    markRowSelected(card, state.selectedRomPath === rom.path && !state.selectedSessionId);

    var media = document.createElement('div');
    media.className = 'mobile-card-media';

    if (rom.hasPreview !== false) {
      var video = document.createElement('video');
      video.className = 'mobile-card-video';
      video.autoplay = true;
      video.loop = true;
      video.muted = true;
      video.playsInline = true;
      video.preload = 'metadata';
      video.src = romPreviewUrl(rom.path);
      video.onerror = function() {
        media.innerHTML = '';
        media.appendChild(createMediaFallback('暂无预览'));
      };
      media.appendChild(video);
    } else {
      media.appendChild(createMediaFallback('暂无预览'));
    }

    var body = document.createElement('div');
    body.className = 'mobile-card-body';

    var title = document.createElement('h4');
    title.className = 'mobile-card-title';
    title.textContent = rom.displayName;

    var badgeEl = badge(rom.isLoaded ? '运行中' : '待启动', rom.isLoaded ? 'gold' : '');

    var button = document.createElement('button');
    button.className = 'primary mobile-launch-btn';
    button.textContent = '启动';
    button.onclick = async function() {
      try {
        await launchRom(rom);
      } catch (error) {
        setStatus(error.message);
      }
    };

    body.appendChild(title);
    body.appendChild(badgeEl);
    body.appendChild(button);
    card.appendChild(media);
    card.appendChild(body);
    root.appendChild(card);
  });

  dom.mobileRomPaginationInfo.textContent = '第 ' + state.romPage + ' / ' + pageCount + ' 页';
  dom.mobileRomPrevPageButton.disabled = state.romPage <= 1;
  dom.mobileRomNextPageButton.disabled = state.romPage >= pageCount;
  scheduleMobileSelectionScale();
}

// 渲染桌面端会话列表
function renderSessions() {
  var root = document.getElementById('sessions');
  root.innerHTML = '';

  if (state.allSessions.length === 0) {
    var empty = document.createElement('div');
    empty.className = 'card-placeholder';
    empty.textContent = '当前还没有打开中的游戏窗口。先从左侧启动一个游戏。';
    root.appendChild(empty);
    renderMobileSessions();
    return;
  }

  var pageCount = getSessionPageCount();
  if (state.sessionPage > pageCount) state.sessionPage = pageCount;
  if (state.sessionPage < 1) state.sessionPage = 1;

  var pageStart = (state.sessionPage - 1) * state.sessionsPerPage;
  var pageItems = state.allSessions.slice(pageStart, pageStart + state.sessionsPerPage);

  pageItems.forEach(function(session) {
    var card = document.createElement('article');
    card.className = 'session-card';
    markRowSelected(card, state.selectedSessionId === session.sessionId);

    var media = document.createElement('div');
    media.className = 'card-media';

    var image = document.createElement('img');
    image.className = 'card-image';
    image.alt = session.displayName + ' 实时预览';
    image.setAttribute('data-session-preview', session.sessionId);
    image.src = sessionPreviewUrl(session.sessionId, state.previewRefreshVersion);
    image.onerror = function() {
      media.innerHTML = '';
      media.appendChild(createMediaFallback('当前窗口暂无画面'));
    };
    media.appendChild(image);

    var body = document.createElement('div');
    body.className = 'card-body';

    var title = document.createElement('h4');
    title.className = 'card-title';
    title.textContent = session.displayName;

    var meta = document.createElement('div');
    meta.className = 'card-meta';
    meta.textContent = session.controlSummary;

    var badges = document.createElement('div');
    badges.className = 'badge-line';
    badges.appendChild(badge(formatPlayerSource('1P', session.player1ControlSource), session.player1ControlSource === 1 ? 'warn' : ''));
    badges.appendChild(badge(formatPlayerSource('2P', session.player2ControlSource), session.player2ControlSource === 1 ? 'warn' : ''));

    var actions = document.createElement('div');
    actions.className = 'card-actions';

    var button = document.createElement('button');
    button.textContent = '使用此窗口';
    button.onclick = function() {
      state.selectedSessionId = session.sessionId;
      state.selectedSessionName = session.displayName;
      state.selectedRomPath = null;
      state.selectedRomName = '';
      startStream(session.sessionId); // 选台即开始预览串流
      updateSelectionSummary();
      setStatus('已选择当前窗口: ' + session.displayName);
      renderSessions();
      renderRoms();
    };

    actions.appendChild(button);
    body.appendChild(title);
    body.appendChild(meta);
    body.appendChild(badges);
    body.appendChild(actions);
    card.appendChild(media);
    card.appendChild(body);
    root.appendChild(card);
  });

  dom.sessionPaginationInfo.textContent = '第 ' + state.sessionPage + ' / ' + pageCount + ' 页';
  dom.sessionPrevPageButton.disabled = state.sessionPage <= 1;
  dom.sessionNextPageButton.disabled = state.sessionPage >= pageCount;

  renderMobileSessions();
}

// 渲染移动端会话列表
function renderMobileSessions() {
  var root = dom.mobileSessions;
  root.innerHTML = '';
  root.scrollLeft = 0;

  if (state.allSessions.length === 0) {
    var empty = document.createElement('div');
    empty.className = 'card-placeholder';
    empty.textContent = '暂无运行中的游戏';
    root.appendChild(empty);
    scheduleMobileSelectionScale();
    return;
  }

  var pageCount = getSessionPageCount();
  if (state.sessionPage > pageCount) state.sessionPage = pageCount;
  if (state.sessionPage < 1) state.sessionPage = 1;

  var pageStart = (state.sessionPage - 1) * state.sessionsPerPage;
  var pageItems = state.allSessions.slice(pageStart, pageStart + state.sessionsPerPage);

  pageItems.forEach(function(session) {
    var card = document.createElement('article');
    card.className = 'mobile-session-card';
    markRowSelected(card, state.selectedSessionId === session.sessionId);

    var media = document.createElement('div');
    media.className = 'mobile-card-media';

    var image = document.createElement('img');
    image.className = 'mobile-card-image';
    image.alt = session.displayName;
    image.setAttribute('data-session-preview', session.sessionId);
    image.src = sessionPreviewUrl(session.sessionId, state.previewRefreshVersion);
    image.onerror = function() {
      media.innerHTML = '';
      media.appendChild(createMediaFallback('暂无画面'));
    };
    media.appendChild(image);

    var body = document.createElement('div');
    body.className = 'mobile-card-body';

    var title = document.createElement('h4');
    title.className = 'mobile-card-title';
    title.textContent = session.displayName;

    var badges = document.createElement('div');
    badges.className = 'badge-line';
    badges.appendChild(badge(formatPlayerSource('1P', session.player1ControlSource), session.player1ControlSource === 1 ? 'warn' : ''));
    badges.appendChild(badge(formatPlayerSource('2P', session.player2ControlSource), session.player2ControlSource === 1 ? 'warn' : ''));

    var button = document.createElement('button');
    button.className = 'primary mobile-launch-btn';
    button.textContent = '使用';
    button.onclick = function() {
      state.selectedSessionId = session.sessionId;
      state.selectedSessionName = session.displayName;
      state.selectedRomPath = null;
      state.selectedRomName = '';
      startStream(session.sessionId); // 选台即开始预览串流
      updateSelectionSummary();
      setStatus('已选择: ' + session.displayName);
      renderSessions();
      renderRoms();
    };

    body.appendChild(title);
    body.appendChild(badges);
    body.appendChild(button);
    card.appendChild(media);
    card.appendChild(body);
    root.appendChild(card);
  });

  dom.mobileSessionPaginationInfo.textContent = '第 ' + state.sessionPage + ' / ' + pageCount + ' 页';
  dom.mobileSessionPrevPageButton.disabled = state.sessionPage <= 1;
  dom.mobileSessionNextPageButton.disabled = state.sessionPage >= pageCount;
  scheduleMobileSelectionScale();
}

// --- API 调用 ---
async function api(path, options) {
  var response = await fetch(path, Object.assign({ headers: { 'Content-Type': 'application/json' } }, options || {}));
  var data = await response.json().catch(function() { return {}; });
  if (!response.ok) throw new Error(data.error || '请求失败');
  return data;
}

async function refreshRoms() {
  state.allRoms = await api('/api/roms');
  renderRoms();
}

async function refreshSessions() {
  state.sessionPreviewVersion += 1;
  state.allSessions = await api('/api/sessions');

  if (state.selectedSessionId) {
    var selectedSession = getSessionById(state.selectedSessionId);
    if (!selectedSession) {
      state.selectedSessionId = null;
      state.selectedSessionName = '';
    } else {
      state.selectedSessionName = selectedSession.displayName;
    }
  }

  if (state.claimedSessionId && !getSessionById(state.claimedSessionId))
    clearClaimedControlState();

  renderSessions();
  updateSelectionSummary();
}

async function refreshAllData() {
  await refreshRoms();
  await refreshSessions();
}

async function launchRom(rom) {
  var result = await api('/api/sessions', {
    method: 'POST',
    body: JSON.stringify({ romPath: rom.path })
  });

  state.selectedSessionId = result.sessionId;
  state.selectedSessionName = rom.displayName;
  state.selectedRomPath = null;
  state.selectedRomName = '';
  startStream(state.selectedSessionId); // 启动后立即开始预览串流
  await refreshAllData();
  setStatus('已启动并选择游戏: ' + rom.displayName);
}

// --- WebSocket 控制 ---
function startHeartbeat() {
  stopHeartbeat();
  state.heartbeatTimer = setInterval(function() {
    if (state.controlSocket && state.controlSocket.readyState === WebSocket.OPEN && state.claimedSessionId && state.claimedPortId) {
      state.controlSocket.send(JSON.stringify({
        action: 'heartbeat',
        sessionId: state.claimedSessionId,
        portId: state.claimedPortId
      }));
    }
  }, 2000);
}

function stopHeartbeat() {
  if (state.heartbeatTimer) {
    clearInterval(state.heartbeatTimer);
    state.heartbeatTimer = null;
  }
}

function closeSocket() {
  if (state.controlSocket) {
    try { state.controlSocket.close(); } catch (_error) { }
    state.controlSocket = null;
  }
  stopHeartbeat();
  resetActiveButtonSources();
  clearClaimedControlState();
}

async function ensureSessionReady() {
  if (state.selectedSessionId) return;

  if (!state.selectedRomPath)
    throw new Error('请先启动一个游戏，或从右侧选择已打开窗口');

  var result = await api('/api/sessions', {
    method: 'POST',
    body: JSON.stringify({ romPath: state.selectedRomPath })
  });
  state.selectedSessionId = result.sessionId;
  state.selectedSessionName = state.selectedRomName;
  state.selectedRomPath = null;
  state.selectedRomName = '';
  await refreshSessions();
}

async function ensureSocketClaim() {
  await ensureSessionReady();
  if (!state.selectedSessionId)
    throw new Error('当前没有可接管的游戏窗口');

  if (state.controlSocket &&
      state.controlSocket.readyState === WebSocket.OPEN &&
      state.claimedPortId === getSelectedPortId() &&
      state.claimedSessionId === state.selectedSessionId)
    return;

  if (state.controlSocket || state.claimedPortId != null)
    await releaseControl();

  if (state.controlClaimPromise)
    return state.controlClaimPromise;

  state.controlClaimPromise = new Promise(function(resolve, reject) {
    var settled = false;
    var socket = new WebSocket(wsUrl());

    socket.onopen = function() {
      state.controlSocket = socket;
      var claimSessionId = state.selectedSessionId;
      socket.send(JSON.stringify({
        action: 'claim',
        sessionId: claimSessionId,
        portId: getSelectedPortId(),
        clientName: dom.clientName.value.trim() || null
      }));
    };

    socket.onmessage = function(event) {
      var msg = JSON.parse(event.data);
      if (msg.type === 'claimed') {
        settled = true;
        state.claimedPortId = typeof msg.portId === 'string' ? msg.portId : getSelectedPortId();
        state.claimedSessionId = msg.sessionId || state.selectedSessionId;
        startHeartbeat();
        updateSelectionSummary();
        updateControllerSummary();
        resolve();
        return;
      }
      if (msg.type === 'released') {
        var releasedMatchesClaim = (!msg.sessionId || msg.sessionId === state.claimedSessionId) &&
          (!msg.portId || msg.portId === state.claimedPortId);

        if (releasedMatchesClaim)
          closeSocket();

        updateControllerSummary();
        setStatus(msg.message || '控制权已释放');
        return;
      }
      if (msg.type === 'error') {
        if (msg.message)
          setStatus(msg.message);

        if (!settled) {
          settled = true;
          reject(new Error(msg.message || '接管失败'));
          return;
        }

        closeSocket();
        updateControllerSummary();
      }
    };

    socket.onerror = function() {
      if (!settled) {
        settled = true;
        reject(new Error('WebSocket 连接失败'));
      }
    };

    socket.onclose = function() {
      if (state.controlSocket === socket)
        state.controlSocket = null;
      stopHeartbeat();
      resetActiveButtonSources();
      clearClaimedControlState();
      if (!settled) {
        settled = true;
        reject(new Error('WebSocket 连接已关闭'));
      }
    };
  }).finally(function() {
    state.controlClaimPromise = null;
  });

  await state.controlClaimPromise;
}

async function sendButton(button, pressed) {
  try {
    if (!pressed && !state.controlSocket && !state.controlClaimPromise)
      return;

    await ensureSocketClaim();
    if (state.controlSocket && state.controlSocket.readyState === WebSocket.OPEN) {
      var portId = state.claimedPortId || getSelectedPortId();
      var sessionId = state.claimedSessionId || state.selectedSessionId;
      state.controlSocket.send(JSON.stringify({
        action: 'input',
        sessionId: sessionId,
        portId: portId,
        inputs: [
          {
            portId: portId,
            deviceType: 'webpad',
            actionId: button.toLowerCase(),
            value: pressed ? 1 : 0
          }
        ]
      }));
    }
  } catch (error) {
    setStatus(error.message);
  }
}

async function releaseControl() {
  await releaseAllActiveButtons();

  var releasePortId = state.claimedPortId || getSelectedPortId();
  var releaseSessionId = state.claimedSessionId || state.selectedSessionId;

  if (state.controlSocket && state.controlSocket.readyState === WebSocket.OPEN) {
    state.controlSocket.send(JSON.stringify({
      action: 'release',
      sessionId: releaseSessionId,
      portId: releasePortId
    }));
    closeSocket();
    return;
  }

  if (releaseSessionId && releasePortId)
    await api('/api/sessions/' + releaseSessionId + '/release', {
      method: 'POST',
      body: JSON.stringify({ portId: releasePortId })
    });

  clearClaimedControlState();
}

async function handlePlayerSelectionChanged() {
  updateSelectionSummary();
  updateControllerSummary();

  if (!hasActiveControlClaim())
    return;

  if (state.claimedPortId === getSelectedPortId() && state.claimedSessionId === state.selectedSessionId)
    return;

  try {
    if (state.controlClaimPromise) {
      try { await state.controlClaimPromise; } catch (_error) { }
    }

    if (state.controlSocket || state.claimedPortId != null)
      await releaseControl();

    if (isControllerViewVisible() && state.selectedSessionId) {
      await ensureSocketClaim();
      updateControllerSummary();
      setStatus('已切换到 ' + playerLabel() + ' 控制');
    } else {
      setStatus('已切换当前目标玩家为 ' + playerLabel());
    }

    await refreshSessions();
  } catch (error) {
    setStatus(error.message);
  }
}

async function closeCurrentSession() {
  if (!state.selectedSessionId)
    throw new Error('当前没有可关闭的游戏窗口');

  await releaseControl();
  await api('/api/sessions/' + state.selectedSessionId + '/close', { method: 'POST' });
  var closedName = state.selectedSessionName;
  state.selectedSessionId = null;
  state.selectedSessionName = '';
  await refreshAllData();
  showSelectionView();
  setStatus('已关闭游戏: ' + closedName);
}

// --- 预览刷新 ---
function refreshLivePreviewImages() {
  state.previewRefreshVersion += 1;
  document.querySelectorAll('[data-session-preview]').forEach(function(img) {
    img.src = sessionPreviewUrl(img.getAttribute('data-session-preview'), state.previewRefreshVersion);
  });
}


function startAutoRefresh() {
  stopAutoRefresh();
  state.listRefreshTimer = setInterval(function() { refreshAllData().catch(function() { }); }, 5000);
  state.sidePreviewRefreshTimer = setInterval(refreshLivePreviewImages, 1200); // 右侧 session 列表缩略图
}

function stopAutoRefresh() {
  if (state.listRefreshTimer) { clearInterval(state.listRefreshTimer); state.listRefreshTimer = null; }
  if (state.sidePreviewRefreshTimer) { clearInterval(state.sidePreviewRefreshTimer); state.sidePreviewRefreshTimer = null; }
}

// --- 事件绑定 ---
function setupEventListeners() {
  // 按钮控制（支持触控）
  document.querySelectorAll('[data-button]').forEach(function(button) {
    var buttonName = button.getAttribute('data-button');

    function handlePress(event) {
      if (event.pointerType === 'mouse' && event.button != null && event.button !== 0)
        return;

      preventDefaultIfCancelable(event);
      clearTextSelection();
      var pointerId = event.pointerId;
      var sourceId = getPointerSourceId(pointerId);
      try {
        if (pointerId != null && button.setPointerCapture)
          button.setPointerCapture(pointerId);
      } catch (_error) { }
      setButtonSourceState(buttonName, sourceId, true);
    }

    function handleRelease(event) {
      if (event.pointerType === 'mouse' && event.button != null && event.button !== 0)
        return;

      preventDefaultIfCancelable(event);
      clearTextSelection();
      var pointerId = event.pointerId;
      setButtonSourceState(buttonName, getPointerSourceId(pointerId), false);
      try {
        if (pointerId != null && button.hasPointerCapture && button.hasPointerCapture(pointerId))
          button.releasePointerCapture(pointerId);
      } catch (_error) { }
    }

    function preventNativeInteraction(event) {
      preventDefaultIfCancelable(event);
      clearTextSelection();
    }

    button.addEventListener('pointerdown', handlePress);
    button.addEventListener('pointerup', handleRelease);
    button.addEventListener('pointercancel', handleRelease);
    button.addEventListener('lostpointercapture', handleRelease);
    button.addEventListener('mousedown', preventNativeInteraction);
    button.addEventListener('touchstart', preventNativeInteraction, { passive: false });
    button.addEventListener('touchmove', preventNativeInteraction, { passive: false });
    button.addEventListener('dragstart', preventNativeInteraction);
    
    // 防止长按菜单
    button.addEventListener('contextmenu', preventNativeInteraction);
  });

  function handleDocumentPointerRelease(event) {
    var sourceId = getPointerSourceId(event.pointerId);
    if (!isSourceActive(sourceId))
      return;

    preventDefaultIfCancelable(event);
    clearTextSelection();
    releasePointerSource(event.pointerId);
  }

  document.addEventListener('pointerup', handleDocumentPointerRelease, true);
  document.addEventListener('pointercancel', handleDocumentPointerRelease, true);

  if (dom.selectionToolbarMoreButton) {
    dom.selectionToolbarMoreButton.onclick = function() {
      setSelectionToolbarMoreOpen(!state.selectionToolbarMoreOpen);
    };
  }

  document.addEventListener('pointerdown', function(event) {
    if (state.selectionToolbarMoreOpen && dom.selectionToolbar && !dom.selectionToolbar.contains(event.target))
      setSelectionToolbarMoreOpen(false);
  }, true);

  // 刷新按钮
  document.getElementById('refreshButton').onclick = async function() {
    try {
      setSelectionToolbarMoreOpen(false);
      await refreshAllData();
      setStatus('已刷新 ROM 列表和游戏窗口');
    } catch (error) {
      setStatus(error.message);
    }
  };

  // 开始控制
  document.getElementById('startControlButton').onclick = async function() {
    try {
      setSelectionToolbarMoreOpen(false);
      await ensureSocketClaim();
      // 若串流还未对该 session 建立（如直接从 ROM 列表启动），补充启动
      if (state.selectedSessionId && StreamManager.currentSessionId !== state.selectedSessionId) {
        startStream(state.selectedSessionId);
      }
      updateControllerSummary();
      showControllerView();
      setStatus('已接入 ' + playerLabel() + ' 控制');
    } catch (error) {
      setStatus(error.message);
    }
  };

  // 释放控制
  document.getElementById('releaseButton').onclick = async function() {
    try {
      await releaseControl();
      setStatus('已释放控制');
    } catch (error) {
      setStatus(error.message);
    }
  };

  // 返回选台
  document.getElementById('backToSelectionButton').onclick = async function() {
    try {
      await releaseControl();
      await refreshSessions();
      showSelectionView();
      setStatus('已返回选台页面，游戏保持运行');
    } catch (error) {
      setStatus(error.message);
    }
  };

  // 关闭游戏
  document.getElementById('closeSessionButton').onclick = async function() {
    try {
      await closeCurrentSession();
    } catch (error) {
      setStatus(error.message);
    }
  };

  // 全屏
  document.getElementById('fullscreenButton').onclick = async function() {
    try {
      setSelectionToolbarMoreOpen(false);
      if (getFullscreenElement()) {
        await exitNativeFullscreen();
      } else {
        if (document.documentElement.requestFullscreen || document.documentElement.webkitRequestFullscreen) {
          await requestNativeFullscreen(document.documentElement);
        } else if (state.isMobile) {
          setStatus('当前手机浏览器不支持网页原生全屏，沉浸模式已关闭；建议直接横屏使用。');
        } else {
          throw new Error('当前浏览器不支持网页全屏');
        }
      }
    } catch (error) {
      setStatus(error.message);
    }
  };

  if (dom.controllerChromeButton) {
    dom.controllerChromeButton.onclick = function() {
      revealControllerChrome();
    };
  }

  if (dom.controllerHeader) {
    dom.controllerHeader.addEventListener('pointerdown', function() {
      revealControllerChrome();
    });
    dom.controllerHeader.addEventListener('focusin', function() {
      setControllerChromeVisible(true);
      clearControllerChromeHideTimer();
    });
    dom.controllerHeader.addEventListener('focusout', function() {
      scheduleControllerChromeHide();
    });
  }

  if (dom.controllerStream) {
    dom.controllerStream.addEventListener('pointerdown', function() {
      revealControllerChrome();
    });
  }

  if (dom.keybindPanelButton) {
    dom.keybindPanelButton.onclick = function() {
      setKeybindPanelOpen(!state.keybindPanelOpen);
    };
  }

  if (dom.closeKeybindPanelButton) {
    dom.closeKeybindPanelButton.onclick = function() {
      setKeybindPanelOpen(false);
    };
  }

  if (dom.resetKeybindsButton) {
    dom.resetKeybindsButton.onclick = function() {
      state.keyBindings = cloneDefaultKeyBindings();
      saveKeyBindings();
      cancelKeyCapture(false);
      renderKeybindUi();
      setStatus('已恢复默认键盘映射');
    };
  }

  // ROM 分页
  dom.romPrevPageButton.onclick = function() {
    if (state.romPage <= 1) return;
    state.romPage -= 1;
    renderRoms();
  };
  dom.romNextPageButton.onclick = function() {
    if (state.romPage >= getRomPageCount()) return;
    state.romPage += 1;
    renderRoms();
  };

  // 会话分页
  dom.sessionPrevPageButton.onclick = function() {
    if (state.sessionPage <= 1) return;
    state.sessionPage -= 1;
    renderSessions();
  };
  dom.sessionNextPageButton.onclick = function() {
    if (state.sessionPage >= getSessionPageCount()) return;
    state.sessionPage += 1;
    renderSessions();
  };

  // 移动端 ROM 分页
  dom.mobileRomPrevPageButton.onclick = function() {
    if (state.romPage <= 1) return;
    state.romPage -= 1;
    renderRoms();
  };
  dom.mobileRomNextPageButton.onclick = function() {
    if (state.romPage >= getRomPageCount()) return;
    state.romPage += 1;
    renderRoms();
  };

  // 移动端会话分页
  dom.mobileSessionPrevPageButton.onclick = function() {
    if (state.sessionPage <= 1) return;
    state.sessionPage -= 1;
    renderSessions();
  };
  dom.mobileSessionNextPageButton.onclick = function() {
    if (state.sessionPage >= getSessionPageCount()) return;
    state.sessionPage += 1;
    renderSessions();
  };

  // 面板折叠
  dom.toggleRomPanelButton.onclick = function() {
    dom.romPanel.classList.toggle('is-collapsed');
    dom.toggleRomPanelButton.textContent = dom.romPanel.classList.contains('is-collapsed') ? '▶' : '◀';
    dom.contentGrid.classList.toggle('hide-rom-panel', dom.romPanel.classList.contains('is-collapsed'));
  };

  dom.toggleSessionPanelButton.onclick = function() {
    dom.sessionPanel.classList.toggle('is-collapsed');
    dom.toggleSessionPanelButton.textContent = dom.sessionPanel.classList.contains('is-collapsed') ? '◀' : '▶';
    dom.contentGrid.classList.toggle('hide-session-panel', dom.sessionPanel.classList.contains('is-collapsed'));
  };

  // 玩家选择
  dom.playerSelect.onchange = function() {
    handlePlayerSelectionChanged().catch(function(error) {
      setStatus(error.message);
    });
  };

  window.addEventListener('keydown', handleKeyboardKeydown, true);
  window.addEventListener('keyup', handleKeyboardKeyup, true);
  var releaseActiveInputs = function() {
    releaseAllActiveButtons().catch(function() { });
  };

  window.addEventListener('blur', releaseActiveInputs);
  window.addEventListener('pagehide', releaseActiveInputs);
  document.addEventListener('visibilitychange', function() {
    if (document.hidden) {
      releaseActiveInputs();
      clearControllerChromeHideTimer();
    } else if (isControllerViewVisible()) {
      revealControllerChrome();
    }
  });

  // 页面卸载
  window.addEventListener('beforeunload', function() {
    stopAutoRefresh();
    stopInputWatchdog();
    clearControllerChromeHideTimer();
    releaseActiveInputs();
    closeSocket();
    stopStream();
  });
}

// --- 初始化 ---
function init() {
  loadKeyBindings();
  state.activeButtonSources = createActiveButtonSourceMap();
  state.dispatchedButtonStates = createDispatchedButtonStateMap();
  startInputWatchdog();
  setSelectionToolbarMoreOpen(false);
  setControllerChromeVisible(true);
  detectDevice();
  setupOrientationListener();
  updateLayout();
  updateKeybindHint();
  renderKeybindUi();
  setupEventListeners();

  // 游戏控制界面：阻止触摸/鼠标操作时产生文字选择
  var blockSelection = function(e) {
    if (shouldSuppressMobileSelection(e.target) || dom.controllerView.contains(e.target))
      e.preventDefault();
  };
  var clearMobileSelection = function() {
    if (!state.isMobile)
      return;

    var active = document.activeElement;
    if (isEditableTarget(active))
      return;

    clearTextSelection();
  };

  dom.controllerView.addEventListener('selectstart', blockSelection, true);
  dom.controllerView.addEventListener('contextmenu', blockSelection, true);
  dom.mobileSelection.addEventListener('selectstart', blockSelection, true);
  dom.mobileSelection.addEventListener('contextmenu', blockSelection, true);
  document.addEventListener('selectstart', blockSelection, true);
  document.addEventListener('contextmenu', blockSelection, true);
  dom.controllerView.addEventListener('dragstart', blockSelection, true);
  dom.mobileSelection.addEventListener('dragstart', blockSelection, true);
  document.addEventListener('dragstart', blockSelection, true);
  document.addEventListener('selectionchange', clearMobileSelection, true);
  document.addEventListener('pointerup', clearMobileSelection, true);
  document.addEventListener('pointercancel', clearMobileSelection, true);
  document.addEventListener('touchend', clearMobileSelection, true);
  document.addEventListener('touchcancel', clearMobileSelection, true);
  document.addEventListener('fullscreenchange', updateFullscreenButton);
  document.addEventListener('webkitfullscreenchange', updateFullscreenButton);

  dom.entryUrl.textContent = location.href;
  showSelectionView();
  refreshAllData().then(function() {
    startAutoRefresh();
    scheduleMobileSelectionScale();
  }).catch(function(error) {
    setStatus(error.message);
    startAutoRefresh();
    scheduleMobileSelectionScale();
  });
}

// 启动
init();
