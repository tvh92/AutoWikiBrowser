AutoWikiBrowser
===============

AutoWikiBrowser (often abbreviated AWB) is a semi-automated MediaWiki editor.

## About this branch (`dark-mode`)

**Status: unfinished / experimental.** This branch is not ready for everyday use and is not intended to be merged as-is.

Its purpose is to replace the earlier dark-mode hack with a proper light/dark theme engine:

- Light and dark palettes that follow the Windows app mode and accent colour by default, switchable under View > Theme (System / Light / Dark / Classic).
- Controls are styled once per window, when it is first activated, instead of being recoloured on a timer.
- Modernised look: Segoe UI, flat menus and buttons, accent-coloured check boxes and radio buttons, and underline tabs.
- AWB's hard-coded runtime colours (login state, find highlight, syntax highlighting, typo stats, diff CSS) are drawn from the active palette.

Fixed since the first review, but only compile-checked so far (not yet looked at on Windows):

- Main window layout: the "Find and replace" check box and the Normal / Advanced / Template buttons are now laid out from their real sizes, so the buttons no longer cover the check box. "Auto changes skip options" is split over two lines on purpose.
- Text boxes, lists and drop-downs (e.g. the "Make list" source and category inputs) get a dark outline instead of the native bright white border, and an accent-coloured outline while focused.
- Preferences: a grey Windows accent colour is no longer used, because a grey OK button looked disabled next to Cancel. The default blue is used instead. Text on accent-coloured buttons is now picked by measured contrast, so the dark-mode default button gets dark text. On Windows 11 the title bar matches the window colour.
- Disabled text boxes and number boxes (e.g. the autosave seconds box) are drawn with readable grey text, not the etched system grey.
- Preview pages get a dark page background. The About box drops its light green background in dark mode. The Find button no longer turns light grey after a search, and pale button colours (e.g. Save) are toned down in dark mode.
- Classic mode uses AWB's original status, highlight and syntax colours again.
- Unit tests cover the palette: contrast, accent selection and the classic colours.

Known gaps:

- None of the fixes above have been checked visually yet. Look at the Options tab at 100 % and 150 % text size, at Preferences, and at a disabled number box.
- A custom `style.css` next to AWB.exe replaces the diff styles completely, so it gets no dark overrides.
- Pages shown in the preview keep the wiki's own CSS. Tables and infoboxes with a hard-coded white background stay white.
- Not yet checked in dark mode: the Site, Tools, Privacy and Alerts tabs of Preferences and the plugin windows (a code review found no hard-coded colours in either), the find and replace dialogs, and Classic mode.

See https://en.wikipedia.org/wiki/Wikipedia:AutoWikiBrowser for more information.

Canonical code repository can be found on Sourceforge at http://sourceforge.net/p/autowikibrowser/code/HEAD/tree/

Please report any issues to https://phabricator.wikimedia.org/maniphest/task/create/?projects=AutoWikiBrowser