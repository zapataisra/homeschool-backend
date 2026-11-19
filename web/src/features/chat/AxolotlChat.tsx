import { useEffect, useState, type FormEvent, type ReactNode } from "react";
import { Navigate, useNavigate } from "react-router-dom";
import type { components } from "../../api/generated/schema";
import { apiClient } from "../../shared/api/apiClient";
import { useAuth } from "../../shared/contexts/AuthContext";
import {
  MAX_MESSAGE_LENGTH,
  formatTimeSpent,
  useLearningSession,
  type AttemptEvaluationOutcome,
  type AttemptOutcomeDto,
  type ChatMessage,
  type SessionStartError,
} from "./useLearningSession";
import { applyBackupToMetrics, closedTopicIds, type ProgressBackup } from "./progressLocalBackup";
import { resolveAssetUrl } from "../../lib/assets";

type LearningSession = components["schemas"]["LearningSessionResponse"];
type ExplorerSummary = components["schemas"]["ExplorerSummaryDto"];
type LearningMetrics = components["schemas"]["LearningMetricsDto"];
type ActiveLesson = components["schemas"]["ActiveLessonDto"];
type LessonBlock = components["schemas"]["LessonBlockDto"];
type TopicHistoryItem = components["schemas"]["TopicHistoryItemDto"];
type MissionOption = components["schemas"]["MissionOptionDto"];

export default function AxolotlChat() {
  const { isStarting, startError, session, messages, isSending, sendError, lastOutcome, progressBackup, sendMessage, retry, advanceToNextLesson, selectMission } =
    useLearningSession();
  const navigate = useNavigate();
  const { logout } = useAuth();

  const handleLogout = async () => {
    await logout();
    navigate("/login");
  };

  const handleChangeExplorer = async () => {
    // Restaura la sesión adulta antes de ir al selector de exploradores
    // (en sesión Child, GET /api/family/children devuelve 403).
    try {
      await apiClient.POST("/api/auth/exit-child", {});
    } catch {
      // ProfileSelect también saldrá de la sesión delegada si hace falta.
    }
    navigate("/profile-select");
  };

  if (isStarting) return <LearningCenterSkeleton />;

  if (startError?.kind === "unauthorized") return <Navigate to="/login" replace />;
  if (startError?.kind === "forbidden") return <Navigate to="/profile-select" replace />;

  if (startError || !session) {
    return (
      <LearningCenterError
        error={startError}
        onRetry={() => void retry()}
        onLogout={() => void handleLogout()}
      />
    );
  }

  return (
    <LearningCenter
      session={session}
      progressBackup={progressBackup}
      messages={messages}
      isSending={isSending}
      sendError={sendError}
      lastOutcome={lastOutcome}
      onSend={(message) => void sendMessage(message)}
      onAdvance={() => void advanceToNextLesson()}
      onSelectMission={(topicId) => void selectMission(topicId)}
      onChangeExplorer={() => void handleChangeExplorer()}
      onLogout={() => void handleLogout()}
    />
  );
}

function KawaiiShell({
  children,
  ariaLabel,
  centered = false,
}: {
  children: ReactNode;
  ariaLabel?: string;
  centered?: boolean;
}) {
  return (
    <div className="relative flex min-h-screen w-full flex-col overflow-x-hidden bg-white">
      <img
        src="/assets/greca-top.svg"
        alt=""
        aria-hidden="true"
        className="pointer-events-none fixed inset-x-0 top-0 z-30 h-8 w-full select-none bg-white object-cover object-bottom md:h-12 lg:h-14"
      />
      <img
        src="/assets/greca-bottom.svg"
        alt=""
        aria-hidden="true"
        className="pointer-events-none fixed inset-x-0 bottom-0 z-30 h-8 w-full select-none bg-white object-cover object-top md:h-12 lg:h-14"
      />

      <main
        aria-label={ariaLabel}
        className={
          centered
            ? "relative z-10 mx-auto flex w-full max-w-md flex-1 flex-col items-center justify-center px-4 pt-16 pb-16 md:pt-20 md:pb-24 lg:pt-24 lg:pb-28"
            : "relative z-10 mx-auto flex w-full max-w-4xl flex-col gap-6 px-4 pt-16 pb-16 md:gap-8 md:px-6 md:pt-20 md:pb-24 lg:pt-24 lg:pb-28"
        }
      >
        {children}
      </main>
    </div>
  );
}

