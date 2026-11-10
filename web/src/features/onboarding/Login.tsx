import { useState, type FormEvent, type ReactNode } from "react";

type AuthMode = "login" | "register";

export default function Login() {
  const [mode, setMode] = useState<AuthMode>("login");
  const isLogin = mode === "login";

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    // La conexión con POST /api/auth/login|register llega en una tarea posterior.
    event.preventDefault();
  };

  return (
    <div className="min-h-screen w-full relative bg-white overflow-hidden flex flex-col items-center justify-center">
      {/* Marco superior e inferior: grecas absolutas */}
      <img src="/assets/greca-top.svg" className="absolute top-0 inset-x-0 w-full h-8 md:h-12 lg:h-14 object-cover object-bottom z-0" alt="Marco superior" />
      <img src="/assets/greca-bottom.svg" className="absolute bottom-0 inset-x-0 w-full h-8 md:h-12 lg:h-14 object-cover object-top z-0" alt="Marco inferior" />

      {/* Contenedor central: logo + xolos + formulario */}
      <div className="relative z-10 flex flex-col items-center w-full px-4 mt-2">
        <img
          src="/assets/logo.svg"
          alt="Escuela en casa MX"
          className="w-56 md:w-72 lg:w-[320px] mb-2 drop-shadow-md"
        />

        {/* Xolos guardianes */}
        <img src="/assets/xolos-center.svg" alt="Xolos Guardianes" className="w-24 md:w-32 mb-6" />

        {/* Formulario y ajolote MÁS COMPACTO */}
        <div className="relative w-[90%] max-w-[300px] z-10">
         {/* Ajolote Flotante */}
          <div className="absolute z-20 -top-12 left-1/2 -translate-x-1/2 md:top-1/2 md:left-0 md:-translate-x-[85%] md:-translate-y-1/2 pointer-events-none">
            <img
              src="/assets/Axolotl.svg"
              className="w-28 md:w-32 lg:w-36 animate-breathe drop-shadow-sm pointer-events-none transition-transform hover:scale-105 duration-300"
              alt="Ajolote"
            />
          </div>

          {/* Tarjeta sticker */}
          <div className="bg-[#F5EEDB] border-4 border-[#3E2723] rounded-[1.5rem] shadow-[4px_6px_0px_#3E2723] ring-4 ring-white p-4">
            <div role="tablist" aria-label="Modo de acceso" className="mb-3 flex rounded-full border-2 border-[#3E2723] bg-white p-1">
              <AuthTab active={isLogin} onClick={() => setMode("login")}>Entrar</AuthTab>
              <AuthTab active={!isLogin} onClick={() => setMode("register")}>Crear</AuthTab>
            </div>

            <h1 className="mb-0.5 text-center font-heading text-lg font-extrabold text-[#3E2723]">
              {isLogin ? "¡Hola de nuevo!" : "¡Crea tu cuenta!"}
            </h1>
            <p className="mb-3 text-center font-body text-[10px] leading-tight text-[#3E2723]/70">
              {isLogin ? "Nos alegra verte de nuevo." : "Únete a la aventura LUMI."}
            </p>

            <form onSubmit={handleSubmit}>
              <div className="mb-2">
                <label htmlFor="email" className="mb-0.5 block font-heading text-xs font-bold text-[#3E2723]">Correo</label>
                <input
                  id="email"
                  name="email"
                  type="email"
                  required
                  autoComplete="email"
                  placeholder="correo@ejemplo.com"
                  className="w-full rounded-xl border-2 border-[#3E2723] bg-white px-3 py-1.5 font-body text-sm text-[#3E2723] placeholder-[#3E2723]/50 outline-none focus:ring-4 focus:ring-quetzal"
                />
              </div>

              <div className="mb-3">
                <label htmlFor="password" className="mb-0.5 block font-heading text-xs font-bold text-[#3E2723]">Contraseña</label>
                <input
                  id="password"
                  name="password"
                  type="password"
                  required
                  autoComplete={isLogin ? "current-password" : "new-password"}
                  placeholder="••••••••"
                  className="w-full rounded-xl border-2 border-[#3E2723] bg-white px-3 py-1.5 font-body text-sm text-[#3E2723] placeholder-[#3E2723]/50 outline-none focus:ring-4 focus:ring-quetzal"
                />
              </div>

              <button
                type="submit"
                className="mt-1 w-full rounded-xl border-2 border-[#3E2723] bg-quetzal px-4 py-1.5 font-heading text-sm font-extrabold text-[#3E2723] shadow-[4px_6px_0px_#3E2723] transition-all duration-100 hover:brightness-105 active:translate-x-[4px] active:translate-y-[6px] active:shadow-none"
              >
                {isLogin ? "Entrar" : "Crear cuenta"}
              </button>
            </form>
          </div>
        </div>
      </div>
    </div>
  );
}

function AuthTab({ active, onClick, children }: { active: boolean; onClick: () => void; children: ReactNode }) {
  return (
    <button
      type="button"
      role="tab"
      aria-selected={active}
      onClick={onClick}
      className={`flex-1 rounded-full px-2 py-1 font-heading text-xs font-bold text-[#3E2723] transition-colors ${active ? "bg-quetzal border border-[#3E2723]" : "hover:bg-[#F5EEDB]"}`}
    >
      {children}
    </button>
  );
}