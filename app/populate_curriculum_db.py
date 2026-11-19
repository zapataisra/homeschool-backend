#!/usr/bin/env python3
"""Puebla public.curriculum_topics (temas del Programa Sintético + láminas desde embeddings_sep).

Paso A: preserva los 27 temas semilla de SeedCurriculumCatalog.cs (mismo topic_id/order_index/blocks).
Paso B: extrae Contenidos + PDA por grado de los 3 PDFs del Programa Sintético (data/cache/*.json).
Paso C: cruza cada Contenido/PDA contra public.embeddings_sep para armar 3 láminas y genera sus SVG.
100% local ($0 USD): psycopg + data/cache + app/generate_lesson_visuals (sin LLM).

USO: python app/populate_curriculum_db.py
"""

import json
import re
import sys
import unicodedata
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

import psycopg
import pymupdf as fitz

import app.adapters.retrieval as retrieval
from app.generate_lesson_visuals import build_slide

DATA_DIR = REPO_ROOT / "data"
LESSONS_DIR = REPO_ROOT / "web" / "public" / "lessons"
SEED_CS = REPO_ROOT / "HomeschoolAI.Adapters" / "Learning" / "SeedCurriculumCatalog.cs"

CAMPOS = ["Lenguajes", "Saberes y Pensamiento Científico", "Ética, Naturaleza y Sociedades", "De lo Humano y lo Comunitario"]
CAMPO_SLUG = {
    "Lenguajes": "lenguajes",
    "Saberes y Pensamiento Científico": "saberes",
    "Ética, Naturaleza y Sociedades": "etica",
    "De lo Humano y lo Comunitario": "humano",
}
CAMPO_ARCHETYPE = {
    "Saberes y Pensamiento Científico": "matematicas",
    "Lenguajes": "lenguajes",
    "Ética, Naturaleza y Sociedades": "etica",
    "De lo Humano y lo Comunitario": "ciencias",
}

SKIP_PREFIXES = (
    "programa de estudio", "■", "contenidos", "procesos de desarrollo", "tercer grado",
    "cuarto grado", "primer grado", "segundo grado", "quinto grado", "sexto grado",
    "finalidades del campo", "especificidades del campo",
)

DDL = """
CREATE TABLE IF NOT EXISTS public.curriculum_topics (
    topic_id text PRIMARY KEY,
    grade integer NOT NULL,
    fase integer NOT NULL,
    trimester integer NOT NULL,
    order_index integer NOT NULL,
    title text NOT NULL,
    campo_formativo text NOT NULL,
    pda_description text NOT NULL,
    search_query text NOT NULL,
    weekly_project_title text NOT NULL,
    weekly_project_goal text NOT NULL,
    prerequisite_topic_id text NULL,
    source_book text NULL,
    blocks_json jsonb NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    updated_at_utc timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS idx_curriculum_topics_grade_order
    ON public.curriculum_topics (grade, order_index) WHERE is_active = true;
"""


def fase_of(grade):
    return 3 if grade <= 2 else (4 if grade <= 4 else 5)


def log(msg):
    print(msg, flush=True)


# ---------------------------------------------------------------- Paso A
def parse_seed_catalog():
    """Lee SeedCurriculumCatalog.cs y devuelve los temas semilla con sus blocks_json."""
    text = SEED_CS.read_text(encoding="utf-8")
    topics = []
    for chunk in text.split("new(")[1:]:
        m = re.match(
            r'\s*(\d+),\s*(\d+),\s*(\d+),\s*"([^"]+)",\s*"([^"]+)",\s*"([^"]+)",\s*'
            r'"([^"]+)",\s*"([^"]+)",\s*"([^"]+)",\s*(?:"([^"]+)"|null),\s*Blocks\(',
            chunk)
        if not m:
            continue
        grade, trimester, order = int(m.group(1)), int(m.group(2)), int(m.group(3))
        blocks = [
            {"type": b[0], "content": b[1], "altText": b[2], "narrationText": b[3], "imageUrl": b[4]}
            for b in re.findall(r'\("([^"]*)",\s*"([^"]*)",\s*"([^"]*)",\s*"([^"]*)",\s*"([^"]*)"\)', chunk)
        ]
        topics.append({
            "topic_id": m.group(4), "grade": grade, "fase": fase_of(grade),
            "trimester": trimester, "order_index": order,
            "title": m.group(5), "campo_formativo": m.group(6),
            "pda_description": "", "search_query": m.group(7),
            "weekly_project_title": m.group(8), "weekly_project_goal": m.group(9),
            "prerequisite_topic_id": m.group(10), "source_book": None,
            "blocks_json": blocks, "is_seed": True,
        })
    return topics


