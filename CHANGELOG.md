# Changelog

All notable changes to MarkUp Markdown Editor will be documented in this file.

## [Unreleased]

### Added
- **Synchronized scrolling between panes**: Scrolling the source editor mirrors proportionally
  into the preview pane and vice versa, with echo suppression so neither pane fights the other.
  Toggleable via View ▸ Synchronized Scrolling (on by default).
- **Caret mirroring in the preview**: The editor's caret position is now shown in the preview
  pane as a blinking caret marker at the corresponding rendered position (selections were
  already mirrored via CSS Custom Highlights). When the editor pane is active, the preview
  auto-scrolls to keep the mirrored caret/selection in view.
- **Zoom parity**: Zoom In/Out/Reset now scales the preview pane together with the editor
  (previously only the editor font scaled). Added main-keyboard-row Ctrl+= / Ctrl+- accelerators
  (previously numpad-only) and Ctrl+0 for Reset Zoom.
- **Persistent settings**: Editor font family/size, zoom level, view mode, word wrap,
  synchronized scrolling, status-bar visibility, and window size are saved to
  `%LocalAppData%\MarkUp\settings.json` on close and restored on launch.
- **Unsaved-changes prompt on close**: Exiting via the menu, Alt+F4, or the title-bar close
  button now prompts to save when the document is dirty (previously only New/Open prompted).
- **Find & Replace improvements**: Ctrl+F now opens the find bar (alongside Ctrl+H), Enter /
  Shift+Enter find next/previous from the find box, Escape closes it, a live match count is
  shown, and backward search wraps around like forward search.
- **Menu state indicators**: View-mode items are now radio-checked, and Word Wrap /
  Synchronized Scrolling / Status Bar show check marks reflecting their current state.
- **Font Settings in the View menu**: Previously only reachable from the toolbar overflow.
- **`.mdown` / `.mkd` file associations**: Registered in the package manifest and added to the
  Open dialog filter (README already advertised them). `.txt` added to the Open dialog filter.

### Fixed
- **Print styles were broken for File ▸ Print and HTML export**: A missing closing brace in the
  generated `@media print` CSS swallowed every print rule after `body`, so printed pages could
  keep dark-mode colours. PDF export was unaffected (separate stylesheet).
- **Code blocks and tables no longer clip when printed**: On paper there is no scrollbar, so
  `overflow-x: auto` code blocks printed with their content cut off. Print and PDF output now
  wraps long code lines (`white-space: pre-wrap` + `overflow-wrap: anywhere`) and lays tables
  out with fixed column widths and in-cell word breaking. Tall code blocks and tables are also
  allowed to continue across page boundaries instead of being clipped by
  `page-break-inside: avoid` (rows still keep together).
- **Plain-text export corrupted fenced code blocks**: `**` / `~~` sequences inside code fences
  were stripped (breaking e.g. `**kwargs`); fence content is now preserved verbatim while the
  fence lines themselves are dropped. Single-asterisk and underscore emphasis markers are now
  stripped outside code (word-internal underscores like `snake_case` are preserved).
- **Preview could render stale content**: An editor change arriving while a preview render was
  already in flight was silently dropped; it is now queued and re-rendered.
- **App froze permanently when typing a bare `#`**: `MarkdownSelectionProjection` looped
  forever on lines starting with `#` that are not valid ATX headings (`#`, `##nospace`) —
  the paragraph fallback refused to consume them, so the document walk never advanced and
  the UI thread hung. The parser has long had the equivalent guard; the projection now has
  it too, with timeout-protected regression tests. Previously latent (the projection only
  built for non-empty selections); caret mirroring made it build on every caret move.
- **Hard crash (0xc000027b) when preview scripting failed**: Fire-and-forget
  `ExecuteScriptAsync` calls discarded the WinRT async operation; if one failed (e.g. during
  WebView2 navigation or teardown) the unobserved failure crashed the entire process with a
  stowed exception. All fire-and-forget preview scripts now run through a helper that
  observes and swallows failures, and selection/caret mirroring waits for the initial
  preview navigation to complete.
- **Ctrl+H/Ctrl+F now reliably open Find & Replace with the editor focused**: The TextBox
  consumed Ctrl+H before menu accelerators saw it; a tunneling PreviewKeyDown interceptor
  now handles both, debounced so the double pipeline (accelerator + interceptor) toggles
  exactly once. A MenuFlyoutItem also silently loses all its shortcuts if it declares two
  KeyboardAccelerators, so the extra shortcuts (Ctrl+F, main-row Ctrl+=/-, Ctrl+0) are
  registered window-level instead.
