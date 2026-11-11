// Logger centralizado. En producción (build) no imprime nada; en el futuro se
// conectará al backend. Regla: nunca usar console.log/error directamente en componentes.
const isDev = import.meta.env.DEV;

export const logger = {
  info(message: string, ...args: unknown[]): void {
    if (isDev) console.info(message, ...args);
  },
  warn(message: string, ...args: unknown[]): void {
    if (isDev) console.warn(message, ...args);
  },
  error(message: string, ...args: unknown[]): void {
    if (isDev) console.error(message, ...args);
  },
};
