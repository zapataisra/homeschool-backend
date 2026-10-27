import os
import time
import json
import fitz  # PyMuPDF

def process_all_pdfs(data_dir="data", cache_dir="data/cache"):
    os.makedirs(cache_dir, exist_ok=True)
    
    # Buscar todos los archivos PDF en la carpeta
    pdf_files = [f for f in os.listdir(data_dir) if f.endswith('.pdf')]
    
    if not pdf_files:
        print("❌ No se encontraron archivos PDF en el directorio.")
        return

    for filename in pdf_files:
        pdf_path = os.path.join(data_dir, filename)
        cache_path = os.path.join(cache_dir, f"{os.path.splitext(filename)[0]}.json")
        
        print(f"\n📄 Analizando: {filename}")
        pdf_size_kb = os.path.getsize(pdf_path) / 1024
        
        if os.path.exists(cache_path):
            cache_size_kb = os.path.getsize(cache_path) / 1024
            print(f"✅ Caché existente. Omitiendo. (PDF: {pdf_size_kb:.2f} KB | Caché: {cache_size_kb:.2f} KB)")
            continue
            
        start_time = time.perf_counter()
        text_content = ""
        with fitz.open(pdf_path) as doc:
            for page in doc:
                text_content += page.get_text()
                
        extraction_time_ms = (time.perf_counter() - start_time) * 1000
        
        with open(cache_path, 'w', encoding='utf-8') as f:
            json.dump({"source": filename, "content": text_content}, f, ensure_ascii=False, indent=4)
            
        cache_size_kb = os.path.getsize(cache_path) / 1024
        
        print(f"✅ Extracción completada.")
        print(f"⏱️ Tiempo: {extraction_time_ms:.2f} ms")
        print(f"💾 Tamaños - PDF: {pdf_size_kb:.2f} KB | Caché: {cache_size_kb:.2f} KB")

if __name__ == "__main__":
    # El punto "." significa "busca en la carpeta donde estoy parado"
    process_all_pdfs(data_dir=".", cache_dir="cache")