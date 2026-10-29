# Fonts

`noto-sans-tamil.woff2` — **Noto Sans Tamil**, Google, under the
[SIL Open Font License 1.1](https://scripts.sil.org/OFL). Redistribution is
permitted and this notice is the licence condition.

It is the Tamil subset Google Fonts serves (U+0B82–0BFA, plus the Devanagari
dandas, the zero-width joiners Tamil needs for correct ligatures, **U+20B9 ₹** and
the dotted circle). 50 KB, one variable file covering regular through semibold.

Served from here rather than linked from `fonts.googleapis.com` on purpose:

- the container has no outbound network, so a CDN link would fail and silently
  fall back to whatever the operating system has;
- the page then renders Tamil in a face that does not match the Latin beside it,
  which is the thing the font was added to fix;
- and no request leaves the member's browser to a third party.

Latin text keeps the Bootstrap system stack. Only `body.lang-ta` uses this.