- **Ctrl+I no longer inserts a literal tab while italicizing**: The TextBox translates
  Ctrl+I into a TAB control character, which replaced the selection alongside the italic
  accelerator's wrap. The same PreviewKeyDown interceptor now handles Ctrl+I, suppressing
  the tab and applying italic exactly once.

### Test infrastructure
- **Local end-to-end mode**: `UITEST_DRIVER_URL` overrides the hard-wired remote Appium
  endpoint (skipping the WinRM package install), enabling the UI suite to run against a locally
  started Appium + WinAppDriver. Recipe in `MarkUp.UITests/README.md`.
- **Deterministic app state under test**: `MARKUP_UITEST=1` (set by the Appium host
  environment) disables settings persistence and the unsaved-changes close prompt inside the
  app so runs cannot inherit state from earlier launches or block on teardown dialogs.
- **Menu bar AutomationIds**: `MenuBarFile/Edit/Format/View/Help` now carry explicit
  AutomationIds, so menu lookups resolve directly instead of falling back to by-name UIA tree
  crawls that intermittently blocked for minutes inside the WebView2 accessibility subtree.
- **Idle-session reaping disabled**: Both driver sessions set `newCommandTimeout: 0`; Appium's
  60-second default killed the desktop session while the app session was busy.

### Performance
- **Eliminated a full Markdown→HTML re-parse on every keystroke and caret move**: The
  automation-state HTML fragment is now cached per content revision.
- **Preview text-map caching**: The DOM text-node map used for selection/caret mirroring is
  built once per content change instead of once per caret move.
- **Redundant preview DOM updates skipped**: Identical rendered HTML is no longer re-pushed to
  the preview, preserving selection state and avoiding needless layout work.

## [1.7.0] - 2026-04-22

### Added
- **Rich-text paste — HTML to Markdown conversion**: Pasting HTML content (from browsers,
  Microsoft Word, Outlook, or any app that writes CF_HTML to the clipboard) now automatically
  converts the HTML to clean Markdown before inserting it into the editor. Bold, italic,
  bold+italic, strikethrough, headings, links, images, tables, code blocks, blockquotes, and
  ordered/unordered lists are all converted. Word/Office noise (namespace tags, `mso-*` styles,
  conditional comments, `lang` attributes) is stripped before conversion.
- **Rich-text paste into the preview pane**: When the preview pane has focus, pasting HTML
  clipboard content converts it to Markdown and inserts it as plain text, keeping the Markdown
  source and preview in sync.
- **CF_HTML clipboard fragment extraction** (`HtmlToMarkdownConverter.ExtractCfHtmlFragment`):
  Parses the Windows CF_HTML clipboard format to extract only the `<!--StartFragment-->`...
  `<!--EndFragment-->` content (or byte-offset-delimited range as fallback), discarding the
  surrounding `<html>/<head>/<body>` shell that clipboard providers add.
- **Word/Office HTML pre-processing** in the HTML→Markdown converter: Strips `<o:p>`, `<w:*>`,
  `<m:*>`, `<v:*>` namespace elements, Word conditional comments (both comment-form
  `<!--[if...]-->` and non-comment-form `<![if...]>`), `mso-*` CSS properties, and `lang`
  attributes before structural conversion.
- **Browser/web-origin Word HTML list support**: Detects list items encoded as
  `<p class=MsoNormal style='...mso-list:l0 level1 lfo1...'>` — the pattern produced when
  copying from a web page (e.g. a GitHub README) via Word's clipboard engine — and converts
  them to Markdown bullet items with correct nesting from the `level\d` value.
- **Native Word document list support**: Converts `MsoListParagraph`/`MsoListBullet` class
  paragraphs and heading-tagged first list items (`<h1 class="MsoListBullet">`) to Markdown
  unordered list items with correct indentation.
- **Combined bold+italic span handling**: Recognises `<span style="font-weight:bold;
  font-style:italic;">` in both attribute orderings and emits `***text***`.
- **Preview refresh on file open**: Opening a file now forces the preview to refresh even when
  the preview pane was last focused, eliminating the stale-preview regression that required a
  keypress to trigger the first render of the new document.

### Fixed
- **Bullet glyphs (`·`) appearing in pasted list content**: The `<![if !supportLists]>` block
  containing the visual bullet character is now fully stripped before conversion, so no `·`,
  `•`, or similar glyph appears in the resulting Markdown.
- **Word `<h1 class="MsoListBullet">` producing a heading instead of a list item**: Word
  generates heading-tagged elements for the first list item of each group; these are now
  correctly converted to `- ` list items rather than `# ` headings.
- **Empty Word heading separators producing stray `# ` lines**: Empty `<h1>` elements that
  Word emits as visual section separators now produce no output.
