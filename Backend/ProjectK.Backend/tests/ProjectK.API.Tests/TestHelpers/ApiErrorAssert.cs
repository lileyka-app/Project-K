using Microsoft.AspNetCore.Mvc;

namespace ProjectK.API.Tests.TestHelpers;

/// <summary>
/// Asserts the single error contract every failing endpoint now returns: an <see cref="ObjectResult"/>
/// whose body is <c>{ error, message }</c>. Kept in one place so the contract has one executable
/// definition — before 0.19.0 the API answered failures in five different shapes, including bare
/// status results that ASP.NET rendered as ProblemDetails with no message at all.
/// </summary>
public static class ApiErrorAssert
{
    public static void HasError(IActionResult result, int expectedStatusCode, string? expectedErrorCode = null)
    {
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(expectedStatusCode, objectResult.StatusCode);

        var body = objectResult.Value;
        Assert.NotNull(body);

        var error = Read(body, "error");
        var message = Read(body, "message");

        Assert.False(string.IsNullOrWhiteSpace(error), "The error body carries no error code.");
        Assert.False(string.IsNullOrWhiteSpace(message), "The error body carries no message.");

        if (expectedErrorCode != null)
        {
            Assert.Equal(expectedErrorCode, error);
        }
    }

    private static string? Read(object body, string propertyName)
    {
        var property = body.GetType().GetProperty(propertyName);
        Assert.True(property != null, $"The error body has no '{propertyName}' property.");
        return property!.GetValue(body) as string;
    }
}
