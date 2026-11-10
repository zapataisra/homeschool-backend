import createClient from "openapi-fetch";
import type { paths } from "./generated/schema";

// Único cliente de API compartido (FRONTEND §2). Base en el mismo origen: en desarrollo
// Vite hace de proxy de /api hacia el backend. Las cookies viajan con `credentials`.
// Las mutaciones futuras añadirán la cabecera anti-CSRF "X-CSRF-TOKEN" (backend).
export const apiClient = createClient<paths>({
  baseUrl: "",
  credentials: "include",
});
