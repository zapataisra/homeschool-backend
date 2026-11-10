# Reglas detalladas de frontend (React + Vite + TypeScript)

Se aplican a todo lo que esté en `web/`. Complementan `.clinerules` (arquitectura, reutilización, OWASP, logging). Si una regla choca con una petición, detente y pregunta. No inventes nombres de paquetes, APIs ni versiones: verifícalos en la documentación oficial.

## 1. Alcance y estructura
- **Stack:** React, Vite, TypeScript en modo estricto (`"strict": true`). Prohibido `any` sin justificación.
- **Estructura:** `web/src/app` (rutas), `web/src/features` (pantallas), `web/src/shared` (componentes comunes), `web/src/api` (cliente).
- Todo acceso a recursos gráficos debe apuntar a `/assets/*.svg` alojados en `web/public/assets/`.

- Todo el código del frontend vive en `web/`. La autorización real SIEMPRE es del servidor; ocultar un botón es solo cosmético.

- Organización por funcionalidad: `web/src/app` (arranque, rutas), `web/src/features/<nombre>` (pantallas y lógica), `web/src/shared` (componentes, hooks y utilidades comunes), `web/src/api` (cliente y tipos generados).

- Componentes pequeños con una sola responsabilidad. La lógica de acceso a la API y de grabación de audio vive en hooks o módulos, no dentro de los componentes visuales.
- Cada paquete npm nuevo se justifica antes de añadirlo. Versiones fijadas con el archivo de bloqueo versionado.

## 2. Contrato con la API

- Los tipos se generan desde el documento OpenAPI de la API con una herramienta de generación (verifica cuál es la vigente). El código generado vive en `web/src/api/generated/` y no se edita a mano. Debe existir un script npm para regenerarlo.
- Si la API aún no publica OpenAPI, detente y pídeme el cambio de backend; no copies los DTO a mano.
- Un único cliente de API (`web/src/api/client.ts`) usado por todas las funcionalidades: URL base, envío de cookies, cabecera anti-CSRF, interpretación de `ProblemDetails`, lectura del identificador de correlación y tiempos de espera.

## 3. Seguridad (OWASP)

- **Secretos:** ninguna clave en el cliente. Las variables `VITE_*` se incrustan en el paquete público: solo configuración no secreta. El navegador nunca llama a OpenAI ni a ningún proveedor de IA; todo pasa por nuestra API.
- **XSS:** prohibido `dangerouslySetInnerHTML` e `innerHTML`; la regla de lint `react/no-danger` debe estar activa como error. El texto del LLM y los fragmentos recuperados son datos no confiables y se renderizan como texto. Si algún día hace falta Markdown, se añade con un sanitizador reconocido y una justificación.
- **Sesión:** la sesión es una cookie HttpOnly. Jamás se guardan tokens ni identificadores de sesión en `localStorage` ni `sessionStorage`. Las peticiones que modifican datos envían el token anti-CSRF que defina el backend (si no existe, pídelo).
- **Mismo origen:** en desarrollo, Vite hace de proxy de `/api` hacia la API; en producción se sirve desde el mismo origen. No se configuran orígenes cruzados con credenciales.
- **CSP:** sin scripts ni estilos en línea; se prepara una política `Content-Security-Policy` estricta compatible con la compilación de Vite y el service worker, que se aplica en el servidor o proxy.
- **Navegación:** nada de redirecciones a URLs tomadas de parámetros; los enlaces externos llevan `rel="noopener noreferrer"`.
- **Entrada:** la validación del cliente es solo UX y repite los límites del servidor (mensaje no vacío y de 2000 caracteres como máximo; `threadId` alfanumérico y con guiones, máximo 64). El servidor sigue siendo la autoridad.
- **Dependencias:** `npm ci` en CI, `npm audit` antes de cerrar una tarea, sin paquetes abandonados y con atención a los scripts de instalación.
- **Consola y registros:** en producción no se escribe en consola texto de mensajes ni datos de la niña.

## 4. Privacidad y datos de la niña

