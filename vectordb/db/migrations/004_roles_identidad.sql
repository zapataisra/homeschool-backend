-- 004_roles_identidad.sql
-- Rol de EJECUCIÓN de la API (runtime), separado del rol de migración (admin).
--   - Puede leer y escribir FILAS del esquema app_identity (incluidas las tablas
--     que creen las migraciones futuras).
--   - NO puede crear ni borrar tablas (no tiene DDL).
--   - NO ve embeddings_sep (esquema public).
-- La contraseña se asigna aparte:  \password lumi_identity_rw
--   (o ALTER ROLE lumi_identity_rw PASSWORD '...').

-- 1) Crear el rol de login (idempotente).
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'lumi_identity_rw') THEN
        CREATE ROLE lumi_identity_rw LOGIN;
    END IF;
END
$$;

-- 2) Uso del esquema app_identity.
GRANT USAGE ON SCHEMA app_identity TO lumi_identity_rw;

-- 3) DML sobre las tablas existentes del esquema.
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA app_identity TO lumi_identity_rw;

-- 4) Secuencias (identidades autoincrementales de ASP.NET Core Identity).
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA app_identity TO lumi_identity_rw;

-- 5) Privilegios por defecto para las tablas que cree el rol de migración (admin).
--    Si migras con un rol distinto a admin, cambia "admin" aquí.
ALTER DEFAULT PRIVILEGES FOR ROLE admin IN SCHEMA app_identity
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO lumi_identity_rw;
ALTER DEFAULT PRIVILEGES FOR ROLE admin IN SCHEMA app_identity
    GRANT USAGE, SELECT ON SEQUENCES TO lumi_identity_rw;

-- 6) Aislamiento: sin acceso al esquema public (donde vive embeddings_sep).
REVOKE ALL ON SCHEMA public FROM lumi_identity_rw;
REVOKE ALL ON ALL TABLES IN SCHEMA public FROM lumi_identity_rw;
