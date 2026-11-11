import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from "react";
import { apiClient } from "../api/apiClient";
import { logger } from "../utils/logger";

export type AuthResult = { success: boolean; message?: string };

type AuthContextValue = {
  isAuthenticated: boolean;
  login: (email: string, password: string) => Promise<AuthResult>;
  register: (email: string, password: string, familyName: string) => Promise<AuthResult>;
  logout: () => Promise<void>;
};

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [isAuthenticated, setIsAuthenticated] = useState(false);

  const login = useCallback(async (email: string, password: string): Promise<AuthResult> => {
    try {
      const token = await fetchCsrfToken();
      const { response } = await apiClient.POST("/api/auth/login", {
        body: { email, password },
        headers: token ? { "X-CSRF-TOKEN": token } : undefined,
      });

      if (!response.ok) {
        logger.error(`Login fallido (${response.status})`);
        return { success: false, message: await readProblemTitle(response, "Credenciales inválidas.") };
      }

      setIsAuthenticated(true);
      return { success: true };
    } catch {
      logger.error("Error de red en login");
      return { success: false, message: "No se pudo conectar con el servidor." };
    }
  }, []);

  const register = useCallback(
    async (email: string, password: string, familyName: string): Promise<AuthResult> => {
      try {
        const token = await fetchCsrfToken();
        const { response } = await apiClient.POST("/api/auth/register", {
          body: { email, password, familyName },
          headers: token ? { "X-CSRF-TOKEN": token } : undefined,
        });

        if (!response.ok) {
          logger.error(`Registro fallido (${response.status})`);
          return { success: false, message: await readProblemTitle(response, "No se pudo crear la cuenta.") };
        }

        setIsAuthenticated(true);
        return { success: true };
      } catch {
        logger.error("Error de red en registro");
        return { success: false, message: "No se pudo conectar con el servidor." };
      }
    },
    [],
  );

  const logout = useCallback(async () => {
    try {
      const { response } = await apiClient.POST("/api/auth/logout", {});
      if (response.ok) {
        setIsAuthenticated(false);
      }
    } catch {
      logger.error("Error de red en logout");
    }
  }, []);

  const value = useMemo(
    () => ({ isAuthenticated, login, register, logout }),
    [isAuthenticated, login, register, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error("useAuth debe usarse dentro de <AuthProvider>.");
  }
  return context;
}

async function fetchCsrfToken(): Promise<string | null> {
  try {
    const { response } = await apiClient.GET("/api/antiforgery/token");
    if (!response.ok) return null;
    return readStringProperty(await readJsonBody(response), "token");
  } catch {
    logger.warn("No se pudo obtener el token anti-CSRF");
    return null;
  }
}

async function readProblemTitle(response: Response, fallback: string): Promise<string> {
  const title = readStringProperty(await readJsonBody(response), "title");
  return title && title.length > 0 ? title : fallback;
}

async function readJsonBody(response: Response): Promise<unknown> {
  try {
    return await response.json();
  } catch {
    return null;
  }
}

function readStringProperty(value: unknown, key: string): string | null {
  if (typeof value !== "object" || value === null) return null;
  const candidate = (value as Record<string, unknown>)[key];
  return typeof candidate === "string" ? candidate : null;
}