- Sin analíticas, rastreadores, scripts de terceros ni fuentes cargadas desde servidores externos: todo se sirve desde nuestro propio origen.
- Micrófono y cámara: el permiso se pide solo tras una acción explícita de la persona, con un indicador visible de grabación, y las pistas se detienen al terminar. El audio crudo vive en memoria y se envía solo cuando la persona lo decide; nunca se guarda en el navegador.
- Nada de datos personales en `localStorage`, `sessionStorage` ni IndexedDB. Al cerrar sesión se limpian todos los datos locales y las cachés del service worker.

## 5. PWA y service worker

- Plugin de PWA para Vite (verifica el vigente y su compatibilidad con la versión de Vite).
- Manifiesto con nombre, iconos, colores y `lang` en español de México; `display: standalone`.
- El service worker solo precachea el cascarón estático (HTML, JS, CSS, fuentes e imágenes propias). NO cachea rutas `/api`, ni respuestas con cookies de sesión, ni contenido generado para la niña.
- Instalación y service worker exigen contexto seguro: `localhost` sirve en tu máquina; para probar en otro dispositivo se necesita HTTPS local.
- Las actualizaciones se avisan con claridad y no interrumpen una actividad en curso.

## 6. Producto y experiencia

"Mesoamérica Kawaii" (REGLA ESTRICTA)
La interfaz usa un diseño "Sticker / Juguete" inspirado en Chiikawa y la cultura azteca.
- **Paleta de Colores:** 
  - Fondo: Arena/crema (`#FFF9E6` o `#F5EEDB`).
  - Ink (Trazos/Textos): Marrón oscuro cálido (`#3E2723`). **NUNCA usar negro puro**.
  - Acentos: Verde Quetzal (`emerald-400`), Rosa Ajolote (`rose-300`), Azul Tláloc (`sky-400`), Amarillo Elote (`amber-300`).
- **Anatomía de Componentes (Efecto Sticker):** Todos los contenedores principales (tarjetas, modales) deben llevar esta combinación estricta de Tailwind:
  `bg-[#F5EEDB] border-4 border-[#3E2723] rounded-[2rem] shadow-[6px_8px_0px_#3E2723] ring-8 ring-white`
- **Botones y Táctilidad:** Al estado `:active`, el botón debe trasladarse hacia abajo (`translate-y-1` o `2`) y reducir su `shadow` para simular que un bloque de gomita es presionado físicamente.
- **Responsividad de Marcos:** Los marcos decorativos (grecas) usan posición absoluta (`absolute inset-0`). En dispositivos móviles (`max-md`), las grecas laterales deben ocultarse (`hidden md:block`) o comprimirse para no ahogar el formulario central.
- **Fuentes:** Redondeadas y amigables.



## 7. Reutilización

- Un cliente de API, un manejo de errores, un hook de grabación de audio y un conjunto de componentes base compartidos. Antes de crear algo, busca si ya existe.
- Colores, espaciados y tipografía salen de las variables de diseño; nada de valores repetidos en cada componente.

## 8. Pruebas y definición de terminado

Una tarea de frontend está terminada solo si:
1. `tsc --noEmit` sin errores y lint sin advertencias (con `react/no-danger` como error).
2. Las pruebas pasan (Vitest y Testing Library). Obligatorias: una que verifica que un texto malicioso del LLM (por ejemplo, una etiqueta `img` con `onerror`) se muestra como texto y no se ejecuta, y otra del cliente de API con respuestas `ProblemDetails`.
3. `npm run build` correcto.
4. `npm audit` revisado: sin vulnerabilidades altas o críticas sin justificar.
5. Se comprueba manualmente que el service worker no cachea `/api` y que no hay secretos en el paquete compilado.
6. Resumen final: qué cambió, qué reutilizaste, paquetes añadidos con su justificación y qué queda pendiente.

## 9. Prohibiciones

- No tocar backend, `app/adapters/`, `vectordb/` ni `data/` sin pedirlo.
- No versionar `node_modules/`, `dist/` ni archivos `.env` locales.
- No desactivar reglas de lint ni de TypeScript sin una justificación escrita.
- No instalar software en el sistema (por ejemplo, Node) sin preguntarme.