- **Multi-line heading content splitting across output lines**: Heading content that spans
  a newline in the source HTML (e.g. long bold text in Word) is now collapsed to a single line.
- **Stale preview after opening a second file**: `LoadFileFromPathAsync` now calls
  `UpdatePreviewAsync(forceWhenPreviewFocused: true)` instead of the plain `UpdatePreview()`,
  bypassing the focused-panel suppression guard that previously left the old document's preview
  visible until the user typed or clicked.

### Changed
- **IL trimming disabled for all build configurations**: `PublishTrimmed` is now permanently
  `False`. WinUI 3 / Windows App SDK are not trim-compatible; enabling trimming caused Microsoft
  Store submissions to fail at runtime with `MissingMethodException`. `ReadyToRun` is kept
  for Release builds to preserve JIT warm-up performance.

### Tests
- `MarkdownDocumentTests`: Added `DocumentLoad_PreviewInitializedResetToFalse` and
  `DocumentLoad_AfterReset_NewContentIsReflected` to guard the reset-and-reload contract used
  by `LoadFileFromPathAsync`.
- `HtmlToMarkdownConverterTests`: 37 new tests covering Office/Word HTML stripping, Word list
  paragraph conversion (including indentation), browser-origin Word HTML list items, non-comment
  IE conditional stripping, bullet glyph removal, combined bold+italic spans, empty heading
  suppression, multi-line heading normalisation, and CF_HTML fragment extraction (end-to-end
  and edge cases).
- `FileWorkflowTests` (UI): Added `OpenNewFile_EditorUpdatesImmediately_WhenPreviewWasLastFocused`
  to guard against the stale-preview regression.
- `EditWorkflowTests` (UI): Added `Paste_HtmlClipboard_InsertsMarkdownIntoEditor` to exercise
  the async paste plumbing end-to-end.

## [1.6.0] - 2026-03-24

### Added
- **Full bidirectional editing with toolbar formatting from the preview pane**: All toolbar
  formatting commands (Bold, Italic, Strikethrough, Inline Code, Heading, Table) now work
  correctly when the preview pane is focused. The selection made in the preview is preserved
  across formatting operations and the preview DOM selection is restored after every re-render,
  making formatting feel instantaneous.
