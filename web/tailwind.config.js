/** @type {import('tailwindcss').Config} */
// Design System "Mesoamérica Kawaii" (estilo Chiikawa Azteca).
// Única fuente de verdad para colores, tipografía, sombras y animaciones.
export default {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        // Fondo arena/crema cálido.
        arena: "#F5EEDB",
        // Ink: marrón oscuro cálido para textos y trazos. NUNCA negro puro.
        ink: "#3E2723",
        // Verde Quetzal / Jade (emerald-400).
        quetzal: "#34d399",
        // Rosa Ajolote (rose-300).
        ajolote: "#fda4af",
        // Azul Tláloc (sky-400).
        tlaloc: "#38bdf8",
        // Amarillo Elote (amber-300).
        elote: "#fcd34d",
      },
      fontFamily: {
        // Redondeadas y gruesas para títulos.
        heading: ["Nunito", "Balsamiq Sans", "ui-rounded", "system-ui", "sans-serif"],
        // Geométricas suaves para el cuerpo.
        body: ["Varela Round", "ui-rounded", "system-ui", "sans-serif"],
      },
      borderRadius: {
        blob: "2rem",
      },
      boxShadow: {
        // Sombra "Solid Offset": plana, dura y desplazada abajo-derecha (FRONTEND.md).
        solid: "6px 8px 0px #611232",
        "solid-sm": "2px 3px 0px #611232",
      },
      keyframes: {
        // "Breathing/floating": traslación suave en Y en bucle de 3.5s.
        breathe: {
          "0%, 100%": { transform: "translateY(0)" },
          "50%": { transform: "translateY(-6px)" },
        },
        // Aparición con rebote (spring/bounce).
        "bounce-in": {
          "0%": { transform: "scale(0.6)", opacity: "0" },
          "60%": { transform: "scale(1.06)", opacity: "1" },
          "80%": { transform: "scale(0.96)" },
          "100%": { transform: "scale(1)", opacity: "1" },
        },
        // Deslizamiento horizontal suave del carrusel de láminas.
        "slide-in-right": {
          "0%": { transform: "translateX(24px)", opacity: "0" },
          "100%": { transform: "translateX(0)", opacity: "1" },
        },
        "slide-in-left": {
          "0%": { transform: "translateX(-24px)", opacity: "0" },
          "100%": { transform: "translateX(0)", opacity: "1" },
        },
        // Barras de sonido del indicador de voz de Axolotl.
        soundbar: {
          "0%, 100%": { transform: "scaleY(0.3)" },
          "50%": { transform: "scaleY(1)" },
        },
      },
      animation: {
        breathe: "breathe 3.5s ease-in-out infinite",
        "bounce-in": "bounce-in 0.4s ease-out",
        "slide-in-right": "slide-in-right 0.3s ease-in-out",
        "slide-in-left": "slide-in-left 0.3s ease-in-out",
        soundbar: "soundbar 0.9s ease-in-out infinite",
      },
    },
  },
  plugins: [],
};
