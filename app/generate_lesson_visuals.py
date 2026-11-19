#!/usr/bin/env python3
"""Generador paramétrico de ilustraciones educativas SVG estilo "Mesoamérica Kawaii".

Genera 3 láminas SVG por tema en web/public/lessons/{topicId}/slide-{1,2,3}.svg
y un manifiesto idempotente con hashes SHA-256 en web/public/lessons/manifest.json.
Sin APIs de pago ($0). Personaje guía: Axolotl (kawaii) en la esquina.
Arquetipos: matematicas, etica, ciencias, lenguajes.
Lámina 1 = Concepto visual, 2 = Ejemplo paso a paso, 3 = Reto práctico de hoy.

USO: python app/generate_lesson_visuals.py
"""

import hashlib
import json
import math
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
OUT_DIR = REPO_ROOT / "web" / "public" / "lessons"

# Paleta Mesoamérica Kawaii.
PERGAMINO = "#FDF6E3"
PERGAMINO_2 = "#F5E6C8"
CACAO = "#3D2B1F"
QUETZAL = "#2EC4B6"
ELOTE = "#FFBF69"
AJOLOTE = "#E71D73"
CEMPASUCHIL = "#FF9F1C"

SLIDE_LABELS = {1: "Concepto visual", 2: "Ejemplo paso a paso", 3: "Reto práctico de hoy"}

