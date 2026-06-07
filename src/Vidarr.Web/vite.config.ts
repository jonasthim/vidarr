import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";

const DEFAULT_BACKEND_PORT = 5027;
const DEFAULT_FRONTEND_PORT = 5173;

export default defineConfig(({ mode }) => {
  // loadEnv reads .env files; we also fall through to process.env so
  // scripts/dev.sh can override at launch time.
  const env = { ...loadEnv(mode, process.cwd(), ""), ...process.env };

  const backendPort = Number(env.VIDARR_DEV_BACKEND_PORT) || DEFAULT_BACKEND_PORT;
  const frontendPort = Number(env.VIDARR_DEV_FRONTEND_PORT) || DEFAULT_FRONTEND_PORT;
  // Default to "dev-key" so a bare `npm run dev` (no env) still matches the
  // dev appsettings.Development.json fixed key.
  const apiKey = env.VIDARR_API_KEY ?? "dev-key";

  return {
    plugins: [
      react(),
      {
        // Inject the dev API key into index.html so the SPA's
        // window.VIDARR_API_KEY reader (src/api.ts) picks it up without any
        // per-machine config. In production the placeholder is replaced with
        // empty string by `vite build`.
        name: "vidarr-inject-api-key",
        transformIndexHtml(html) {
          return html.replace(/%VIDARR_API_KEY%/g, apiKey);
        },
      },
    ],
    build: {
      outDir: "dist",
      emptyOutDir: true,
      sourcemap: true,
    },
    server: {
      port: frontendPort,
      proxy: {
        "/api": `http://localhost:${backendPort}`,
      },
    },
  };
});