function LearningCenter({ session, progressBackup, messages, isSending, sendError, lastOutcome, onSend, onAdvance, onSelectMission, onChangeExplorer, onLogout }: {
  session: LearningSession;
  progressBackup: ProgressBackup | null;
  messages: ChatMessage[];
  isSending: boolean;
  sendError: string | null;
  lastOutcome: AttemptOutcomeDto | null;
  onSend: (message: string) => void;
  onAdvance: () => void;
  onSelectMission: (topicId: string) => void;
  onChangeExplorer: () => void;
  onLogout: () => void;
}) {
  // Respaldo local: oculta misiones ya cerradas en este navegador y completa métricas
  // degradadas o vacías (nivel, XP, insignias y conteo de completados).
  const closed = closedTopicIds(progressBackup);
  const effectiveMetrics = applyBackupToMetrics(session.metrics, progressBackup);
  const visibleMissions = (session.availableMissions ?? []).filter((m) => !m.topicId || !closed.has(m.topicId));

  const selectingMission = !session.activeLesson && visibleMissions.length > 0;
  const allDone = !session.activeLesson && !selectingMission && ((effectiveMetrics?.completedTopicsCount ?? 0) > 0 || closed.size > 0);

  return (
    <KawaiiShell>
      {session.explorer && <ExplorerBar explorer={session.explorer} onChangeExplorer={onChangeExplorer} onLogout={onLogout} />}
      {effectiveMetrics && <ProgressZone metrics={effectiveMetrics} />}
      {selectingMission ? (
        <MissionSelectorZone missions={visibleMissions} dailyGreeting={session.dailyGreeting ?? ""} onSelect={onSelectMission} />
      ) : allDone ? (
        <AllDoneZone />
      ) : (
        <>
          {session.activeLesson && <LessonZone lesson={session.activeLesson} />}
          <ChatZone lesson={session.activeLesson} messages={messages} isSending={isSending} sendError={sendError} lastOutcome={lastOutcome} dailyGreeting={session.dailyGreeting ?? ""} onSend={onSend} onAdvance={onAdvance} />
        </>
      )}
    </KawaiiShell>
  );
}

function AllDoneZone() {
  return (
    <section aria-label="Todos los proyectos completados" className="rounded-[2rem] border-4 border-ink bg-arena p-6 shadow-solid ring-8 ring-white text-center">
      <img src="/assets/Axolotl.svg" alt="Axolotl" className="mx-auto h-20 w-20 animate-breathe" />
      <h2 className="mt-3 font-heading text-xl font-extrabold text-ink">¡Completaste todos los proyectos de tu grado! 🎉</h2>
      <p className="mt-2 font-body text-ink/80">Eres increíble. Puedes repasar los temas en tu historial cuando quieras.</p>
    </section>
  );
}

function ExplorerBar({ explorer, onChangeExplorer, onLogout }: {
  explorer: ExplorerSummary;
  onChangeExplorer: () => void;
  onLogout: () => void;
}) {
  const avatarUrl = explorer.avatarUrl?.trim() ? `/assets/${explorer.avatarUrl}` : "/assets/avatar-aguila.svg";
  return (
    <header className="flex flex-wrap items-center gap-3">
      <img
        src={avatarUrl}
        alt={`Avatar de ${explorer.childName ?? "tu explorador"}`}
        className="h-14 w-14 shrink-0 rounded-full border-4 border-ink bg-arena object-contain p-1 md:h-20 md:w-20"
      />
      <div className="min-w-0 flex-1">
        <h1 className="truncate font-heading text-xl font-extrabold text-ink md:text-2xl">{explorer.childName ?? "Explorador"}</h1>
        <p className="font-body text-xs text-ink/70 md:text-sm">{explorer.grade != null ? `Grado ${explorer.grade}` : "Grado"}</p>
      </div>
      <div className="flex flex-wrap gap-2">
        <button
          type="button"
          onClick={onChangeExplorer}
          className="rounded-xl border-2 border-ink bg-white px-3 py-2 font-heading text-xs font-bold text-ink transition-all hover:brightness-95 focus:outline-none focus:ring-4 focus:ring-quetzal active:translate-x-[4px] active:translate-y-[6px] active:shadow-none disabled:cursor-not-allowed disabled:opacity-60 md:text-sm"
        >
          Cambiar explorador
        </button>
        <button
          type="button"
          onClick={onLogout}
          className="rounded-xl border-2 border-ink bg-arena px-3 py-2 font-heading text-xs font-bold text-ink transition-all hover:brightness-95 focus:outline-none focus:ring-4 focus:ring-quetzal active:translate-x-[4px] active:translate-y-[6px] active:shadow-none disabled:cursor-not-allowed disabled:opacity-60 md:text-sm"
        >
          Cerrar sesión
        </button>
      </div>
    </header>
  );
}

function MetricCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-2xl border-2 border-ink bg-white px-3 py-3 text-center">
      <p className="font-heading text-base font-extrabold text-ink md:text-lg">{value}</p>
      <p className="mt-0.5 font-body text-[11px] text-ink/70 md:text-xs">{label}</p>
    </div>
  );
}

function ProgressZone({ metrics }: { metrics: LearningMetrics }) {
  return (
    <section aria-label="Mi progreso" className="rounded-[2rem] border-4 border-ink bg-arena p-5 shadow-solid ring-8 ring-white md:p-6">
      <h2 className="font-heading text-xl font-extrabold text-ink md:text-2xl">Mi progreso</h2>
      <div className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
        <MetricCard label="Nivel" value={`${metrics.currentLevel ?? 1}`} />
        <MetricCard label="Experiencia" value={`${metrics.totalXp ?? 0} XP`} />
        <MetricCard label="Tiempo total" value={formatTimeSpent(metrics.totalTimeSpentSeconds ?? 0)} />
        <MetricCard label="Promedio por tema" value={formatTimeSpent(metrics.averageTimePerTopicSeconds ?? 0)} />
        <MetricCard label="Temas completados" value={`${metrics.completedTopicsCount ?? 0}`} />
      </div>

      <h3 className="mt-5 font-heading text-base font-extrabold text-ink md:text-lg">Insignias</h3>
      <Badges badges={metrics.badgesEarned} />

      <h3 className="mt-5 font-heading text-base font-extrabold text-ink md:text-lg">Historial de temas</h3>
      <TopicHistory history={metrics.topicHistory} />
    </section>
  );
}

function Badges({ badges }: { badges: string[] | null | undefined }) {
  if (!badges || badges.length === 0) {
    return (
      <p className="mt-2 rounded-2xl border-2 border-dashed border-ink/40 bg-white/60 px-4 py-3 font-body text-sm text-ink/80">
        ¡Completa tu primer reto con Axolotl para ganar tu primera insignia! 🌟
      </p>
    );
  }
  return (
    <ul className="mt-2 flex flex-wrap gap-2">
      {badges.map((badge) => (
        <li key={badge} className="rounded-full border-2 border-ink bg-elote px-3 py-1 font-heading text-xs font-bold text-ink">
          {badge}
        </li>
      ))}
    </ul>
  );
}

function TopicHistory({ history }: { history: TopicHistoryItem[] | null | undefined }) {
  if (!history || history.length === 0) {
    return (
      <p className="mt-2 rounded-2xl border-2 border-dashed border-ink/40 bg-white/60 px-4 py-3 font-body text-sm text-ink/80">
        Todavía no hay temas en tu historial. ¡Tu primer tema aparecerá aquí!
      </p>
    );
  }
  return (
    <ul className="mt-2 space-y-2">
      {history.map((item, index) => (
        <li key={item.topicId ?? index} className="flex flex-wrap items-center gap-x-3 gap-y-1 rounded-2xl border-2 border-ink bg-white px-3 py-2">
          <div className="min-w-0 flex-1">
            <p className="truncate font-heading text-sm font-bold text-ink">{item.topicTitle ?? "Tema"}</p>
            <p className="truncate font-body text-xs text-ink/70">{item.campoFormativo ?? ""}</p>
          </div>
          <span className={`rounded-full border-2 border-ink px-2 py-0.5 font-heading text-xs font-bold ${item.status === "Completed" ? "bg-quetzal text-ink" : "bg-elote text-ink"}`}>
            {topicStatusLabel(item.status)}
          </span>
          <span className="font-body text-xs text-ink/70">{item.attempts ?? 0} intentos</span>
          <span className="font-body text-xs text-ink/70">{formatTimeSpent(item.timeSpentSeconds ?? 0)}</span>
        </li>
      ))}
    </ul>
  );
}

