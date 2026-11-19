import os
import json
import time
import psycopg
from dotenv import load_dotenv
from sentence_transformers import SentenceTransformer

load_dotenv()

DB_USER = os.getenv("POSTGRES_USER")
DB_PASS = os.getenv("POSTGRES_PASSWORD")
DB_NAME = os.getenv("POSTGRES_DB")
DB_HOST = os.getenv("POSTGRES_HOST", "localhost")
DB_PORT = os.getenv("POSTGRES_PORT", "5432")

DB_URL = f"postgresql://{DB_USER}:{DB_PASS}@{DB_HOST}:{DB_PORT}/{DB_NAME}"
CHUNKS_DIR = "data/chunks"

def embed_and_store():
    print("Cargando modelo local all-MiniLM-L6-v2...")
    model = SentenceTransformer('all-MiniLM-L6-v2')
    
    total_chunks = 0
    total_embed_time = 0.0
    total_db_time = 0.0

    with psycopg.connect(DB_URL) as conn:
        with conn.cursor() as cur:
            cur.execute("""
                CREATE TABLE IF NOT EXISTS embeddings_sep (
                    id serial PRIMARY KEY,
                    source text,
                    chunk_id integer,
                    texto text,
                    embedding vector(384)
                );
            """)
            
            # Limpiar datos previos para medir un escenario desde cero
            cur.execute("TRUNCATE embeddings_sep;")
            
            for filename in os.listdir(CHUNKS_DIR):
                if not filename.endswith('.jsonl'):
                    continue
                    
                filepath = os.path.join(CHUNKS_DIR, filename)
                print(f"Procesando: {filename}")
                
                with open(filepath, 'r', encoding='utf-8') as f:
                    for line in f:
                        data = json.loads(line)
                        texto = data['text']
                        
                        # Métrica de latencia de cómputo local
                        start_embed = time.perf_counter()
                        vector = model.encode(texto).tolist()
                        total_embed_time += (time.perf_counter() - start_embed)
                        
                        # Métrica de latencia de inserción
                        start_db = time.perf_counter()
                        cur.execute("""
                            INSERT INTO embeddings_sep (source, chunk_id, texto, embedding)
                            VALUES (%s, %s, %s, %s)
                        """, (data['source'], data['chunk_id'], texto, str(vector)))
                        total_db_time += (time.perf_counter() - start_db)
                        
                        total_chunks += 1
                        
            conn.commit()
            
    print("\n✅ Proceso de vectorización completado.")
    print(f"📊 Número total de chunks procesados: {total_chunks}")
    print(f"⏱️ Tiempo total de embedding (Cómputo local): {total_embed_time:.2f} segundos")
    print(f"💾 Tiempo total de inserción (pgvector): {total_db_time:.2f} segundos")

if __name__ == "__main__":
    embed_and_store()