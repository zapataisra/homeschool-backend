using Xunit;

namespace HomeschoolAI.Tests;

/// <summary>
/// Colección compartida de pruebas de integración: una única instancia de
/// <see cref="IdentityTestContext"/> (base de pruebas + migraciones) para todas
/// las clases de integración, evitando migraciones concurrentes.
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<IdentityTestContext>
{
    public const string Name = "Integration";
}
