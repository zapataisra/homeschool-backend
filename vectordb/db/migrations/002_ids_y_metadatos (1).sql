-- 002: identificadores únicos y metadatos para trazabilidad.
-- ANTES de aplicar, comprueba que NO hay duplicados reales (mismo documento y mismo chunk):
--   SELECT source, chunk_id, COUNT(*) FROM embeddings_sep GROUP BY source, chunk_id HAVING COUNT(*) > 1;
-- (Agrupar solo por chunk_id da falsos positivos: chunk_id se repite entre documentos a propósito.)

ALTER TABLE embeddings_sep ADD COLUMN IF NOT EXISTS embedding_model text;
ALTER TABLE embeddings_sep ADD COLUMN IF NOT EXISTS page integer;

UPDATE embeddings_sep SET embedding_model = 'all-MiniLM-L6-v2' WHERE embedding_model IS NULL;

DO $$
BEGIN
    ALTER TABLE embeddings_sep
        ADD CONSTRAINT embeddings_sep_source_chunk_uq UNIQUE (source, chunk_id);
EXCEPTION WHEN duplicate_table OR duplicate_object THEN
    NULL;  -- la restricción ya existía
END $$;