# ---------------------------------------------------------------- Paso B
def _norm(s):
    """Normaliza para comparar: sin acentos, minúsculas, signos fuera, espacios colapsados."""
    s = unicodedata.normalize("NFKD", s)
    s = "".join(c for c in s if not unicodedata.combining(c))
    s = s.lower()
    s = re.sub(r"[^a-zñ0-9]+", " ", s)
    return re.sub(r"\s+", " ", s).strip()


def _repair_hyphenation(text):
    # une guiones de fin de línea ("impli- ca" -> "implica")
    return re.sub(r"(\w)-\s+(\w)", r"\1\2", text)


def _normalize_text(text):
    text = _repair_hyphenation(text)
    return re.sub(r"\s+", " ", text).strip()


def _cell_text(words, bbox):
    """Reconstruye el texto limpio de una celda desde sus palabras (conserva espacios)."""
    if bbox is None:
        return ""
    x0, y0, x1, y1 = bbox
    ws = [w for w in words if w[0] >= x0 - 1 and w[2] <= x1 + 1 and w[1] >= y0 - 1 and w[3] <= y1 + 1]
    if not ws:
        return ""
    ws.sort(key=lambda w: (round(w[1], 1), w[0]))
    out = []
    for w in ws:
        t = w[4]
        if out and out[-1].endswith("-") and not t.startswith((".", ",", ";", ":")):
            out[-1] = out[-1][:-1] + t
        else:
            out.append(t)
    return _normalize_text(" ".join(out))


def _detect_campo(page_text, current):
    """Campo Formativo activo según el encabezado '■ <Campo>' de la página."""
    for m in re.finditer(r"■", page_text):
        tail = _norm(page_text[m.end():m.end() + 90])
        for campo in CAMPOS:
            if tail.startswith(_norm(campo)):
                return campo
    return current


_GRADE_HEADERS = {"primer grado", "segundo grado", "tercer grado", "cuarto grado", "quinto grado", "sexto grado"}


def _is_header_row(c0, c1, c2):
    low0, low1, low2 = c0.lower(), c1.lower(), c2.lower()
    if low0.strip() == "contenidos":
        return True
    return low1.strip() in _GRADE_HEADERS or low2.strip() in _GRADE_HEADERS


def _clean_title(text):
    """Normaliza un Contenido; devuelve None si es inválido o está truncado."""
    text = _normalize_text(text)
    text = re.sub(r"\.$", "", text).strip()
    if len(text) < 3:
        return None
    low = text.lower()
    if any(low.startswith(p) for p in SKIP_PREFIXES):
        return None
    if text[0].islower() or text[0].isdigit():
        return None  # título truncado que empieza en minúscula (p. ej. "rrollo…")
    if len(text.split()) < 2:
        return None
    return text


def _is_pda_table(words, row):
    cells = [c for c in list(row.cells) if c is not None]
    if len(cells) < 3:
        return False
    c0 = _cell_text(words, cells[0]).lower()
    c1 = _cell_text(words, cells[1]).lower()
    return "contenido" in c0 and ("aprendizaje" in c1 or "proceso" in c1)


