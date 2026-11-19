import type { components } from "../../api/generated/schema";

// Respaldo local (best-effort) del progreso del alumno, por perfil. Ante una respuesta
// degradada o vacía del backend, permite que el selector de misiones no muestre como
// pendientes temas ya cerrados en este navegador y conserve XP/insignias.
// No guarda tokens ni identificadores de sesión: la clave es un hash estable del perfil.

type LearningMetrics = components["schemas"]["LearningMetricsDto"];
type ExplorerSummary = components["schemas"]["ExplorerSummaryDto"];

export type ProgressBackup = {
  completedTopicIds: string[];
  pausedTopicIds: string[];
  totalXp: number;
  currentLevel: number;
  badges: string[];
  savedAt: string;
};

const STORAGE_PREFIX = "homeschool:progress:";

// djb2: hash estable y sin dependencias para no guardar el nombre del alumno en claro.
function stableHash(text: string): string {
  let hash = 5381;
  for (let i = 0; i < text.length; i++) {
    hash = ((hash << 5) + hash + text.charCodeAt(i)) >>> 0;
  }
  return hash.toString(36);
}

export function profileStorageKey(explorer: ExplorerSummary | null | undefined): string | null {
  const identity = [explorer?.childName ?? "", String(explorer?.grade ?? ""), explorer?.avatarUrl ?? ""].join("|");
  if (identity === "||") return null;
  return `${STORAGE_PREFIX}${stableHash(identity)}`;
}

export function readProgressBackup(key: string): ProgressBackup | null {
  try {
    const raw = localStorage.getItem(key);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Partial<ProgressBackup>;
    return {
      completedTopicIds: toArrayOfStrings(parsed.completedTopicIds),
      pausedTopicIds: toArrayOfStrings(parsed.pausedTopicIds),
      totalXp: typeof parsed.totalXp === "number" ? parsed.totalXp : 0,
      currentLevel: typeof parsed.currentLevel === "number" ? parsed.currentLevel : 1,
      badges: toArrayOfStrings(parsed.badges),
      savedAt: typeof parsed.savedAt === "string" ? parsed.savedAt : "",
    };
  } catch {
    return null;
  }
}

export function writeProgressBackup(key: string, metrics: LearningMetrics | null | undefined): void {
  if (!metrics) return;
  const history = metrics.topicHistory ?? [];
  // Sin historial no hay nada que respaldar; además evita pisar el respaldo local con
  // una respuesta degradada (historial vacío) que no persistió en el servidor.
  if (history.length === 0) return;
  const snapshot: ProgressBackup = {
    completedTopicIds: history.filter((t) => t.status === "Completed").map((t) => t.topicId).filter(isNonEmptyString),
    pausedTopicIds: history.filter((t) => t.status === "Partial").map((t) => t.topicId).filter(isNonEmptyString),
    totalXp: metrics.totalXp ?? 0,
    currentLevel: metrics.currentLevel ?? 1,
    badges: metrics.badgesEarned ?? [],
    savedAt: new Date().toISOString(),
  };
  try {
    localStorage.setItem(key, JSON.stringify(snapshot));
  } catch {
    // storage lleno o no disponible: el respaldo es best-effort y nunca rompe la UI.
  }
}

// Devuelve los topicId ya cerrados (completados o pausados) según el respaldo local.
export function closedTopicIds(backup: ProgressBackup | null): ReadonlySet<string> {
  if (!backup) return new Set<string>();
  return new Set<string>([...backup.completedTopicIds, ...backup.pausedTopicIds]);
}

// Fusiona las métricas del servidor con el respaldo local: si el servidor vino
// degradado o vacío, completa nivel, XP, insignias y conteo de completados.
export function applyBackupToMetrics(
  metrics: LearningMetrics | null | undefined,
  backup: ProgressBackup | null,
): LearningMetrics | null | undefined {
  if (!backup) return metrics;
  if (!metrics) {
    return {
      currentLevel: backup.currentLevel,
      totalXp: backup.totalXp,
      completedTopicsCount: backup.completedTopicIds.length,
      badgesEarned: backup.badges.length > 0 ? backup.badges : null,
      topicHistory: null,
    };
  }
  return {
    ...metrics,
    currentLevel: metrics.currentLevel ?? backup.currentLevel,
    totalXp: metrics.totalXp ?? backup.totalXp,
    completedTopicsCount: Math.max(metrics.completedTopicsCount ?? 0, backup.completedTopicIds.length),
    badgesEarned: metrics.badgesEarned && metrics.badgesEarned.length > 0 ? metrics.badgesEarned : backup.badges,
  };
}

function toArrayOfStrings(value: unknown): string[] {
  return Array.isArray(value) ? value.filter(isNonEmptyString) : [];
}

function isNonEmptyString(value: unknown): value is string {
  return typeof value === "string" && value.length > 0;
}