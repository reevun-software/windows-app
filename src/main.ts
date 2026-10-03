import { app } from "electron";
// The shared app (the window showing reevun.app, updates), built from
// reevun-software/app-core into dist/core next to this file - so it's
// loaded from there at run time, and typed from there at build time.
const { startApp } = require("./core/electron/main") as typeof import("../dist/core/electron/main");

// The app draws its own minimize / maximize / close buttons in its title
// strip (the system's overlay buttons left a seam under them and showed
// their tooltips twice); the window keeps its resize edges and snapping.
const TITLE_BAR_HEIGHT = 36;

startApp({
  platform: "windows",
  window: {
    titleBarStyle: "hidden",
  },
  titleBar: { height: TITLE_BAR_HEIGHT, insetLeft: 0, insetRight: 0, windowButtons: true },
  // Notifications and the taskbar group the app under its own name (the
  // Store package already carries one).
  onReady: () => {
    if (!process.windowsStore) app.setAppUserModelId("app.reevun.desktop");
  },
});
