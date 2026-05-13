import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { defineConfig, type Plugin } from "vite";
import react from "@vitejs/plugin-react";

const rootDir = path.dirname(fileURLToPath(import.meta.url));

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

        if (req.method !== "GET" || req.url !== "/api/story-state") {
          next();
          return;
        }

        const dataDirectory = process.env.STORY_INSPECTOR_DATA_DIR;
        if (!dataDirectory) {
          res.statusCode = 500;
          res.setHeader("Content-Type", "application/json; charset=utf-8");
          res.setHeader("Cache-Control", "no-store");
          res.end(
            JSON.stringify({ error: "STORY_INSPECTOR_DATA_DIR is not set" }),
          );
          return;
        }

        const storyStatePath = path.resolve(
          dataDirectory,
          "runtime",
          "state",
          "story-state.evaluated.json",
        );

        try {
          const json = await fs.readFile(storyStatePath, "utf-8");
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
                error: "story-state.evaluated.json not found",
                path: storyStatePath,
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

export default defineConfig({
  plugins: [react(), storyStateApiPlugin()],
  publicDir: path.resolve(rootDir, "../shared"),
  server: {
    fs: {
      allow: [path.resolve(rootDir, "..")],
    },
  },
});
