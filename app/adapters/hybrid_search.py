import os
import time
import psycopg
from dotenv import load_dotenv
from sentence_transformers import SentenceTransformer

load_dotenv()
DB_URL = f"postgresql://{os.getenv('POSTGRES_USER')}:{os.getenv('POSTGRES_PASSWORD')}@{os.getenv('POSTGRES_HOST', 'localhost')}:{os.getenv('POSTGRES_PORT', '5432')}/{os.getenv('POSTGRES_DB')}"

def hybrid_search(user_query, limit=5):
    print("Cargando modelo de embeddings...")
    model = SentenceTransformer('all-MiniLM-L6-v2')
    
    # Excluir el tiempo de inferencia del LLM para medir solo PostgreSQL
    query_vector = model.encode(user_query).tolist()

    vector_sql = """
        SELECT chunk_id, texto FROM embeddings_sep 
        ORDER BY embedding <=> %s::vector LIMIT %s;
    """
    
    hybrid_sql = """
        WITH semantic_search AS (
            SELECT id, chunk_id, texto, RANK() OVER (ORDER BY embedding <=> %s::vector) AS rank
            FROM embeddings_sep ORDER BY embedding <=> %s::vector LIMIT 10
        ),
        keyword_search AS (
            SELECT id, chunk_id, texto, RANK() OVER (ORDER BY ts_rank(fts, plainto_tsquery('spanish', %s)) DESC) AS rank
            FROM embeddings_sep WHERE fts @@ plainto_tsquery('spanish', %s) LIMIT 10
        )
        SELECT COALESCE(s.chunk_id, k.chunk_id) as chunk_id, COALESCE(s.texto, k.texto) as texto
        FROM semantic_search s
        FULL OUTER JOIN keyword_search k ON s.id = k.id
        ORDER BY (COALESCE(1.0 / (60 + s.rank), 0.0) + COALESCE(1.0 / (60 + k.rank), 0.0)) DESC
        LIMIT %s;
    """

    with psycopg.connect(DB_URL) as conn:
        with conn.cursor() as cur:
            # 1. Medir Búsqueda Vectorial
            start_vec = time.perf_counter()
            cur.execute(vector_sql, (str(query_vector), limit))
            cur.fetchall()
            vec_time = (time.perf_counter() - start_vec) * 1000

            # 2. Medir Búsqueda Híbrida (RRF)
            start_hyb = time.perf_counter()
            cur.execute(hybrid_sql, (str(query_vector), str(query_vector), user_query, user_query, limit))
            results_hyb = cur.fetchall()
            hyb_time = (time.perf_counter() - start_hyb) * 1000

    print(f"\n✅ Pregunta: '{user_query}'")
    print(f"⏱️ Latencia puramente vectorial: {vec_time:.2f} ms")
    print(f"⏱️ Latencia híbrida (RRF): {hyb_time:.2f} ms\n")
    
    for i, row in enumerate(results_hyb, 1):
        print(f"[{i}] Chunk {row[0]}: {row[1][:120]}...")

if __name__ == "__main__":
    # Prueba tu sistema RAG con una pregunta específica del dominio curricular
    pregunta = "¿Cómo se aborda el pensamiento crítico en el aula?"
    hybrid_search(pregunta)