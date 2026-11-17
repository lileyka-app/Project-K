using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ProjectK.API.Tests.Security;

/// <summary>
/// Program.cs relies on UseExceptionHandler to keep the CORS headers on a 500: without them the
/// browser reports a CORS error and the app cannot read the failure. This pins that contract with
/// the same middleware order.
/// </summary>
public class UnhandledErrorCorsTests
{
    private const string Origin = "https://app.example";

    [Fact]
    public async Task UnhandledException_Returns500ProblemThatKeepsCorsHeaders()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseTestServer();
        builder.Services.AddProblemDetails();
        builder.Services.AddCors(options => options.AddPolicy("EnvCorsPolicy", policy =>
            policy.WithOrigins(Origin).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

        await using var app = builder.Build();
        app.UseExceptionHandler();
        app.UseRouting();
        app.UseCors("EnvCorsPolicy");
        app.MapPost("/fails", IResult () => throw new InvalidOperationException("boom"));
        await app.StartAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/fails");
        request.Headers.Add("Origin", Origin);
        using var response = await app.GetTestClient().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowed).Should().BeTrue();
        allowed!.Single().Should().Be(Origin);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        (await response.Content.ReadAsStringAsync()).Should().NotContain("boom");

        await app.StopAsync();
    }
}
