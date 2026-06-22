using Microsoft.AspNetCore.Http;
using Rpom.Application.Abstraction.User;

namespace Rpom.Infrastructure.User;

internal sealed class CurrentTerminal(IHttpContextAccessor httpContextAccessor) : ICurrentTerminal
{
    public const string HeaderName = "X-Terminal-Token";

    public string? TerminalToken
    {
        get
        {
            string? raw = httpContextAccessor.HttpContext?.Request.Headers[HeaderName];
            return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
        }
    }
}
