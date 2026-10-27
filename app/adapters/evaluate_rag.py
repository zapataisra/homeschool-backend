import os
import json
import psycopg
from dotenv import load_dotenv
from sentence_transformers import SentenceTransformer

load_dotenv()
DB_URL = f"postgresql://{os.getenv('POSTGRES_USER')}:{os.getenv('POSTGRES_PASSWORD')}@{os.getenv('POSTGRES_HOST', 'localhost')}:{os.getenv('POSTGRES_PORT', '5432')}/{os.getenv('POSTGRES_DB')}"

def evaluate_golden_set(json_path="data/evaluation/golden_set.json"):
    print("Cargando modelo local y Golden Set...")
    model = SentenceTransformer('all-MiniLM-L6-v2')
    
    with open(json_path, 'r', encoding='utf-8') as f:
        golden_set = json.load(f)

    sql_query = """
    WITH semantic_search AS (
        SELECT chunk_id, RANK() OVER (ORDER BY embedding <=> %s::vector) AS rank
        FROM embeddings_sep ORDER BY embedding <=> %s::vector LIMIT 10
    ),
    keyword_search AS (
        SELECT chunk_id, RANK() OVER (ORDER BY ts_rank(fts, websearch_to_tsquery('spanish', %s)) DESC) AS rank
        FROM embeddings_sep WHERE fts @@ websearch_to_tsquery('spanish', %s) LIMIT 10
    )
    SELECT COALESCE(s.chunk_id, k.chunk_id) as chunk_id
    FROM semantic_search s
    FULL OUTER JOIN keyword_search k ON s.chunk_id = k.chunk_id
    ORDER BY (COALESCE(1.0 / (60 + s.rank), 0.0) + COALESCE(1.0 / (60 + k.rank), 0.0)) DESC
    LIMIT 10;
    """

    hits_at_3 = 0
    mrr_sum = 0.0
    total_queries = len(golden_set)

    with psycopg.connect(DB_URL) as conn:
        with conn.cursor() as cur:
            for item in golden_set:
                pregunta = item['pregunta']
                target_ids = item['chunk_id_esperado'] # Lista de IDs válidos
                
                query_vector = model.encode(pregunta).tolist()
                cur.execute(sql_query, (str(query_vector), str(query_vector), pregunta, pregunta))
                
                resultados = [row[0] for row in cur.fetchall()]
                top_3 = resultados[:3]
                
                # Calcular Hit@3: ¿Al menos un ID válido está en el Top 3?
                if any(tid in top_3 for tid in target_ids):
                    hits_at_3 += 1
                    
                # Calcular MRR: Sumar solo la posición más alta del primer acierto
                for rank, res_id in enumerate(resultados, 1):
                    if res_id in target_ids:
                        mrr_sum += (1.0 / rank)
                        break
                
                print(f"Q: {pregunta[:40]}... | Targets: {target_ids} | Top 3 Obtenido: {top_3}")

    hit_rate = (hits_at_3 / total_queries) * 100
    mrr = mrr_sum / total_queries

    print(f"\n📊 Resultados de Evaluación sobre {total_queries} preguntas:")
    print(f"🎯 Hit@3: {hit_rate:.2f}%")
    print(f"📈 MRR: {mrr:.4f}")

if __name__ == "__main__":
    evaluate_golden_set()