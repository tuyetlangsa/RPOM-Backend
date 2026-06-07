namespace Rpom.Api.Results;

public class ApiResult<TResult>
{
    public bool IsSuccess { get; set; }
    public TResult? Data { get; set; }

    public static ApiResult<TResult> Success(TResult? data) => new() { IsSuccess = true, Data = data };
}
