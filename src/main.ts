import { app } from "electron";
// The shared app (the window showing reevun.app, updates), built from
// reevun-software/app-core into dist/core next to this file - so it's
// loaded from there at run time, and typed from there at build time.
const { startApp } = require("./core/electron/main") as typeof import("../dist/core/electron/main");

// Windows draws its own minimize / maximize / close buttons over the app's
// title strip (titleBarOverlay); the strip leaves room for them. The buttons
// stop a pixel short of the strip's bottom edge, so its divider line runs
// under them too instead of breaking off.
const TITLE_BAR_HEIGHT = 36;
const CAPTION_BUTTONS_WIDTH = 138;

startApp({
  platform: "windows",
  window: {
    titleBarStyle: "hidden",
    titleBarOverlay: { color: "#fbfbfa", symbolColor: "#37352f", height: TITLE_BAR_HEIGHT - 1 },
  },
  titleBar: { height: TITLE_BAR_HEIGHT, insetLeft: 0, insetRight: CAPTION_BUTTONS_WIDTH },
  // Notifications and the taskbar group the app under its own name (the
  // Store package already carries one).
  onReady: () => {
    if (!process.windowsStore) app.setAppUserModelId("app.reevun.desktop");
  },
});
