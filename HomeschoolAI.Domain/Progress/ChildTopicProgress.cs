namespace HomeschoolAI.Domain.Progress;

/// <summary>
/// Progreso de un niño sobre un tema concreto del currículo (p. ej. "El ciclo del agua"),
/// incluyendo el historial de tiempo real dedicado y los intentos realizados.
/// </summary>
public sealed class ChildTopicProgress
{
    public Guid Id { get; set; }

    /// <summary>FK hacia <c>ChildProfile.Id</c>.</summary>
    public Guid ChildProfileId { get; set; }

    /// <summary>Identificador estable del tema (p. ej. "fase4-saberes-ciclo-agua").</summary>
    public string TopicId { get; set; } = string.Empty;

    /// <summary>Título legible del tema (p. ej. "El ciclo del agua").</summary>
    public string TopicTitle { get; set; } = string.Empty;

    /// <summary>Campo formativo del tema (p. ej. "Saberes y Pensamiento Científico").</summary>
    public string CampoFormativo { get; set; } = string.Empty;

    public TopicStatus Status { get; set; } = TopicStatus.InProgress;

    /// <summary>Intentos realizados sobre el tema (rango 0..3).</summary>
    public int Attempts { get; set; }

    /// <summary>Segundos reales acumulados dedicados al tema.</summary>
    public int TimeSpentSeconds { get; set; }

    /// <summary>Insignia obtenida, si la hay (p. ej. "Rayo de Inspiración").</summary>
    public string? BadgeEarned { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public DateTime LastActivityAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>
    /// Registra actividad del niño: acumula los segundos transcurridos desde la última
    /// actividad, acotados por <paramref name="maxIdleSecondsPerTurn"/> para no inflar el
    /// tiempo ante inactividad prolongada, y actualiza la marca de última actividad.
    /// </summary>
    public void RecordActivity(DateTime nowUtc, int maxIdleSecondsPerTurn = 600)
    {
        var elapsedSeconds = (int)(nowUtc - LastActivityAtUtc).TotalSeconds;
        TimeSpentSeconds += Math.Clamp(elapsedSeconds, 0, maxIdleSecondsPerTurn);
        LastActivityAtUtc = nowUtc;
    }
}