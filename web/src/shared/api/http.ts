import { apiClient } from "./apiClient";
import { logger } from "../utils/logger";

// Helpers HTTP compartidos. openapi-fetch YA consume el cuerpo de la respuesta
// (response.json()/text()); aquí se leen los campos desde `data`/`error`, nunca
// volviendo a leer response.body (lanzaría "body stream already read").

export async function fetchCsrfToken(): Promise<string | null> {
  try {
    const { response, data } = await apiClient.GET("/api/antiforgery/token");
    if (!response.ok) return null;
    return readStringProperty(data, "token");
  } catch {
    logger.warn("No se pudo obtener el token anti-CSRF");
    return null;
  }
}

export function readStringProperty(value: unknown, key: string): string | null {
  if (typeof value !== "object" || value === null) return null;
  const candidate = (value as Record<string, unknown>)[key];
  return typeof candidate === "string" ? candidate : null;
}
