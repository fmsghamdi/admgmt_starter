import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import path from "path";
import { fileURLToPath } from "url";

// __dirname في ESM:
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// عدّل المنفذ إذا لزم
const FRONT_PORT = 5174;
// هدف الـ API (باكند)
const API_TARGET = "http://localhost:5000";

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "src"),
    },
  },
  server: {
    port: FRONT_PORT,
    strictPort: true,
    open: true,
    proxy: {
      // يسمح لك أثناء التطوير تنادي /api مباشرة بدون CORS
      "/api": {
        target: API_TARGET,
        changeOrigin: true,
      },
    },
  },
});