# (id, título, arquetipo, grado, lámina1, lámina2, lámina3)
# Cada lámina = (encabezado, viñeta1, viñeta2)
TOPICS = [
    ("fase3-lenguajes-letras", "Las letras y sus sonidos", "lenguajes", 1,
     ("Cada letra tiene un sonido", "La 'a' suena /a/ como en abeja", "Al juntar letras formamos palabras"),
     ("Las vocales a-e-i-o-u", "'a' de abeja, 'e' de elefante", "Toca tu boca y siente el sonido"),
     ("Reto: cazador de sonidos", "Busca 3 objetos con la letra de tu nombre", "Di su sonido en voz alta")),
    ("fase3-saberes-numeros", "Contar hasta 20", "matematicas", 1,
     ("Contar es saber cuántas cosas hay", "Cuenta una por una, sin saltarte", "Agrupa de 5 en 5 para ir más rápido"),
     ("Ejemplo: 12 canicas", "5 + 5 + 2 = 12", "Dos grupos de 5 y quedan 2 sueltas"),
     ("Reto: colección de 20", "Reúne 20 objetos favoritos", "Cuéntalos de 5 en 5")),
    ("fase3-saberes-figuras", "Las figuras geométricas", "matematicas", 1,
     ("Las figuras están en todas partes", "El círculo rueda y no tiene esquinas", "El cuadrado y el triángulo sí las tienen"),
     ("Ejemplo: lados y esquinas", "Cuadrado: 4 lados, Triángulo: 3 lados", "Círculo: 0 esquinas, rueda"),
     ("Reto: buscador de figuras", "Encuentra un círculo, un cuadrado y un triángulo", "Dinos dónde los viste")),
    ("fase3-lenguajes-cuentos", "Los cuentos y sus personajes", "lenguajes", 2,
     ("Todo cuento tiene personajes", "Tiene inicio, desarrollo y final", "El personaje vive una aventura"),
     ("Ejemplo: Caperucita Roja", "Inicio: lleva comida a su abuelita", "Desarrollo: aparece el lobo; Final: llega el leñador"),
     ("Reto: tu personaje favorito", "¿Qué problema tiene?", "Cuéntalo en 3 pasos: inicio, desarrollo y final")),
    ("fase3-saberes-sumas", "Sumas con llevadas", "matematicas", 2,
     ("Sumar es juntar cantidades", "Si pasas de 9, usas una 'llevada'", "Guardas una decena y la subes"),
     ("Ejemplo: 27 + 18", "7 + 8 = 15: escribe 5 y lleva 1", "2 + 1 + 1 = 4 → resultado 45"),
     ("Reto: tiendita de juguete", "Suma 36 + 27", "Usa la llevada y di tu resultado")),
    ("fase3-saberes-restas", "Las restas", "matematicas", 2,
     ("Restar es quitar una cantidad", "10 − 4 = 6: quedan 6", "La resta nos dice cuánto queda"),
     ("Ejemplo: mercado de frutas", "9 frutas, se llevan 5", "9 − 5 = 4 frutas quedan"),
     ("Reto: junta y quita", "Junta 12 objetos y quita 5", "Cuenta cuántos quedan")),
    ("fase4-saberes-ciclo-agua", "El ciclo del agua", "ciencias", 3,
     ("El agua viaja sin parar", "El Sol la evapora y sube", "Forma nubes y cae como lluvia"),
     ("Ejemplo: la gotita viajera", "Se evapora del mar, forma nube", "Llueve, baja por el río y vuelve al mar"),
     ("Reto: observa el agua", "Busca vapor, rocío o lluvia hoy", "Di en qué etapa del ciclo está")),
    ("fase4-lenguajes-noticias", "La noticia y sus partes", "lenguajes", 3,
     ("Una noticia cuenta algo importante", "Responde qué, quién, cuándo y dónde", "Titular atrapa, cuerpo da detalles"),
     ("Ejemplo: noticia escolar", "Titular: '4.º A sembró un huerto'", "Cuerpo: cuándo, dónde y qué plantaron"),
     ("Reto: escribe tu noticia", "Elige un hecho de tu día", "Ponle titular y cuerpo")),
    ("fase4-saberes-multiplicacion", "La multiplicación", "matematicas", 3,
     ("Multiplicar es sumar grupos iguales", "4 grupos de 3 = 4 × 3 = 12", "Es una suma repetida y rápida"),
     ("Ejemplo: 5 cajas de 6 lápices", "5 × 6 = 30 lápices", "Cuenta por grupos y comprueba"),
     ("Reto: arma grupos iguales", "3 grupos de 4 fichas", "Escribe la multiplicación")),
    ("fase4-saberes-repartos", "Repartos y fracciones equivalentes", "matematicas", 4,
     ("Repartir justo es la clave", "1/4 es dividir en 4 y tomar 1", "2/8 equivale a 1/4"),
     ("Ejemplo: 8 dulces entre 4", "Cada quien recibe 2 dulces = 2/8", "2/8 = 1/4: mismo reparto, dos formas"),
     ("Reto: fiesta de repartos", "Reparte 8 dibujos entre 4 personas", "Comprueba que 2/8 = 1/4")),
    ("fase4-saberes-fracciones", "Las fracciones", "matematicas", 4,
     ("Una fracción divide un todo en partes iguales", "Abajo: en cuántas partes; arriba: cuántas tomas", "2/4 = media pizza"),
     ("Ejemplo: 2/4 vs 3/8", "2/4 son 2 rebanadas de 4", "2/4 es mayor que 3/8"),
     ("Reto: dobla y compara", "Dobla papel en 4 y colorea 2", "Compara 2/4 con 3/8")),
    ("fase4-etica-derechos", "Los derechos de la niñez", "etica", 4,
     ("Todas y todos tenemos derechos", "Salud, educación, juego y familia", "Nos protegen y nos ayudan a crecer"),
     ("Ejemplo: tus derechos hoy", "Ir a la escuela es tu derecho a la educación", "Jugar y descansar también son derechos"),
     ("Reto: mural de derechos", "Elige un derecho que uses hoy", "Dibújalo y di por qué importa")),
    ("fase4-saberes-nutricion", "La nutrición y el Plato del Bien Comer", "ciencias", 4,
     ("Nuestro cuerpo necesita nutrirse", "El Plato del Bien Comer tiene 3 grupos", "Verduras, cereales y leguminosas"),
     ("Ejemplo: un desayuno saludable", "Fruta + tortilla + huevo", "Un alimento de cada grupo"),
     ("Reto: dibuja tu plato de hoy", "Marca el grupo de cada alimento", "Di qué grupo te faltó")),
    ("fase4-saberes-movimiento", "El movimiento y la fuerza", "ciencias", 4,
     ("Los objetos están en reposo o en movimiento", "Hace falta una fuerza para moverse", "La velocidad dice qué tan rápido"),
     ("Ejemplo: la pelota", "En reposo hasta que la pateas", "Más fuerza = más rápido"),
     ("Reto: observa 3 objetos", "¿Reposo o movimiento?", "Di qué fuerza los mueve")),
    ("fase4-lenguajes-instructivos", "Manuales e instructivos", "lenguajes", 4,
     ("Un instructivo explica paso a paso", "Usa verbos en orden y frases cortas", "Cualquiera puede seguirlo"),
     ("Ejemplo: avión de papel", "1) dobla, 2) esquinas, 3) lanza", "Cada paso empieza con una acción"),
     ("Reto: escribe 3 pasos", "De tu juego favorito", "Que Axolotl los revise")),
    ("fase4-saberes-areas", "Áreas y perímetros", "matematicas", 4,
     ("El perímetro mide el contorno", "El área mide la superficie", "Son dos medidas distintas"),
     ("Ejemplo: rectángulo 4 x 3", "Perímetro 4+3+4+3 = 14", "Área 4 × 3 = 12"),
     ("Reto: mide un cuarto", "Con tus pasos", "Calcula perímetro y área")),
    ("fase4-etica-ecosistemas", "Ecosistemas de México", "ciencias", 4,
     ("Un ecosistema une seres vivos y lugar", "México tiene gran biodiversidad", "Selvas, bosques, mares y desiertos"),
     ("Ejemplo: el bosque", "Árboles, aves e insectos", "Contaminar el agua afecta a todos"),
     ("Reto: cuida tu ecosistema", "Una acción: no tirar basura", "Cuéntale tu plan a Axolotl")),
    ("fase4-lenguajes-leyendas", "Leyendas de mi comunidad", "lenguajes", 4,
     ("Las leyendas mezclan lo real y lo fantástico", "Se cuentan de boca en boca", "Explican el origen de algo"),
     ("Ejemplo: la leyenda mexica", "Peregrinaron dos siglos", "Hasta hallar el águila sobre el nopal"),
     ("Reto: escucha una leyenda", "De tu comunidad o familia", "Escríbela con inicio, desarrollo y final")),
    ("fase4-etica-paz", "Cultura de paz", "etica", 4,
     ("La paz se construye con diálogo", "Resolvemos sin gritos ni golpes", "Con acuerdos de convivencia"),
     ("Ejemplo: el mismo juego", "Acuerdan turnos: primero uno, luego otro", "Así todos ganan"),
     ("Reto: escribe 2 acuerdos", "Para tu salón o tu casa", "Di cómo ayudan a convivir")),
    ("fase4-saberes-graficas", "Registro de datos y gráficas", "matematicas", 4,
     ("Organizamos datos en tablas y gráficas", "Las barras comparan cantidades", "La más alta es la ganadora"),
     ("Ejemplo: fruta favorita", "10 votos vs 7 votos", "La barra alta gana"),
     ("Reto: pregunta a 5 personas", "Su color favorito", "Dibuja tu gráfica de barras")),
    ("fase4-humano-salud", "Hábitos saludables y emociones", "ciencias", 4,
     ("Cuidar cuerpo y emociones nos hace sentir bien", "Lavarse las manos y dormir bien", "Expresar lo que sentimos"),
     ("Ejemplo: antes de comer", "Lavarse las manos evita enfermedades", "Decir lo que sientes sin gritar"),
     ("Reto: tu rutina de bienestar", "Higiene + una emoción", "Compártela con Axolotl")),
    ("fase5-saberes-energia", "La energía y sus formas", "ciencias", 5,
     ("La energía hace funcionar las cosas", "Puede ser luz, calor, movimiento o sonido", "Siempre se transforma"),
     ("Ejemplo: el foco", "La electricidad se vuelve luz y calor", "Pedalear convierte tu energía en movimiento"),
     ("Reto: rastrea la energía", "Elige 3 cosas que uses hoy", "Di de dónde viene su energía")),
    ("fase5-lenguajes-poesia", "La poesía y sus recursos", "lenguajes", 5,
     ("La poesía juega con las palabras", "Usa rima, ritmo y metáforas", "Una metáfora compara sin decir 'como'"),
     ("Ejemplo: metáfora", "'El sol es una moneda de oro'", "La rima repite sonidos: luna… cuna"),
     ("Reto: tu poema", "Escribe dos versos con rima", "Añade una metáfora")),
    ("fase5-saberes-decimales", "Los números decimales", "matematicas", 5,
     ("Los decimales son partes de un entero", "0.50 es la mitad de un peso", "Un peso tiene 100 centavos"),
     ("Ejemplo: el vuelto", "Lápiz $7.50, pagas $10", "10.00 − 7.50 = 2.50 de vuelto"),
     ("Reto: caja registradora", "Suma $5.25 + $3.75", "Calcula el vuelto de $10")),
    ("fase5-saberes-porcentajes", "Los porcentajes", "matematicas", 6,
     ("Un porcentaje es una fracción de 100", "20% = 20 de cada 100", "Nos ayuda a comparar partes"),
     ("Ejemplo: 20% de descuento", "$100 con 20% descuento ahorras $20", "Pagas $80 (20% = 1/5)"),
     ("Reto: tienda de descuentos", "Calcula 10% de $50", "Di cuánto pagas")),
    ("fase5-etica-consumo", "Consumo responsable", "etica", 6,
     ("Consumir con responsabilidad es pensar antes de comprar", "¿Lo necesito o solo lo quiero?", "Reutilizar y reciclar cuida el planeta"),
     ("Ejemplo: antes de comprar", "Repara o intercambia el juguete", "Así gastas menos y tiras menos basura"),
     ("Reto: revisa un producto", "¿Lo necesitas de verdad?", "¿Puedes reutilizarlo o reciclarlo?")),
    ("fase5-saberes-sistema-solar", "El sistema solar", "ciencias", 6,
     ("El Sol y los planetas que lo rodean", "Mercurio, Venus, Tierra, Marte…", "…Júpiter, Saturno, Urano y Neptuno"),
     ("Ejemplo: la Tierra", "Tercer planeta desde el Sol", "Júpiter es el más grande; Saturno tiene anillos"),
     ("Reto: maqueta solar", "Ordena los 8 planetas desde el Sol", "Elige tu favorito y di su posición")),
]


