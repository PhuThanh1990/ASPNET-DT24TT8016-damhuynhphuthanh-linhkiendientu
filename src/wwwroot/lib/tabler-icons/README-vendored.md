# Tabler Icons (vendored subset)

Version 3.46.0, taken from `@tabler/icons-webfont`.

Only `fonts/tabler-icons.woff2` is vendored. The stylesheet's `@font-face` also lists
`.woff` and `.ttf`, but a browser stops at the first format it supports and every browser
since 2016 supports woff2, so those two are never requested — vendoring them would add
~3.6 MB to the repository for files nobody downloads.

If a legacy browser ever has to be supported, fetch the two missing files into `dist/fonts/`:

    https://cdn.jsdelivr.net/npm/@tabler/icons-webfont@3.46.0/dist/fonts/tabler-icons.woff
    https://cdn.jsdelivr.net/npm/@tabler/icons-webfont@3.46.0/dist/fonts/tabler-icons.ttf
