import time
import psycopg

# Cadena de conexión basada en el docker-compose
DB_URL = "postgresql://admin:password123@localhost:5432/vectordb"

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