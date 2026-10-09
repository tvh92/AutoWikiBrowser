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

- TODO: list what is still missing or broken (for example, which dialogs or plugins are not yet themed, and whether the full build and test suite pass).

See https://en.wikipedia.org/wiki/Wikipedia:AutoWikiBrowser for more information.

Canonical code repository can be found on Sourceforge at http://sourceforge.net/p/autowikibrowser/code/HEAD/tree/

Please report any issues to https://phabricator.wikimedia.org/maniphest/task/create/?projects=AutoWikiBrowser