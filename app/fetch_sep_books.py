#!/usr/bin/env python3
"""Descargador de libros completos de CONALITEG (ciclo 2026-2027) + data/manifest.json.

El lector de CONALITEG sirve cada libro como UN PDF en:
    https://libros.conaliteg.gob.mx/pdf-reader/assets/{nivel}/{ciclo}/{clave}.pdf
(no paginas JPG; verificado en pdf-reader/reader.bundle.js). Se descarga el PDF
directo con reintentos y cache en data/cache/conaliteg/{code}/.

En 2026 "Nuestros saberes" de 4. usa P4SDA (antes P4NSA).

LICENCIA: data/*.pdf y data/cache/ estan en .gitignore; este script documenta
como obtenerlos y data/manifest.json registra sha256 para idempotencia.

USO: python app/fetch_sep_books.py P4NSA   |   python app/fetch_sep_books.py --all
"""
#!/usr/bin/env python3
"""Descargador, desbloqueador y auditor de libros CONALITEG (1.o a 6.o de primaria, ciclo 2026-2027).

Ejemplos de uso:
  python app/fetch_sep_books.py --grade 4
  python app/fetch_sep_books.py --all-grades --only-saberes
  python app/fetch_sep_books.py --all-grades
  python app/fetch_sep_books.py --audit-only
"""

import argparse
import base64
import datetime
import hashlib
import json
import os
import re
import shutil
import sys
import time
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path

import pymupdf as fitz
import requests

REPO_ROOT = Path(__file__).resolve().parents[1]
SCHOOL_CYCLE = "2026-2027"
NIVEL = "primaria"
YEARS = ("2026", "2025", "2024", "2023")
BASE_URL = "https://libros.conaliteg.gob.mx/pdf-reader/assets"
BUNDLE_URL = "https://libros.conaliteg.gob.mx/pdf-reader/reader.bundle.js"
USER_AGENT = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/124 Safari/537.36 HomeschoolAI/1.0"
)

BOOK_TYPES = {
    "NSA": ("Nuestros_Saberes", lambda g: [f"P{g}SDA", f"P{g}NSA"]),
    "PAA": ("Proyectos_Aula", lambda g: [f"P{g}PAA"]),
    "PEA": ("Proyectos_Escolares", lambda g: [f"P{g}PEA"]),
    "PCA": ("Proyectos_Comunitarios", lambda g: [f"P{g}PCA"]),
    "MLA": ("Multiples_Lenguajes", lambda g: [f"P{g}MLA"]),
}

BOOK_CATALOG = {
    f"P{grade}{kind}": (f"{label}_{grade}", grade, candidates_fn(grade))
    for grade in range(1, 7)
    for kind, (label, candidates_fn) in BOOK_TYPES.items()
}


def log(message: str) -> None:
    print(message, flush=True)


def sha256_file(path: str) -> str:
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def fetch_candidate_passwords() -> list[str]:
    """Extrae candidatos de contrasena desde reader.bundle.js de CONALITEG."""
    try:
        js = requests.get(
            BUNDLE_URL, timeout=30, headers={"User-Agent": USER_AGENT}
        ).text
    except requests.RequestException:
        return []

    candidates = set(re.findall(r'[\x22\x27]([^\x22\x27\\]{3,80})[\x22\x27]', js))
    for item in list(candidates):
        try:
            decoded = base64.b64decode(item).decode("utf-8")
            if decoded.isprintable():
                candidates.add(decoded)
        except Exception:
            pass
    return list(candidates)


def ensure_unlocked(pdf_path: str, passwords: list[str]) -> bool:
    """Si el PDF tiene contrasena, lo autentica y lo guarda desencriptado en sitio."""
    doc = fitz.open(pdf_path)
    if not doc.needs_pass:
        doc.close()
        return False

    unlocked_ok = False
    for pw in passwords:
        if doc.authenticate(pw):
            unlocked_ok = True
            break

    if not unlocked_ok:
        doc.close()
        raise RuntimeError(f"No se pudo desbloquear el PDF: {pdf_path}")

    tmp_unlocked = f"{pdf_path}.unlocked"
    doc.save(tmp_unlocked, encryption=fitz.PDF_ENCRYPT_NONE)
    doc.close()
    os.replace(tmp_unlocked, pdf_path)
    return True