function topicStatusLabel(status: string | null | undefined): string {
  return status === "Completed" ? "Completado" : "En curso";
}

function LessonZone({ lesson }: { lesson: ActiveLesson }) {
  return (
    <section aria-label="Proyecto de la clase de hoy" className="rounded-[2rem] border-4 border-ink bg-arena p-5 shadow-solid ring-8 ring-white md:p-6">
      <h2 className="font-heading text-xl font-extrabold text-ink md:text-2xl">Proyecto de la clase de hoy</h2>
      <div className="mt-3 rounded-2xl border-2 border-ink bg-white p-4">
        <h3 className="font-heading text-base font-extrabold text-ink md:text-lg">{lesson.weeklyProjectTitle ?? "Proyecto de la clase de hoy"}</h3>
        <p className="mt-1 font-body text-sm text-ink/80">{lesson.weeklyProjectGoal ?? ""}</p>
      </div>
      <SlideCarousel lesson={lesson} />
    </section>
  );
}

function SlideCarousel({ lesson }: { lesson: ActiveLesson }) {
  const blocks = lesson.blocks ?? [];
  const [slideIndex, setSlideIndex] = useState(0);
  const [direction, setDirection] = useState<"next" | "prev">("next");

  if (blocks.length === 0) {
    return (
      <p className="mt-4 rounded-2xl border-2 border-dashed border-ink/40 bg-white/60 px-4 py-3 font-body text-sm text-ink/80">
        La presentación de hoy aún no está lista. ¡Pregúntale a Axolotl por el tema!
      </p>
    );
  }

  const safeIndex = Math.min(slideIndex, blocks.length - 1);
  const current = blocks[safeIndex];
  const goPrev = () => {
    setDirection("prev");
    setSlideIndex((i) => (i <= 0 ? blocks.length - 1 : i - 1));
  };
  const goNext = () => {
    setDirection("next");
    setSlideIndex((i) => (i >= blocks.length - 1 ? 0 : i + 1));
  };

  return (
    <div className="mt-4">
      <div className="flex items-center justify-between gap-3">
        <h3 className="font-heading text-base font-extrabold text-ink md:text-lg">Presentación del tema</h3>
        <p className="font-body text-xs text-ink/70">Lámina {safeIndex + 1} de {blocks.length}</p>
      </div>

      <div className="mt-3 overflow-hidden rounded-2xl border-2 border-ink bg-white">
        <div key={safeIndex} className={direction === "next" ? "animate-slide-in-right" : "animate-slide-in-left"}>
          {current.imageUrl ? (
            <img
              src={resolveAssetUrl(current.imageUrl)}
              alt={current.altText ?? current.narrationText ?? "Diapositiva del tema"}
              className="h-48 w-full rounded-t-lg bg-gray-100 object-cover"
            />
          ) : null}
          <div className="min-h-[120px] p-4">
            <SlideContent block={current} />
          </div>
        </div>
      </div>

      {current.narrationText ? (
        <p className="mt-3 rounded-2xl border-2 border-ink bg-tlaloc/30 px-4 py-2 font-body text-sm text-ink">🗣️ {current.narrationText}</p>
      ) : null}

      <div className="mt-4 flex items-center justify-between gap-3">
        <button
          type="button"
          onClick={goPrev}
          className="rounded-xl border-2 border-ink bg-elote px-4 py-2 font-heading text-sm font-bold text-ink transition-all hover:brightness-105 focus:outline-none focus:ring-4 focus:ring-quetzal active:translate-x-[4px] active:translate-y-[6px] active:shadow-none disabled:cursor-not-allowed disabled:opacity-60"
        >
          « Anterior
        </button>
        <button
          type="button"
          onClick={goNext}
          className="rounded-xl border-2 border-ink bg-elote px-4 py-2 font-heading text-sm font-bold text-ink transition-all hover:brightness-105 focus:outline-none focus:ring-4 focus:ring-quetzal active:translate-x-[4px] active:translate-y-[6px] active:shadow-none disabled:cursor-not-allowed disabled:opacity-60"
        >
          Siguiente »
        </button>
      </div>
    </div>
  );
}

