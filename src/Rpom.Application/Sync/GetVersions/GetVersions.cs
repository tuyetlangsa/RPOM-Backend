using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Domain.Common;

namespace Rpom.Application.Sync.GetVersions;

public static class GetVersions
{
    public sealed record Query(IReadOnlyList<string> Scopes) : IQuery<Response>;

    public sealed record Response(IReadOnlyDictionary<string, long> Versions);

    internal sealed class Handler(IVersionService versionService) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            // Filter unknown scopes silently — protects against typos and
            // future scope keys leaking into shared FE clients.
            var requested = request.Scopes.Where(VersionScopes.IsKnown).ToList();
            if (requested.Count == 0)
            {
                return Result.Success(new Response(new Dictionary<string, long>()));
            }

            IReadOnlyDictionary<string, long> versions = await versionService.GetAsync(requested, ct);
            return Result.Success(new Response(versions));
        }
    }
}
