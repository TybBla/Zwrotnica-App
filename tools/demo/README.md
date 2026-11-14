# Demo recorder

Headless-Playwright scripts that recorded `docs/demo.gif` and `docs/screenshots/` against a running local app (`http://localhost:5173` + API on 5080), using a generated fictional ticket PDF (no real personal data).
They are not part of the app: install their dependencies in a scratch directory with `npm i playwright gifenc jpeg-js` (plus `npx playwright install chromium`) and run them from there.
`record-demo.mjs <outDir> <demo.json>` and `stills.mjs <outDir> <demo.json>` take frames; `demo.json` holds the local demo account as `{"e": "...", "p": "..."}`.
`make-gif.mjs out.gif <delayMs> frame.jpg[:holdMs] ...` assembles the frames into a GIF.
