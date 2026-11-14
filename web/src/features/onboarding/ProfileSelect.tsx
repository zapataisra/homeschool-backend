import { useCallback, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import type { components } from "../../api/generated/schema";
import { apiClient } from "../../shared/api/apiClient";
import { useAuth } from "../../shared/contexts/AuthContext";
import { logger } from "../../shared/utils/logger";

type ChildProfile = components["schemas"]["ChildProfileDto"];

export default function ProfileSelect() {
  const navigate = useNavigate();
  const { logout } = useAuth();

  const [children, setChildren] = useState<ChildProfile[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [selectingId, setSelectingId] = useState<string | null>(null);

  const loadChildren = useCallback(async () => {
    setError(null);
    setChildren(null);
    try {
      const { response, data } = await apiClient.GET("/api/family/children");
      if (!response.ok) {
        setError("No pudimos cargar a tus exploradores. Intenta de nuevo.");
        logger.error(`No se pudieron cargar los exploradores (${response.status})`);
        return;
      }
      setChildren(data ?? []);
    } catch {
      setError("No pudimos conectar con el servidor.");
      logger.error("Error de red al cargar los exploradores");
    }
  }, []);

  useEffect(() => {
    void loadChildren();
  }, [loadChildren]);

  const handleSelect = async (child: ChildProfile) => {
    if (!child.id) return;
    setError(null);
    setSelectingId(child.id);
    try {
      const { response } = await apiClient.POST("/api/family/children/{childId}/session", {
        params: { path: { childId: child.id } },
      });
      if (!response.ok) {
        setError("No pudimos entrar con ese explorador. Intenta de nuevo.");
        logger.error(`Selección de explorador fallida (${response.status})`);
        setSelectingId(null);
        return;
      }
      // La cookie ya quedó actualizada con la sesión del niño (Cero IDOR).
      navigate("/chat");
    } catch {
      setError("No pudimos conectar con el servidor.");
      logger.error("Error de red al seleccionar explorador");
      setSelectingId(null);
    }
  };

  const handleLogout = async () => {
    await logout();
    navigate("/login");
  };

  return (
    <div className="min-h-screen w-full relative bg-white overflow-hidden flex flex-col">
      <img src="/assets/greca-top.svg" className="absolute top-0 inset-x-0 w-full h-8 md:h-12 lg:h-14 object-cover object-bottom z-0 pointer-events-none" aria-hidden="true" />
      <img src="/assets/greca-bottom.svg" className="absolute bottom-0 inset-x-0 w-full h-8 md:h-12 lg:h-14 object-cover object-top z-0 pointer-events-none" aria-hidden="true" />

      <header className="relative z-10 flex items-center justify-end gap-3 px-4 md:px-8 pt-12 md:pt-16">
        <button
          type="button"
          onClick={() => void handleLogout()}
          className="rounded-xl border-2 border-ink bg-arena px-3 py-2 font-heading text-xs md:text-sm font-bold text-ink transition-all hover:brightness-95 focus:outline-none focus:ring-4 focus:ring-quetzal active:translate-y-1"
        >
          Cerrar sesión
        </button>
      </header>

      <div className="relative z-10 flex flex-1 flex-col items-center justify-center w-full px-4 pb-24 pt-2">
        <img src="/assets/logo.svg" alt="Escuela en casa MX" className="w-48 md:w-60 mb-2 drop-shadow-md" />
        <img src="/assets/xolos-center.svg" alt="Xolos Guardianes" className="w-16 md:w-24 mb-6" />

        {children !== null && (
          <h1 className="mb-8 text-center font-heading text-3xl md:text-4xl font-bold text-ink">
            {children.length > 0
              ? "¡Elige a tu explorador para la aventura!"
              : "Todavía no tienes exploradores. ¡Vamos a crear el primero!"}
          </h1>
        )}

        <div className="flex items-center justify-center gap-4 md:gap-8 w-full max-w-3xl">
          <img
            src="/assets/Axolotl.svg"
            alt="Ajolote Guía"
            className="w-24 md:w-32 animate-breathe drop-shadow-sm shrink-0"
          />

          {children === null && !error ? (
            <div className="flex gap-6 md:gap-10 justify-center items-center z-10" aria-label="Cargando exploradores">
              {[0, 1, 2].map((i) => (
                <div key={i} className="flex flex-col items-center gap-3">
                  <div className="h-32 w-32 rounded-full bg-arena/50 border-4 border-ink/20 animate-pulse md:h-40 md:w-40" />
                  <div className="h-4 w-16 rounded-full bg-arena/50 animate-pulse" />
                </div>
              ))}
            </div>
          ) : error ? (
            <div className="z-10 flex flex-col items-center gap-4 text-center">
              <p className="font-body text-ink/80">{error}</p>
              <button type="button" onClick={() => void loadChildren()} className="btn-toy bg-quetzal">
                Intentar de nuevo
              </button>
            </div>
          ) : (
            <div className="flex flex-wrap gap-6 md:gap-10 justify-center items-center z-10">
              {children?.map((child, index) => (
                <Avatar
                  key={child.id ?? index}
                  name={child.name ?? "Explorador"}
                  grade={child.grade}
                  imgSrc={`/assets/${child.avatarUrl ?? "avatar-aguila.svg"}`}
                  disabled={selectingId !== null}
                  onClick={() => void handleSelect(child)}
                />
              ))}
              <Avatar
                name="Añadir Explorador"
                isAdd
                disabled={selectingId !== null}
                onClick={() => navigate("/add-explorer")}
              />
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

function Avatar({
  name,
  grade,
  imgSrc,
  isAdd = false,
  onClick,
  disabled = false,
}: {
  name: string;
  grade?: number;
  imgSrc?: string;
  isAdd?: boolean;
  onClick: () => void;
  disabled?: boolean;
}) {
  return (
    <div className="flex flex-col items-center gap-2">
      <button
        type="button"
        onClick={onClick}
        disabled={disabled}
        aria-label={name}
        className="flex h-32 w-32 items-center justify-center rounded-full bg-arena border-4 border-ink shadow-[4px_6px_0px_#611232] transition-all hover:-translate-y-2 hover:shadow-[6px_8px_0px_#611232] focus:outline-none focus:ring-4 focus:ring-quetzal active:translate-y-0 active:shadow-none disabled:cursor-not-allowed disabled:opacity-60 md:h-40 md:w-40"
      >
        {isAdd ? (
          <span className="font-heading text-5xl font-extrabold text-ink md:text-6xl">+</span>
        ) : (
          <img src={imgSrc} alt={name} className="w-full h-full object-contain p-3 md:p-4 pointer-events-none" />
        )}
      </button>
      <span className="font-heading text-sm font-bold text-ink">{name}</span>
      {grade !== undefined && grade !== null && (
        <span className="-mt-1 font-body text-xs text-ink/70">Grado {grade}</span>
      )}
    </div>
  );
}

