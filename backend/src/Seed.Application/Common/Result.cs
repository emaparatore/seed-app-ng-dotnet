namespace Seed.Application.Common;

public sealed class Result<T>
{
    public bool Succeeded { get; private init; }
    public T? Data { get; private init; }
    public string[] Errors { get; private init; } = [];

    public static Result<T> Success(T data) => new() { Succeeded = true, Data = data };
    public static Result<T> Failure(params string[] errors) => new() { Succeeded = false, Errors = errors };
}