- **`MarkdownSelectionProjection`**: New core library class that maps visible character offsets
  in the rendered preview to their corresponding Markdown source offsets (and back), accounting
  for inline syntax markers (`**`, `*`, `~~`, `` ` ``) that are invisible in the preview but
  occupy characters in the source.
- **Offset-based preview selection protocol**: The preview now posts `selectionChanged` and
  `contentChanged` messages with integer `start`/`length` fields rather than raw selected-text
  strings, enabling precise round-trip selection mapping without text-search ambiguity.
- **`setSelectionOffsets(start, length)` JS function**: Injected into the preview WebView to
  restore a native DOM selection or caret from host-supplied visible-character offsets after a
  preview re-render. Supports both ranged selections and collapsed carets.
- **Collapsed-caret support throughout selection sync**: Single-click cursor positions in the
  preview are now tracked and restored, not just drag-selected ranges.
- **`setMirroredSelection(start, length)` replaces `highlightText(text)`**: The editor→preview
  CSS Custom Highlight is now positioned using offset coordinates rather than text search,
  eliminating false matches when the same text appears more than once in a document.

### Fixed
- **Bold+italic toggle regression**: Toggling italic off on a `***bold italic***` selection now
  correctly produces `**bold**` instead of adding extra markers. A new
  `TryRemoveTripleAsteriskCombination` path handles the bold+italic unwrap case explicitly.
- **Nested marker removal**: Toggling a single-char marker on a span that is already wrapped by
  that marker inside a broader run (e.g. italic inside bold-italic) now strips both layers
  cleanly instead of re-wrapping.
- **Preview timer no longer interrupts preview-owned editing**: The debounce timer that
  re-renders the preview from the editor is suppressed while the preview pane has focus,
  preventing in-progress preview edits from being clobbered.
- **WebView2 message payload double-encoding**: `NormalizeWebMessagePayload` now unwraps the
  outer JSON-string layer that WebView2 adds around `postMessage(JSON.stringify(...))` calls,
  fixing a bug where all structured messages were silently dropped.

### Changed
- Preview selection messages switched from text-based (`"text"` field) to offset-based
  (`"start"` / `"length"` fields) for all `selectionChanged` and `contentChanged` payloads.
- `getSelectionOffsets()` and `postCommittedSelection()` JS functions now allow collapsed
  carets (previously guarded by `sel.isCollapsed`).
- `buildTextMap()` extracted as a shared JS helper used by both `setMirroredSelection` and
  `setSelectionOffsets`.
- `_awaitingUserInput` JS flag renamed to `_selectionMessagesSuppressed` for clarity.

### Tests
- **New `MarkdownSelectionProjectionTests`** (56 unit tests): full coverage of
  source↔visible offset mapping for bold, italic, strikethrough, inline code, mixed
  formatting, and collapsed carets.
- **9 new `MarkdownParserTests`**: verify `contentChanged` payload includes selection offsets,
  `setSelectionOffsets` is emitted in editable mode, and `postCommittedSelection` supports
  collapsed carets.
- **Updated `MarkdownFormatterTests`**: corrected `ToggleItalic_OnBoldItalicSelection`
  expectation and added `TryRemoveTripleAsteriskCombination` / `TryRemoveNestedMarker` cases.

## [1.5.1] - 2026-03-19

### Fixed
- **Real-time preview-to-editor content sync**: Reduced the JavaScript `notifyChange`
  debounce from 400 ms to 100 ms so that typing in the contentEditable preview pane
  updates the Markdown editor almost immediately instead of lagging by nearly half a
  second after the user stops typing.
- **Character-by-character bidirectional selection mirroring**: Merged the CSS Custom
  Highlight update and the C# host notification into a single `requestAnimationFrame`
  callback.  During a pointer drag in the preview, intermediate `selectionChanging`
  messages are posted every animation frame so the editor selection tracks
  character-by-character.  A deferred `selectionChanged` message fires 100 ms after
  the selection stabilises (or immediately on `pointerup`) to trigger the WinUI3 focus
  dance that activates `SelectionHighlightColorWhenNotFocused`.  Previously the C#
  host was only notified via a 200 ms debounce that reset on every `selectionchange`
  event, which meant the editor selection never updated during an active drag.

### Changed
- `MarkdownFormatter.StripInlineMarkdown` is now a public utility method (moved from
  a private helper in `MainWindow.xaml.cs`) for reuse and testability.
- `ApplyPreviewSelectionToEditor` helper method extracted in `MainWindow.xaml.cs` to
  share JSON parsing and selection-mapping logic between intermediate and final
  selection message handlers.
- Editable-preview JS variables renamed: `highlightAF` / `selectionDebounce` →
  `selectionAF` / `selectionFinalTimer` to better reflect the merged selection flow.

### Tests
- 13 new unit tests for `MarkdownFormatter.StripInlineMarkdown` covering plain text,
  bold, italic, bold+italic, underscores, strikethrough, inline code, headings,
  empty/null, mixed formatting, and nested markers.
- 8 new edge-case unit tests for `MarkdownFormatter.ExpandToMarkdownBounds`: partial
  selection inside formatted text, full inner text expansion, boundary conditions
  (start/end of document), zero-length selection, strikethrough, and inline code.

## [1.5.0] - 2026-03-18

### Added
- **File type association for `.md` / `.markdown`**: The app registers itself as a handler
  for Markdown files in the MSIX manifest (`uap:FileTypeAssociation`). Double-clicking any
  `.md` or `.markdown` file in Explorer opens it directly in MarkUp. The app also appears
  in the *Open with* context-menu for those file types. File-activation paths are read via
  `Microsoft.Windows.AppLifecycle.AppInstance.GetActivatedEventArgs()` on startup and the
  document is loaded as soon as the WebView2 is ready.
- **Heading toolbar dropdown**: A new *Heading* `AppBarButton` with a `MenuFlyout` lets you
  apply H1–H6 heading levels directly from the toolbar without opening the Format menu.
- **Blockquote toolbar button**: Inserts a blockquote prefix from the toolbar.
- **Secondary toolbar commands**: Code Block, Task List, and Horizontal Rule are now
  accessible as secondary (overflow) commands in the toolbar `CommandBar`.
- **`ExpandToMarkdownBounds()`** public API on `MarkdownFormatter`: given a plain-text range
  inside Markdown source, expands the selection outward to include any immediately
  surrounding inline syntax markers (`**`, `*`, `~~`, `` ` ``, etc.), matching
  longest-first so `***` is always preferred over `**` or `*`.
- **Cross-pane selection mirroring**: Selecting text in the preview pane posts a
  `selectionChanged` message back to the C# host. A CSS Custom Highlight
  (`::highlight(sync-highlight)`) reflects the selection visually — unlike the browser's
  native DOM selection, the highlight persists after the WebView2 loses focus.
- **`SyncPreviewSelectionToEditorAsync()`**: When a format command is invoked and the
  preview was last focused, the host reads the current preview selection and maps it back
  to the matching span in the Markdown source before applying the formatter.

