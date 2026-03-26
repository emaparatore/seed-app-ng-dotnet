---
name: screenshot-loop
description: >
  Visual feedback loop for frontend development: take a screenshot of the
  running app, analyze the result, fix issues, and repeat until the UI matches
  expectations. Use this skill whenever the user wants Claude Code to: visually
  verify frontend changes; self-correct UI work by looking at the result;
  iterate on CSS/layout/styling until it looks right; compare the live app
  against a design reference or description; do visual QA on a running dev
  server. Trigger when the user says things like "check how it looks",
  "screenshot loop", "visual feedback", "keep fixing until it looks right",
  "compare with the design", "auto-improve the UI", "take a screenshot and
  fix it", or any request where Claude Code should see its own frontend output
  and self-correct. Also trigger when the user asks to verify responsive
  layouts at different viewport sizes or to visually regression-test a
  component after code changes.
---

# Screenshot Loop — Visual Self-Correction for Frontend Development

This skill gives Claude Code the ability to **see** what it builds in the
browser, evaluate the result, and iterate until the UI matches the desired
outcome. It closes the feedback loop that's normally missing when a headless
agent works on frontend code.

## Why this matters

Without visual feedback, Claude Code is essentially coding blind on frontend
tasks. It can write correct HTML/CSS/JS but has no way to verify spacing,
alignment, colors, responsive behavior, or overall visual coherence. This
skill turns Claude Code into a sighted developer who checks the browser after
every meaningful change.


## Modes

The skill's intensity is controlled by a **mode** setting. This allows each
project to calibrate how often the visual loop runs, balancing thoroughness
against token cost. The screenshot always happens **at the end of a
completed task**, never after every single file save.

### Available modes

| Mode      | When it triggers                            | Max passes | Best for                                 |
|-----------|---------------------------------------------|------------|------------------------------------------|
| `always`  | At the end of **every** frontend task       | 5          | Active UI development, new features      |
| `soft`    | Only after heavy frontend tasks (new pages, | 3          | Mature projects, maintenance             |
|           | complex layouts, responsive, design refs)   |            |                                          |
| `off`     | Never (user verifies manually)              | —          | Backend-heavy phases, quick fixes        |

**Default mode: `always`** — if the project's `CLAUDE.md` doesn't specify
a mode, always verify at the end of every frontend task.

### How to set the mode

Add a line to the project's `CLAUDE.md`:

```markdown
# Screenshot Loop
screenshot-loop-mode: soft
```

The mode can be changed at any time. It's also fine for the user to override
inline during a conversation (e.g. "skip the screenshot loop here" or
"do a screenshot check on this one").

### How each mode decides whether to trigger

- **always:** Every task that touches frontend files (HTML, CSS, SCSS,
  component templates, UI-related TypeScript) gets a screenshot check once
  the task is complete and the result is visible in the browser.
- **soft:** Apply judgment. Trigger when the completed task involves:
  building a new component or page; changing layout or positioning;
  responsive adjustments; multi-component coordination; or comparing against
  a design reference. Skip for: color-only tweaks, copy/text updates, adding
  a CSS class to an existing pattern, renaming, single-line style fixes.
- **off:** Only trigger if the user explicitly asks for a screenshot.

This skill uses **Playwright CLI** (`@playwright/cli`) — Microsoft's
recommended tool for AI coding agents. It's ~4× more token-efficient than
the MCP alternative because screenshots and snapshots are saved to disk
instead of being streamed into the context window.

```bash
npm install -g @playwright/cli
# Chromium is auto-installed on first use
```

Verify with: `playwright-cli --version`

If `playwright-cli` is not found when the skill runs, install it
automatically before proceeding.


## The Loop

The core workflow is a **screenshot → evaluate → fix → repeat** cycle. Each
iteration is called a **pass**. The skill exits when the visual result is
acceptable or when a maximum number of passes is reached.

### Phase 0 — Setup

Before entering the loop, establish the context:

1. **Read the mode.** Check the project's `CLAUDE.md` for
   `screenshot-loop-mode`. If absent, default to `always`. If the
   current task doesn't qualify for the active mode, skip the loop entirely.
2. **Identify the target URL.** Usually `http://localhost:<port>/<route>`.
   If the dev server isn't running, start it (e.g. `ng serve`, `npm run dev`,
   `dotnet run`) and wait for it to be ready.
3. **Capture the acceptance criteria.** What should the UI look like? This
   can come from:
   - A natural-language description from the user
   - A reference image provided by the user (design mockup, Figma export)
   - The current task's requirements (from a plan document or user story)
4. **Set viewport(s).** Default: `1280×800`. If responsive testing is
   requested, define a list (e.g. `375×812`, `768×1024`, `1280×800`).
5. **Set max passes.** Use the mode's default (always: 5,
   soft: 3), or the user's override if provided.

### Phase 1 — Screenshot

