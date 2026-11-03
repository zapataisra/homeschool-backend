namespace HomeschoolAI.Domain.Exceptions;

/// <summary>
/// Excepción de dominio con un código de estado HTTP asociado. El middleware de
/// manejo de errores la convierte en <c>ProblemDetails</c> con ese código.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }

    public abstract int StatusCode { get; }
}

/// <summary>Credenciales inválidas (login). Código 401.</summary>
public sealed class InvalidCredentialsException : DomainException
{
    public InvalidCredentialsException() : base("Credenciales inválidas.")
    {
    }

    public override int StatusCode => 401;
}

/// <summary>El correo ya está registrado. Código 409.</summary>
public sealed class EmailAlreadyRegisteredException : DomainException
{
    public EmailAlreadyRegisteredException() : base("El correo ya está registrado.")
    {
    }

    public override int StatusCode => 409;
}

/// <summary>Recurso no encontrado o fuera del alcance de la familia. Código 404.</summary>
public sealed class ResourceNotFoundException : DomainException
{
    public ResourceNotFoundException(string resource) : base($"Recurso no encontrado: {resource}.")
    {
    }

    public override int StatusCode => 404;
}

/// <summary>Acción no permitida para el rol actual. Código 403.</summary>
public sealed class ForbiddenException : DomainException
{
    public ForbiddenException(string message = "No tienes permiso para realizar esta acción.") : base(message)
    {
    }

    public override int StatusCode => 403;
}

/// <summary>Validación de entrada fallida. Código 400.</summary>
public sealed class ValidationException : DomainException
{
    public ValidationException(string message) : base(message)
    {
    }

    public override int StatusCode => 400;
}