function SlideContent({ block }: { block: LessonBlock }) {
  if (block.type === "image") {
    return (
      <img
        src={block.content ?? ""}
        alt={block.altText ?? block.narrationText ?? "Diapositiva del tema"}
        className="mx-auto max-h-48 w-auto rounded-xl object-contain"
      />
    );
  }
  return <p className="font-body leading-relaxed text-ink">{block.content ?? ""}</p>;
}

function MissionSelectorZone({ missions, dailyGreeting, onSelect }: { missions: MissionOption[]; dailyGreeting: string; onSelect: (topicId: string) => void }) {
  return (
    <section aria-label="Elige el proyecto de tu clase de hoy" className="rounded-[2rem] border-4 border-ink bg-arena p-5 shadow-solid ring-8 ring-white md:p-6">
      <div className="flex items-center gap-3">
        <img src="/assets/Axolotl.svg" alt="Axolotl" className="h-12 w-12 shrink-0 md:h-14 md:w-14" />
        <div className="min-w-0">
          <h2 className="font-heading text-lg font-extrabold text-ink md:text-xl">Elige el proyecto de la clase de hoy</h2>
          <p className="font-body text-sm text-ink/80">Escoge 1 de estos 3 proyectos para comenzar la clase de hoy. 🌿</p>
        </div>
      </div>

      {dailyGreeting && (
        <div className="mt-4 flex items-start gap-3 rounded-2xl border-2 border-ink bg-white p-3 shadow-solid-sm">
          <img src="/assets/Axolotl.svg" alt="" aria-hidden="true" className="h-10 w-10 shrink-0" />
          <p className="font-body text-sm text-ink">{dailyGreeting}</p>
        </div>
      )}

      <div className="mt-4 grid gap-3 md:grid-cols-3">
        {missions.map((mission) => (
          <article key={mission.topicId} className="flex flex-col rounded-2xl border-4 border-ink bg-white p-4 shadow-solid-sm">
            <h3 className="font-heading text-base font-extrabold text-ink">{mission.title}</h3>
            <p className="mt-1 font-body text-xs text-ink/70">{mission.campoFormativo}</p>
            <p className="mt-2 font-body text-sm font-bold text-ink">{mission.weeklyProjectTitle}</p>
            <p className="mt-1 font-body text-xs text-ink/70">{mission.weeklyProjectGoal}</p>
            <div className="mt-3 flex flex-wrap gap-1">
              {mission.isRecommended && (
                <span className="rounded-full bg-elote px-2 py-0.5 font-heading text-xs font-bold text-ink">⭐ Recomendada para tu trimestre</span>
              )}
              {mission.status === "Completed" && (
                <span className="rounded-full bg-quetzal px-2 py-0.5 font-heading text-xs font-bold text-ink">✓ Completada</span>
              )}
            </div>
            <button
              type="button"
              onClick={() => mission.topicId && onSelect(mission.topicId)}
              className="mt-3 w-full rounded-xl border-2 border-ink bg-quetzal px-3 py-2 font-heading text-sm font-bold text-ink transition-all hover:brightness-105 focus:outline-none focus:ring-4 focus:ring-quetzal active:translate-x-[4px] active:translate-y-[6px] active:shadow-none disabled:cursor-not-allowed disabled:opacity-60"
            >
              ¡Comenzar esta misión!
            </button>
          </article>
        ))}
      </div>
    </section>
  );
}