### Fixed
- **Bold/italic toggle incorrectly stripped markers on inner text**: `ToggleBold` and
  `ToggleItalic` previously used a simple substring check that matched the first `*` of
  `**` as an italic marker. The new `IsExactMarkerAt()` helper uses boundary guards so a
  single `*` marker is never found inside a `**` run, and toggling italic on text inside
  a bold span now correctly wraps rather than strips.
- **Deployment timeout never enforced**: `ExecuteRemotePackageInstall` called
  `ReadToEnd()` synchronously before `WaitForExit()`, so the 3-minute deployment timeout
  was never actually applied — a hung WinRM session blocked indefinitely. Fixed by starting
  async reads for stdout/stderr first and only collecting the output after `WaitForExit`
  returns.
- **WYSIWYG in-preview toolbar removed**: The floating formatting toolbar that was
  rendered inside the `contenteditable` preview WebView2 is removed. All formatting
  commands are now routed through the WinUI toolbar and Format menu, eliminating a
  redundant UI element and the Z-order / hit-testing issues it caused.
- **Split-pane columns could collapse to zero**: Added `MinWidth = 100` to editor and
  preview grid columns in all view modes so neither panel can be accidentally collapsed
  to zero width by the splitter.
- **`_focusedPanel` renamed to `_lastFocusedPanel`**: Clarifies that the field tracks the
  *last* panel to receive focus, not necessarily the currently focused one, which is
  intentional so toolbar/menu interactions do not reset the routing target.

### Changed
- `Package.appxmanifest` version bumped to `1.5.0.0`.
- `MarkdownFormatter.ToggleWrap` now uses `IsExactMarkerAt()` for all marker boundary
  checks.

## [1.4.5] - 2026-03-17

### Added
- **`AutomationEditorInput` injection bridge**: A hidden single-line `TextBox`
  (`AutomationEditorInput`) and companion `Button` (`AutomationSetEditorContentButton`)
  are now present in the automation bridge `Canvas`. UI tests write encoded content to the
  `TextBox`; the `EditorSyncTimer` debounces the input over ≥2 stable 150 ms ticks
  (300 ms total), decodes `|NEWLINE|` and `|HASH|` placeholders, and applies the content
  to `EditorTextBox` in a single assignment. This path is completely independent of
  keyboard layout and avoids WinUI 3 `TextBox` key-event timing issues.

### Fixed
- **UK keyboard `#` → `£` garbling in UI tests**: `PasteText()` previously called
  `SendKeys` directly on `EditorTextBox`, which routes through Appium's keyboard
  simulation layer and maps `#` to `£` on a UK layout. It now encodes `#` as `|HASH|`
  and newlines as `|NEWLINE|` and injects the content via `AutomationEditorInput`, so
  special characters arrive exactly as typed regardless of the remote machine's keyboard
  layout.
- **W3C Actions not supported by WinAppDriver**: `SendRemoteModifiedKeys()` previously
  built a `Selenium.Interactions.Actions` chain (W3C Actions protocol) which WinAppDriver
  does not implement. Modifier key shortcuts now use chord notation
  (e.g. `Keys.Control + "a"`) routed through `/element/{id}/value`, which WinAppDriver
  does support.
- **Direct `SendKeys` for editor clear in test setup**: `EditorTypingTests` and
  `StatusBarTests` `TestInitialize` methods now send `Ctrl+A` and `Delete` directly on
  the cached `Editor` element rather than through the shared `SendCtrlShortcut` /
  `SendDeleteKey` helpers, eliminating a focus-race that caused the wrong element to
  receive the keystrokes.
- **Silent remote session failure when running a single test**: `InitialiseRemoteSession`
  now performs a TCP connectivity check against the remote WinAppDriver endpoint before
  attempting package deployment or session creation. An unreachable host is diagnosed
  immediately with a clear message rather than cycling silently through all AUMID fallback
  targets.
- **App not appearing on remote screen after session creation**: `WarmUpSessionRoot()`
  now returns `bool` and the initialization loop throws `WebDriverException` if
  `EditorTextBox` does not appear within 30 seconds (previously 15 s, silent return on
  timeout). This ensures WinAppDriver session creation only succeeds when the app window
  is genuinely ready on the remote machine.
- **Stale session not reinitialized for single-test runs**: `SkipIfNoSession()` now
  triggers reinitialization when the existing session is non-null but unresponsive (e.g.
  after a previous test run closed the app), ensuring the full deployment and launch
  pipeline runs for every test regardless of how many tests are selected.

## [1.4.0] - 2026-03-14

### Added
- **Find & Replace bar**: Inline toolbar that slides in below the menu bar with a Find text
  box, Find Previous / Find Next buttons, Match Case checkbox, Replace text box, and
  Replace / Replace All buttons. All controls carry `AutomationId` attributes for reliable
  access from automated UI tests.