```bash
# Navigate to the page
playwright-cli open http://localhost:4200/dashboard --headed

# Wait for network idle (important for SPAs)
playwright-cli wait --state networkidle

# Take the screenshot — saved to disk, minimal context cost
playwright-cli screenshot
# Output: .playwright-cli/page-<timestamp>.png
```

For a specific viewport:
```bash
playwright-cli open http://localhost:4200/dashboard --viewport 375x812
```

### Phase 2 — Evaluate

Analyze the screenshot against the acceptance criteria. Structure the
evaluation as a checklist:

```
## Pass N — Evaluation

### Layout & Structure
- [ ] Main sections are positioned correctly
- [ ] Spacing between elements matches expectations
- [ ] No overlapping or clipped content

### Visual Style
- [ ] Colors match the design / description
- [ ] Typography is correct (size, weight, family)
- [ ] Icons and images render properly

### Responsive (if applicable)
- [ ] Content reflows correctly at target viewport
- [ ] No horizontal scrolling
- [ ] Touch targets are adequately sized

### Functional Appearance
- [ ] Interactive elements look clickable/tappable
- [ ] States (hover, active, disabled) are distinguishable
- [ ] Loading / empty states are handled

### Overall Verdict
- PASS → exit the loop, report success
- FAIL → list the specific issues, proceed to Phase 3
```

If a reference image was provided, do a side-by-side comparison and call out
specific pixel-level or structural differences.

### Phase 3 — Fix

Apply targeted code changes to address **only** the issues identified in
Phase 2. Important principles:

- **Fix one category at a time.** Don't try to fix layout + colors + icons
  in a single pass. Prioritize structural/layout issues first, then visual
  polish.
- **Keep changes minimal.** The goal is to converge, not to rewrite. Small,
  targeted CSS/HTML edits are better than large refactors.
- **Comment what you're fixing.** Leave a brief inline comment or commit
  message that references the pass number and issue
  (e.g. `/* screenshot-loop pass 2: fix sidebar overflow */`).

After applying fixes, if using hot-reload (Angular, React, Vite etc.), wait
for the rebuild to complete before taking the next screenshot. A simple
`sleep 2` or polling the dev server health endpoint works.

### Phase 4 — Cleanup and Exit

When the loop ends (success or max passes reached):

1. **Delete intermediate screenshots.** Keep only the final pass screenshot
   as proof of the end result. Remove all earlier pass images:
   ```bash
   # Keep only the last screenshot, delete the rest
   ls -t .playwright-cli/page-*.png | tail -n +2 | xargs rm -f
   ```
2. **Report the result** (see Output Format below).
3. If **all checks pass** → exit with a success summary.
4. If **max passes reached** → exit with a summary of remaining issues and
   suggest the user review manually.
5. Otherwise → go back to Phase 1.

Make sure `.playwright-cli/` is in the project's `.gitignore` so screenshots
never end up in version control.


## Output Format

At the end of the loop (success or max-passes), produce a concise report:

```
## Screenshot Loop Report

**Target:** http://localhost:4200/dashboard
**Viewport(s):** 1280×800
**Passes:** 3 / 5 (converged)
**Result:** PASS

### Pass History
1. ❌ Sidebar overflowing on small content — fixed padding
2. ❌ Header text color too light — updated to --color-text-primary
3. ✅ All checks pass

### Final Screenshot
.playwright-cli/page-<timestamp>.png
```


## Integration with `requirements-workflow`

When executing a frontend task from an implementation plan, the screenshot
loop can be invoked as a verification step after the code changes. The
acceptance criteria come directly from the user story or task definition.
Reference the task ID in the report (e.g. `TASK-12`).


## Tips & Edge Cases

**SPA route navigation:** If the target is a specific route in an Angular
or React SPA, make sure the dev server serves the app correctly. Use
`playwright-cli open` with the full route, or navigate after opening.

**Auth-protected pages:** If the page requires login, handle authentication
before entering the loop:
```bash
playwright-cli open http://localhost:4200/login
playwright-cli fill "[name=email]" "test@example.com"
playwright-cli fill "[name=password]" "password123"
playwright-cli click "[type=submit]"
playwright-cli wait --state networkidle
# Now navigate to the protected page
playwright-cli goto http://localhost:4200/dashboard
```

**Dark mode / theme variants:** Run the loop once per theme if the user
needs to verify both. Use a separate pass count for each.

**Animations / transitions:** Add a delay before the screenshot to let
animations settle. `playwright-cli wait --timeout 2000` or a simple sleep.

**Docker / headless environments:** If there's no display (CI, remote
server), use headless mode — the CLI uses headless by default.

**Screenshot diff (advanced):** For pixel-perfect regression checks, you
can save a "golden" screenshot and compare subsequent ones with tools like
`pixelmatch` or `resemble.js`. This is optional and typically overkill for
iterative development, but powerful for CI/CD visual regression.
