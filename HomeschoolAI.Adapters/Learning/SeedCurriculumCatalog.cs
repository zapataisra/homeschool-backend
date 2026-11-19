using HomeschoolAI.Application.Learning;
using HomeschoolAI.Domain.Progress;

namespace HomeschoolAI.Adapters.Learning;

/// <summary>Tema semilla del catálogo curricular (SEP, Grados 1 a 6).</summary>
public sealed record SeedTopic(
    int Grade,
    int Trimester,
    int OrderIndex,
    string TopicId,
    string Title,
    string CampoFormativo,
    string SearchQuery,
    string WeeklyProjectTitle,
    string WeeklyProjectGoal,
    string? PrerequisiteTopicId,
    IReadOnlyList<LessonBlockDto> Blocks);

/// <summary>
/// Catálogo curricular semilla (SEP, Grados 1 a 6). Cada grado tiene una secuencia
/// pedagógica (OrderIndex) distribuida en los tres trimestres del ciclo escolar
/// (1 = Ago–Nov, 2 = Dic–Mar, 3 = Abr–Jul) y prerrequisitos para retroceder cuando
/// un tema queda en <see cref="TopicStatus.Partial"/>.
/// </summary>
public static class SeedCurriculumCatalog
{
    private const string SchemaVersion = "1.0";

