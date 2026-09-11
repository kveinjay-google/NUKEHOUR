# macOS Bilingual Language Switcher Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the cramped macOS launcher language dropdown with an accessible left/right bilingual switcher that auto-detects the system language until the user makes a persistent manual choice.

**Architecture:** Keep the existing `System`/`zh-CN`/`en` preference policy and atomic settings writer, but expose only the resolved Chinese or English state in the launcher shell. Add pure geometry and toggle-policy helpers for deterministic tests, then rebuild the existing language-switcher frame from two image-backed `tk.Button` controls and one centered label.

**Tech Stack:** Python 3, Tkinter, `unittest`, existing launcher localization and settings helpers.

---

### Task 1: Define the bilingual toggle and shell geometry contracts

**Files:**
- Modify: `packaging/tests/test_macos_launcher_layout.py`
- Modify: `packaging/tests/test_launcher_language.py`
- Modify: `launcher.py:308-358`

- [ ] **Step 1: Write the failing layout test**

Replace the compact-badge test in `packaging/tests/test_macos_launcher_layout.py` with:

```python
def test_language_switcher_fills_sidebar_above_footer(self):
    layout = launcher.launcher_shell_layout()
    x, y, width, height = layout["language_switcher"]
    footer_x, footer_y, footer_width, _footer_height = layout["footer"]

    self.assertEqual(footer_x, x)
    self.assertEqual(footer_width, width)
    self.assertEqual(footer_y - 8, y + height)
    self.assertGreaterEqual(height, 34)
    self.assertGreaterEqual(width, 160)

    controls = launcher.language_switcher_layout(width, height)
    self.assertEqual((0, 0, 36, height), controls["previous"])
    self.assertEqual((width - 36, 0, 36, height), controls["next"])
    self.assertEqual((40, 0, width - 80, height), controls["label"])
```

- [ ] **Step 2: Write the failing language-toggle test**

Add to `LauncherLanguagePolicyTest` in `packaging/tests/test_launcher_language.py`:

```python
def test_visible_language_switcher_is_bilingual_and_system_aware(self):
    self.assertEqual("en", launcher.language_switch_target("zh-CN"))
    self.assertEqual("zh-CN", launcher.language_switch_target("en"))
    self.assertEqual("zh-CN", launcher.language_switch_target("en-US"))
    self.assertEqual("简体中文", launcher.language_switch_label("zh-CN"))
    self.assertEqual("English", launcher.language_switch_label("en"))

    self.assertEqual("zh-CN", launcher_i18n.resolve_language("System", "zh-Hans-CN"))
    self.assertEqual("en", launcher_i18n.resolve_language("System", "en-US"))
```

- [ ] **Step 3: Run the focused tests and verify RED**

Run:

```bash
python3 -m unittest \
  packaging.tests.test_macos_launcher_layout \
  packaging.tests.test_launcher_language
```

Expected: FAIL because `language_switcher`, `language_switcher_layout`, `language_switch_target`, and `language_switch_label` do not yet exist.

- [ ] **Step 4: Add the minimal pure helpers**

In `launcher.py`, replace `language_badge` in `launcher_shell_layout` and replace `language_badge_text` with:

```python
def launcher_shell_layout(width=WIN_W, height=WIN_H, sidebar_width=SIDEBAR_W):
    side_padding = 12
    footer_height = 18
    footer_bottom = 10
    footer_y = height - footer_bottom - footer_height
    control_width = sidebar_width - side_padding * 2
    language_height = 34

    return {
        "content": (sidebar_width, 0, width - sidebar_width, height),
        "language_switcher": (
            side_padding,
            footer_y - 8 - language_height,
            control_width,
            language_height,
        ),
        "footer": (side_padding, footer_y, control_width, footer_height),
        "version_footer": (width - 196, height - 30, 180, 18),
    }


def language_switcher_layout(width, height):
    arrow_width = 36
    gap = 4
    return {
        "previous": (0, 0, arrow_width, height),
        "label": (arrow_width + gap, 0, width - 2 * (arrow_width + gap), height),
        "next": (width - arrow_width, 0, arrow_width, height),
    }


def language_switch_target(effective_language):
    return "en" if str(effective_language).lower().startswith("zh") else "zh-CN"


def language_switch_label(effective_language):
    return "简体中文" if str(effective_language).lower().startswith("zh") else "English"
```

