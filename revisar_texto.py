import json

cache_path = "data/cache/Programa_Sintetico_Fase_3.json"

try:
    with open(cache_path, 'r', encoding='utf-8') as f:
        data = json.load(f)
        
    print(f"📄 Archivo: {data['source']}\n")
    # Imprimir los primeros 1500 caracteres para revisión
    print(data['content'][:50500])
    print("\n... [Texto truncado] ...")
except FileNotFoundError:
    print("❌ Archivo no encontrado. Verifica la ruta.")