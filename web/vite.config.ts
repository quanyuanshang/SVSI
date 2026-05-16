import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { defineConfig, loadEnv, type Plugin } from "vite";
import react from "@vitejs/plugin-react";

const rootDir = path.dirname(fileURLToPath(import.meta.url));

const DEFAULT_DATA_DIR =
  "D:\\SteamLibrary\\steamapps\\common\\Stardew Valley\\StardewStoryInspector";

function resolveDataDirectory(): string | null {
  const fromEnv = process.env.STORY_INSPECTOR_DATA_DIR?.trim();
  if (fromEnv) {
    return fromEnv;
  }

  return DEFAULT_DATA_DIR || null;
}

function storyStateApiPlugin(): Plugin {
  return {
    name: "story-state-api",
    configureServer(server) {
      server.middlewares.use(async (req, res, next) => {
        if (!req.url) {
          next();
          return;
        }

        if (req.method === "GET" && req.url === "/api/health") {
          res.statusCode = 200;
          res.setHeader("Content-Type", "application/json; charset=utf-8");
          res.setHeader("Cache-Control", "no-store");
          res.end(JSON.stringify({ ok: true }));
          return;
        }

        if (
          req.method !== "GET" ||
          (req.url !== "/api/story-state" && req.url !== "/api/event-history")
        ) {
          next();
          return;
        }

        const dataDirectory = resolveDataDirectory();
        if (!dataDirectory) {
          res.statusCode = 500;
          res.setHeader("Content-Type", "application/json; charset=utf-8");
          res.setHeader("Cache-Control", "no-store");
          res.end(
            JSON.stringify({ error: "STORY_INSPECTOR_DATA_DIR is not set" }),
          );
          return;
        }

        const dataFile =
          req.url === "/api/event-history"
            ? {
                path: path.resolve(
                  dataDirectory,
                  "runtime",
                  "history",
                  "event-history.json",
                ),
                missingMessage: "event-history.json not found",
              }
            : {
                path: path.resolve(
                  dataDirectory,
                  "runtime",
                  "state",
                  "story-state.evaluated.json",
                ),
                missingMessage: "story-state.evaluated.json not found",
              };

        try {
          const json = await fs.readFile(dataFile.path, "utf-8");
          res.statusCode = 200;
          res.setHeader("Content-Type", "application/json; charset=utf-8");
          res.setHeader("Cache-Control", "no-store");
          res.end(json);
        } catch (error) {
          const code = typeof error === "object" && error && "code" in error
            ? String(error.code)
            : "";

          if (code === "ENOENT") {
            res.statusCode = 404;
            res.setHeader("Content-Type", "application/json; charset=utf-8");
            res.setHeader("Cache-Control", "no-store");
            res.end(
              JSON.stringify({
                error: dataFile.missingMessage,
                path: dataFile.path,
              }),
            );
            return;
          }

          next(error as Error);
        }
      });
    },
  };
}

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, rootDir, "STORY_INSPECTOR_");
  if (env.STORY_INSPECTOR_DATA_DIR && !process.env.STORY_INSPECTOR_DATA_DIR) {
    process.env.STORY_INSPECTOR_DATA_DIR = env.STORY_INSPECTOR_DATA_DIR;
  }

  return {
    plugins: [react(), storyStateApiPlugin()],
    // Vite serves files from public/ at the site root (e.g. public/generated → /generated/...).
    publicDir: path.resolve(rootDir, "public"),
    server: {
      fs: {
        allow: [path.resolve(rootDir, "..")],
      },
    },
  };
});
