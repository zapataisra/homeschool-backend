"""Modelo de embeddings local (sentence-transformers). Se mantiene separado de retrieval.py
para que la recuperación SQL no dependa del modelo (y pueda reescribirse en C# sin cambios)."""
import os


def get_embedder():
    """Devuelve (nombre_del_modelo, función texto -> vector) para CONSULTAS.

    Variable: EMBED_MODEL (por defecto all-MiniLM-L6-v2).
    Los modelos E5 exigen el prefijo «query: » en consultas y «passage: » en los documentos;
    si cambias de modelo, reindexa los documentos con el prefijo y la dimensión que corresponda.
    """
    from sentence_transformers import SentenceTransformer
    name = os.getenv("EMBED_MODEL", "all-MiniLM-L6-v2")
    model = SentenceTransformer(name)
    print(f"[embedding] {name} | dimensión {model.get_sentence_embedding_dimension()} "
          f"| longitud máxima {model.max_seq_length} tokens (el texto que exceda se descarta)")
    prefix = "query: " if "e5" in name.lower() else ""
    return name, (lambda text: model.encode(prefix + text).tolist())