def esc(text):
    return (
        str(text)
        .replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
        .replace('"', "&quot;")
    )


def sha256_of_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def axolotl_badge(cx, cy, s=1.0):
    """Badge kawaii de Axolotl (el guía visual de la plataforma)."""
    parts = []
    for side in (-1, 1):
        for i in range(3):
            gx = cx + side * (22 * s + i * 4)
            gy = cy - 12 * s + i * 12 * s
            parts.append(f'<ellipse cx="{gx}" cy="{gy}" rx="{12 * s}" ry="{5 * s}" fill="{AJOLOTE}" stroke="{CACAO}" stroke-width="2"/>')
    parts.append(f'<ellipse cx="{cx}" cy="{cy}" rx="{28 * s}" ry="{24 * s}" fill="{AJOLOTE}" stroke="{CACAO}" stroke-width="3"/>')
    parts.append(f'<ellipse cx="{cx}" cy="{cy + 10 * s}" rx="{16 * s}" ry="{9 * s}" fill="#F9A8C4"/>')
    parts.append(f'<circle cx="{cx - 10 * s}" cy="{cy - 5 * s}" r="{4.5 * s}" fill="{CACAO}"/>')
    parts.append(f'<circle cx="{cx + 10 * s}" cy="{cy - 5 * s}" r="{4.5 * s}" fill="{CACAO}"/>')
    parts.append(f'<path d="M {cx - 8 * s} {cy + 7 * s} Q {cx} {cy + 15 * s} {cx + 8 * s} {cy + 7 * s}" fill="none" stroke="{CACAO}" stroke-width="2.5" stroke-linecap="round"/>')
    parts.append(f'<circle cx="{cx - 16 * s}" cy="{cy + 3 * s}" r="{4 * s}" fill="#F9A8C4"/>')
    parts.append(f'<circle cx="{cx + 16 * s}" cy="{cy + 3 * s}" r="{4 * s}" fill="#F9A8C4"/>')
    return "".join(parts)