function ChatZone({ lesson, messages, isSending, sendError, lastOutcome, dailyGreeting, onSend, onAdvance }: {
  lesson: ActiveLesson | undefined;
  messages: ChatMessage[];
  isSending: boolean;
  sendError: string | null;
  lastOutcome: AttemptOutcomeDto | null;
  dailyGreeting: string;
  onSend: (message: string) => void;
  onAdvance: () => void;
}) {
  const [input, setInput] = useState("");
  const [isRecording, setIsRecording] = useState(false);
  const [isSpeaking, setIsSpeaking] = useState(false);
  const outcome = lastOutcome?.outcome ?? null;
  const isClosed = outcome?.isTopicClosed === true;

  const lastMessage = messages[messages.length - 1];

  // Indicador de voz (solo UI, sin Web Audio API): cuando Axolotl responde con
  // un mensaje nuevo, las barras de sonido se activan 3 segundos junto a su avatar.
  useEffect(() => {
    if (!lastMessage || lastMessage.role !== "assistant") return;
    setIsSpeaking(true);
    const timer = setTimeout(() => setIsSpeaking(false), 3000);
    return () => clearTimeout(timer);
  }, [lastMessage]);

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const message = input.trim();
    if (message.length === 0 || message.length > MAX_MESSAGE_LENGTH || isSending || isClosed) return;
    setInput("");
    onSend(message);
  };

  return (
    <section aria-label="Clase del día con Axolotl" className="rounded-[2rem] border-4 border-ink bg-arena p-5 shadow-solid ring-8 ring-white md:p-6">
      <div className="flex items-center gap-3">
        <img src="/assets/Axolotl.svg" alt="Axolotl" className="h-12 w-12 shrink-0 md:h-14 md:w-14" />
        <div className="min-w-0 flex-1">
          <h2 className="font-heading text-lg font-extrabold text-ink md:text-xl">Clase del día</h2>
          {lesson && (
            <p className="truncate font-body text-xs text-ink/70 md:text-sm">
              {lesson.campoFormativo ?? ""} · {lesson.title ?? ""}
            </p>
          )}
        </div>
        {(isRecording || isSpeaking) && (
          <VoiceIndicator label={isRecording ? "Axolotl está escuchando…" : "Axolotl está hablando…"} />
        )}
      </div>

      {dailyGreeting && (
        <p className="mt-3 rounded-2xl border-2 border-ink bg-tlaloc/30 px-3 py-2 font-body text-sm text-ink">
          {dailyGreeting}
        </p>
      )}

      <div className="mt-4 flex max-h-[420px] flex-col gap-3 overflow-y-auto rounded-[1.5rem] border-4 border-ink bg-white p-4 ring-4 ring-white">
        {messages.map((message, index) => (
          <MessageBubble key={index} message={message} />
        ))}

        {isSending && (
          <div className="flex items-start gap-3" aria-label="Axolotl está pensando">
            <img src="/assets/Axolotl.svg" alt="" className="h-10 w-10 shrink-0" />
            <div className="h-12 w-40 animate-pulse rounded-2xl border-2 border-ink/20 bg-arena/50" />
          </div>
        )}
      </div>

      {lastOutcome && !isClosed && (
        <p className="mt-3 inline-block rounded-full border-2 border-ink bg-elote/60 px-3 py-1 font-heading text-xs font-bold text-ink">
          Intento {outcome?.attemptNumber ?? 1} de 3 · ¡Sigue explorando!
        </p>
      )}

      {isClosed && outcome && <MissionBanner outcome={outcome} onAdvance={onAdvance} />}

      {sendError && <p className="mt-2 text-center font-body text-sm text-ink/70">{sendError}</p>}

      <form onSubmit={handleSubmit} className="mt-3 flex items-end gap-2">
        <button
          type="button"
          onPointerDown={() => setIsRecording(true)}
          onPointerUp={() => setIsRecording(false)}
          onPointerLeave={() => setIsRecording(false)}
          onPointerCancel={() => setIsRecording(false)}
          aria-label="Mantén presionado para hablar con Axolotl"
          aria-pressed={isRecording}
          disabled={isClosed}
          className={`flex h-16 w-16 shrink-0 items-center justify-center rounded-full border-4 border-ink text-3xl shadow-solid transition-all duration-100 hover:brightness-105 focus:outline-none focus:ring-4 focus:ring-quetzal active:translate-x-[4px] active:translate-y-[6px] active:shadow-none disabled:cursor-not-allowed disabled:opacity-60 ${isRecording ? "animate-pulse bg-ajolote" : "bg-quetzal"}`}
        >
          🎤
        </button>
        <textarea
          value={input}
          onChange={(event) => setInput(event.target.value)}
          placeholder="Escribe tu respuesta…"
          rows={2}
          maxLength={MAX_MESSAGE_LENGTH}
          aria-label="Escribe tu respuesta para Axolotl"
          disabled={isClosed}
          className="flex-1 resize-none rounded-2xl border-2 border-ink bg-white px-3 py-2 font-body text-ink placeholder-ink/50 outline-none focus:ring-4 focus:ring-quetzal disabled:cursor-not-allowed disabled:opacity-60"
        />
        <button
          type="submit"
          disabled={isSending || input.trim().length === 0 || isClosed}
          aria-label="Enviar respuesta"
          className="rounded-2xl border-4 border-ink bg-quetzal px-5 py-3 font-heading text-lg font-extrabold text-ink shadow-solid transition-all duration-100 hover:brightness-105 active:translate-x-[4px] active:translate-y-[6px] active:shadow-none disabled:cursor-not-allowed disabled:opacity-60"
        >
          Enviar
        </button>
      </form>
    </section>
  );
}