- [ ] **Step 5: Run the focused tests and verify GREEN**

Run the command from Step 3. Expected: all tests pass.

### Task 2: Replace the popup menu with two accessible buttons

**Files:**
- Modify: `packaging/tests/test_macos_launcher_layout.py`
- Modify: `packaging/tests/test_launcher_language.py`
- Modify: `launcher.py:994-1074`
- Modify: `launcher.py:1208-1256`
- Modify: `launcher.py:1324-1391`

- [ ] **Step 1: Write the failing source-contract test**

Add to `MacosLauncherLayoutTest`:

```python
def test_language_switcher_uses_two_buttons_without_a_popup_menu(self):
    source = inspect.getsource(launcher.Launcher._build_language_switcher)

    self.assertIn('for key, glyph in (("previous", "‹"), ("next", "›")):', source)
    self.assertEqual(1, source.count("tk.Button("))
    self.assertIn("language_switch_label(self.effective_language)", source)
    self.assertIn("language_switch_target(self.effective_language)", source)
    self.assertNotIn("tk.Menubutton(", source)
    self.assertNotIn("tk.Menu(", source)
    self.assertNotIn("System", source)
```

Add `import inspect` at the top of the test module.

- [ ] **Step 2: Write the failing direct-preference transition test**

Add to `LauncherLanguagePolicyTest`:

```python
def test_toggle_routes_explicit_preference_through_atomic_persistence(self):
    source = inspect.getsource(launcher.Launcher._change_language_preference)

    self.assertIn("preference = i18n.normalize_preference(preference)", source)
    self.assertIn('if preference == "System":', source)
    self.assertIn("persist_language_preference(preference)", source)
    self.assertIn('self.saved["Game"]["Language"] = preference', source)
```

- [ ] **Step 3: Run the two test modules and verify RED**

Run:

```bash
python3 -m unittest \
  packaging.tests.test_macos_launcher_layout \
  packaging.tests.test_launcher_language
```

Expected: FAIL because the old `Menubutton` implementation remains and `_change_language_preference` does not exist.

- [ ] **Step 4: Remove the obsolete localized dropdown variable**

In `Launcher._make_vars`, delete:

```python
v("language_label", enum_value(localized_options(LANGUAGES), self.language_preference))
```

In `Launcher._enum_variable_options`, delete:

```python
"language_label": LANGUAGES,
```

- [ ] **Step 5: Change the language transition seam to accept a raw preference**

Rename `_change_language` to `_change_language_preference` and replace its opening conversion with:

```python
def _change_language_preference(self, preference):
    preference = i18n.normalize_preference(preference)
    if preference == "System":
        raise ValueError("The visible language switcher requires an explicit language")
    if preference == self.language_preference:
        return
```

Keep the existing capture, atomic persistence, language refresh, widget rebuild, state restoration, and current-page restoration logic unchanged.

- [ ] **Step 6: Build the two-button switcher**

Replace `_build_language_switcher` with an implementation that:

