using System.Text;
using System.Text.Json;
using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace ProjectK.LoadTests;

static class Program
{
    static void Main(string[] args)
    {
        var (baseUrl, apiKey) = ParseArgs(args);

        Console.WriteLine($"Starting authenticated load tests against: {baseUrl}");

        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("WARNING: --api-key is empty. Requests will be subject to normal rate limits, and passwordless login will fail if configured.");
            Console.WriteLine("Usage: dotnet run --url <URL> --api-key <YOUR_SECRET>");
        }

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        string jwtToken = string.Empty;

        var scenario = Scenario.Create("read_probes_catalog", async context =>
        {
            var request = Http.CreateRequest("GET", $"{baseUrl}/api/catalog/probes")
                              .WithHeader("Accept", "application/json")
                              .WithHeader("Authorization", $"Bearer {jwtToken}");

            if (!string.IsNullOrEmpty(apiKey))
            {
                request = request.WithHeader("X-RateLimit-Bypass", apiKey);
            }

            try
            {
                var response = await Http.Send(httpClient, request);
                return response;
            }
            catch (TaskCanceledException)
            {
                // Timeout occurred
                return Response.Fail(statusCode: "Timeout", message: "Request took longer than 3s");
            }
            catch (Exception ex)
            {
                return Response.Fail(statusCode: "Error", message: ex.Message);
            }
        })
        .WithInit(async context =>
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("API Key (--api-key) is required to authenticate the load test user.");
            }

            context.Logger.Information("Authenticating load test user using API Key...");
            var loginPayload = new { apiKey = apiKey };
            var content = new StringContent(JsonSerializer.Serialize(loginPayload), Encoding.UTF8, "application/json");

            // Temporary HttpClient without the strict 3s timeout for initialization
            using var initClient = new HttpClient();
            var authResponse = await initClient.PostAsync($"{baseUrl}/api/auth/loadtest-login", content);
            if (!authResponse.IsSuccessStatusCode)
            {
                var errBody = await authResponse.Content.ReadAsStringAsync();
                context.Logger.Error("Passwordless authentication failed: {StatusCode}. Body: {Body}", authResponse.StatusCode, errBody);
                throw new InvalidOperationException("Authentication failed. Check --api-key.");
            }

            var responseContent = await authResponse.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(responseContent);
            jwtToken = jsonDoc.RootElement.GetProperty("accessToken").GetString() ?? "";

            if (string.IsNullOrEmpty(jwtToken))
            {
                throw new InvalidOperationException("Failed to extract token from login response");
            }

            context.Logger.Information("Successfully obtained JWT token.");
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            // Warmup phase
            Simulation.Inject(rate: 10, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10)),
            Simulation.Inject(rate: 50, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(15)),

            // Stress phase
            Simulation.Inject(rate: 100, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30)),
            Simulation.Inject(rate: 200, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(1)),
            Simulation.Inject(rate: 300, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(1)),
            Simulation.Inject(rate: 400, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(1)),
            Simulation.Inject(rate: 500, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(1))
        );

        NBomberRunner
            .RegisterScenarios(scenario)
            .WithReportFileName("projectk_load_test_report")
            .WithReportFolder("reports")
            .Run();
    }

    /// <summary>Simple argument parsing: --url &lt;url&gt; --api-key &lt;key&gt;.</summary>
    private static (string BaseUrl, string ApiKey) ParseArgs(string[] args)
    {
        var baseUrl = "http://localhost:5037";
        var apiKey = "";

        var i = 0;
        while (i < args.Length)
        {
            if (args[i] == "--url" && i + 1 < args.Length)
            {
                baseUrl = args[i + 1];
                i += 2;
            }
            else if (args[i] == "--api-key" && i + 1 < args.Length)
            {
                apiKey = args[i + 1];
                i += 2;
            }
            else
            {
                i++;
            }
        }

        return (baseUrl, apiKey);
    }
}
