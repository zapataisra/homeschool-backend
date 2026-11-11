import { useNavigate } from "react-router-dom";

export default function ProfileSelect() {
  const navigate = useNavigate();

  return (
    <div className="min-h-screen w-full relative bg-white overflow-hidden flex flex-col items-center justify-center">
      <img src="/assets/greca-top.svg" className="absolute top-0 inset-x-0 w-full h-8 md:h-12 lg:h-14 object-cover object-bottom z-0 pointer-events-none" aria-hidden="true" />
      <img src="/assets/greca-bottom.svg" className="absolute bottom-0 inset-x-0 w-full h-8 md:h-12 lg:h-14 object-cover object-top z-0 pointer-events-none" aria-hidden="true" />

      <div className="relative z-10 flex flex-col items-center w-full px-4 mt-2">
        {/* Branding idéntico al Login */}
        <img src="/assets/logo.svg" alt="Escuela en casa MX" className="w-48 md:w-56 mb-2 drop-shadow-md" />
        <img src="/assets/xolos-center.svg" alt="Xolos Guardianes" className="w-20 md:w-28 mb-8" />

        <h1 className="mb-10 text-center font-heading text-2xl md:text-3xl font-extrabold text-ink">
          ¡Hola! ¿Quién eres?
        </h1>

        <div className="relative flex justify-center w-full max-w-3xl">
          {/* Ajolote Guía Flotante */}
          <div className="absolute z-20 -top-16 md:-top-20 left-2 md:left-12 pointer-events-none">
            <img
              src="/assets/Axolotl.svg"
              className="w-24 md:w-32 animate-breathe drop-shadow-sm transition-transform hover:scale-105 duration-300"
              alt="Ajolote Guía"
            />
          </div>

          {/* Grid de Avatares */}
          <div className="flex gap-6 md:gap-10 justify-center items-center z-10">
            <Avatar name="Mía" imgSrc="/assets/avatar-aguila.svg" onClick={() => navigate("/family-dashboard")} />
            <Avatar name="Cid" imgSrc="/assets/avatar-jaguar.svg" onClick={() => navigate("/family-dashboard")} />
            <Avatar name="Añadir Explorador" isAdd onClick={() => navigate("/family-dashboard")} />
          </div>
        </div>
      </div>
    </div>
  );
}

function Avatar({ name, imgSrc, isAdd = false, onClick }: { name: string; imgSrc?: string; isAdd?: boolean; onClick: () => void }) {
  return (
    <div className="flex flex-col items-center gap-3">
      <button
        type="button"
        onClick={onClick}
        aria-label={name}
        className="flex h-28 w-28 items-center justify-center rounded-full bg-arena border-4 border-ink shadow-[4px_6px_0px_#611232] transition-all hover:-translate-y-2 hover:shadow-[6px_8px_0px_#611232] focus:outline-none focus:ring-4 focus:ring-quetzal active:translate-y-0 active:shadow-none md:h-40 md:w-40"
      >
        {isAdd ? (
          <span className="font-heading text-4xl font-extrabold text-ink md:text-5xl">+</span>
        ) : (
          <img src={imgSrc} alt={name} className="w-full h-full object-contain p-2 md:p-3 pointer-events-none" />
        )}
      </button>
      <span className="font-heading text-sm font-bold text-ink">{name}</span>
    </div>
  );
}