- **Bidirectional preview editing**: The preview pane is now `contenteditable`. Changes are
  debounced (400 ms), posted back to the host via `window.chrome.webview.postMessage`,
  converted from HTML to Markdown through `HtmlToMarkdownConverter`, and synced to the
  source editor in real time. A `_suppressNotify` flag and `updateContent()` JS function
  prevent round-trip feedback loops.
- **Focus-aware Edit menu routing**: A `FocusedPanel` enum (`None`, `Editor`, `Preview`)
  tracks which pane currently holds keyboard focus via `GotFocus`/`LostFocus` handlers.
  Undo, Redo, Cut, Copy, Paste, and Select All are all routed to the active panel — editor
  operations use the `TextBox` API; preview operations use `document.execCommand` /
  the Clipboard API inside the WebView2.
- **Setext heading support** in `MarkdownParser`: underline-style headings (`===` for H1,
  `---` for H2) are now recognised and rendered correctly alongside ATX-style headings.
- **Automation bridge panel**: A hidden 10×10 px `Canvas` (`AutomationBridgePanel`)
  positioned early in the XAML tree — before WebView2 — so WinAppDriver's UIA traversal
  reaches it without entering the Chromium accessibility subtree. Contains:
  - `AutomationFocusEditorButton` / `AutomationFocusPreviewButton` — 1×1 invisible buttons
    that programmatically set panel focus for test setup.
  - `AutomationPreviewInsertTextButton` / `AutomationPreviewBoldButton` — inject known
    content into the preview's `contenteditable` body for bidirectional-editing tests.
  - `AutomationDocumentContent`, `AutomationPreviewHtml`, `AutomationFocusedPanel`,
    `AutomationViewMode`, `AutomationLastSyncSource` — read-only `TextBlock`s that
    mirror live app state so tests can assert without querying internal fields.
- **`MarkUp.UITests` project** added to the solution: WinAppDriver + Appium (OpenQA.Selenium
  .Appium 6.x) automation test suite with 200+ tests covering startup, editor typing, all
  menu operations, Find & Replace workflows, status bar statistics, zoom, view modes, the
  splitter, and help dialogs. Supports both local WinAppDriver and remote execution against
  a second machine (configurable via `UITEST_DRIVER_URL` and `UITEST_REMOTE_APP_PATH`
  environment variables).
- **Expanded unit test coverage** — `MarkUp.Tests` grows from 151 to 288+ tests:
  - `MarkdownParserTests`: setext headings, heading slug/ID generation, inline code HTML
    escaping, `+`-prefix unordered lists, nested ordered/unordered lists, task lists,
    fenced code blocks, GFM table column alignment, blockquotes.
  - `HtmlToMarkdownConverterTests`: `<span>` bold/italic/strikethrough styles, `<div>`
    line wrapping, nested lists, task lists, table alignment separators, numeric decimal
    and hex HTML entity decoding, links, images, edge cases (empty divs, multiple `<br>`).
  - `MarkdownFormatterTests`: heading levels H3–H6, out-of-range level clamping,
    strikethrough and inline-code toggle on/off, no-selection marker insertion,
    `InsertHorizontalRule`, `InsertLink` (with and without selection), `InsertImage`.
  - `MarkdownDocumentTests`: new-document state, dirty/clean window title, `DisplayName`
    after reset, multi-change dirty tracking, `MarkSaved` cycle.
  - `DocumentStatisticsTests`: single-line counting, trailing-newline edge case, tab
    characters, `\r`/`\r\n`/`\n` mixed line endings, multi-word accuracy.
  - `DocumentExporterTests`: dark/light mode HTML output, heading conversion, plain-text
    marker stripping (bold, italic, bold-italic, code fences, image alt text, blank-line
    collapsing), null input handling.
- **`RoundTripTests` suite**: verifies Markdown → HTML → Markdown fidelity for headings,
  bold, italic, lists, blockquotes, code blocks, and GFM tables.

### Fixed
- **Ctrl+A / Copy-Paste targeting wrong panel**: Before this release, `Ctrl+A` and Paste
  always acted on the editor `TextBox` regardless of where the user had clicked. Focus is
  now tracked per panel and shortcuts are dispatched accordingly.
- **Inline code HTML escaping**: `<` and `>` inside backtick spans (e.g. `` `a < b` ``)
  were emitted as raw angle brackets, breaking HTML rendering. They are now escaped to
  `&lt;` and `&gt;` before output.
- **Table column alignment ignored**: A regex word-boundary bug in `ThCellRegex` caused
  `<th` to match the start of `<thead`, discarding any `style="text-align:center/right"`
  attributes and always producing `---` separators. The pattern now uses `\b` correctly
  so `:---:` and `---:` alignment separators are emitted as expected.
