# Plan de 8 semanas (56 días, dedicación completa)

Objetivo: portafolio de ingeniería de IA con evidencia medible, incluyendo identidad, niveles de acceso, seguridad y pagos. Reparto: Python para ingesta, evaluación y experimentos; C# para API, dominio, agentes, identidad y pagos. Ver CONTRATO.md y DISENO_ACCESO_PAGOS.md.

Ritmo diario: 4 h construir, 1 h aprender, 1 h medir, 30 min explicar (preguntas de entrevista), 30 min documentar (diario, ADR).

## Semana 1: datos y RAG (Python)

- Docker Compose con PostgreSQL + pgvector.
- Extracción local de los PDFs completos con PyMuPDF; caché en disco. Reutiliza el chunker estructural y el comando check de ingestion.py.
- Tabla de chunks: texto, ruta de títulos, página, embedding y columna tsvector; índice HNSW (vectores) e índice GIN (texto).
- Búsqueda híbrida con Reciprocal Rank Fusion en SQL.
- Golden set de 30 preguntas con la fuente esperada.
- Terminado cuando: informe de evaluación con hit@3 y MRR de cuatro variantes (solo vectorial, solo texto, híbrida, híbrida con ruta de títulos).

## Semana 2: backend C# y Bibliotecario

- Solución .NET: Domain, Application, Adapters, Api, Tests.
- Adaptador de ICurriculumRetriever con EF Core, Npgsql y Pgvector.
- Bibliotecario con function calling y respuestas con citas.
- ADR: Semantic Kernel vs. Microsoft Agent Framework (spike de 2 horas por cada uno).
- OpenTelemetry básico y pruebas xUnit.
- Terminado cuando: la API responde preguntas curriculares con citas, se niega a responder sin evidencia y las pruebas pasan.

## Semana 3: identidad, acceso y seguridad (C#)

- Contexto identity: Account, Family, FamilyMember, ChildProfile. ASP.NET Core Identity sobre PostgreSQL; registro, confirmación de correo (Mailpit), cierre de sesión, bloqueo por intentos fallidos, 2FA opcional.
- ADR: cookie vs. tokens; aislamiento con filtros de EF Core vs. también Row-Level Security.
- Roles Owner, Guardian, Child y Admin; políticas y autorización por recurso (FamilyId).
- Sesión delegada del niño y re-autenticación para acciones sensibles.
- Limitación de tasa en login, registro y endpoints de IA.
- Matriz endpoint × rol y pruebas IDOR automatizadas.
- Terminado cuando: ninguna petición de otra familia ni de un rol insuficiente tiene éxito, verificado en CI, y existe el informe de seguridad v1.

## Semana 4: Crítico, Axolotl y orquestador (C#)

- IMathValidator determinista (fracciones, operaciones básicas, unidades) con pruebas.
- Crítico con Paso A (números) y Paso B (estrategia contra un conjunto cerrado); EvaluationJSON.
- Flujo de aprobación parental y auditoría de cada evaluación.
- Axolotl con memoria; orquestador de la Demo con máximo 3 intentos, XP e insignias.
- Las herramientas de los agentes verifican autorización con el contexto del usuario.
- Terminado cuando: Demo de texto completa de punta a punta, los casos trampa (por ejemplo, 4x3=15) se detectan y un intento de usar una herramienta con permisos ajenos falla.

## Semana 5: pagos y derechos (C#)

- Contexto billing: Plan, Subscription, Entitlement, PaymentEvent. Puerto IPaymentGateway con adaptador Stripe en modo de prueba: Checkout, webhooks con firma e idempotencia, máquina de estados de la suscripción.
- Derechos por plan: número de niños, consultas de IA al día, voz, foto y exportación; cuota que corta antes de llamar al LLM.
- Plan mínimo: un plan gratuito y uno de pago mensual. Portal del cliente y segundo adaptador (Mercado Pago) solo si sobra tiempo.
- Pruebas: webhook con firma inválida, duplicado y fuera de orden; la redirección de pago por sí sola no activa nada.
- Terminado cuando: pagar con tarjeta de prueba activa funciones, cancelar las desactiva, un webhook falso es rechazado y la cuota de un plan frena el gasto de IA.

## Semana 6: voz, visión y UI mínima

- Transcripción con la API de OpenAI y con faster-whisper local; mide tasa de error, latencia y costo con 20 audios sintéticos.
- Síntesis de voz para Axolotl.
- Foto del proyecto con un modelo multimodal; defensa contra texto malicioso dentro de la imagen; moderación en cada turno.
- PWA mínima: registro e inicio de sesión, selección de perfil de niño, tablón, voz, cámara, resultado y pantalla de plan y pago.
- Terminado cuando: demo con voz y foto de punta a punta, con datos sintéticos, desde el registro hasta el resultado.

## Semana 7: calidad, observabilidad, seguridad y comparativos

- Evaluación en CI: subconjunto del golden set, casos del Crítico (falsos positivos y negativos), pruebas de inyección y pruebas de seguridad de la semana 3 y 5.
- Langfuse o Phoenix: trazas, tokens, costo por familia y por sesión, latencia p95.
- Caché de respuestas y alertas de gasto.
- Laboratorio Python: el Bibliotecario en LangGraph u OpenAI Agents SDK, comparado con la versión C#.
- Embeddings de OpenAI vs. un modelo local multilingüe: métricas de recuperación y costo.
- Terminado cuando: las tablas de resultados y de seguridad se reproducen con un solo comando.

## Semana 8: empaquetado y búsqueda de empleo

- README, diagramas (arquitectura, roles, flujo de la sesión del niño y del pago), ADRs, informes y un video de demo de 2 a 3 minutos con datos sintéticos.
- Dos artículos técnicos (por ejemplo, chunking; cómo evitar que el LLM califique mal; seguridad de agentes con autorización en las herramientas).
- CV, LinkedIn, historias STAR y práctica de preguntas técnicas.
- Empieza a postular desde la semana 6 con lo que ya tengas.

## Si hay que recortar

Recorta primero la UI, la Fase 2 y el segundo adaptador de pagos. Nunca recortes la evaluación, las métricas ni las pruebas de autorización.
