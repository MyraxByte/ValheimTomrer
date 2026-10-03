#!/usr/bin/env node
// The two "never break" checks of CLAUDE.md, in one place for every OS.
//   node scripts/check-rules.mjs            check the whole repo (npm run check)
//   node scripts/check-rules.mjs --hook     Claude Code hook: reads the tool call from stdin, checks the edited file,
//                                           exit 2 with the reason on stderr when a rule is broken
// World-save rule: the mod writes nothing of its own into the world (no ZDO keys, RPCs, network messages).
// Art rule: no image, mesh, material or bundle file in the repo, except thunderstore/icon.png.
import { readdirSync, readFileSync, statSync } from "node:fs";
import { dirname, join, relative, sep } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const worldSave = /ZDO\(\)\.Set|\.Set\(ZDOVars|InvokeRPC|RoutedRPC|Register</;
const worldSaveFolders = ["src/Blueprints", "src/Editor", "src/Patches", "src/Plugin.cs"];
const artExt = /\.(png|jpg|jpeg|tga|glb|fbx|obj|mat|mesh|bundle|asset|prefab)$/i;
const artAllowed = new Set(["thunderstore/icon.png"]);
const skipDirs = new Set([".git", ".devtest", "bin", "obj", "node_modules", "build"]);

const rel = (p) => relative(root, p).split(sep).join("/");

function walk(dir, out = []) {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      if (!skipDirs.has(name)) walk(full, out);
    } else out.push(full);
  }
  return out;
}

function worldSaveHits(file) {
  const r = rel(file);
  if (!r.endsWith(".cs") || !worldSaveFolders.some((f) => r === f || r.startsWith(f + "/"))) return [];
  return readFileSync(file, "utf8").split("\n")
    .map((line, i) => (worldSave.test(line) ? `${r}:${i + 1}: ${line.trim()}` : null))
    .filter(Boolean);
}

const artHit = (file) => (artExt.test(file) && !artAllowed.has(rel(file)) ? [`${rel(file)}: game art in the repo`] : []);

let files;
if (process.argv.includes("--hook")) {
  let input = "";
  for await (const chunk of process.stdin) input += chunk;
  let path;
  try { path = JSON.parse(input)?.tool_input?.file_path; } catch { /* not JSON, nothing to check */ }
  if (!path) process.exit(0);
  files = [path];
} else {
  files = walk(root);
}

const problems = files.flatMap((f) => [...worldSaveHits(f), ...artHit(f)]);
if (problems.length) {
  console.error("Rule broken (CLAUDE.md, 'Never break'). The mod writes nothing into the world save and no game art to disk:\n" + problems.join("\n"));
  process.exit(process.argv.includes("--hook") ? 2 : 1);
}
if (!process.argv.includes("--hook")) console.log("rules: ok (world-save rule, art rule)");
