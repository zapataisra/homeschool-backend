import { useNavigate } from "react-router-dom";

export default function FamilyDashboard() {
  const navigate = useNavigate();

  return (
    <div className="min-h-screen w-full relative bg-white overflow-hidden flex flex-col items-center justify-center">
      <img src="/assets/greca-top.svg" className="absolute top-0 inset-x-0 w-full h-8 md:h-12 lg:h-14 object-cover object-bottom z-0 pointer-events-none" aria-hidden="true" />
      <img src="/assets/greca-bottom.svg" className="absolute bottom-0 inset-x-0 w-full h-8 md:h-12 lg:h-14 object-cover object-top z-0 pointer-events-none" aria-hidden="true" />

      <div className="relative z-10 flex flex-col items-center w-full px-4 py-10">
        <img src="/assets/xolos-center.svg" alt="Xolos Guardianes" className="w-16 md:w-20 mb-4" />
        <div className="w-[90%] max-w-3xl bg-arena border-4 border-ink rounded-[2rem] shadow-[6px_8px_0px_#611232] ring-4 ring-white p-6 md:p-8">
          {/* Encabezado */}
          <header className="flex items-center justify-between gap-4">
            <h1 className="font-heading text-2xl font-extrabold text-ink md:text-3xl">Panel Familiar</h1>
            <button
              type="button"
              onClick={() => navigate("/login")}
              className="rounded-xl border-2 border-ink bg-white px-4 py-2 font-heading text-sm font-bold text-ink transition-all hover:brightness-95 focus:outline-none focus:ring-4 focus:ring-quetzal active:translate-y-1"
            >
              Cerrar sesión
            </button>
          </header>

          {/* Grid de 2 columnas */}
          <div className="mt-6 grid gap-8 md:grid-cols-2">
            {/* Columna izquierda: Suscripción */}
            <section aria-label="Suscripción" className="rounded-xl border-2 border-ink bg-white p-4">
              <h2 className="font-heading text-lg font-extrabold text-ink">Plan Actual: Gratuito</h2>
              <p className="mt-1 font-body text-sm text-ink/70">20 interacciones restantes</p>
              <button
                type="button"
                className="bg-elote border-2 border-ink rounded-xl shadow-solid-sm hover:brightness-105 active:translate-y-1 active:shadow-none focus:outline-none focus:ring-4 focus:ring-quetzal font-bold py-2 px-4 w-full mt-4 transition-all"
              >
                Mejorar a Premium
              </button>
            </section>

            {/* Columna derecha: Exploradores */}
            <section aria-label="Exploradores" className="rounded-xl border-2 border-ink bg-white p-4">
              <h2 className="font-heading text-lg font-extrabold text-ink">Exploradores</h2>
              <ul className="mt-3 space-y-2">
                <li className="flex items-center gap-2 font-body text-ink">
                  <span className="flex h-8 w-8 items-center justify-center rounded-full bg-arena border-2 border-ink font-heading font-bold">M</span>
                  Mía · Grado 3
                </li>
                <li className="flex items-center gap-2 font-body text-ink">
                  <span className="flex h-8 w-8 items-center justify-center rounded-full bg-arena border-2 border-ink font-heading font-bold">C</span>
                  Cid · Grado 1
                </li>
              </ul>
              <button
                type="button"
                className="mt-4 w-full rounded-xl border-2 border-ink bg-transparent px-4 py-2 font-heading font-bold text-ink transition-all hover:bg-arena focus:outline-none focus:ring-4 focus:ring-quetzal active:translate-y-1"
              >
                Editar perfiles
              </button>
            </section>
          </div>
        </div>
      </div>
    </div>
  );
}
