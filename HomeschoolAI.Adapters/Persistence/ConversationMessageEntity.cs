namespace HomeschoolAI.Adapters.Persistence;

/// <summary>
/// Mensaje de conversación persistido en PostgreSQL (tabla <c>app_identity.conversation_messages</c>).
/// </summary>
public sealed class ConversationMessageEntity
{
    public Guid Id { get; set; }

    public string SessionId { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}