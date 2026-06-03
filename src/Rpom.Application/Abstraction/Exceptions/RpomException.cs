using Rpom.Domain.Common;

namespace Rpom.Application.Abstraction.Exceptions;

public sealed class RpomException : Exception
{
    public RpomException(string requestName, Error? error = default, Exception? innerException = default)
        : base("Application exception", innerException)
    {
        RequestName = requestName;
        Error = error;
    }

    public string RequestName { get; }
    public Error? Error { get; }
}
