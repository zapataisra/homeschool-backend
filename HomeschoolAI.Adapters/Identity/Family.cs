namespace HomeschoolAI.Adapters.Identity;

/// <summary>
/// Tenant principal del sistema. Todo dato de aprendizaje, identidad y pagos
/// se filtra por <see cref="Id"/> para aislar a las familias entre sí.
/// </summary>
public sealed class Family
{
    public Guid Id { get; set; }

    /// <summary>Nombre del grupo familiar (solo para el panel parental).</summary>
    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>True si la familia tiene la suscripción premium activa (la activa solo el webhook de Stripe).</summary>
    public bool IsPremium { get; set; }

    public ICollection<FamilyMember> Members { get; set; } = new List<FamilyMember>();

    public ICollection<ChildProfile> Children { get; set; } = new List<ChildProfile>();
}