def resolve_book(session: requests.Session, candidate_codes: list[str]):
    """Devuelve (year, code, url) del primer PDF 200/application/pdf disponible."""
    for code in candidate_codes:
        for year in YEARS:
            url = f"{BASE_URL}/{NIVEL}/{year}/{code}.pdf"
            try:
                with session.get(
                    url, stream=True, timeout=30, headers={"User-Agent": USER_AGENT}
                ) as resp:
                    content_type = resp.headers.get("content-type", "")
                    if resp.status_code == 200 and "application/pdf" in content_type:
                        return year, code, url
            except requests.RequestException:
                continue
    return None


def download_file(
    session: requests.Session, url: str, dest_path: str, retries: int = 3
) -> str:
    os.makedirs(os.path.dirname(dest_path) or ".", exist_ok=True)
    tmp_path = f"{dest_path}.part"
    for attempt in range(1, retries + 1):
        try:
            with session.get(
                url, stream=True, timeout=180, headers={"User-Agent": USER_AGENT}
            ) as resp:
                resp.raise_for_status()
                with open(tmp_path, "wb") as handle:
                    for chunk in resp.iter_content(chunk_size=1 << 16):
                        if chunk:
                            handle.write(chunk)
            os.replace(tmp_path, dest_path)
            return dest_path
        except requests.RequestException as exc:
            log(f"      reintento {attempt}/{retries} ({type(exc).__name__}: {exc})")
            time.sleep(2**attempt)
    if os.path.exists(tmp_path):
        os.remove(tmp_path)
    raise RuntimeError(f"No se pudo descargar: {url}")


def load_manifest(manifest_path: str) -> dict:
    if not os.path.exists(manifest_path):
        return {}
    with open(manifest_path, "r", encoding="utf-8") as handle:
        return json.load(handle)


def manifest_sha_map(manifest: dict) -> dict[str, str]:
    return {
        entry["filename"]: entry["sha256"]
        for entry in manifest.get("books", [])
        if entry.get("filename") and entry.get("sha256")
    }


def download_book(
    book_key: str,
    manifest_map: dict[str, str],
    data_dir: str,
    cache_dir: str,
    passwords: list[str],
):
    name, grade, candidates = BOOK_CATALOG[book_key]
    filename = f"{book_key}_{name}.pdf"
    final_path = os.path.join(data_dir, filename)

    session = requests.Session()
    session.headers["User-Agent"] = USER_AGENT

    resolution = resolve_book(session, candidates)
    if resolution is None:
        log(f"X {book_key}: no encontrado en los ciclos {list(YEARS)}.")
        return None

    year, code, url = resolution

    if os.path.exists(final_path):
        was_locked = ensure_unlocked(final_path, passwords)
        if was_locked:
            log(f"DESBLOQUEADO {filename}: se elimino la contrasena del PDF existente.")
        if manifest_map.get(filename) == sha256_file(final_path):
            log(f"OK {filename}: ya existe, desbloqueado y con SHA-256 verificado.")
            return {
                "filename": filename,
                "book_code": book_key,
                "grade": grade,
                "year": year,
                "code": code,
            }

    log(f"-> {book_key}: descargando {code} (ciclo {year})...")
    cache_path = os.path.join(cache_dir, book_key, f"{code}.pdf")
    download_file(session, url, cache_path, retries=3)
    ensure_unlocked(cache_path, passwords)
    shutil.copyfile(cache_path, final_path)
    log(f"OK {filename}: descargado y desbloqueado en data/{filename}")
    return {
        "filename": filename,
        "book_code": book_key,
        "grade": grade,
        "year": year,
        "code": code,
    }


def audit_pdf(path: str, passwords: list[str]) -> dict:
    """Audita paginas con texto digital nativo vs paginas que requieren OCR e imagenes."""
    ensure_unlocked(path, passwords)
    with fitz.open(path) as doc:
        page_count = doc.page_count
        native_text_pages = 0
        total_images = 0
        for page in doc:
            if len(page.get_text("text").strip()) > 50:
                native_text_pages += 1
            total_images += len(page.get_images(full=True))
        return {
            "is_encrypted": bool(doc.needs_pass),
            "page_count": page_count,
            "pages_with_native_text": native_text_pages,
            "pages_needing_ocr": page_count - native_text_pages,
            "total_embedded_images": total_images,
        }