def _extract_fase_pairs(fase):
    """Devuelve [(campo, contenido, pda_impar, pda_par), ...] para una fase."""
    doc = fitz.open(DATA_DIR / f"Programa_Sintetico_Fase_{fase}.pdf")
    pairs = []
    campo = None
    open_row = None  # {"campo", "contenido", "pda_odd", "pda_even"}
    try:
        for pno in range(doc.page_count):
            page = doc[pno]
            page_text = page.get_text()
            campo = _detect_campo(page_text, campo)
            tables = page.find_tables()
            if not tables.tables or campo is None:
                continue
            words = page.get_text("words")
            for tab in tables.tables:
                if tab.col_count != 3:
                    continue
                rows = list(tab.rows)
                if not rows or not _is_pda_table(words, rows[0]):
                    continue
                for row in rows:
                    cells = [c for c in list(row.cells) if c is not None]
                    if len(cells) < 3:
                        continue
                    c0 = _cell_text(words, cells[0])
                    c1 = _cell_text(words, cells[1])
                    c2 = _cell_text(words, cells[2])
                    if _is_header_row(c0, c1, c2):
                        continue
                    contenido = _clean_title(c0)
                    if contenido:
                        if open_row:
                            pairs.append(open_row)
                        open_row = {"campo": campo, "contenido": contenido, "pda_odd": c1, "pda_even": c2}
                    elif open_row is not None:
                        # Costura multipágina: continúa la fila abierta (Contenido vacío).
                        if c1:
                            open_row["pda_odd"] = _normalize_text(open_row["pda_odd"] + " " + c1)
                        if c2:
                            open_row["pda_even"] = _normalize_text(open_row["pda_even"] + " " + c2)
        if open_row:
            pairs.append(open_row)
    finally:
        doc.close()
    return pairs


def extract_pdf_topics():
    """Extrae Contenidos + PDA por grado (columna) y devuelve (topics, stats)."""
    topics = []
    stats = {}
    for fase, (g_odd, g_even) in ((3, (1, 2)), (4, (3, 4)), (5, (5, 6))):
        pairs = _extract_fase_pairs(fase)
        distinct = sum(1 for p in pairs if p["pda_odd"].strip() != p["pda_even"].strip())
        stats[fase] = {"pairs": len(pairs), "distinct_pda": distinct}
        for p in pairs:
            for grade, pda in ((g_odd, p["pda_odd"]), (g_even, p["pda_even"])):
                pda = pda or p["contenido"]
                topics.append({
                    "grade": grade, "fase": fase, "campo_formativo": p["campo"],
                    "title": p["contenido"], "pda_description": pda,
                    "search_query": f"{p['contenido']} {p['campo']} {grade} grado primaria SEP",
                    "weekly_project_title": p["contenido"],
                    "weekly_project_goal": pda[:200],
                    "source_book": None, "is_seed": False,
                })
    return topics, stats


# ---------------------------------------------------------------- Paso C
def _truncate(text, limit=420):
    text = re.sub(r"\s+", " ", text).strip()
    return text if len(text) <= limit else text[:limit].rsplit(" ", 1)[0] + "…"


def _sentences(text, n=3):
    parts = [s.strip() for s in re.split(r"(?<=[.!?])\s+", text) if s.strip()]
    return [_truncate(s, 140) for s in parts[:n]]


def fetch_fragments(conn, grade, query_text, limit=3):
    tsq = retrieval.build_tsquery(query_text)
    if not tsq:
        return []
    with conn.cursor() as cur:
        cur.execute(
            """
            SELECT texto FROM embeddings_sep
            WHERE source ILIKE %(src)s AND fts @@ to_tsquery('spanish', %(tsq)s)
            ORDER BY ts_rank_cd(fts, to_tsquery('spanish', %(tsq)s)) DESC
            LIMIT %(limit)s
            """,
            {"src": f"P{grade}%", "tsq": tsq, "limit": limit})
        return [r[0] for r in cur.fetchall()]