def grecas(y, step=44):
    out = []
    x = 40
    i = 0
    while x < 920:
        color = QUETZAL if i % 2 == 0 else CEMPASUCHIL
        out.append(f'<polygon points="{x},{y} {x + step / 2},{y - 9} {x + step},{y} {x + step / 2},{y + 9}" fill="{color}" opacity="0.75"/>')
        x += step
        i += 1
    return "".join(out)


def wrapped_lines(x, y, text, size, weight, fill, max_chars, line_h):
    words = str(text).split()
    lines, cur = [], ""
    for w in words:
        candidate = (cur + " " + w).strip()
        if (len(candidate) <= max_chars) or not cur:
            cur = candidate
        else:
            lines.append(cur)
            cur = w
    if cur:
        lines.append(cur)
    out = []
    for i, ln in enumerate(lines):
        out.append(f'<text x="{x}" y="{y + i * line_h}" font-size="{size}" font-weight="{weight}" fill="{fill}">{esc(ln)}</text>')
    return "".join(out), len(lines)


def text_block(headline, bullets, x=64, y=196):
    out, _ = wrapped_lines(x, y, headline, 24, "800", CACAO, 34, 30)
    yy = y + 52
    for b in bullets:
        out += f'<circle cx="{x + 11}" cy="{yy - 8}" r="5" fill="{CEMPASUCHIL}"/>'
        lines, n = wrapped_lines(x + 28, yy, b, 18, "400", CACAO, 42, 26)
        out += lines
        yy += n * 26 + 10
    return out