def build_manifest(
    data_dir: str, resolved_books: list[dict], passwords: list[str]
) -> dict:
    books = []
    by_filename = {r["filename"]: r for r in resolved_books}
    for filename in sorted(
        f for f in os.listdir(data_dir) if f.lower().endswith(".pdf")
    ):
        path = os.path.join(data_dir, filename)
        resolved = by_filename.get(filename)
        audit = audit_pdf(path, passwords)
        match_grade = re.match(r"^P([1-6])[A-Z]{3}_", filename)
        match_fase = re.search(r"Fase[_ ]*([0-9])", filename)
        fase_or_grade = (
            resolved["grade"]
            if resolved
            else (
                int(match_grade.group(1))
                if match_grade
                else (f"Fase {match_fase.group(1)}" if match_fase else None)
            )
        )
        books.append(
            {
                "filename": filename,
                "book_code": resolved["book_code"]
                if resolved
                else (filename.split("_")[0] if match_grade else None),
                "fase_or_grade": fase_or_grade,
                "conaliteg_year_resolved": resolved["year"]
                if resolved
                else ("2026" if match_grade else None),
                "size_mb": round(os.path.getsize(path) / (1024 * 1024), 2),
                **audit,
                "sha256": sha256_file(path),
                "indexed_at_utc": datetime.datetime.now(
                    datetime.timezone.utc
                ).isoformat(),
            }
        )
    return {
        "school_cycle": SCHOOL_CYCLE,
        "generated_at_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "books": books,
    }


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "codes", nargs="*", help="Claves individuales (ej. P4NSA P4PAA)."
    )
    parser.add_argument(
        "--grade",
        type=int,
        nargs="+",
        choices=range(1, 7),
        help="Grado(s) a descargar (1 a 6).",
    )
    parser.add_argument(
        "--all-grades",
        action="store_true",
        help="Descargar los 30 libros de 1.o a 6.o grado.",
    )
    parser.add_argument(
        "--only-saberes",
        action="store_true",
        help="Descargar solo Nuestros Saberes del grado elegido.",
    )
    parser.add_argument(
        "--audit-only",
        action="store_true",
        help="Solo desbloquear y auditar los PDFs actuales en data/.",
    )
    parser.add_argument("--data-dir", default=str(REPO_ROOT / "data"))
    parser.add_argument(
        "--cache-dir", default=str(REPO_ROOT / "data" / "cache" / "conaliteg")
    )
    parser.add_argument("--manifest", default=str(REPO_ROOT / "data" / "manifest.json"))
    parser.add_argument("--workers", type=int, default=3)
    args = parser.parse_args(argv)

    os.makedirs(args.data_dir, exist_ok=True)
    os.makedirs(args.cache_dir, exist_ok=True)
    passwords = fetch_candidate_passwords()

    codes = list(args.codes)
    if args.all_grades:
        grades = list(range(1, 7))
    elif args.grade:
        grades = args.grade
    else:
        grades = []

    for g in grades:
        if args.only_saberes:
            codes.append(f"P{g}NSA")
        else:
            codes.extend([f"P{g}NSA", f"P{g}PAA", f"P{g}PEA", f"P{g}PCA", f"P{g}MLA"])

    unknown = [c for c in codes if c not in BOOK_CATALOG]
    if unknown:
        log(f"X Claves desconocidas: {unknown}. Soportadas: {sorted(BOOK_CATALOG)}")
        return 2

    codes = sorted(set(codes))
    resolved = []
    if not args.audit_only and codes:
        manifest_map = manifest_sha_map(load_manifest(args.manifest))
        with ThreadPoolExecutor(max_workers=args.workers) as pool:
            futures = {
                pool.submit(
                    download_book,
                    code,
                    manifest_map,
                    args.data_dir,
                    args.cache_dir,
                    passwords,
                ): code
                for code in codes
            }
            for future in as_completed(futures):
                res = future.result()
                if res:
                    resolved.append(res)

    manifest = build_manifest(args.data_dir, resolved, passwords)
    with open(args.manifest, "w", encoding="utf-8") as handle:
        json.dump(manifest, handle, ensure_ascii=False, indent=2)

    log("\n=== RESUMEN DE AUDITORIA (data/manifest.json) ===")
    for b in manifest["books"]:
        log(
            f"- {b['filename']}: {b['page_count']} pags | "
            f"texto nativo: {b['pages_with_native_text']}/{b['page_count']} | "
            f"necesitan OCR: {b['pages_needing_ocr']} | "
            f"imagenes: {b['total_embedded_images']} | {b['size_mb']} MB"
        )
    return 0


if __name__ == "__main__":
    sys.exit(main())