def build_blocks(grade, campo, title, pda, conn):
    frags = fetch_fragments(conn, grade, f"{title} {pda}")
    f0 = _truncate(frags[0]) if len(frags) > 0 else ""
    s = _sentences(f0) if f0 else []
    s1 = s[0] if s else ""
    return [
        {"type": "text", "content": f"{title}. {s1}".strip() if s1 else title,
         "altText": f"Ilustración del tema: {title}", "narrationText": s1 or title},
        {"type": "text", "content": f0 or f"Ejemplo sobre {title}.",
         "altText": f"Ejemplo del libro sobre {title}", "narrationText": (s[1] if len(s) > 1 else (s1 or title))},
        {"type": "text", "content": f"Reto del día: {_truncate(pda)}",
         "altText": f"Reto práctico sobre {title}", "narrationText": "Explica tu reto a Axolotl."},
    ]


def to_slide_tuple(block):
    s = _sentences(block["content"], 3)
    headline = s[0] if s else block["narrationText"]
    b1 = s[1] if len(s) > 1 else block["narrationText"]
    b2 = s[2] if len(s) > 2 else ""
    return (headline, b1, b2)


def write_svg(topic_id, title, campo, blocks):
    archetype = CAMPO_ARCHETYPE.get(campo, "ciencias")
    folder = LESSONS_DIR / topic_id
    folder.mkdir(parents=True, exist_ok=True)
    for n, block in enumerate(blocks, start=1):
        svg = build_slide(topic_id, title, archetype, n, to_slide_tuple(block))
        (folder / f"slide-{n}.svg").write_text(svg, encoding="utf-8")
        block["imageUrl"] = f"/lessons/{topic_id}/slide-{n}.svg"
    return blocks


def round_robin(items):
    by_campo = {}
    for t in items:
        by_campo.setdefault(t["campo_formativo"], []).append(t)
    result = []
    lists = list(by_campo.values())
    max_len = max((len(l) for l in lists), default=0)
    for i in range(max_len):
        for l in lists:
            if i < len(l):
                result.append(l[i])
    return result


def assign_order_and_ids(all_topics):
    by_grade = {}
    for t in all_topics:
        by_grade.setdefault(t["grade"], []).append(t)

    for grade, topics in by_grade.items():
        seeds = sorted([t for t in topics if t.get("is_seed")], key=lambda t: t["order_index"])
        pdfs = [t for t in topics if not t.get("is_seed")]
        ordered_pdfs = round_robin(pdfs)
        max_order = max((t["order_index"] for t in seeds), default=0)

        last_by_campo = {t["campo_formativo"]: t["topic_id"] for t in seeds}
        counter = {}
        for i, t in enumerate(ordered_pdfs):
            t["order_index"] = max_order + i + 1
            t["trimester"] = ((t["order_index"] - 1) % 3) + 1
            campo = t["campo_formativo"]
            counter[campo] = counter.get(campo, 0) + 1
            t["topic_id"] = f"fase{grade}-{CAMPO_SLUG[campo]}-{counter[campo]:02d}"
            t["prerequisite_topic_id"] = last_by_campo.get(campo)
            last_by_campo[campo] = t["topic_id"]
    return all_topics


UPSERT = """
INSERT INTO public.curriculum_topics
    (topic_id, grade, fase, trimester, order_index, title, campo_formativo,
     pda_description, search_query, weekly_project_title, weekly_project_goal,
     prerequisite_topic_id, source_book, blocks_json, is_active)
VALUES (%(topic_id)s, %(grade)s, %(fase)s, %(trimester)s, %(order_index)s, %(title)s,
        %(campo_formativo)s, %(pda_description)s, %(search_query)s, %(weekly_project_title)s,
        %(weekly_project_goal)s, %(prerequisite_topic_id)s, %(source_book)s, %(blocks_json)s, true)
ON CONFLICT (topic_id) DO UPDATE SET
    grade = EXCLUDED.grade, fase = EXCLUDED.fase, trimester = EXCLUDED.trimester,
    order_index = EXCLUDED.order_index, title = EXCLUDED.title,
    campo_formativo = EXCLUDED.campo_formativo, pda_description = EXCLUDED.pda_description,
    search_query = EXCLUDED.search_query, weekly_project_title = EXCLUDED.weekly_project_title,
    weekly_project_goal = EXCLUDED.weekly_project_goal,
    prerequisite_topic_id = EXCLUDED.prerequisite_topic_id, source_book = EXCLUDED.source_book,
    blocks_json = EXCLUDED.blocks_json, updated_at_utc = now();
"""