function MissionBanner({ outcome, onAdvance }: { outcome: AttemptEvaluationOutcome; onAdvance: () => void }) {
  const completed = outcome.newStatus === "Completed";
  return (
    <div className={`mt-4 rounded-2xl border-2 border-ink p-4 ${completed ? "bg-quetzal/30" : "bg-arena"}`}>
      <p className="font-heading text-base font-extrabold text-ink md:text-lg">
        {completed
          ? `¡Misión completada! +${outcome.xpAwarded ?? 0} XP`
          : `¡Gran esfuerzo! +${outcome.xpAwarded ?? 0} XP · Pausaremos este reto para reforzar las bases en la siguiente misión.`}
      </p>
      {completed && outcome.badgeEarned ? (
        <p className="mt-1 font-body text-sm text-ink/80">🏅 {outcome.badgeEarned}</p>
      ) : null}
      <button
        type="button"
        onClick={onAdvance}
        className="mt-3 w-full rounded-xl border-2 border-ink bg-elote px-4 py-2 font-heading text-sm font-bold text-ink transition-all hover:brightness-105 focus:outline-none focus:ring-4 focus:ring-quetzal active:translate-x-[4px] active:translate-y-[6px] active:shadow-none disabled:cursor-not-allowed disabled:opacity-60"
      >
        Siguiente misión »
      </button>
    </div>
  );
}

function MessageBubble({ message }: { message: ChatMessage }) {
  const isUser = message.role === "user";
  return (
    <div className={`flex ${isUser ? "justify-end" : "justify-start"}`}>
      {!isUser && <img src="/assets/Axolotl.svg" alt="" className="mr-2 h-10 w-10 shrink-0" />}
      <p className={`max-w-[80%] border-2 border-ink px-4 py-2 font-body text-ink ${isUser ? "rounded-2xl rounded-tr-none bg-quetzal" : "rounded-2xl rounded-tl-none bg-arena"}`}>
        {message.content}
      </p>
    </div>
  );
}

function VoiceIndicator({ label }: { label: string }) {
  return (
    <div className="flex shrink-0 items-center gap-2 rounded-full border-2 border-ink bg-white px-3 py-1.5" role="status" aria-live="polite">
      <SoundBars />
      <span className="font-body text-xs font-bold text-ink">{label}</span>
    </div>
  );
}

function SoundBars() {
  return (
    <span className="flex h-6 items-center gap-1" aria-hidden="true">
      {[0, 1, 2, 3].map((i) => (
        <span key={i} className="h-full w-1.5 origin-bottom animate-soundbar rounded-full bg-quetzal" style={{ animationDelay: `${i * 150}ms` }} />
      ))}
    </span>
  );
}

