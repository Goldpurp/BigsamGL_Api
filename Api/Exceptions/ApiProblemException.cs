namespace Api.Exceptions;

public sealed class ApiProblemException : Exception
{
    public ApiProblemException(int status, string code, string title, string detail,
        IReadOnlyDictionary<string, string[]>? errors = null,
        IReadOnlyDictionary<string, object?>? extensions = null) : base(detail)
    {
        Status = status;
        Code = code;
        Title = title;
        Errors = errors;
        Extensions = extensions;
    }

    public int Status { get; }
    public string Code { get; }
    public string Title { get; }
    public IReadOnlyDictionary<string, string[]>? Errors { get; }
    public IReadOnlyDictionary<string, object?>? Extensions { get; }
}