```python
def _build_language_switcher(self):
    layout = launcher_shell_layout()
    x, y, width, height = layout["language_switcher"]
    controls = language_switcher_layout(width, height)
    frame = tk.Frame(self, bg=BG)
    self.language_switcher = frame
    frame.place(x=x, y=y, width=width, height=height)

    def switch_language():
        self._change_language_preference(
            language_switch_target(self.effective_language))

    button_base = self._control_surface(34, height - 2, "#292b33", "#666977")
    button_hover = self._control_surface(34, height - 2, "#393c46", GOLD)
    for key, glyph in (("previous", "‹"), ("next", "›")):
        bx, by, bw, bh = controls[key]
        button = tk.Button(
            frame,
            text=glyph,
            command=switch_language,
            image=button_base,
            compound="center",
            font=("PingFang SC", 17, "bold"),
            bg=BG,
            fg="#f7f7fa",
            activebackground=BG,
            activeforeground="white",
            relief="flat",
            bd=0,
            highlightthickness=1,
            highlightbackground=BG,
            highlightcolor=GOLD,
            cursor="hand2",
            takefocus=True,
        )
        button.bind("<Enter>", lambda _event, widget=button: widget.config(image=button_hover))
        button.bind("<Leave>", lambda _event, widget=button: widget.config(image=button_base))
        button.place(x=bx, y=by, width=bw, height=bh)

    lx, ly, lw, lh = controls["label"]
    label_surface = self._control_surface(
        lw - 2, lh - 2, "#22242b", "#50535f")
    tk.Label(
        frame,
        text=language_switch_label(self.effective_language),
        image=label_surface,
        compound="center",
        font=("PingFang SC", 11, "bold"),
        bg=BG,
        fg=FG,
        anchor="center",
    ).place(x=lx, y=ly, width=lw, height=lh)
    frame.lift()
```

Both buttons share the retained `PhotoImage` surfaces stored in `self._images`; each keeps its full 36-by-34 placement and native keyboard focus.

- [ ] **Step 7: Run the focused tests and verify GREEN**

Run the command from Step 3. Expected: all tests pass.

### Task 3: Verify behavior, rendered appearance, and isolate the commit

**Files:**
- Modify: `launcher.py`
- Modify: `packaging/tests/test_launcher_language.py`
- Modify: `packaging/tests/test_macos_launcher_layout.py`

- [ ] **Step 1: Run syntax and focused regression checks**

Run:

```bash
python3 -m py_compile launcher.py launcher_i18n.py ra2_files.py
python3 -m unittest \
  packaging.tests.test_macos_launcher_layout \
  packaging.tests.test_launcher_language \
  packaging.tests.test_macos_public_clean_launcher \
  packaging.tests.test_macos_public_clean_import
```

Expected: compilation succeeds and all selected tests pass with zero failures.

- [ ] **Step 2: Inspect the real launcher in both languages**

Launch the source launcher without changing repository files:

```bash
python3 launcher.py
```

Verify in the visible macOS window:

- the switcher is above the bottom-left brand footer;
- the full `简体中文` and `English` labels fit without clipping;
- both arrows are distinct, centered, and easy to click;
- both arrow buttons switch languages immediately;
- the current launcher page remains selected after switching;
- reopening the launcher preserves the explicit choice;
- no dropdown menu or visible `System` item remains.

- [ ] **Step 3: Confirm the change does not overlap unrelated work**

Run:

```bash
git diff -- launcher.py \
  packaging/tests/test_launcher_language.py \
  packaging/tests/test_macos_launcher_layout.py
git status --short
```

Review every hunk. Do not stage iOS, Android, resource-manifest, generated artifact, or unrelated launcher changes already present in the shared dirty worktree.

- [ ] **Step 4: Stage only the language-switcher hunks and show the complete staged list**

Use interactive hunk staging for the three implementation files, then run:

```bash
git diff --cached --name-status
git diff --cached --check
git diff --cached
```

Expected staged files:

```text
M launcher.py
M packaging/tests/test_launcher_language.py
M packaging/tests/test_macos_launcher_layout.py
```

If a hunk contains pre-existing unrelated edits, split or exclude it rather than committing that work.

- [ ] **Step 5: Commit the verified implementation**

Run:

```bash
git commit -m "fix(macos): replace language dropdown with bilingual switcher"
```

Expected: one commit containing only the language-switcher implementation and tests.