def frac_circle(cx, cy, r, n, k, color):
    out = []
    for i in range(n):
        a0 = -90 + i * (360 / n)
        a1 = a0 + (360 / n)
        x0 = cx + r * math.cos(math.radians(a0))
        y0 = cy + r * math.sin(math.radians(a0))
        x1 = cx + r * math.cos(math.radians(a1))
        y1 = cy + r * math.sin(math.radians(a1))
        large = 1 if (360 / n) > 180 else 0
        fill = color if i < k else "#FFFFFF"
        out.append(f'<path d="M {cx} {cy} L {x0:.1f} {y0:.1f} A {r} {r} 0 {large} 1 {x1:.1f} {y1:.1f} Z" fill="{fill}" stroke="{CACAO}" stroke-width="2"/>')
    return "".join(out)


MATH_TAG = {
    "fase3-saberes-numeros": "5 + 5 + 5 + 5 = 20",
    "fase3-saberes-figuras": "4 lados · 3 lados · 0",
    "fase3-saberes-sumas": "27 + 18 = 45",
    "fase3-saberes-restas": "10 − 4 = 6",
    "fase4-saberes-multiplicacion": "4 × 3 = 12",
    "fase5-saberes-decimales": "10.00 − 7.50 = 2.50",
    "fase5-saberes-porcentajes": "20% de 100 = 20",
}


def draw_math(topic_id):
    x0, y0 = 560, 205
    if topic_id == "fase4-saberes-repartos":
        out = []
        for g in range(4):
            gx = x0 + g * 68
            for r in range(2):
                out.append(f'<circle cx="{gx}" cy="{y0 + 25 + r * 44}" r="15" fill="{ELOTE}" stroke="{CACAO}" stroke-width="2.5"/>')
        out.append(f'<text x="{x0}" y="{y0 + 135}" font-size="26" font-weight="800" fill="{AJOLOTE}">2/8 = 1/4</text>')
        out.append(f'<text x="{x0}" y="{y0 + 165}" font-size="16" fill="{CACAO}">4 grupos de 2 dulces</text>')
        return "".join(out)
    if topic_id == "fase4-saberes-fracciones":
        out = [frac_circle(x0, y0 + 55, 62, 4, 2, QUETZAL), frac_circle(x0 + 175, y0 + 55, 62, 8, 3, ELOTE)]
        out.append(f'<text x="{x0 + 18}" y="{y0 + 140}" font-size="22" font-weight="800" fill="{CACAO}">2/4</text>')
        out.append(f'<text x="{x0 + 193}" y="{y0 + 140}" font-size="22" font-weight="800" fill="{CACAO}">3/8</text>')
        out.append(f'<text x="{x0 + 40}" y="{y0 + 175}" font-size="19" font-weight="800" fill="{AJOLOTE}">2/4 es mayor</text>')
        return "".join(out)
    out = []
    for g in range(3):
        gx = x0 + g * 92
        for r in range(3):
            out.append(f'<circle cx="{gx}" cy="{y0 + 30 + r * 38}" r="12" fill="{QUETZAL}" stroke="{CACAO}" stroke-width="2"/>')
    tag = MATH_TAG.get(topic_id, "grupos iguales")
    out.append(f'<text x="{x0}" y="{y0 + 165}" font-size="22" font-weight="800" fill="{CACAO}">{esc(tag)}</text>')
    return "".join(out)


