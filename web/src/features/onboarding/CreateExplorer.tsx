import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { apiClient } from "../../shared/api/apiClient";
import { logger } from "../../shared/utils/logger";

const AVATARS = [
  { file: "avatar-aguila.svg", src: "/assets/avatar-aguila.svg", label: "Águila" },
  { file: "avatar-jaguar.svg", src: "/assets/avatar-jaguar.svg", label: "Jaguar" },
  { file: "avatar-serpiente.svg", src: "/assets/avatar-serpiente.svg", label: "Serpiente" },
  { file: "avatar-lluvia.svg", src: "/assets/avatar-lluvia.svg", label: "Lluvia" },
];

const GRADES = [1, 2, 3, 4, 5, 6];

export default function CreateExplorer() {
  const navigate = useNavigate();
  const [name, setName] = useState("");
  const [grade, setGrade] = useState(1);
  const [avatar, setAvatar] = useState(AVATARS[0].file);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const trimmed = name.trim();
    if (trimmed.length === 0 || saving) return;

    setError(null);
    setSaving(true);
    try {
      // El backend crea el perfil con nombre y grado; el avatar es solo visual.
      const { response } = await apiClient.POST("/api/family/children", {
        body: { name: trimmed, grade, avatarUrl: avatar },
      });
      if (!response.ok) {
        setError("No pudimos crear a tu explorador. Intenta de nuevo.");
        logger.error(`Creación de explorador fallida (${response.status})`);
        setSaving(false);
        return;
      }
      navigate("/profile-select");
    } catch {
      setError("No pudimos conectar con el servidor.");
      logger.error("Error de red al crear explorador");
      setSaving(false);
    }
  };

  return (
    <div className="min-h-screen w-full relative bg-white overflow-hidden flex flex-col">
      <img src="/assets/greca-top.svg" className="absolute top-0 inset-x-0 w-full h-8 md:h-12 lg:h-14 object-cover object-bottom z-0 pointer-events-none" aria-hidden="true" />
      <img src="/assets/greca-bottom.svg" className="absolute bottom-0 inset-x-0 w-full h-8 md:h-12 lg:h-14 object-cover object-top z-0 pointer-events-none" aria-hidden="true" />

      <div className="relative z-10 flex flex-1 flex-col items-center justify-center w-full px-4 py-8">
        <div className="w-[90%] max-w-xl rounded-[2rem] border-4 border-ink bg-arena shadow-[6px_8px_0px_#611232] ring-8 ring-white p-6 md:p-8">
          <h1 className="mb-6 text-center font-heading text-3xl md:text-4xl font-bold text-ink">
            ¡Arma a tu explorador!
          </h1>

          <form onSubmit={handleSubmit} className="flex flex-col gap-5">
            <fieldset>
              <legend className="mb-2 block font-heading text-sm font-bold text-ink">Elige su avatar</legend>
              <div className="flex flex-wrap justify-center gap-3">
                {AVATARS.map((a) => (
                  <button
                    key={a.file}
                    type="button"
                    onClick={() => setAvatar(a.file)}
                    aria-label={a.label}
                    aria-pressed={avatar === a.file}
                    className={`flex h-20 w-20 items-center justify-center rounded-full bg-white border-4 transition-all focus:outline-none focus:ring-4 focus:ring-quetzal active:translate-y-1 ${
                      avatar === a.file ? "border-quetzal ring-4 ring-quetzal" : "border-ink"
                    }`}
                  >
                    <img src={a.src} alt={a.label} className="w-full h-full object-contain p-2 pointer-events-none" />
                  </button>
                ))}
              </div>
            </fieldset>

            <div>
              <label htmlFor="name" className="mb-1 block font-heading text-sm font-bold text-ink">
                Nombre del explorador
              </label>
              <input
                id="name"
                name="name"
                type="text"
                required
                maxLength={100}
                value={name}
                onChange={(event) => setName(event.target.value)}
                placeholder="Ej. Mía"
                className="w-full rounded-xl border-2 border-ink bg-white px-3 py-2 font-body text-ink placeholder-ink/50 outline-none focus:ring-4 focus:ring-quetzal"
              />
            </div>

            <fieldset>
              <legend className="mb-2 block font-heading text-sm font-bold text-ink">Grado escolar</legend>
              <div className="flex flex-wrap justify-center gap-2">
                {GRADES.map((g) => (
                  <button
                    key={g}
                    type="button"
                    onClick={() => setGrade(g)}
                    aria-pressed={grade === g}
                    className={`h-10 w-10 rounded-full border-2 font-heading font-bold transition-all focus:outline-none focus:ring-4 focus:ring-quetzal active:translate-y-1 ${
                      grade === g ? "bg-quetzal border-ink" : "bg-white border-ink"
                    }`}
                  >
                    {g}
                  </button>
                ))}
              </div>
            </fieldset>

            {error && <p className="text-center font-body text-sm text-ink/80">{error}</p>}

            <button
              type="submit"
              disabled={saving || name.trim().length === 0}
              className="mt-1 w-full rounded-2xl border-4 border-ink bg-quetzal px-6 py-3 font-heading text-lg font-extrabold text-ink shadow-[4px_6px_0px_#611232] transition-all duration-100 hover:brightness-105 active:translate-x-[4px] active:translate-y-[6px] active:shadow-none disabled:cursor-not-allowed disabled:opacity-50"
            >
              {saving ? "Creando..." : "Crear Explorador"}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}
