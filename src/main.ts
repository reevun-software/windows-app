import { app } from "electron";
// The shared app (window, sign-in, interface, updates), built from
// reevun-software/app-core into dist/core next to this file - so it's
// loaded from there at run time, and typed from there at build time.
const { startApp } = require("./core/electron/main") as typeof import("../dist/core/electron/main");

// Windows draws its own minimize / maximize / close buttons over the app's
// title strip (titleBarOverlay); the strip leaves room for them.
const TITLE_BAR_HEIGHT = 36;
const CAPTION_BUTTONS_WIDTH = 138;

startApp({
  platform: "windows",
  window: {
    titleBarStyle: "hidden",
    titleBarOverlay: { color: "#101010", symbolColor: "#a3a3a3", height: TITLE_BAR_HEIGHT },
  },
  titleBar: { height: TITLE_BAR_HEIGHT, insetLeft: 0, insetRight: CAPTION_BUTTONS_WIDTH },
  // Notifications and the taskbar group the app under its own name.
  onReady: () => app.setAppUserModelId("app.reevun.desktop"),
});