function LearningCenterSkeleton() {
  return (
    <KawaiiShell ariaLabel="Cargando tu clase">
      <div className="flex items-center gap-3">
        <div className="h-14 w-14 animate-pulse rounded-full bg-arena/50 md:h-20 md:w-20" />
        <div className="flex-1 space-y-2">
          <div className="h-6 w-40 animate-pulse rounded-full bg-arena/50" />
          <div className="h-4 w-24 animate-pulse rounded-full bg-arena/50" />
        </div>
      </div>

      <div className="space-y-3">
        <div className="h-6 w-36 animate-pulse rounded-full bg-arena/50" />
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
          {[0, 1, 2, 3, 4].map((i) => (
            <div key={i} className="h-20 animate-pulse rounded-2xl bg-arena/50" />
          ))}
        </div>
        <div className="h-24 animate-pulse rounded-2xl bg-arena/50" />
      </div>

      <div className="space-y-3">
        <div className="h-6 w-40 animate-pulse rounded-full bg-arena/50" />
        <div className="h-32 animate-pulse rounded-2xl bg-arena/50" />
        <div className="h-12 animate-pulse rounded-xl bg-arena/50" />
      </div>

      <div className="space-y-3">
        <div className="h-6 w-36 animate-pulse rounded-full bg-arena/50" />
        <div className="h-56 animate-pulse rounded-2xl bg-arena/50" />
        <div className="h-12 w-32 animate-pulse rounded-2xl bg-arena/50" />
      </div>
    </KawaiiShell>
  );
}

function LearningCenterError({ error, onRetry, onLogout }: {
  error: SessionStartError | null;
  onRetry: () => void;
  onLogout: () => void;
}) {
  return (
    <KawaiiShell centered>
      <div className="flex w-full flex-col items-center gap-4 text-center">
        <img src="/assets/Axolotl.svg" alt="Axolotl" className="w-28 animate-breathe drop-shadow-sm" />
        <div className="w-full rounded-[2rem] border-4 border-ink bg-arena p-6 shadow-solid ring-8 ring-white">
          <h1 className="font-heading text-xl font-extrabold text-ink">
            {error?.kind === "payment" ? "¡Qué aventura tan larga!" : "¡Uy, algo pasó!"}
          </h1>
          <p className="mt-2 font-body text-sm text-ink/80">{error?.message ?? "No pudimos preparar tu clase de hoy."}</p>
          <div className="mt-4 flex flex-col gap-2">
            {error?.kind === "payment" ? (
              <button
                type="button"
                onClick={onLogout}
                className="w-full rounded-xl border-2 border-ink bg-elote px-4 py-2 font-heading text-sm font-bold text-ink transition-all hover:brightness-105 focus:outline-none focus:ring-4 focus:ring-quetzal active:translate-x-[4px] active:translate-y-[6px] active:shadow-none disabled:cursor-not-allowed disabled:opacity-60"
              >
                Cerrar sesión
              </button>
            ) : (
              <>
                <button
                  type="button"
                  onClick={onRetry}
                  className="w-full rounded-xl border-2 border-ink bg-quetzal px-4 py-2 font-heading text-sm font-bold text-ink transition-all hover:brightness-105 focus:outline-none focus:ring-4 focus:ring-quetzal active:translate-x-[4px] active:translate-y-[6px] active:shadow-none disabled:cursor-not-allowed disabled:opacity-60"
                >
                  Intentar de nuevo
                </button>
                <button
                  type="button"
                  onClick={onLogout}
                  className="w-full rounded-xl border-2 border-ink bg-white px-4 py-2 font-heading text-sm font-bold text-ink transition-all hover:brightness-95 focus:outline-none focus:ring-4 focus:ring-quetzal active:translate-x-[4px] active:translate-y-[6px] active:shadow-none disabled:cursor-not-allowed disabled:opacity-60"
                >
                  Cerrar sesión
                </button>
              </>
            )}
          </div>
        </div>
      </div>
    </KawaiiShell>
  );
}