using HomeschoolAI.Application.Learning;
using HomeschoolAI.Domain.Progress;

namespace HomeschoolAI.Adapters.Learning;

/// <summary>Tema semilla del catálogo curricular.</summary>
public sealed record SeedTopic(
    int Grade,
    string TopicId,
    string Title,
    string CampoFormativo,
    string SearchQuery,
    string WeeklyProjectTitle,
    string WeeklyProjectGoal,
    IReadOnlyList<LessonBlockDto> Blocks);

/// <summary>
/// Catálogo curricular semilla (SEP, Grados 1 a 6). Proporciona el siguiente tema
/// para un grado dado y construye el <see cref="ActiveLessonDto"/> correspondiente.
/// </summary>
public static class SeedCurriculumCatalog
{
    private const string SchemaVersion = "1.0";

    private static readonly IReadOnlyList<SeedTopic> Topics = new List<SeedTopic>
    {
        // Grado 1 (Fase 3)
        new(1, "fase3-lenguajes-letras", "Las letras y sus sonidos", "Lenguajes",
            "conciencia fonológica letras y sonidos primer grado primaria SEP",
            "Mi abecedario de casa", "Buscar objetos de casa que empiecen con cada letra y armar un abecedario ilustrado.",
            Blocks(
                ("text", "Hoy descubriremos que cada letra tiene un sonido especial que nos ayuda a formar palabras.", "", "Cada letra tiene su propio sonido, como una clave secreta para leer."),
                ("image", "/assets/lessons/letras-sonidos.svg", "Letras con dibujos de animales", "Observa las letras y los dibujos que empiezan con cada sonido."),
                ("text", "Jugaremos a escuchar el primer sonido de las palabras, como la 'a' de 'abeja'.", "", "Escucha el primer sonido de cada palabra y repítelo conmigo."))),
        new(1, "fase3-saberes-numeros", "Contar hasta 20", "Saberes y Pensamiento Científico",
            "conteo números del 1 al 20 primer grado primaria SEP",
            "Mi colección de 20 cosas", "Reunir y contar 20 objetos favoritos agrupándolos de 5 en 5.",
            Blocks(
                ("text", "Vamos a contar del 1 al 20 para entender cuántas cosas tenemos.", "", "Contar nos ayuda a saber cuántas cosas hay sin equivocarnos."),
                ("image", "/assets/lessons/contar-20.svg", "Grupos de objetos para contar", "Mira los grupos de objetos y cuenta conmigo."),
                ("text", "Agruparemos de 5 en 5 para contar más rápido y sin perdernos.", "", "Contar de 5 en 5 es un atajo genial para números grandes."))),
        // Grado 2 (Fase 3)
        new(2, "fase3-lenguajes-cuentos", "Los cuentos y sus personajes", "Lenguajes",
            "cuento personajes inicio desarrollo final segundo grado primaria SEP",
            "Mi cuento ilustrado", "Escribir e ilustrar un cuento corto con un personaje favorito.",
            Blocks(
                ("text", "Un cuento tiene personajes y una historia con inicio, desarrollo y final.", "", "Todo cuento tiene personajes que viven una aventura."),
                ("image", "/assets/lessons/cuentos-personajes.svg", "Personajes de cuento", "Mira a los personajes e imagina su aventura."),
                ("text", "Piensa en tu personaje favorito: ¿qué problema tiene y cómo lo resuelve?", "", "Cada personaje tiene un reto que debe resolver."))),
        new(2, "fase3-saberes-sumas", "Sumas con llevadas", "Saberes y Pensamiento Científico",
            "suma con llevadas segundo grado primaria SEP",
            "La tiendita de juguete", "Jugar a comprar y vender sumando precios con llevadas.",
            Blocks(
                ("text", "Cuando sumamos y pasamos de 9, usamos una 'llevada' para no perdernos.", "", "La llevada es un empujoncito que ayuda a sumar números grandes."),
                ("image", "/assets/lessons/sumas-llevadas.svg", "Bloques de unidades y decenas", "Observa cómo las unidades forman una decena."),
                ("text", "Practicaremos sumas en la tiendita para ver cómo se usan las llevadas.", "", "Sumar con llevadas es como juntar monedas para pagar."))),
        // Grado 3 (Fase 4)
        new(3, "fase4-saberes-ciclo-agua", "El ciclo del agua", "Saberes y Pensamiento Científico",
            "ciclo del agua evaporación condensación precipitación SEP primaria",
            "Mi diario del agua", "Observar durante la semana dónde aparece agua y registrar el ciclo.",
            Blocks(
                ("text", "El agua viaja por la Tierra: se evapora, forma nubes y regresa como lluvia.", "", "El agua es una gran viajera que nunca se queda quieta."),
                ("image", "/assets/lessons/ciclo-agua.svg", "Diagrama del ciclo del agua", "Mira cómo el agua sube, se junta y vuelve a caer."),
                ("text", "Hoy seguiremos el viaje de una gotita de agua por el cielo y la tierra.", "", "Acompaña a la gotita en su aventura por el ciclo del agua."))),
        new(3, "fase4-lenguajes-noticias", "La noticia y sus partes", "Lenguajes",
            "la noticia titular cuerpo periodístico tercer grado primaria SEP",
            "Mi periódico escolar", "Escribir una noticia real del salón con titular y cuerpo.",
            Blocks(
                ("text", "Una noticia responde qué pasó, a quién, cuándo y dónde.", "", "Una noticia cuenta algo importante que acaba de pasar."),
                ("image", "/assets/lessons/noticia-partes.svg", "Estructura de una noticia", "Mira el titular y el cuerpo de la noticia."),
                ("text", "Vamos a construir una noticia con titular llamativo y cuerpo claro.", "", "El titular atrapa y el cuerpo cuenta los detalles."))),
        // Grado 4 (Fase 4)
        new(4, "fase4-saberes-fracciones", "Las fracciones", "Saberes y Pensamiento Científico",
            "fracciones equivalentes cuarto grado primaria SEP",
            "La pizza fraccionaria", "Repartir una pizza (o papel) en partes iguales y comparar fracciones.",
            Blocks(
                ("text", "Una fracción representa partes iguales de un todo, como rebanadas de pizza.", "", "Una fracción es una forma justa de repartir un todo."),
                ("image", "/assets/lessons/fracciones.svg", "Círculos divididos en fracciones", "Observa cómo se divide un círculo en partes iguales."),
                ("text", "Descubriremos que 1/2 y 2/4 son la misma cantidad aunque se vean distinto.", "", "Algunas fracciones diferentes representan la misma cantidad."))),
        new(4, "fase4-etica-derechos", "Los derechos de la niñez", "Ética, Naturaleza y Sociedades",
            "derechos de las niñas y los niños cuarto grado primaria SEP",
            "El mural de nuestros derechos", "Dibujar y exponer un derecho de la niñez con un ejemplo cotidiano.",
            Blocks(
                ("text", "Todas las niñas y los niños tenemos derechos que nos protegen.", "", "Los derechos nos cuidan y nos ayudan a crecer felices."),
                ("image", "/assets/lessons/derechos-ninez.svg", "Niños y niñas conviviendo", "Mira a las niñas y los niños ejerciendo sus derechos."),
                ("text", "Piensa en un derecho que usas todos los días, como jugar o ir a la escuela.", "", "Cada día vives tus derechos sin darte cuenta."))),
        // Grado 5 (Fase 5)
        new(5, "fase5-saberes-energia", "La energía y sus formas", "Saberes y Pensamiento Científico",
            "formas de energía quinto grado primaria SEP",
            "Mi invento con energía", "Diseñar un juguete o invento que use una forma de energía.",
            Blocks(
                ("text", "La energía puede ser luz, calor, movimiento o sonido, y se transforma.", "", "La energía cambia de forma para hacer funcionar las cosas."),
                ("image", "/assets/lessons/energia-formas.svg", "Formas de energía", "Observa las distintas formas de energía a tu alrededor."),
                ("text", "Hoy rastrearemos de dónde viene la energía de las cosas que usamos.", "", "Todo lo que se mueve o brilla usa alguna energía."))),
        new(5, "fase5-lenguajes-poesia", "La poesía y sus recursos", "Lenguajes",
            "poesía rima metáfora quinto grado primaria SEP",
            "Mi antología poética", "Escribir tres poemas cortos usando rima y una metáfora.",
            Blocks(
                ("text", "La poesía juega con las palabras usando rima, ritmo y metáforas.", "", "La poesía es como música hecha con palabras."),
                ("image", "/assets/lessons/poesia-recursos.svg", "Libro de poemas ilustrado", "Mira cómo las palabras crean imágenes en un poema."),
                ("text", "Descubriremos cómo una metáfora compara dos cosas para crear una imagen.", "", "Una metáfora pinta con palabras algo que no es literal."))),
        // Grado 6 (Fase 5)
        new(6, "fase5-saberes-porcentajes", "Los porcentajes", "Saberes y Pensamiento Científico",
            "porcentaje descuento sexto grado primaria SEP",
            "La tienda de descuentos", "Calcular descuentos reales de productos y hacer un folleto de ofertas.",
            Blocks(
                ("text", "Un porcentaje es una fracción de 100, como un descuento del 20%.", "", "El porcentaje nos dice cuánto de cada 100 estamos tomando."),
                ("image", "/assets/lessons/porcentajes.svg", "Etiquetas de descuento", "Observa cómo se aplican los descuentos en las etiquetas."),
                ("text", "Practicaremos calculando cuánto pagamos cuando hay un descuento.", "", "Con el porcentaje sabes cuánto ahorras en una compra."))),
        new(6, "fase5-etica-consumo", "Consumo responsable", "Ética, Naturaleza y Sociedades",
            "consumo responsable recursos sexto grado primaria SEP",
            "Mi guía de consumo responsable", "Elaborar una guía familiar para comprar y usar con responsabilidad.",
            Blocks(
                ("text", "Consumir con responsabilidad es pensar antes de comprar y cuidar los recursos.", "", "Comprar con conciencia cuida el planeta y nuestro dinero."),
                ("image", "/assets/lessons/consumo-responsable.svg", "Familia comprando con conciencia", "Mira cómo una familia elige productos con responsabilidad."),
                ("text", "Hoy pensaremos qué necesitamos de verdad antes de comprar algo.", "", "Pregúntate: ¿lo necesito o solo lo quiero?"))),
    };

    public static SeedTopic SelectNextTopic(int grade, IReadOnlyCollection<string> usedTopicIds)
    {
        var gradeTopics = Topics.Where(t => t.Grade == grade).ToList();
        if (gradeTopics.Count == 0)
        {
            gradeTopics = Topics.ToList();
        }

        return gradeTopics.FirstOrDefault(t => !usedTopicIds.Contains(t.TopicId)) ?? gradeTopics[0];
    }

    public static SeedTopic Find(string topicId) =>
        Topics.FirstOrDefault(t => t.TopicId == topicId)
        ?? throw new InvalidOperationException($"No se encontró el tema semilla '{topicId}' en el catálogo.");

    public static ActiveLessonDto ToActiveLesson(SeedTopic topic) =>
        new(topic.TopicId, SchemaVersion, topic.Title, topic.CampoFormativo, topic.WeeklyProjectTitle, topic.WeeklyProjectGoal, topic.Blocks);

    private static IReadOnlyList<LessonBlockDto> Blocks(params (string Type, string Content, string Alt, string Narration)[] items) =>
        items.Select(i => new LessonBlockDto(i.Type, i.Content, i.Alt, i.Narration)).ToList();
}