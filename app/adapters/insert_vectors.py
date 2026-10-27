import os
import psycopg
from dotenv import load_dotenv
# Cargar variables desde el archivo .env
load_dotenv()

# Construir la cadena de conexión dinámicamente
DB_USER = os.getenv("POSTGRES_USER")
DB_PASS = os.getenv("POSTGRES_PASSWORD")
DB_NAME = os.getenv("POSTGRES_DB")
DB_HOST = os.getenv("POSTGRES_HOST", "localhost")
DB_PORT = os.getenv("POSTGRES_PORT", "5432")
DB_URL = f"postgresql://{DB_USER}:{DB_PASS}@{DB_HOST}:{DB_PORT}/{DB_NAME}"
CHUNKS_DIR = "data/chunks"
def insert_test_embeddings():
    with psycopg.connect(DB_URL) as conn:
        with conn.cursor() as cur:
            # Crear tabla con una columna vectorial de 3 dimensiones
            cur.execute("""
                CREATE TABLE IF NOT EXISTS contenido_educativo (
                    id serial PRIMARY KEY,
                    texto text,
                    embedding vector(3)
                );
            """)
            
            # Limpiar datos previos en caso de múltiples ejecuciones
            cur.execute("TRUNCATE contenido_educativo;")
            
            # Insertar vectores de prueba
            cur.execute("""
                INSERT INTO contenido_educativo (texto, embedding) 
                VALUES 
                    ('Lumi el robot azul sin pies', '[0.1, 0.2, 0.3]'),
                    ('Juego simulador con mecánicas de progresión', '[0.8, 0.7, 0.9]');
            """)
            
            conn.commit()
            print("✅ Tabla creada y embeddings de prueba insertados exitosamente.")

if __name__ == "__main__":
    insert_test_embeddings()