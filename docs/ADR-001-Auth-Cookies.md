# ADR-001: Autenticación por cookies (HttpOnly/Secure/SameSite) en lugar de JWT

- **Estado:** Aceptada
- **Fecha:** 2026-11-03
- **Contexto:** PWA + API bajo el mismo origen (HomeschoolAI / LUMI).

## Contexto

Necesitamos autenticar al adulto (Owner/Guardian) y emitir la sesión delegada del niño (Child, sin credenciales propias). Hay dos opciones principales:

1. **Tokens JWT** (access + refresh) gestionados por el cliente.
2. **Cookies de sesión** gestionadas por el servidor.

## Decisión

Usar **ASP.NET Core Identity + autenticación por cookies**:

- `HttpOnly = true`: el JavaScript del navegador no puede leer la cookie.
- `Secure` (sobre HTTPS) y `SameSite = Lax`.
- Cookie **encriptada y firmada** con Data Protection; contiene los claims `family_id`, `role` y `child_profile_id`.
- Los eventos de la cookie devuelven códigos HTTP en lugar de redirecciones HTML:
  - `OnRedirectToLogin` → **401**
  - `OnRedirectToAccessDenied` → **403**

## Justificación (mitigación de XSS)

- Un JWT guardado en `localStorage`/`sessionStorage` es **accesible por JavaScript**: si hay un XSS, el atacante puede robar el token y suplantar al usuario.
- Una cookie `HttpOnly` **no es accesible por JavaScript**, así que un XSS no puede robarla directamente (la envía el navegador automáticamente con cada petición).
- `SameSite=Lax` mitiga CSRF en navegaciones de nivel superior; para mutaciones se añadirá verificación anti-CSRF cuando la superficie lo requiera.

## Consecuencias

**Positivas**
- Menor superficie de robo de sesión por XSS.
- El cliente no gestiona tokens ni refresco: más simple y menos código propenso a errores.

**Negativas**
- Requiere HTTPS en producción (`Secure`).
- Hay que mantener `SameSite` y (para mutaciones) protección anti-CSRF.
- No es la opción ideal para clientes móviles de terceros (en ese caso se añadiría un endpoint de tokens separado).

## Alternativas rechazadas

- **JWT en el cliente**: mayor riesgo de XSS (token accesible por JS), requiere lógica de refresco y revocación más compleja.
