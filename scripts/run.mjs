#!/usr/bin/env node
// One entry for the npm scripts on every OS: runs scripts/<name>.ps1 on Windows, scripts/<name>.sh elsewhere.
//   node scripts/run.mjs dev --debug
import { spawnSync } from "node:child_process";
import { existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const [name, ...args] = process.argv.slice(2);
if (!name) {
  console.error("usage: node scripts/run.mjs <dev|autotest|zip> [args]");
  process.exit(2);
}

const dir = dirname(fileURLToPath(import.meta.url));
const windows = process.platform === "win32";
const file = join(dir, `${name}.${windows ? "ps1" : "sh"}`);
if (!existsSync(file)) {
  console.error(`missing ${file}`);
  process.exit(2);
}

let command, argv;
if (windows) {
  const pwsh = spawnSync("pwsh", ["-v"], { stdio: "ignore" }).status === 0;
  command = pwsh ? "pwsh" : "powershell";
  // --debug becomes -debug: PowerShell switches are case-insensitive
  argv = ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", file, ...args.map((a) => (a.startsWith("--") ? a.slice(1) : a))];
} else {
  command = "bash";
  argv = [file, ...args];
}
process.exit(spawnSync(command, argv, { stdio: "inherit" }).status ?? 1);
