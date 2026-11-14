import { useCallback, useEffect, useRef, useState } from "react";
import type { components } from "../../api/generated/schema";
import { apiClient } from "../../shared/api/apiClient";
import { fetchCsrfToken } from "../../shared/api/http";
import { logger } from "../../shared/utils/logger";

export type LearningSession = components["schemas"]["LearningSessionResponse"];
export type ChatMessage = { role: "user" | "assistant"; content: string };
export type SessionStartError =
  | { kind: "payment"; message: string }
  | { kind: "unauthorized"; message: string }
  | { kind: "forbidden"; message: string }
  | { kind: "generic"; message: string };

export const MAX_MESSAGE_LENGTH = 2000;

// Formatea segundos a texto legible para la niña (p. ej. 0 → "< 1 min", 95 → "1 min 35 s").
export function formatTimeSpent(totalSeconds: number): string {
  const whole = Math.max(0, Math.floor(Number.isFinite(totalSeconds) ? totalSeconds : 0));
  if (whole === 0) return "< 1 min";
  if (whole < 60) return `${whole} s`;
  const minutes = Math.floor(whole / 60);
  const seconds = whole % 60;
  return seconds === 0 ? `${minutes} min` : `${minutes} min ${seconds} s`;
}

export function useLearningSession() {
  const [isStarting, setIsStarting] = useState(true);
  const [startError, setStartError] = useState<SessionStartError | null>(null);
  const [session, setSession] = useState<LearningSession | null>(null);
  const [threadId, setThreadId] = useState<string | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isSending, setIsSending] = useState(false);
  const [sendError, setSendError] = useState<string | null>(null);
  const startedRef = useRef(false);

  const start = useCallback(async () => {
    setIsStarting(true);
    setStartError(null);
    setSendError(null);
    try {
      const token = await fetchCsrfToken();
      const { response, data } = await apiClient.POST("/api/agent/session/start", {
        headers: token ? { "X-CSRF-TOKEN": token } : undefined,
      });

      if (!response.ok) {
        if (response.status === 402) {
          setStartError({ kind: "payment", message: "Hoy ya usaste todas tus interacciones gratuitas. Pide a una persona adulta que revise el plan para seguir aprendiendo. 🌿" });
        } else if (response.status === 401) {
          setStartError({ kind: "unauthorized", message: "Tu sesión terminó. Vuelve a entrar para continuar." });
        } else if (response.status === 403) {
          setStartError({ kind: "forbidden", message: "Elige a tu explorador para empezar la clase." });
        } else {
          setStartError({ kind: "generic", message: "No pudimos preparar tu clase de hoy. Intenta de nuevo en un momento." });
        }
        logger.error(`Inicio de sesión de aprendizaje fallido (${response.status})`);
        return;
      }

      if (!data) {
        setStartError({ kind: "generic", message: "No pudimos preparar tu clase de hoy. Intenta de nuevo en un momento." });
        return;
      }

      setSession(data);
      setThreadId(data.threadId ?? null);
      const greeting = data.initialAxolotlMessage?.trim();
      setMessages(greeting ? [{ role: "assistant", content: greeting }] : []);
    } catch {
      setStartError({ kind: "generic", message: "No pudimos conectar con el servidor. Revisa tu conexión e inténtalo de nuevo." });
      logger.error("Error de red al iniciar la sesión de aprendizaje");
    } finally {
      setIsStarting(false);
    }
  }, []);

  // React.StrictMode monta dos veces en desarrollo: la guarda evita el doble POST.
  useEffect(() => {
    if (startedRef.current) return;
    startedRef.current = true;
    void start();
  }, [start]);

  const sendMessage = useCallback(
    async (raw: string) => {
      const message = raw.trim();
      if (message.length === 0 || message.length > MAX_MESSAGE_LENGTH || isSending) return;

      setSendError(null);
      setMessages((prev) => [...prev, { role: "user", content: message }]);
      setIsSending(true);

      try {
        const token = await fetchCsrfToken();
        const { response, data } = await apiClient.POST("/api/agent/chat", {
          body: { threadId: threadId ?? undefined, message },
          headers: token ? { "X-CSRF-TOKEN": token } : undefined,
        });

        if (!response.ok) {
          if (response.status === 402) {
            setSendError("Hoy ya usaste todas tus interacciones gratuitas. Pide a una persona adulta que revise el plan. 🌿");
          } else {
            setMessages((prev) => [
              ...prev,
              { role: "assistant", content: "¡Uy! Me perdí un momentito. ¿Me cuentas otra vez tu idea? 🌿" },
            ]);
            setSendError("No pudimos enviar tu respuesta. Intenta de nuevo.");
          }
          logger.error(`Chat fallido (${response.status})`);
          return;
        }

        if (data?.threadId) setThreadId(data.threadId);
        setMessages((prev) => [...prev, { role: "assistant", content: data?.content ?? "" }]);
      } catch {
        setMessages((prev) => [
          ...prev,
          { role: "assistant", content: "¡Uy! No pude responder ahora. Intenta de nuevo en un ratito. 💧" },
        ]);
        setSendError("No pudimos conectar con el servidor.");
        logger.error("Error de red al enviar mensaje");
      } finally {
        setIsSending(false);
      }
    },
    [isSending, threadId],
  );

  return { isStarting, startError, session, threadId, messages, isSending, sendError, sendMessage, retry: start };
}
