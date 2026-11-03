import time
import os
import psycopg
from dotenv import load_dotenv

load_dotenv()

# Cadena de conexión construida desde variables de entorno (.env)
DB_USER = os.getenv("POSTGRES_USER")
DB_PASS = os.getenv("POSTGRES_PASSWORD")
DB_NAME = os.getenv("POSTGRES_DB")
DB_HOST = os.getenv("POSTGRES_HOST", "localhost")
DB_PORT = os.getenv("POSTGRES_PORT", "5432")

DB_URL = f"postgresql://{DB_USER}:{DB_PASS}@{DB_HOST}:{DB_PORT}/{DB_NAME}"

def measure_connection():
    print("Iniciando prueba de latencia (Cold Start)...")
    start_time = time.perf_counter()
    
    try:
        # Medir el tiempo de conexión pura
        with psycopg.connect(DB_URL) as conn:
            connection_time = (time.perf_counter() - start_time) * 1000
            
            # Medir latencia de una consulta (ping)
            with conn.cursor() as cur:
                cur.execute("SELECT 1;")
                cur.fetchone()
                
                # Aseguramos nuevamente que la extensión esté lista
                cur.execute("CREATE EXTENSION IF NOT EXISTS vector;")
                conn.commit()
                
            total_time = (time.perf_counter() - start_time) * 1000
            
        print(f"✅ Conexión exitosa a PostgreSQL")
        print(f"⏱️ Tiempo de conexión (Latencia): {connection_time:.2f} ms")
        print(f"⏱️ Tiempo total con ping SQL: {total_time:.2f} ms")
        
    except Exception as e:
        print(f"❌ Error al conectar: {e}")

if __name__ == "__main__":
    measure_connection()