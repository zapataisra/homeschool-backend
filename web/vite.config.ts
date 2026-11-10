import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// En desarrollo, Vite hace de proxy: /api → backend (contenedor `api`).
// El backend es la única fuente de verdad; el navegador nunca llama a proveedores de IA.
export default defineConfig({
  plugins: [react()],
  server: {
    host: true,
    port: 5173,
    proxy: {
      "/api": {
        target: "http://api:8080",
        changeOrigin: true,
      },
    },
  },
});