    /// <summary>
    /// Devuelve el trimestre SEP para un mes dado (1–12).
    /// 1 = Ago–Nov, 2 = Dic–Mar, 3 = Abr–Jul.
    /// </summary>
    public static int GetSepTrimesterForMonth(int month) => month switch
    {
        8 or 9 or 10 or 11 => 1,
        12 or 1 or 2 or 3 => 2,
        4 or 5 or 6 or 7 => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(month), month, "El mes debe estar entre 1 y 12.")
    };

    /// <summary>
    /// Selecciona el siguiente tema para un niño dado el estado actual de su progreso.
    /// Prioridad: continuar el tema InProgress → retroceder al prerrequisito si el
    /// último tema cerrado quedó Partial → primer tema del trimestre actual (alumno
    /// nuevo) → siguiente tema por OrderIndex no completado.
    /// </summary>
    public static SeedTopic SelectNextTopic(int grade, DateTime nowUtc, IReadOnlyList<ChildTopicProgress> topics) =>
        SelectNextTopic(GetTopicsForGrade(grade), nowUtc, topics);

    /// <summary>Versión pura: selecciona el siguiente tema a partir de una lista de temas dada.</summary>
    public static SeedTopic SelectNextTopic(IReadOnlyList<SeedTopic> gradeTopics, DateTime nowUtc, IReadOnlyList<ChildTopicProgress> topics)
    {
        var inProgress = topics.FirstOrDefault(t => t.Status == TopicStatus.InProgress);
        if (inProgress is not null)
        {
            return FindInList(gradeTopics, inProgress.TopicId);
        }

        var lastClosed = topics
            .Where(t => t.Status != TopicStatus.InProgress)
            .OrderByDescending(t => t.LastActivityAtUtc)
            .FirstOrDefault();

        if (lastClosed is { Status: TopicStatus.Partial })
        {
            var lastSeed = FindInList(gradeTopics, lastClosed.TopicId);
            if (lastSeed.PrerequisiteTopicId is { } prerequisiteId &&
                topics.All(t => t.TopicId != prerequisiteId || t.Status != TopicStatus.Completed))
            {
                return FindInList(gradeTopics, prerequisiteId);
            }
        }

        var completedIds = topics
            .Where(t => t.Status == TopicStatus.Completed)
            .Select(t => t.TopicId)
            .ToHashSet(StringComparer.Ordinal);

        if (topics.Count == 0)
        {
            var trimester = GetSepTrimesterForMonth(nowUtc.Month);
            return gradeTopics.FirstOrDefault(t => t.Trimester == trimester) ?? gradeTopics[0];
        }

        return gradeTopics.FirstOrDefault(t => !completedIds.Contains(t.TopicId)) ?? gradeTopics[0];
    }

    private static SeedTopic FindInList(IReadOnlyList<SeedTopic> topics, string topicId) =>
        topics.FirstOrDefault(t => t.TopicId == topicId)
        ?? throw new InvalidOperationException($"No se encontró el tema '{topicId}' en el catálogo curricular.");

    /// <summary>
    /// Devuelve hasta <paramref name="limit"/> temas pendientes (no completados ni pausados)
    /// para la sección "Elige el proyecto de la clase de hoy": primero el tema en curso
    /// (InProgress) y después los siguientes NotStarted en orden curricular. Los temas
    /// completados/pausados ya se muestran en el historial.
    /// </summary>
    public static IReadOnlyList<SeedTopic> SelectMissionOptions(int grade, IReadOnlyList<ChildTopicProgress> topics, int limit = 3) =>
        SelectMissionOptions(GetTopicsForGrade(grade), topics, limit);

    /// <summary>Versión pura: devuelve hasta <paramref name="limit"/> temas pendientes de la lista dada.</summary>
    public static IReadOnlyList<SeedTopic> SelectMissionOptions(IReadOnlyList<SeedTopic> gradeTopics, IReadOnlyList<ChildTopicProgress> topics, int limit = 3)
    {
        var closedIds = topics
            .Where(t => t.Status is TopicStatus.Completed or TopicStatus.Partial)
            .Select(t => t.TopicId)
            .ToHashSet(StringComparer.Ordinal);

        var inProgressIds = topics
            .Where(t => t.Status == TopicStatus.InProgress)
            .Select(t => t.TopicId)
            .ToHashSet(StringComparer.Ordinal);

        return gradeTopics
            .Where(t => !closedIds.Contains(t.TopicId))
            .OrderBy(t => inProgressIds.Contains(t.TopicId) ? 0 : 1)
            .ThenBy(t => t.OrderIndex)
            .Take(limit)
            .ToList();
    }

    public static SeedTopic Find(string topicId) =>
        Topics.FirstOrDefault(t => t.TopicId == topicId)
        ?? throw new InvalidOperationException($"No se encontró el tema semilla '{topicId}' en el catálogo.");

    public static SeedTopic? TryFind(string topicId) =>
        Topics.FirstOrDefault(t => t.TopicId == topicId);

    public static ActiveLessonDto ToActiveLesson(SeedTopic topic) =>
        new(topic.TopicId, SchemaVersion, topic.Title, topic.CampoFormativo, topic.WeeklyProjectTitle, topic.WeeklyProjectGoal, topic.Blocks);

    private static IReadOnlyList<LessonBlockDto> Blocks(params (string Type, string Content, string Alt, string Narration, string ImageUrl)[] items) =>
        items.Select(i => new LessonBlockDto(i.Type, i.Content, i.Alt, i.Narration, i.ImageUrl)).ToList();

    /// <summary>Devuelve los temas semilla del grado, ordenados por <c>OrderIndex</c> (o todos si el grado no tiene temas).</summary>
    public static IReadOnlyList<SeedTopic> GetTopicsForGrade(int grade)
    {
        var gradeTopics = Topics.Where(t => t.Grade == grade).OrderBy(t => t.OrderIndex).ToList();
        return gradeTopics.Count > 0 ? gradeTopics : Topics.OrderBy(t => t.OrderIndex).ToList();
    }

    private static readonly IReadOnlyList<SeedTopic> Topics = new List<SeedTopic>
    {
        // Grado 1 (Fase 3)
        new(1, 1, 1, "fase3-lenguajes-letras", "Las letras y sus sonidos", "Lenguajes",
            "conciencia fonológica letras y sonidos primer grado primaria SEP",
            "Mi abecedario de casa", "Buscar objetos de casa que empiecen con cada letra y armar un abecedario ilustrado.",
            null,
            Blocks(
                ("text", "Cada letra tiene un sonido propio que, al juntarse con otros, forma palabras. Es como una clave secreta: la 'm' suena /m/ como en 'mano' y 'mamá'. Hoy empezaremos a descubrir esos sonidos.", "Letras grandes con dibujos de animales que ilustran su sonido", "Cada letra tiene un sonido especial que nos ayuda a leer.", "/lessons/fase3-lenguajes-letras/slide-1.svg"),
                ("text", "Ejemplo: la 'a' de 'abeja' suena /a/ y la 'e' de 'elefante' suena /e/. Di en voz alta a-e-i-o-u y toca tu boca para sentir cómo cambia el sonido. Esas cinco letras se llaman vocales.", "Las vocales a-e-i-o-u con ejemplos ilustrados", "Las vocales a-e-i-o-u suenan distinto; repítelas conmigo.", "/lessons/fase3-lenguajes-letras/slide-2.svg"),
                ("text", "Reto: busca en tu casa 3 objetos que empiecen con la misma letra de tu nombre. Di su sonido en voz alta y cuéntale a Axolotl cuáles encontraste.", "Niño buscando objetos por su sonido inicial", "Busca objetos de casa y descubre su primer sonido.", "/lessons/fase3-lenguajes-letras/slide-3.svg"))),
        new(1, 2, 2, "fase3-saberes-numeros", "Contar hasta 20", "Saberes y Pensamiento Científico",
            "conteo números del 1 al 20 primer grado primaria SEP",
            "Mi colección de 20 cosas", "Reunir y contar 20 objetos favoritos agrupándolos de 5 en 5.",
            "fase3-lenguajes-letras",
            Blocks(
                ("text", "Contar es saber cuántas cosas hay, una por una y sin saltarnos ninguna. Podemos contar de 1 en 1 o, cuando hay muchas, agruparlas de 5 en 5 para ir más rápido.", "Niño contando grupos de objetos con los dedos", "Contar nos dice cuántas cosas hay sin equivocarnos.", "/lessons/fase3-saberes-numeros/slide-1.svg"),
                ("text", "Ejemplo: si tienes 12 canicas, cuenta 1, 2, 3… hasta 12. Para ir más rápido, forma dos grupos de 5 y quedan 2 sueltas: 5 + 5 + 2 = 12.", "Grupos de 5 canicas que muestran 5 + 5 + 2 = 12", "Agrupa de 5 en 5 y cuenta más rápido.", "/lessons/fase3-saberes-numeros/slide-2.svg"),
                ("text", "Reto: reúne 20 objetos favoritos (frijoles, tapas o juguetes) y cuéntalos de 5 en 5. Cuéntale a Axolotl cuántos grupos de 5 formaste.", "Colección de 20 objetos agrupados de 5 en 5", "Reúne 20 objetos y cuéntalos de 5 en 5.", "/lessons/fase3-saberes-numeros/slide-3.svg"))),
        new(1, 3, 3, "fase3-saberes-figuras", "Las figuras geométricas", "Saberes y Pensamiento Científico",
            "figuras geométricas círculo cuadrado triángulo primer grado primaria SEP",
            "Mi collage de figuras", "Recortar y pegar figuras geométricas para formar un dibujo de la casa.",
            "fase3-saberes-numeros",
            Blocks(
                ("text", "Las figuras geométricas están en todas partes: el círculo de un reloj, el cuadrado de una ventana, el triángulo de una montaña. Cada una tiene lados y esquinas distintos.", "Figuras geométricas básicas con caritas kawaii", "Las figuras geométricas se esconden en los objetos de casa.", "/lessons/fase3-saberes-figuras/slide-1.svg"),
                ("text", "Ejemplo: un cuadrado tiene 4 lados iguales y 4 esquinas; un triángulo tiene 3 lados y 3 esquinas; el círculo no tiene esquinas y rueda. Señala cada figura en una hoja.", "Cuadrado, triángulo y círculo con sus lados marcados", "El cuadrado tiene 4 lados, el triángulo 3 y el círculo ninguno.", "/lessons/fase3-saberes-figuras/slide-2.svg"),
                ("text", "Reto: recorre tu casa y encuentra un círculo, un cuadrado y un triángulo. Cuéntale a Axolotl dónde los viste.", "Objetos de casa con formas geométricas", "Busca figuras geométricas a tu alrededor.", "/lessons/fase3-saberes-figuras/slide-3.svg"))),
        // Grado 2 (Fase 3)
        new(2, 1, 1, "fase3-lenguajes-cuentos", "Los cuentos y sus personajes", "Lenguajes",
            "cuento personajes inicio desarrollo final segundo grado primaria SEP",
            "Mi cuento ilustrado", "Escribir e ilustrar un cuento corto con un personaje favorito.",
            null,
            Blocks(
                ("text", "Todo cuento tiene personajes que viven una aventura y una historia con inicio, desarrollo y final. El inicio presenta al personaje, el desarrollo cuenta el problema y el final lo resuelve.", "Personajes de cuento con escenas de inicio, desarrollo y final", "Todo cuento tiene personajes que viven una aventura.", "/lessons/fase3-lenguajes-cuentos/slide-1.svg"),
                ("text", "Ejemplo: Caperucita Roja lleva comida a su abuelita (inicio); en el desarrollo se encuentra con el lobo; en el final llega el leñador y todo se resuelve.", "Mapa de un cuento en tres partes: inicio, desarrollo y final", "El cuento tiene inicio, desarrollo y final.", "/lessons/fase3-lenguajes-cuentos/slide-2.svg"),
                ("text", "Reto: piensa en tu personaje favorito. ¿Qué problema tiene y cómo lo resuelve? Cuéntaselo a Axolotl en 3 pasos: inicio, desarrollo y final.", "Niño imaginando a su personaje favorito", "Cuéntale a Axolotl tu cuento en tres pasos.", "/lessons/fase3-lenguajes-cuentos/slide-3.svg"))),
        new(2, 2, 2, "fase3-saberes-sumas", "Sumas con llevadas", "Saberes y Pensamiento Científico",
            "suma con llevadas segundo grado primaria SEP",
            "La tiendita de juguete", "Jugar a comprar y vender sumando precios con llevadas.",
            "fase3-lenguajes-cuentos",
            Blocks(
                ("text", "Sumar es juntar cantidades. Cuando al sumar las unidades pasamos de 9, usamos una 'llevada': guardamos una decena y la subimos a la siguiente columna.", "Bloques de unidades y decenas que muestran la llevada", "La llevada es un empujoncito que nos ayuda a sumar.", "/lessons/fase3-saberes-sumas/slide-1.svg"),
                ("text", "Ejemplo: 27 + 18. Suma las unidades: 7 + 8 = 15; escribe el 5 y 'lleva' 1 decena. Suma las decenas: 2 + 1 + 1 (la llevada) = 4. Resultado: 45.", "Suma 27 + 18 resuelta paso a paso", "7 + 8 = 15: escribe 5 y lleva 1.", "/lessons/fase3-saberes-sumas/slide-2.svg"),
                ("text", "Reto: en tu tiendita de juguete, suma el precio de dos productos como 36 + 27. Usa la llevada y cuéntale a Axolotl tu resultado.", "Tiendita con precios para sumar con llevadas", "Suma precios en tu tiendita usando la llevada.", "/lessons/fase3-saberes-sumas/slide-3.svg"))),
        new(2, 3, 3, "fase3-saberes-restas", "Las restas", "Saberes y Pensamiento Científico",
            "resta quitar segundo grado primaria SEP",
            "El mercado de frutas", "Jugar a vender frutas y restar lo que se lleva cada cliente.",
            "fase3-saberes-sumas",
            Blocks(
                ("text", "Restar es quitar una cantidad de otra para saber cuánto queda. Si de 10 manzanas regalo 4, me quedan 6, porque 10 − 4 = 6.", "Canasta de frutas de la que se quitan algunas", "Restar nos dice cuánto queda después de quitar.", "/lessons/fase3-saberes-restas/slide-1.svg"),
                ("text", "Ejemplo: en el mercado vendes 9 frutas y un cliente se lleva 5. Quedan 9 − 5 = 4 frutas. Cuenta las que quedan para comprobar.", "Restas 9 − 5 con frutas del mercado", "9 menos 5 son 4.", "/lessons/fase3-saberes-restas/slide-2.svg"),
                ("text", "Reto: junta 12 tapas o piedritas, quita 5 y cuenta cuántas quedan. Dile a Axolotl la resta que hiciste.", "Niño quitando tapas para resolver una resta", "Junta 12 objetos, quita 5 y cuenta cuántos quedan.", "/lessons/fase3-saberes-restas/slide-3.svg"))),
        // Grado 3 (Fase 4)
        new(3, 1, 1, "fase4-saberes-ciclo-agua", "El ciclo del agua", "Saberes y Pensamiento Científico",
            "ciclo del agua evaporación condensación precipitación SEP primaria",
            "Mi diario del agua", "Observar durante la semana dónde aparece agua y registrar el ciclo.",
            null,
            Blocks(
                ("text", "El agua viaja sin parar: el Sol la calienta y se evapora, sube al cielo y forma nubes (condensación), y cae de nuevo como lluvia (precipitación). Es un ciclo que nunca termina.", "Diagrama circular del ciclo del agua con flechas", "El agua es una gran viajera que nunca se queda quieta.", "/lessons/fase4-saberes-ciclo-agua/slide-1.svg"),
                ("text", "Ejemplo: una gotita del mar se evapora, se junta con otras en una nube y llueve sobre la montaña; baja por el río y vuelve al mar. El ciclo se repite una y otra vez.", "Gotita viajando por las etapas del ciclo del agua", "Evaporación, condensación y precipitación: el viaje de la gotita.", "/lessons/fase4-saberes-ciclo-agua/slide-2.svg"),
                ("text", "Reto: hoy observa dónde aparece agua (el vapor del caldo, el rocío, la lluvia) y anota en qué parte del ciclo está. Compártelo con Axolotl.", "Niño observando el agua en la naturaleza", "Observa el agua de hoy y di en qué etapa está.", "/lessons/fase4-saberes-ciclo-agua/slide-3.svg"))),
        new(3, 2, 2, "fase4-lenguajes-noticias", "La noticia y sus partes", "Lenguajes",
            "la noticia titular cuerpo periodístico tercer grado primaria SEP",
            "Mi periódico escolar", "Escribir una noticia real del salón con titular y cuerpo.",
            "fase4-saberes-ciclo-agua",
            Blocks(
                ("text", "Una noticia cuenta algo importante que acaba de pasar. Responde cinco preguntas: qué pasó, a quién, cuándo, dónde y por qué.", "Estructura de una noticia con titular y cuerpo", "Una noticia cuenta algo importante que acaba de pasar.", "/lessons/fase4-lenguajes-noticias/slide-1.svg"),
                ("text", "Ejemplo: titular «La clase 4.º A sembró un huerto»; cuerpo: «El lunes, en el patio, los alumnos plantaron jitomates para aprender de las plantas». El titular atrapa y el cuerpo da detalles.", "Ejemplo de noticia escolar con titular y cuerpo", "El titular atrapa y el cuerpo cuenta los detalles.", "/lessons/fase4-lenguajes-noticias/slide-2.svg"),
                ("text", "Reto: escribe una noticia real de tu salón o tu casa con un titular llamativo y un cuerpo que responda qué, quién, cuándo y dónde.", "Niño escribiendo su propio periódico escolar", "Escribe tu propia noticia con titular y cuerpo.", "/lessons/fase4-lenguajes-noticias/slide-3.svg"))),
        new(3, 3, 3, "fase4-saberes-multiplicacion", "La multiplicación", "Saberes y Pensamiento Científico",
            "multiplicación grupos iguales tercer grado primaria SEP",
            "Mi tabla de grupos", "Agrupar objetos en cantidades iguales y escribir su multiplicación.",
            "fase4-lenguajes-noticias",
            Blocks(
                ("text", "Multiplicar es sumar varias veces la misma cantidad. Si hay 4 grupos de 3 manzanas, en total hay 3 + 3 + 3 + 3 = 12, que escribimos 4 × 3 = 12.", "Grupos iguales de objetos que forman una multiplicación", "Multiplicar es una suma rápida de grupos iguales.", "/lessons/fase4-saberes-multiplicacion/slide-1.svg"),
                ("text", "Ejemplo: una caja trae 6 lápices y hay 5 cajas. Suma 6 cinco veces o multiplica 5 × 6 = 30. En total hay 30 lápices.", "Cinco cajas de 6 lápices que dan 30", "5 cajas de 6 lápices son 30.", "/lessons/fase4-saberes-multiplicacion/slide-2.svg"),
                ("text", "Reto: arma 3 grupos con la misma cantidad de objetos (por ejemplo 4 fichas cada uno) y escribe la multiplicación. Compártela con Axolotl.", "Niño armando grupos iguales de fichas", "Arma grupos iguales y escribe su multiplicación.", "/lessons/fase4-saberes-multiplicacion/slide-3.svg"))),
        // Grado 4 (Fase 4)
        new(4, 1, 1, "fase4-saberes-repartos", "Repartos y fracciones equivalentes", "Saberes y Pensamiento Científico",
            "repartos fracciones equivalentes cuarto grado primaria SEP",
            "La fiesta de repartos", "Repartir dulces o dibujos en partes iguales y descubrir fracciones equivalentes.",
            null,
            Blocks(
                ("text", "Repartir en partes iguales es el corazón de las fracciones. Una fracción como 1/4 significa que dividimos el todo en 4 partes iguales y tomamos 1. Distintas fracciones pueden representar la misma cantidad: 2/8 es igual a 1/4.", "Diagrama de 8 elementos repartidos en 4 grupos que muestra 2/8 = 1/4", "Repartir justo es la puerta de entrada a las fracciones.", "/lessons/fase4-saberes-repartos/slide-1.svg"),
                ("text", "Ejemplo: reparte 8 dulces entre 4 amigos: cada quien recibe 2 dulces, es decir 2/8 del total, y 2/8 equivale a 1/4. Es un reparto justo escrito de dos formas.", "Reparto equitativo de 8 dulces entre 4 personas", "8 dulces entre 4 son 2 para cada quien: 2/8 = 1/4.", "/lessons/fase4-saberes-repartos/slide-2.svg"),
                ("text", "Reto: en la fiesta de repartos, divide 8 dibujos o dulces entre 4 personas y comprueba que 2/8 = 1/4. Cuéntale a Axolotl cómo lo comprobaste.", "Niño repartiendo dulces en partes iguales", "Reparte 8 objetos entre 4 y comprueba la equivalencia.", "/lessons/fase4-saberes-repartos/slide-3.svg"))),
        new(4, 2, 2, "fase4-saberes-fracciones", "Las fracciones", "Saberes y Pensamiento Científico",
            "fracciones equivalentes cuarto grado primaria SEP",
            "La pizza fraccionaria", "Repartir una pizza (o papel) en partes iguales y comparar fracciones.",
            "fase4-saberes-repartos",
            Blocks(
                ("text", "Una fracción representa partes iguales de un todo. El número de abajo (denominador) dice en cuántas partes se divide, y el de arriba (numerador) cuántas tomamos.", "Círculos divididos en fracciones con porciones coloreadas", "Una fracción es una forma justa de repartir un todo.", "/lessons/fase4-saberes-fracciones/slide-1.svg"),
                ("text", "Ejemplo: una pizza dividida en 4 y de la que tomas 2 rebanadas es 2/4, que equivale a media pizza (1/2). Compara: 2/4 es mayor que 3/8 porque sus rebanadas son más grandes.", "Dos círculos fraccionados: 2/4 y 3/8 con porciones coloreadas", "2/4 de pizza es media pizza y es mayor que 3/8.", "/lessons/fase4-saberes-fracciones/slide-2.svg"),
                ("text", "Reto: dobla un papel en 4 partes iguales y colorea 2 (2/4); luego compáralo con 3/8. Dile a Axolotl cuál es mayor y por qué.", "Niño comparando fracciones con papel doblado", "Compara 2/4 y 3/8 con papel y color.", "/lessons/fase4-saberes-fracciones/slide-3.svg"))),
        new(4, 3, 3, "fase4-etica-derechos", "Los derechos de la niñez", "Ética, Naturaleza y Sociedades",
            "derechos de las niñas y los niños cuarto grado primaria SEP",
            "El mural de nuestros derechos", "Dibujar y exponer un derecho de la niñez con un ejemplo cotidiano.",
            "fase4-saberes-fracciones",
            Blocks(
                ("text", "Todas las niñas y los niños tenemos derechos que nos protegen y nos ayudan a crecer felices: derecho a la salud, a la educación, al juego y a vivir en familia.", "Escudo protector de derechos con íconos de salud, educación, juego y familia", "Los derechos nos cuidan y nos ayudan a crecer felices.", "/lessons/fase4-etica-derechos/slide-1.svg"),
                ("text", "Ejemplo: ir a la escuela es tu derecho a la educación; jugar en el parque es tu derecho al juego; recibir vacunas y cuidarte es tu derecho a la salud. Nadie debe quitarte estos derechos.", "Niñas y niños ejerciendo sus derechos en la escuela y el parque", "Educación, salud, juego y familia son tus derechos.", "/lessons/fase4-etica-derechos/slide-2.svg"),
                ("text", "Reto: piensa en un derecho que usas hoy (jugar, aprender, descansar) y dibújalo para el mural. Cuéntale a Axolotl por qué es importante para ti.", "Niño dibujando su derecho para el mural de la clase", "Dibuja un derecho que uses hoy y cuéntaselo a Axolotl.", "/lessons/fase4-etica-derechos/slide-3.svg"))),
        new(4, 1, 4, "fase4-saberes-nutricion", "La nutrición y el Plato del Bien Comer", "Saberes y Pensamiento Científico",
            "nutrición Plato del Bien Comer alimentación saludable cuarto grado primaria SEP",
            "Mi menú saludable", "Armar un menú de un día con los tres grupos del Plato del Bien Comer.",
            "fase4-etica-derechos",
            Blocks(
                ("text", "Nuestro cuerpo necesita nutrirse para crecer y tener energía. El Plato del Bien Comer agrupa los alimentos en tres grupos: verduras y frutas, cereales, y leguminosas con alimentos de origen animal.", "Plato del Bien Comer con sus tres grupos de alimentos", "El Plato del Bien Comer nos ayuda a comer sano.", "/lessons/fase4-saberes-nutricion/slide-1.svg"),
                ("text", "Ejemplo: un desayuno saludable tiene fruta (grupo 1), una tortilla (grupo 2) y un huevo (grupo 3). Revisa tu plato: ¿tiene algo de cada grupo?", "Desayuno saludable con fruta, cereal y huevo", "Frutas, cereales y leguminosas: los tres grupos.", "/lessons/fase4-saberes-nutricion/slide-2.svg"),
                ("text", "Reto: dibuja tu plato de hoy y marca de qué grupo es cada alimento. Cuéntale a Axolotl qué grupo te faltó y cómo lo completarías.", "Niño dibujando su plato saludable", "Dibuja tu plato y di qué grupo te faltó.", "/lessons/fase4-saberes-nutricion/slide-3.svg"))),
        new(4, 1, 5, "fase4-saberes-movimiento", "El movimiento y la fuerza", "Saberes y Pensamiento Científico",
            "movimiento fuerza velocidad estado de reposo cuarto grado primaria SEP",
            "La pista de carreras", "Registrar cómo se mueven los objetos de casa y qué fuerza los mueve.",
            "fase4-saberes-nutricion",
            Blocks(
                ("text", "Los objetos pueden estar en movimiento o en reposo (quietos). Para que algo se mueva hace falta una fuerza: empujar, jalar o soltar. La velocidad nos dice qué tan rápido se mueve.", "Pelota en reposo y en movimiento con una fuerza aplicada", "Los objetos están en reposo o en movimiento.", "/lessons/fase4-saberes-movimiento/slide-1.svg"),
                ("text", "Ejemplo: una pelota en el piso está en reposo; al patearla, tu pie aplica una fuerza y la pelota se mueve. Si la pateas más fuerte, va más rápido.", "Niño pateando una pelota", "Una fuerza como patear hace que algo se mueva.", "/lessons/fase4-saberes-movimiento/slide-2.svg"),
                ("text", "Reto: elige 3 objetos de tu casa, di si están en reposo o en movimiento y qué fuerza los mueve. Compártelo con Axolotl.", "Objetos de casa en reposo y en movimiento", "Observa 3 objetos y di qué fuerza los mueve.", "/lessons/fase4-saberes-movimiento/slide-3.svg"))),
        new(4, 2, 6, "fase4-lenguajes-instructivos", "Manuales e instructivos", "Lenguajes",
            "manual instructivo reglamento pasos cuarto grado primaria SEP",
            "El manual de mi juego", "Escribir un instructivo con pasos claros para armar o jugar algo.",
            "fase4-saberes-movimiento",
            Blocks(
                ("text", "Un instructivo o manual explica, paso a paso, cómo hacer algo. Usa verbos en orden y frases cortas para que cualquiera pueda seguirlo.", "Manual ilustrado con pasos numerados", "Un instructivo explica cómo hacer algo paso a paso.", "/lessons/fase4-lenguajes-instructivos/slide-1.svg"),
                ("text", "Ejemplo: para hacer un avión de papel: 1) dobla la hoja a la mitad, 2) dobla las esquinas, 3) lánzalo. Cada paso empieza con una acción clara.", "Pasos numerados para hacer un avión de papel", "Cada paso empieza con una acción clara.", "/lessons/fase4-lenguajes-instructivos/slide-2.svg"),
                ("text", "Reto: escribe 3 pasos de un instructivo para tu juego favorito. Pídele a Axolotl que verifique si se entienden.", "Niño escribiendo su instructivo", "Escribe 3 pasos de tu juego favorito.", "/lessons/fase4-lenguajes-instructivos/slide-3.svg"))),
        new(4, 2, 7, "fase4-saberes-areas", "Áreas y perímetros", "Saberes y Pensamiento Científico",
            "área perímetro medir superficie cuarto grado primaria SEP",
            "El patio de mi escuela", "Medir y comparar el perímetro y el área de espacios de la casa o la escuela.",
            "fase4-lenguajes-instructivos",
            Blocks(
                ("text", "El perímetro es la medida del contorno de una figura: la suma de todos sus lados. El área es la medida de la superficie que ocupa por dentro.", "Rectángulo con lados marcados mostrando perímetro y área", "El perímetro mide el contorno y el área la superficie.", "/lessons/fase4-saberes-areas/slide-1.svg"),
                ("text", "Ejemplo: un rectángulo de 4 m por 3 m tiene perímetro 4+3+4+3 = 14 m y área 4 × 3 = 12 metros cuadrados. Son dos medidas distintas.", "Rectángulo con las operaciones de perímetro y área", "4+3+4+3 de perímetro y 4×3 de área.", "/lessons/fase4-saberes-areas/slide-2.svg"),
                ("text", "Reto: mide con pasos un cuarto de tu casa, calcula su perímetro y estima su área. Dile a Axolotl cuál obtuviste.", "Niño midiendo un cuarto con pasos", "Mide un cuarto y calcula su perímetro y área.", "/lessons/fase4-saberes-areas/slide-3.svg"))),
        new(4, 2, 8, "fase4-etica-ecosistemas", "Ecosistemas de México y biodiversidad", "Ética, Naturaleza y Sociedades",
            "ecosistema biodiversidad cuidado medio ambiente México cuarto grado primaria SEP",
            "Guardianes del ecosistema", "Identificar un ecosistema cercano y proponer una acción para cuidarlo.",
            "fase4-saberes-areas",
            Blocks(
                ("text", "Un ecosistema es el conjunto de seres vivos y el lugar donde viven, que se relacionan entre sí. México tiene una gran biodiversidad: selvas, bosques, mares y desiertos.", "Bosque con animales y plantas que se relacionan", "Un ecosistema une a los seres vivos con su lugar.", "/lessons/fase4-etica-ecosistemas/slide-1.svg"),
                ("text", "Ejemplo: en un bosque viven árboles, aves e insectos que dependen unos de otros. Si contaminamos el agua del río, todos los seres del ecosistema se afectan.", "Río contaminado que afecta a todo el ecosistema", "Contaminar el agua afecta a todo el ecosistema.", "/lessons/fase4-etica-ecosistemas/slide-2.svg"),
                ("text", "Reto: piensa en un ecosistema cercano y en una acción para cuidarlo (no tirar basura, ahorrar agua). Cuéntale a Axolotl tu plan.", "Niño proponiendo una acción para cuidar el ambiente", "Propón una acción para cuidar tu ecosistema.", "/lessons/fase4-etica-ecosistemas/slide-3.svg"))),
        new(4, 3, 9, "fase4-lenguajes-leyendas", "Leyendas de mi comunidad", "Lenguajes",
            "leyenda tradición narración comunidad cuarto grado primaria SEP",
            "El libro de leyendas", "Contar por escrito una leyenda de tu comunidad o familia.",
            "fase4-etica-ecosistemas",
            Blocks(
                ("text", "Las leyendas son narraciones que mezclan hechos reales con elementos fantásticos. Se transmiten de boca en boca y cuentan el origen de un lugar o un suceso.", "Libro de leyendas con el águila sobre el nopal", "Las leyendas mezclan lo real con lo fantástico.", "/lessons/fase4-lenguajes-leyendas/slide-1.svg"),
                ("text", "Ejemplo: la leyenda de los mexicas cuenta que peregrinaron durante casi dos siglos hasta encontrar la señal del águila sobre el nopal. Así nació su ciudad.", "Narración de la leyenda mexica", "Los mexicas peregrinaron hasta hallar la señal del águila.", "/lessons/fase4-lenguajes-leyendas/slide-2.svg"),
                ("text", "Reto: pregunta a un adulto una leyenda de tu comunidad y escríbela con inicio, desarrollo y final. Compártela con Axolotl.", "Niño escuchando una leyenda familiar", "Escucha y escribe una leyenda de tu comunidad.", "/lessons/fase4-lenguajes-leyendas/slide-3.svg"))),
        new(4, 3, 10, "fase4-etica-paz", "Cultura de paz y acuerdos de convivencia", "Ética, Naturaleza y Sociedades",
            "cultura de paz convivencia resolución de conflictos acuerdos cuarto grado primaria SEP",
            "Nuestros acuerdos de convivencia", "Crear acuerdos para resolver conflictos del salón con respeto y sin violencia.",
            "fase4-lenguajes-leyendas",
            Blocks(
                ("text", "La cultura de paz es resolver los conflictos con respeto, diálogo y acuerdos, sin gritos ni golpes. Los acuerdos de convivencia nos ayudan a vivir mejor juntos.", "Niños dialogando y haciendo acuerdos", "La paz se construye con diálogo y acuerdos.", "/lessons/fase4-etica-paz/slide-1.svg"),
                ("text", "Ejemplo: si dos compañeros quieren el mismo juego, en lugar de pelear hacen un acuerdo: primero uno y luego el otro, o juegan juntos. Así todos ganan.", "Acuerdo de turnos para compartir un juego", "Compartir turnos evita los conflictos.", "/lessons/fase4-etica-paz/slide-2.svg"),
                ("text", "Reto: escribe 2 acuerdos de convivencia para tu salón o tu casa. Cuéntale a Axolotl cómo ayudan a resolver conflictos.", "Lista de acuerdos de convivencia", "Escribe 2 acuerdos para convivir mejor.", "/lessons/fase4-etica-paz/slide-3.svg"))),
        new(4, 3, 11, "fase4-saberes-graficas", "Registro de datos y gráficas", "Saberes y Pensamiento Científico",
            "registro de datos gráfica de barras cuarto grado primaria SEP",
            "La encuesta del salón", "Recolectar datos de la clase y representarlos en una gráfica de barras.",
            "fase4-etica-paz",
            Blocks(
                ("text", "Para entender muchos datos los organizamos en tablas y gráficas. Una gráfica de barras usa barras de distinta altura para comparar cantidades de un vistazo.", "Gráfica de barras comparando votos", "Las gráficas de barras comparan datos de un vistazo.", "/lessons/fase4-saberes-graficas/slide-1.svg"),
                ("text", "Ejemplo: si 10 niñas y 7 niños votaron por su fruta favorita, dibujamos una barra alta (10) y una más baja (7). La barra más alta es la fruta ganadora.", "Tabla de datos y su gráfica de barras", "Una barra alta y una baja muestran qué ganó.", "/lessons/fase4-saberes-graficas/slide-2.svg"),
                ("text", "Reto: pregunta a 5 personas su color favorito, haz tu tabla y dibuja la gráfica de barras. Cuéntale a Axolotl cuál ganó.", "Niño dibujando una gráfica de barras", "Pregunta a 5 personas y dibuja tu gráfica.", "/lessons/fase4-saberes-graficas/slide-3.svg"))),
        new(4, 3, 12, "fase4-humano-salud", "Hábitos saludables y emociones", "De lo Humano y lo Comunitario",
            "hábitos de higiene vida saludable emociones cuarto grado primaria SEP",
            "Mi rutina de bienestar", "Armar una rutina diaria con hábitos de higiene y cuidado de las emociones.",
            "fase4-saberes-graficas",
            Blocks(
                ("text", "Cuidar nuestro cuerpo y nuestras emociones nos ayuda a sentirnos bien. Lavarse las manos, dormir bien y expresar lo que sentimos son hábitos saludables.", "Niño lavándose las manos y expresando emociones", "Cuidar el cuerpo y las emociones nos hace sentir bien.", "/lessons/fase4-humano-salud/slide-1.svg"),
                ("text", "Ejemplo: lavarse las manos antes de comer evita enfermedades. Y si te sientes triste o enojado, decirlo con palabras (sin gritar) ayuda a sentirte mejor.", "Rutina diaria de higiene", "Lavarse las manos y expresar lo que sientes son hábitos saludables.", "/lessons/fase4-humano-salud/slide-2.svg"),
                ("text", "Reto: escribe tu rutina de bienestar (higiene + una emoción que quieras cuidar). Compártela con Axolotl.", "Niño hablando de sus emociones", "Escribe tu rutina de bienestar.", "/lessons/fase4-humano-salud/slide-3.svg"))),
        // Grado 5 (Fase 5)
        new(5, 1, 1, "fase5-saberes-energia", "La energía y sus formas", "Saberes y Pensamiento Científico",
            "formas de energía quinto grado primaria SEP",
            "Mi invento con energía", "Diseñar un juguete o invento que use una forma de energía.",
            null,
            Blocks(
                ("text", "La energía es la capacidad de hacer que las cosas funcionen o cambien. Puede presentarse como luz, calor, movimiento o sonido, y siempre se transforma de una forma a otra.", "Formas de energía con íconos de luz, calor, movimiento y sonido", "La energía cambia de forma para hacer funcionar las cosas.", "/lessons/fase5-saberes-energia/slide-1.svg"),
                ("text", "Ejemplo: al encender un foco, la energía eléctrica se transforma en luz y calor. Al pedalear, la energía de tus músculos (movimiento) mueve la bicicleta.", "Foco encendido que transforma la electricidad en luz y calor", "La electricidad se vuelve luz y calor en un foco.", "/lessons/fase5-saberes-energia/slide-2.svg"),
                ("text", "Reto: rastrea de dónde viene la energía de 3 cosas que uses hoy (la tele, la estufa, tu cuerpo) y di en qué forma se transforma.", "Niño rastreando la energía de los objetos de casa", "Rastrea la energía de 3 cosas que usas hoy.", "/lessons/fase5-saberes-energia/slide-3.svg"))),
        new(5, 2, 2, "fase5-lenguajes-poesia", "La poesía y sus recursos", "Lenguajes",
            "poesía rima metáfora quinto grado primaria SEP",
            "Mi antología poética", "Escribir tres poemas cortos usando rima y una metáfora.",
            "fase5-saberes-energia",
            Blocks(
                ("text", "La poesía juega con las palabras usando rima, ritmo y metáforas. Una metáfora compara dos cosas sin decir «como»: «tus ojos son luceros».", "Libro de poemas ilustrado con rima y metáfora", "La poesía es como música hecha con palabras.", "/lessons/fase5-lenguajes-poesia/slide-1.svg"),
                ("text", "Ejemplo: «El sol es una moneda de oro en el cielo». Aquí se compara el sol con una moneda dorada para crear una imagen. La rima repite sonidos al final: «luna… cuna».", "Metáfora del sol como moneda de oro", "Una metáfora pinta con palabras algo que no es literal.", "/lessons/fase5-lenguajes-poesia/slide-2.svg"),
                ("text", "Reto: escribe un poema de dos versos con rima y una metáfora sobre tu animal favorito. Compártelo con Axolotl.", "Niño escribiendo su poema con rima y metáfora", "Escribe dos versos con rima y una metáfora.", "/lessons/fase5-lenguajes-poesia/slide-3.svg"))),
        new(5, 3, 3, "fase5-saberes-decimales", "Los números decimales", "Saberes y Pensamiento Científico",
            "números decimales décimos centésimos quinto grado primaria SEP",
            "La caja registradora", "Usar decimales para calcular precios y vueltos en una tienda de juguete.",
            "fase5-lenguajes-poesia",
            Blocks(
                ("text", "Los decimales representan partes de un entero, como los centavos de un peso. Un peso tiene 100 centavos, así que 0.50 es la mitad: 50 centavos.", "Monedas y precios con decimales", "Los decimales nos ayudan a medir partes pequeñas.", "/lessons/fase5-saberes-decimales/slide-1.svg"),
                ("text", "Ejemplo: un lápiz cuesta $7.50 y pagas con $10. El vuelto es 10.00 − 7.50 = 2.50, es decir, 2 pesos con 50 centavos.", "Vuelto de 10.00 − 7.50 con monedas", "10.00 menos 7.50 es 2.50 de vuelto.", "/lessons/fase5-saberes-decimales/slide-2.svg"),
                ("text", "Reto: en la caja registradora, suma el precio de dos productos (por ejemplo $5.25 + $3.75) y calcula el vuelto de $10. Cuéntale a Axolotl tu resultado.", "Caja registradora de juguete con precios decimales", "Suma precios con decimales y calcula el vuelto.", "/lessons/fase5-saberes-decimales/slide-3.svg"))),
        // Grado 6 (Fase 5)
        new(6, 1, 1, "fase5-saberes-porcentajes", "Los porcentajes", "Saberes y Pensamiento Científico",
            "porcentaje descuento sexto grado primaria SEP",
            "La tienda de descuentos", "Calcular descuentos reales de productos y hacer un folleto de ofertas.",
            null,
            Blocks(
                ("text", "Un porcentaje es una fracción de 100: 20% significa 20 de cada 100. Nos ayuda a comparar partes de un todo de forma sencilla.", "Etiquetas de descuento con porcentajes", "El porcentaje nos dice cuánto de cada 100 tomamos.", "/lessons/fase5-saberes-porcentajes/slide-1.svg"),
                ("text", "Ejemplo: un juego de $100 con 20% de descuento ahorra $20 y cuesta $80. Para calcularlo, multiplica 100 × 20 ÷ 100 = 20, o divide en 5 partes (20% = 1/5).", "Producto de $100 con 20% de descuento", "20% de 100 es 20: pagas 80.", "/lessons/fase5-saberes-porcentajes/slide-2.svg"),
                ("text", "Reto: en la tienda de descuentos, calcula cuánto pagas por un producto de $50 con 10% de descuento. Explica tu cálculo a Axolotl.", "Tienda de descuentos con etiquetas", "Calcula el 10% de 50 y di cuánto pagas.", "/lessons/fase5-saberes-porcentajes/slide-3.svg"))),
        new(6, 2, 2, "fase5-etica-consumo", "Consumo responsable", "Ética, Naturaleza y Sociedades",
            "consumo responsable recursos sexto grado primaria SEP",
            "Mi guía de consumo responsable", "Elaborar una guía familiar para comprar y usar con responsabilidad.",
            "fase5-saberes-porcentajes",
            Blocks(
                ("text", "Consumir con responsabilidad es pensar antes de comprar y cuidar los recursos. Pregúntate: ¿lo necesito o solo lo quiero? ¿Puedo reutilizar o reciclar?", "Familia comprando con conciencia y símbolos de reciclaje", "Comprar con conciencia cuida el planeta y nuestro dinero.", "/lessons/fase5-etica-consumo/slide-1.svg"),
                ("text", "Ejemplo: antes de comprar un juguete nuevo, una familia revisa si puede reparar el que tiene o intercambiarlo. Así gasta menos y genera menos basura.", "Decisiones de consumo responsable: reparar y reutilizar", "Reparar y reutilizar genera menos basura.", "/lessons/fase5-etica-consumo/slide-2.svg"),
                ("text", "Reto: hoy revisa un producto de tu casa y responde: ¿lo necesitas de verdad? ¿cómo puedes reutilizarlo o reciclarlo? Compártelo con Axolotl.", "Niño revisando un producto en casa", "Revisa un producto y piensa si lo necesitas.", "/lessons/fase5-etica-consumo/slide-3.svg"))),
        new(6, 3, 3, "fase5-saberes-sistema-solar", "El sistema solar", "Saberes y Pensamiento Científico",
            "sistema solar planetas sol sexto grado primaria SEP",
            "Mi maqueta del sistema solar", "Construir una maqueta del sistema solar con los planetas en orden.",
            "fase5-etica-consumo",
            Blocks(
                ("text", "El sistema solar está formado por el Sol y los planetas que giran a su alrededor: Mercurio, Venus, Tierra, Marte, Júpiter, Saturno, Urano y Neptuno.", "Planetas alrededor del Sol en orden", "El Sol es la estrella que ilumina a todos los planetas.", "/lessons/fase5-saberes-sistema-solar/slide-1.svg"),
                ("text", "Ejemplo: la Tierra es el tercer planeta desde el Sol y el único (que sepamos) con vida. Júpiter es el más grande y Saturno tiene anillos brillantes de hielo y roca.", "La Tierra y sus vecinos en el sistema solar", "La Tierra es el tercer planeta y Júpiter el más grande.", "/lessons/fase5-saberes-sistema-solar/slide-2.svg"),
                ("text", "Reto: ordena los 8 planetas desde el Sol y elige tu favorito para la maqueta. Cuéntale a Axolotl en qué posición está.", "Maqueta del sistema solar con los planetas", "Ordena los 8 planetas desde el Sol.", "/lessons/fase5-saberes-sistema-solar/slide-3.svg"))),
    };
}