- **Incremental preview sync flicker**: Every keystroke triggered `NavigateToString`,
  resetting scroll position and causing a white flash. Subsequent updates now call
  `updateContent(escapedHtml)` via `ExecuteScriptAsync`, which replaces only the body
  `innerHTML` without a page reload.
- **Print dialog corrupting the UIA session**: Using `CoreWebView2PrintDialogKind.Browser`
  hosted the print UI inside the WebView2 renderer; dismissing the dialog triggered an
  internal back-navigation that put the WebView2 UIA provider into an unrecoverable state,
  breaking all subsequent automated tests. Switched to `CoreWebView2PrintDialogKind.System`
  which opens the native Windows print dialog in a separate OS window.
- **Nested list HTML→Markdown conversion**: `HtmlToMarkdownConverter` previously processed
  outer `<ul>`/`<ol>` first, causing inner list items to be emitted without indentation.
  Lists are now expanded innermost-first (inside-out) so child items are correctly indented
  by three spaces relative to their parent.
- **`<div>` wrapper conversion**: `contenteditable` editors wrap each line in a `<div>`;
  the new `ConvertDivs()` method maps `<div><br></div>` to blank lines and
  `<div>content</div>` to text lines, preserving paragraph structure when pasting from or
  syncing the preview.
- **`<span>` inline formatting conversion**: Inline span styles emitted by browsers
  (`font-weight:700`, `font-style:italic`, `text-decoration:line-through`) are now
  converted to `**bold**`, `*italic*`, and `~~strikethrough~~` respectively. Underline
  (`text-decoration:underline`) has no Markdown equivalent; the tag is stripped and its
  text content is preserved.
- **Strikethrough tag variants**: `<del>`, `<s>`, and `<strike>` all now convert to
  `~~text~~`; previously only `<del>` was handled.
- **Numeric HTML entity decoding**: `HtmlToMarkdownConverter` now decodes numeric decimal
  entities (`&#160;`) and numeric hex entities (`&#xA0;`) in addition to the named
  entities that were already supported.

## [1.3.2] - 2026-02-17

### Fixed
- **Line count not updating when opening a file**: `CountLines` only counted `\n` characters
  but WinUI 3's `TextBox` normalises line endings to `\r`. When the deferred `TextChanged`
  event fired after opening a file, the line count reverted to 1. Updated `CountLines` to
  recognise `\r`, `\n`, and `\r\n` as line separators. Also fixed `CountParagraphs` which
  had the same single-separator issue.
- **4 new unit tests** covering `\r`-only and `\r\n` line and paragraph counting (151 total).


## [1.3.1] - 2026-02-14

### Fixed
- **Print footer no longer shows about:blank**: Preview and print content is now served via
  virtual host URLs (`https://markup.preview/` and `https://markup.print/`) using
  `WebResourceRequested`, so the page has a real URL instead of `about:blank`.
- **PDF export footer no longer shows about:blank**: `PrintToPdfAsync` now sets `FooterUri`
  to a blank space to suppress the URL in the footer. Header title is preserved.
- **Print margins restored**: Reverted the `@page { margin: 0 }` approach. Normal print
  margins are used so the browser's header (title, date) and footer (page numbers) are
  preserved — only the about:blank URL is removed.
- **Print and PDF margins now match**: Both print and PDF export use the same default browser
  margins for consistent output.

## [1.3.0] - 2026-02-13

### Added
- **Document title in preview HTML**: `ToHtml()` now accepts a `documentTitle` parameter; the
  preview HTML includes a `<title>` tag so the document name appears in browser print headers.
- **Anchor link navigation**: Clicking `#anchor` links in the preview pane now smoothly scrolls
  to the target heading instead of being blocked.
- **Resizable split panes**: The centre splitter can be dragged left or right to resize the
  editor and preview panels. Each panel enforces a minimum width of 20%.
- **4 new unit tests** covering document title in HTML output, default title fallback, and
  anchor link scrollIntoView script presence (147 total).

### Fixed
- **Print uses browser dialog with preview**: Print now uses `ShowPrintUI(Browser)` on the
  main `PreviewWebView`, which shows the Chromium print preview dialog with full WYSIWYG
  preview. `@media print` CSS rules automatically switch to light theme and hide the toolbar.
- **Window icon**: Uses multi-resolution `.ico` file (16/32/48/256px) instead of `.png` so
  `AppWindow.SetIcon()` works correctly.

