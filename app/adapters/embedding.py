"""Modelo de embeddings local (sentence-transformers). Se mantiene separado de retrieval.py
para que la recuperacion SQL no dependa del modelo (y pueda reescribirse en C# sin cambios)."""
import os

_MODEL_CACHE = {}


def _load_model():
    from sentence_transformers import SentenceTransformer

    name = os.getenv("EMBED_MODEL", "all-MiniLM-L6-v2")
    if name not in _MODEL_CACHE:
        model = SentenceTransformer(name)
        print(
            f"[embedding] {name} | dimension {model.get_sentence_embedding_dimension()} "
            f"| longitud maxima {model.max_seq_length} tokens (el texto que exceda se descarta)",
            flush=True,
        )
        _MODEL_CACHE[name] = model
    return name, _MODEL_CACHE[name]


def get_embedder():
    """Devuelve (nombre_del_modelo, funcion texto -> vector) para CONSULTAS individuales.

    Variable: EMBED_MODEL (por defecto all-MiniLM-L6-v2).
    Los modelos E5 exigen el prefijo «query: » en consultas y «passage: » en los documentos;
    si cambias de modelo, reindexa los documentos con el prefijo y la dimension que corresponda.
    """
    name, model = _load_model()
    prefix = "query: " if "e5" in name.lower() else ""
    return name, (lambda text: model.encode(prefix + text, normalize_embeddings=True).tolist())


def get_document_batch_embedder(batch_size: int = 64):
    """Devuelve (nombre_del_modelo, funcion lista_textos -> lista_vectores) para INGESTA masiva."""
    name, model = _load_model()
    prefix = "passage: " if "e5" in name.lower() else ""

    def encode_batch(texts: list[str]) -> list[list[float]]:
        if not texts:
            return []
        prefixed = [prefix + t for t in texts] if prefix else texts
        embeddings = model.encode(
            prefixed,
            batch_size=batch_size,
            show_progress_bar=False,
            normalize_embeddings=True,
        )
        return embeddings.tolist()

    return name, encode_batch