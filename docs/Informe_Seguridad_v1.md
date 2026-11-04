# Informe de Seguridad v1

- **Fecha:** 2026-11-03
- **Alcance:** MVP (Semana 3) — autorización por rol, aislamiento multi-tenant por `FamilyId` y mínimos privilegios del rol PostgreSQL.

## 1. Aislamiento multi-tenant (FamilyId)

- Cada familia es un tenant. Todo dato de negocio se filtra por el `FamilyId` del usuario autenticado, obtenido de los claims de la cookie (nunca de la petición).
- Los endpoints derivan el `FamilyId` del contexto del usuario (`UserContext`), no de parámetros del cliente. Por eso, aunque el cliente envíe un `childId` o `threadId` de otra familia, la consulta se limita a su propia familia.

## 2. Matriz endpoint × rol (resultado esperado)

| Endpoint | Anónimo | Owner | Guardian | Child |
|---|---|---|---|---|
| `GET /api/family` | 401 | 200 | 200 | 403 |
| `GET /api/family/children` | 401 | 200 | 200 | 403 |
| `POST /api/family/children` | 401 | 200 | 200 | 403 |
| `POST /api/family/children/{id}/session` | 401 | 200 | 200 | 403 |
| `GET /api/family/me` | 401 | 403 | 403 | 200 |
| `GET /api/family/billing` | 401 | 200 | 403 | 403 |
| `POST /api/agent/chat` | 401 | 200 | 200 | 200 |

## 3. Pruebas IDOR (acceso horizontal entre familias)

- Familia B intenta iniciar la sesión delegada del hijo de la familia A → **404** (`ChildProfile` filtrado por `FamilyId`).
- El historial de conversación del agente se guarda por `(FamilyMemberId, threadId)`: si B adivina el `threadId` de A, no ve ni continúa su conversación (se crea un hilo nuevo para B).

Cobertura automatizada: `IdentityIntegrationTests` (matriz + IDOR), `ChatServiceTests` y `ConversationStoreTests` (aislamiento de conversaciones por usuario).

## 4. Mínimos privilegios en PostgreSQL

| Rol | Esquema | Privilegios |
|---|---|---|
| `app_readonly` | `public` (`embeddings_sep`) | Solo `SELECT` (RAG de solo lectura) |
| `lumi_identity_rw` | `app_identity` | `SELECT/INSERT/UPDATE/DELETE` sobre tablas y secuencias; `ALTER DEFAULT PRIVILEGES` para tablas de migraciones futuras |
| `admin` | (solo migraciones) | DDL manual vía `dotnet ef database update`; nunca lo usa la API en runtime |

Verificado en PostgreSQL real:

- `lumi_identity_rw` puede leer/escribir en `app_identity` (0 errores en `SELECT`).
- `lumi_identity_rw` **no** puede crear tablas (`permission denied for schema app_identity`).
- `lumi_identity_rw` **no** puede leer `embeddings_sep` (`permission denied for table embeddings_sep`).

## 5. Otros controles

- Cookies `HttpOnly/Secure/SameSite`; sesión del niño con alcance limitado (rol `Child`).
- Rate limiting nativo de ASP.NET Core: `auth` (10 req/min en login/registro) y `agent` (20 req/min).
- Errores de dominio mapeados a `ProblemDetails` (401/403/404/409/400) sin trazas de pila.

## 6. Estado

- Build sin advertencias.
- **20/20 pruebas en verde** (matriz endpoint × rol, IDOR, aislamiento de conversaciones por usuario).
