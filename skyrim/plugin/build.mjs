// Bundles the plugin into one file for Data/Platform/Plugins. SkyrimPlatform provides the
// "skyrimPlatform" module as a global object at runtime, so imports of it resolve to that.
import { copyFileSync, mkdirSync } from "node:fs";
import { build } from "esbuild";

await build({
  entryPoints: ["src/index.ts"],
  bundle: true,
  format: "iife",
  target: "es2019",
  outfile: "dist/Platform/Plugins/tristinwold.js",
  plugins: [{
    name: "skyrim-platform-global",
    setup(b) {
      b.onResolve({ filter: /^skyrimPlatform$/ }, () => ({ path: "skyrimPlatform", namespace: "sp" }));
      b.onLoad({ filter: /.*/, namespace: "sp" }, () => ({ contents: "module.exports = skyrimPlatform;", loader: "js" }));
    },
  }],
});
mkdirSync("dist/Platform/UI", { recursive: true });
copyFileSync("ui/tristinwold.html", "dist/Platform/UI/tristinwold.html");
copyFileSync("ui/tristinwold-settings.txt", "dist/Platform/Plugins/tristinwold-settings.txt");
console.log("Built dist/Platform: copy it into Skyrim's Data folder.");
