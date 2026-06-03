using MediatR;
using Rpom.Domain.Common;

namespace Rpom.Application.Abstraction.Messaging;

public interface ICommand : IRequest<Result>, IBaseCommand;

public interface ICommand<TResponse> : IBaseCommand, IRequest<Result<TResponse>>;

public interface IBaseCommand;
