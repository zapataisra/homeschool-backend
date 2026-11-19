import os
import json
import time

def create_chunks(text, chunk_size=1000, overlap=200):
    chunks = []
    step = chunk_size - overlap
    for i in range(0, len(text), step):
        chunk = text[i:i + chunk_size]
        chunks.append(chunk)
        if i + chunk_size >= len(text):
            break
    return chunks

def process_all_caches(cache_dir="data/cache", chunks_dir="data/chunks"):
    os.makedirs(chunks_dir, exist_ok=True)
    
    cache_files = [f for f in os.listdir(cache_dir) if f.endswith('.json')]
    
    # Inicialización de métricas
    start_time = time.perf_counter()
    docs_processed = 0
    total_chunks = 0
    total_chars = 0
    
    for filename in cache_files:
        cache_path = os.path.join(cache_dir, filename)
        base_name = os.path.splitext(filename)[0]
        chunks_path = os.path.join(chunks_dir, f"{base_name}.jsonl")
        
        with open(cache_path, 'r', encoding='utf-8') as f:
            data = json.load(f)
            
        text_content = data.get("content", "")
        source_name = data.get("source", filename)
        
        # Generar fragmentos
        text_chunks = create_chunks(text_content, chunk_size=1000, overlap=200)
        
        # Acumular métricas
        docs_processed += 1
        total_chunks += len(text_chunks)
        total_chars += sum(len(chunk) for chunk in text_chunks)
        
        # Guardar en disco sobreescribiendo si ya existía para asegurar una métrica limpia
        with open(chunks_path, 'w', encoding='utf-8') as f:
            for i, chunk_text in enumerate(text_chunks):
                chunk_doc = {
                    "chunk_id": i,
                    "source": source_name,
                    "text": chunk_text
                }
                f.write(json.dumps(chunk_doc, ensure_ascii=False) + "\n")
                
    # Cálculo final
    execution_time_ms = (time.perf_counter() - start_time) * 1000
    avg_chunk_size = total_chars / total_chunks if total_chunks > 0 else 0
    
    print("\n✅ Proceso de segmentación completado.")
    print(f"⏱️ 1. Tiempo total de ejecución: {execution_time_ms:.2f} ms")
    print(f"📄 2. Documentos fuente procesados: {docs_processed}")
    print(f"🧩 3. Número total de chunks generados: {total_chunks}")
    print(f"📏 4. Tamaño promedio de chunks: {avg_chunk_size:.0f} caracteres")

if __name__ == "__main__":
    process_all_caches()