def clean_orphan_svgs(referenced_urls):
    """Elimina SVGs en web/public/lessons/ no referenciados por ningún blocks_json activo."""
    removed = 0
    for svg in sorted(LESSONS_DIR.rglob("*.svg")):
        rel = "/" + svg.relative_to(LESSONS_DIR.parent).as_posix()
        if rel not in referenced_urls:
            svg.unlink(missing_ok=True)
            removed += 1
    # Elimina carpetas vacías (de temas antiguos).
    for folder in sorted(
        (p for p in LESSONS_DIR.rglob("*") if p.is_dir()),
        key=lambda p: len(p.parts),
        reverse=True,
    ):
        try:
            folder.rmdir()
        except OSError:
            pass
    return removed


def _print_summary(all_topics, stats, deleted_rows, removed_svgs):
    by_grade = {}
    for t in all_topics:
        by_grade[t["grade"]] = by_grade.get(t["grade"], 0) + 1

    log("")
    log("=== Resumen ===")
    log("Temas por grado:")
    for grade in sorted(by_grade):
        log(f"  Grado {grade}: {by_grade[grade]}")

    log("PDA distinto entre el par de grados (por fase):")
    for fase in sorted(stats):
        pairs = stats[fase]["pairs"]
        distinct = stats[fase]["distinct_pda"]
        pct = (100.0 * distinct / pairs) if pairs else 0.0
        log(f"  Fase {fase}: {distinct}/{pairs} pares con PDA distinto ({pct:.1f}%)")

    truncated = sum(1 for t in all_topics if t["title"] and t["title"][0].islower())
    log(f"Títulos truncados en minúscula: {truncated}")
    log(f"Filas antiguas eliminadas: {deleted_rows}")
    log(f"SVGs huérfanos eliminados: {removed_svgs}")


def main():
    from dotenv import load_dotenv
    load_dotenv()

    log("Cargando temas semilla desde SeedCurriculumCatalog.cs…")
    seed = parse_seed_catalog()
    log(f"  {len(seed)} temas semilla.")

    log("Extrayendo Contenidos + PDA del Programa Sintético (tablas por columna)…")
    pdf, stats = extract_pdf_topics()
    log(f"  {len(pdf)} temas extraídos del PDF.")

    all_topics = assign_order_and_ids(seed + pdf)

    conn = retrieval.connect()
    with conn.cursor() as cur:
        cur.execute(DDL)
    conn.commit()

    seed_ids = {t["topic_id"] for t in seed}
    referenced = set()

    inserted = 0
    with conn.cursor() as cur:
        for t in all_topics:
            if t.get("is_seed"):
                blocks = t["blocks_json"]
            else:
                blocks = build_blocks(t["grade"], t["campo_formativo"], t["title"], t["pda_description"], conn)
                blocks = write_svg(t["topic_id"], t["title"], t["campo_formativo"], blocks)
            for b in blocks:
                if b.get("imageUrl"):
                    referenced.add(b["imageUrl"])
            cur.execute(UPSERT, {**t, "blocks_json": json.dumps(blocks, ensure_ascii=False)})
            inserted += 1
    conn.commit()

    # Elimina filas antiguas (heurísticas) que ya no corresponden al catálogo limpio,
    # conservando siempre los temas semilla base.
    keep_ids = sorted(seed_ids | {t["topic_id"] for t in all_topics})
    with conn.cursor() as cur:
        cur.execute("DELETE FROM public.curriculum_topics WHERE NOT (topic_id = ANY(%s))", (keep_ids,))
        deleted = cur.rowcount
    conn.commit()
    conn.close()

    removed = clean_orphan_svgs(referenced)

    _print_summary(all_topics, stats, deleted, removed)


if __name__ == "__main__":
    main()



