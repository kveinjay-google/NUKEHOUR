# macOS Launcher Website Link and Version Footer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the macOS launcher emblem open the NUKE HOUR website and show `NUKE HOUR <engine version>` as a persistent right-bottom footer.

**Architecture:** Keep the existing launcher shell and version source unchanged. Add one small browser action method to `Launcher`, bind it to the existing emblem label, and expose a deterministic content-footer rectangle from `launcher_shell_layout`; render one root-level version label after page construction so it stays visible across page switches.

**Tech Stack:** Python 3, Tkinter/Aqua, `webbrowser`, `unittest`.

---

### Task 1: Add failing launcher contracts

**Files:**
- Modify: `packaging/tests/test_macos_launcher_layout.py`
- Modify: `launcher.py` only if a test import requires a public constant/helper; do not add production behavior in this task.

- [ ] **Step 1: Write the failing tests**

Add tests for the exact public contract:

```python
from unittest import mock


class MacosLauncherLayoutTest(unittest.TestCase):
    # Keep the existing tests in this class.

    def test_version_footer_is_inside_content_and_right_bottom_aligned(self):
        layout = launcher.launcher_shell_layout()
        x, y, width, height = layout["version_footer"]
        content_x, content_y, content_width, content_height = layout["content"]

        self.assertGreaterEqual(x, content_x)
        self.assertGreaterEqual(y, content_y)
        self.assertLessEqual(x + width, content_x + content_width)
        self.assertLessEqual(y + height, content_y + content_height)
        self.assertEqual(x + width, launcher.WIN_W - 16)
        self.assertEqual(y + height, launcher.WIN_H - 12)

    def test_version_text_uses_brand_and_engine_version_without_ra2(self):
        text = launcher.launcher_version_text("release-test")

        self.assertEqual("NUKE HOUR release-test", text)
        self.assertNotIn("RA2", text)

    @mock.patch("launcher.webbrowser.open_new_tab", return_value=True)
    def test_open_brand_website_uses_nuke_hour_url(self, open_new_tab):
        launcher.Launcher._open_brand_website(object())

        open_new_tab.assert_called_once_with("https://nukehour.com")


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run the targeted tests and verify the red state**

Run:

```bash
python3 -m unittest packaging.tests.test_macos_launcher_layout -v
```

Expected: the existing layout tests pass, while the new tests fail because
`version_footer`, `launcher_version_text`, and `_open_brand_website` do not yet
exist.

### Task 2: Implement the website link and persistent version footer

**Files:**
- Modify: `launcher.py:8-35,195-233,1099-1185,1342-1368`
- Test: `packaging/tests/test_macos_launcher_layout.py`

- [ ] **Step 1: Add the browser dependency, URL, version formatter, and layout rectangle**

Import `webbrowser`, define `BRAND_WEBSITE_URL = "https://nukehour.com"`, add:

```python
def launcher_version_text(version):
    return f"{BRAND_NAME} {version}"
```

Extend `launcher_shell_layout()` with a `version_footer` rectangle whose right
edge is `width - 16` and bottom edge is `height - 12`, using an 180px width and
18px height so it remains stable for the current version strings.

- [ ] **Step 2: Bind the existing emblem to the website action**

Create the existing image label as a named widget, set `cursor="hand2"`, bind
`<Button-1>` to `self._open_brand_website`, and add:

```python
def _open_brand_website(self, _event=None):
    try:
        if not webbrowser.open_new_tab(BRAND_WEBSITE_URL):
            raise RuntimeError("Unable to open the default browser")
    except Exception as exc:
        messagebox.showerror(_tr("操作失败"), str(exc), parent=self)
```

The handler must not alter the current launcher page or synchronously fetch the
website.

- [ ] **Step 3: Render the version footer once at root level and remove the old home label**

After `_build_pages()` and before the initial `show_page()` call, create one
`tk.Label` as a child of the root window with text
`launcher_version_text(self._engine_version())`, `bg=PANEL`, `fg=DIM`, and
`font=FONT_SMALL`. Place it with the `version_footer` rectangle and
`anchor="e"`. Delete the home-page-only label that currently renders
`NUKE HOUR <version> · RA2`.

- [ ] **Step 4: Run the targeted tests and verify the green state**

Run:

```bash
python3 -m unittest packaging.tests.test_macos_launcher_layout -v
```

Expected: all layout, URL, and version-text tests pass.

### Task 3: Run regression checks and commit

**Files:**
- Verify: `launcher.py`
- Verify: `packaging/tests/test_macos_launcher_layout.py`

- [ ] **Step 1: Run Python syntax and targeted regression checks**

Run:

```bash
python3 -m py_compile launcher.py launcher_i18n.py
python3 -m unittest packaging.tests.test_macos_launcher_layout -v
```

Expected: both commands exit with status 0 and the unittest suite reports zero
failures or errors.

- [ ] **Step 2: Review the diff and commit only the intended files**

Run:

```bash
git diff --check
git diff -- launcher.py packaging/tests/test_macos_launcher_layout.py
git status --short
git add launcher.py packaging/tests/test_macos_launcher_layout.py
git commit -m "feat: link launcher emblem and move version footer"
```

The commit must not stage the pre-existing untracked build, cache, preview, or
test-result paths in the worktree.
