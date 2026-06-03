using MediatR;
using Rpom.Domain.Common;

namespace Rpom.Application.Abstraction.Messaging;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>;
