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

Known gaps:

- Main window layout: "Find and replace" is clipped under the Advanced settings button, the Normal / Advanced / Template buttons overlap their group box, and the "Auto changes skip" label wraps awkwardly.
- The "Make list" source and category inputs have a bright white border that does not fit the dark palette.
- Preferences: the OK button is light grey while Cancel is dark, so the default-button styling is inconsistent. The dialog title bar is black against a dark grey body.
- Preferences > Editing and saving: the seconds value box is very low contrast when disabled. Confirm once it is enabled.
- Not yet checked in dark mode: the Site, Tools, Privacy and Alerts tabs, plugin windows, the diff view, the find and replace dialogs, and Classic mode.
- The unit tests and CI have not been run against this branch, and there are no tests for the theme code.

See https://en.wikipedia.org/wiki/Wikipedia:AutoWikiBrowser for more information.

Canonical code repository can be found on Sourceforge at http://sourceforge.net/p/autowikibrowser/code/HEAD/tree/

Please report any issues to https://phabricator.wikimedia.org/maniphest/task/create/?projects=AutoWikiBrowser