def draw_etica(topic_id):
    x0, y0 = 600, 210
    out = []
    if topic_id == "fase4-etica-derechos":
        out.append(f'<path d="M {x0} {y0} L {x0 + 170} {y0} L {x0 + 170} {y0 + 60} Q {x0 + 85} {y0 + 150} {x0} {y0 + 60} Z" fill="{ELOTE}" stroke="{CACAO}" stroke-width="3"/>')
        icons = [("+", QUETZAL), ("A", CEMPASUCHIL), ("O", AJOLOTE), ("F", QUETZAL)]
        for i, (sym, color) in enumerate(icons):
            ix = x0 + 30 + (i % 2) * 90
            iy = y0 + 45 + (i // 2) * 65
            out.append(f'<circle cx="{ix}" cy="{iy}" r="20" fill="{color}" stroke="{CACAO}" stroke-width="2.5"/>')
            out.append(f'<text x="{ix}" y="{iy + 8}" font-size="20" font-weight="800" fill="{CACAO}" text-anchor="middle">{sym}</text>')
        out.append(f'<text x="{x0 + 85}" y="{y0 + 200}" font-size="18" font-weight="800" fill="{AJOLOTE}" text-anchor="middle">Tus derechos te protegen</text>')
        return "".join(out)
    if topic_id == "fase4-etica-paz":
        out.append(f'<circle cx="{x0 + 20}" cy="{y0 + 30}" r="16" fill="{QUETZAL}" stroke="{CACAO}" stroke-width="2.5"/>')
        out.append(f'<circle cx="{x0 + 150}" cy="{y0 + 30}" r="16" fill="{CEMPASUCHIL}" stroke="{CACAO}" stroke-width="2.5"/>')
        out.append(f'<path d="M {x0 + 40} {y0 + 55} Q {x0 + 85} {y0 + 80} {x0 + 130} {y0 + 55}" fill="none" stroke="{CACAO}" stroke-width="3"/>')
        out.append(f'<text x="{x0 + 85}" y="{y0 + 105}" font-size="26" fill="{AJOLOTE}" text-anchor="middle">♥</text>')
        out.append(f'<text x="{x0 + 85}" y="{y0 + 135}" font-size="18" font-weight="800" fill="{CACAO}" text-anchor="middle">Diálogo y acuerdos</text>')
        return "".join(out)
    out.append(f'<path d="M {x0 + 85} {y0 - 10} L {x0 + 150} {y0 + 95} L {x0 + 20} {y0 + 95} Z" fill="none" stroke="{QUETZAL}" stroke-width="6"/>')
    out.append(f'<text x="{x0 + 85}" y="{y0 + 60}" font-size="40" font-weight="800" fill="{CACAO}" text-anchor="middle">?</text>')
    out.append(f'<text x="{x0 + 85}" y="{y0 + 140}" font-size="19" font-weight="800" fill="{CACAO}" text-anchor="middle">¿Lo necesito o lo quiero?</text>')
    return "".join(out)


def draw_ciencias(topic_id):
    x0, y0 = 585, 210
    out = []
    if topic_id == "fase5-saberes-sistema-solar":
        sun_x, sun_y = x0 + 60, y0 + 60
        out.append(f'<circle cx="{sun_x}" cy="{sun_y}" r="30" fill="{ELOTE}" stroke="{CACAO}" stroke-width="3"/>')
        sizes = [12, 10, 14, 10, 20, 18, 14, 12]
        colors = [QUETZAL, CEMPASUCHIL, QUETZAL, AJOLOTE, CEMPASUCHIL, ELOTE, QUETZAL, AJOLOTE]
        for i in range(8):
            px = sun_x + 40 + i * 24
            out.append(f'<circle cx="{px}" cy="{sun_y}" r="{sizes[i]}" fill="{colors[i]}" stroke="{CACAO}" stroke-width="2"/>')
        out.append(f'<text x="{sun_x + 90}" y="{y0 + 150}" font-size="18" font-weight="800" fill="{CACAO}" text-anchor="middle">El Sol y los 8 planetas</text>')
        return "".join(out)
    labels = {"fase4-saberes-ciclo-agua": ["Evapora", "Nube", "Lluvia", "Río/mar"],
              "fase5-saberes-energia": ["Luz", "Calor", "Movimiento", "Sonido"],
              "fase4-saberes-nutricion": ["Verduras", "Cereales", "Leguminosas", "Agua"],
              "fase4-saberes-movimiento": ["Reposo", "Fuerza", "Movimiento", "Velocidad"],
              "fase4-etica-ecosistemas": ["Seres vivos", "Agua", "Suelo", "Aire"],
              "fase4-humano-salud": ["Higiene", "Sueño", "Emociones", "Alimentación"]}.get(topic_id, ["Etapa 1", "Etapa 2", "Etapa 3", "Etapa 4"])
    for i in range(4):
        cx = x0 + (i % 2) * 130
        cy = y0 + 40 + (i // 2) * 110
        out.append(f'<circle cx="{cx}" cy="{cy}" r="30" fill="{QUETZAL if i % 2 == 0 else ELOTE}" stroke="{CACAO}" stroke-width="3"/>')
        out.append(f'<text x="{cx}" y="{cy + 8}" font-size="22" font-weight="800" fill="{CACAO}" text-anchor="middle">{i + 1}</text>')
        out.append(f'<text x="{cx}" y="{cy + 50}" font-size="16" font-weight="700" fill="{CACAO}" text-anchor="middle">{labels[i]}</text>')
    out.append(f'<path d="M {x0 + 30} {y0 + 70} Q {x0 + 65} {y0 + 40} {x0 + 100} {y0 + 70}" fill="none" stroke="{CACAO}" stroke-width="3"/>')
    out.append(f'<path d="M {x0 + 160} {y0 + 70} Q {x0 + 195} {y0 + 100} {x0 + 160} {y0 + 150}" fill="none" stroke="{CACAO}" stroke-width="3"/>')
    return "".join(out)


def draw_lenguajes(topic_id):
    x0, y0 = 570, 210
    out = []
    if topic_id == "fase3-lenguajes-letras":
        for i, letter in enumerate("AEIOU"):
            out.append(f'<text x="{x0 + i * 55}" y="{y0 + 80}" font-size="46" font-weight="800" fill="{QUETZAL if i % 2 == 0 else CEMPASUCHIL}">{letter}</text>')
        out.append(f'<text x="{x0}" y="{y0 + 120}" font-size="17" fill="{CACAO}">Cinco vocales, cinco sonidos</text>')
        return "".join(out)
    if topic_id in ("fase3-lenguajes-cuentos", "fase4-lenguajes-leyendas"):
        boxes = [("Inicio", QUETZAL), ("Desarrollo", ELOTE), ("Final", CEMPASUCHIL)]
        for i, (label, color) in enumerate(boxes):
            bx = x0 + i * 100
            out.append(f'<rect x="{bx}" y="{y0}" width="86" height="60" rx="12" fill="{color}" stroke="{CACAO}" stroke-width="3"/>')
            out.append(f'<text x="{bx + 43}" y="{y0 + 38}" font-size="16" font-weight="800" fill="{CACAO}" text-anchor="middle">{label}</text>')
            if i < 2:
                out.append(f'<path d="M {bx + 86} {y0 + 30} L {bx + 100} {y0 + 30}" stroke="{CACAO}" stroke-width="3"/>')
        return "".join(out)
    if topic_id == "fase4-lenguajes-instructivos":
        for i in range(3):
            bx = x0 + i * 85
            out.append(f'<rect x="{bx}" y="{y0}" width="70" height="60" rx="12" fill="{QUETZAL if i % 2 == 0 else CEMPASUCHIL}" stroke="{CACAO}" stroke-width="3"/>')
            out.append(f'<text x="{bx + 35}" y="{y0 + 38}" font-size="24" font-weight="800" fill="{CACAO}" text-anchor="middle">{i + 1}</text>')
            if i < 2:
                out.append(f'<path d="M {bx + 70} {y0 + 30} L {bx + 85} {y0 + 30}" stroke="{CACAO}" stroke-width="3"/>')
        out.append(f'<text x="{x0 + 40}" y="{y0 + 90}" font-size="17" fill="{CACAO}" text-anchor="middle">Pasos en orden</text>')
        return "".join(out)
    if topic_id == "fase4-lenguajes-noticias":
        out.append(f'<rect x="{x0}" y="{y0}" width="200" height="46" rx="10" fill="{QUETZAL}" stroke="{CACAO}" stroke-width="3"/>')
        out.append(f'<text x="{x0 + 100}" y="{y0 + 30}" font-size="17" font-weight="800" fill="{CACAO}" text-anchor="middle">Titular</text>')
        out.append(f'<rect x="{x0}" y="{y0 + 60}" width="200" height="70" rx="10" fill="#FFFFFF" stroke="{CACAO}" stroke-width="3"/>')
        out.append(f'<text x="{x0 + 100}" y="{y0 + 95}" font-size="15" fill="{CACAO}" text-anchor="middle">Cuerpo: qué, quién, cuándo, dónde</text>')
        return "".join(out)
    out.append(f'<rect x="{x0}" y="{y0}" width="120" height="90" rx="10" fill="{CEMPASUCHIL}" stroke="{CACAO}" stroke-width="3"/>')
    out.append(f'<text x="{x0 + 60}" y="{y0 + 50}" font-size="20" font-weight="800" fill="{CACAO}" text-anchor="middle">Poema</text>')
    out.append(f'<text x="{x0 + 150}" y="{y0 + 40}" font-size="17" fill="{CACAO}">"El sol es una</text>')
    out.append(f'<text x="{x0 + 150}" y="{y0 + 70}" font-size="17" fill="{CACAO}">moneda de oro"</text>')
    out.append(f'<text x="{x0 + 150}" y="{y0 + 105}" font-size="16" fill="{AJOLOTE}">rima + metáfora</text>')
    return "".join(out)


ARCHETYPES = {
    "matematicas": draw_math,
    "etica": draw_etica,
    "ciencias": draw_ciencias,
    "lenguajes": draw_lenguajes,
}


def build_slide(topic_id, title, archetype, slide_num, slide):
    headline, b1, b2 = slide
    content = text_block(headline, [b1, b2]) + ARCHETYPES[archetype](topic_id)
    return (
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 960 540" font-family="Nunito, Balsamiq Sans, Comic Sans MS, sans-serif">\n'
        f'<rect width="960" height="540" fill="{PERGAMINO}"/>\n'
        f'<rect x="24" y="24" width="912" height="492" rx="28" fill="{PERGAMINO_2}" stroke="{CACAO}" stroke-width="3"/>\n'
        f'{grecas(60)}\n{grecas(500)}\n'
        f'{axolotl_badge(878, 96, 1.0)}\n'
        f'<text x="64" y="104" font-size="38" font-weight="800" fill="{CACAO}">{esc(title)}</text>\n'
        f'<text x="64" y="142" font-size="20" font-weight="700" fill="{AJOLOTE}">Lámina {slide_num} · {esc(SLIDE_LABELS[slide_num])}</text>\n'
        f'{content}\n'
        f'</svg>\n'
    )


def main():
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    manifest = {"lessons": {}}
    generated, skipped = 0, 0
    for topic_id, title, archetype, grade, s1, s2, s3 in TOPICS:
        folder = OUT_DIR / topic_id
        folder.mkdir(parents=True, exist_ok=True)
        files = {}
        for slide_num, slide in ((1, s1), (2, s2), (3, s3)):
            svg = build_slide(topic_id, title, archetype, slide_num, slide)
            data = svg.encode("utf-8")
            digest = sha256_of_bytes(data)
            path = folder / f"slide-{slide_num}.svg"
            if path.exists() and hashlib.sha256(path.read_bytes()).hexdigest() == digest:
                skipped += 1
            else:
                path.write_text(svg, encoding="utf-8")
                generated += 1
            files[f"slide-{slide_num}.svg"] = digest
        manifest["lessons"][topic_id] = {"title": title, "grade": grade, "files": files}

    (OUT_DIR / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Listo: {generated} SVG nuevos, {skipped} sin cambios, {len(TOPICS)} temas.")


if __name__ == "__main__":
    main()




