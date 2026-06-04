namespace Shared.Exceptions;

public class BusinessException : Exception
{
    public int StatusCode { get; }

    public BusinessException(string message, int statusCode = 400) : base(message)
    {
        StatusCode = statusCode;
    }
}

public class NotFoundException : BusinessException
{
    public NotFoundException(string message) : base(message, 404) { }
}

public class UnauthorizedException : BusinessException
{
    public UnauthorizedException(string message) : base(message, 401) { }
}

public class ForbiddenException : BusinessException
{
    public ForbiddenException(string message) : base(message, 403) { }
}

public class ConflictException : BusinessException
{
    public ConflictException(string message) : base(message, 409) { }
}
