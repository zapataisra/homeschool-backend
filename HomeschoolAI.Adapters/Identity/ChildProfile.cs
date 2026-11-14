namespace HomeschoolAI.Adapters.Identity;

/// <summary>
/// Perfil del niño. NO hereda de <see cref="IdentityUser"/>: el niño no tiene
/// credenciales propias y opera en una sesión delegada de alcance limitado.
/// </summary>
public sealed class ChildProfile
{
    public Guid Id { get; set; }

    public Guid FamilyId { get; set; }

    /// <summary>Nombre o seudónimo del niño (identificador anónimo en logs y en el LLM).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Grado escolar (alineado a Common Core), p. ej. 1 a 6.</summary>
    public int Grade { get; set; }

    /// <summary>Fase SEP correspondiente (3, 4 o 5); puede derivarse del grado.</summary>
    public int? Fase { get; set; }

    /// <summary>Nombre del archivo de avatar del niño (p. ej. "avatar-aguila.svg").</summary>
    public string AvatarUrl { get; set; } = "avatar-aguila.svg";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Family? Family { get; set; }
}
