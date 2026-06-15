namespace ClassificaLega.Api.Services;

/// <summary>Carries an HTTP status for the endpoint layer to translate into a problem response.</summary>
public class ApiException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public static ApiException NotFound(string message) => new(404, message);
    public static ApiException BadRequest(string message) => new(400, message);
    public static ApiException Conflict(string message) => new(409, message);
}
