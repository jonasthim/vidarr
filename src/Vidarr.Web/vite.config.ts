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
  const isDev = mode !== "production";
  const devApiKey = env.VIDARR_API_KEY ?? "dev-key";

  return {
    plugins: [
      react(),
      {
        // In dev: inject the key so the SPA can authenticate without any
        // per-machine setup.
        // In production: leave the %VIDARR_API_KEY% placeholder verbatim so
        // the .NET host's IndexHtmlHandler can substitute it at request time
        // with the live (DB-persisted, rotatable) value.
        name: "vidarr-inject-api-key",
        transformIndexHtml(html) {
          return isDev ? html.replace(/%VIDARR_API_KEY%/g, devApiKey) : html;
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
