namespace HomeschoolAI.Domain.Progress;

/// <summary>
/// Progreso acumulado de un perfil de niño (relación 1:1 con <c>ChildProfile</c>).
/// Se separa del perfil para aislar la identidad (datos del menor) de la dimensión
/// de aprendizaje (XP y nivel), y para evolucionar el modelo de progreso sin tocar
/// la tabla de perfiles.
/// </summary>
public sealed class ChildProgress
{
    /// <summary>Clave primaria y FK 1:1 hacia <c>ChildProfile.Id</c>.</summary>
    public Guid ChildProfileId { get; set; }

    public int TotalXP { get; set; }

    public int CurrentLevel { get; set; }
}
