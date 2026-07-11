namespace CityLeague.Api.Common;

/// <summary>An exception carrying an HTTP status code, translated to ProblemDetails by middleware.</summary>
public class ServiceException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public static ServiceException NotFound(string message = "Not found") => new(404, message);
    public static ServiceException Forbidden(string message = "Forbidden") => new(403, message);
    public static ServiceException Conflict(string message) => new(409, message);
    public static ServiceException BadRequest(string message) => new(400, message);
}