### Changed
- `MarkdownParser.ToHtml()` signature now includes optional `documentTitle` parameter.
- `MarkdownParser.BuildHtmlPage()` now emits a `<title>` tag.
- Splitter minimum width changed from fixed 100px to 20% of available width.
- Print operation no longer sets `document.title` via JavaScript (title is in HTML).

## [1.2.0] - 2026-02-13

### Added
- **Ctrl+Click to follow links**: Links in the preview pane can now be opened in the default
  browser by Ctrl+Clicking. A hover tooltip ("Ctrl+Click to follow link") appears on all links
  in both editable and non-editable modes.
- **7 new unit tests** covering link tooltip rendering, contentEditable attribute presence,
  WYSIWYG toolbar rendering, and Ctrl+Click script injection for both editable and non-editable
  preview modes.

### Fixed
- **Toolbar left-aligned**: The formatting toolbar is now left-aligned instead of stretching
  across the full window width.
- **Open dialog restricted to Markdown files**: The Open file dialog now only offers `.md` and
  `.markdown` file types, removing the previous `.txt` and `*` (all files) options.
- **Print no longer disrupts the preview panel**: Print and PDF export now use a dedicated hidden
  WebView2, so the visible preview pane is never navigated away from the current content. The
  preview panel stays exactly as-is during print and PDF operations.

### Changed
- Print and PDF export operations now use a separate background `PrintWebView` WebView2 instance.
- `MenuPrint_Click` and `MenuExportPdf_Click` no longer call `UpdatePreview()` after printing
  since the preview is never disrupted.

## [1.1.0] - 2026-02-13

### Added
- **WYSIWYG Preview Editor**: The preview pane is now a full rich-text editor. Users can edit
  directly in the rendered preview using the built-in formatting toolbar (bold, italic,
  strikethrough, headings, lists, code, links, blockquotes, horizontal rules). Changes in the
  preview are automatically converted back to Markdown and synced to the source editor.
- **HtmlToMarkdownConverter**: New core library class that converts HTML (from contentEditable)
  back to Markdown, supporting headings, bold, italic, strikethrough, inline code, code blocks
  with language, links, images, unordered lists, ordered lists, blockquotes, tables, horizontal
  rules, and paragraphs. Includes HTML entity decoding.
- **51 new unit tests** for the HtmlToMarkdownConverter, including round-trip tests that verify
  Markdown → HTML → Markdown fidelity.
- **3 new print-related unit tests** verifying document title in print output, default title
  fallback, and `!important` colour rule usage.

### Fixed
- **Print header shows correct filename**: The printed document now displays the actual document
  filename (e.g., "README.md") in the browser print header instead of "about:blank". Both
  `<title>` tag and `document.title` are set before printing. PDF export also sets the header
  title in print settings.
- **Print colour management**: Print output no longer loses text colours. All colour and
  background rules in the print stylesheet now use `!important` to prevent browser print
  overrides. Added `print-color-adjust: exact` and `-webkit-print-color-adjust: exact` to
  preserve styled backgrounds. Link colours are preserved as blue (`#0066cc`), code blocks
  retain their grey backgrounds, and table headers/cells have explicit background colours.

### Changed
- Preview panel header label changed from "PREVIEW" to "PREVIEW / EDIT" to indicate WYSIWYG
  editing capability.
- Preview WebView2 now enables context menus for standard right-click editing operations
  (cut/copy/paste) in the WYSIWYG editor.
- `MarkdownParser.ToHtml()` now accepts an optional `editable` parameter to generate
  contentEditable HTML with an embedded WYSIWYG toolbar.
- `MarkdownParser.ToHtmlForPrint()` now accepts an optional `documentTitle` parameter.

## [1.0.0] - 2026-02-13

### Added
- Initial release of MarkUp Markdown Editor.
- Split-pane Markdown editor with live HTML preview using WebView2.
- Dark mode with Mica backdrop (WinUI 3, Windows App SDK 1.8).
- Full Markdown support: headings, bold, italic, strikethrough, inline code, fenced code blocks,
  links, images, unordered/ordered/task lists, blockquotes, tables, horizontal rules.
- Menu bar and toolbar with keyboard shortcuts for all formatting operations.
- Find & Replace with case-sensitive matching.
- File operations: New, Open, Save, Save As.
- Export to HTML, Plain Text, and PDF.
- Print with print preview via WebView2.
- Font customisation dialog (font family and size).
- Zoom controls (50%–200%).
- View modes: Split, Editor Only, Preview Only.
- Word wrap toggle.
- Status bar with word/character/line count, cursor position, encoding, zoom level.
- About dialog with version, build date, runtime, architecture, and OS information.
- Markdown Quick Reference cheat sheet.
- File type associations for `.md`, `.markdown`, `.mdown`, `.mkd`.
- 80 MSTest unit tests across 5 test classes.
