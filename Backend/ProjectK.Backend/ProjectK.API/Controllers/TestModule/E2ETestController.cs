using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectK.API.Extensions;
using ProjectK.API.Hosting;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Rules.Shared;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Seeding;

namespace ProjectK.API.Controllers.TestModule;

/// <summary>
/// Fixtures for the end-to-end suite. Outside the E2E environment the controller is taken out of the
/// application model by <see cref="E2EOnlyControllerFeatureProvider"/>, so its routes do not exist
/// in a normal deployment; the environment check inside each action is the second line, for a host
/// that was built without that provider.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/test/e2e")]
public sealed class E2ETestController : ControllerBase
{
    private const string ResetTokenHeader = "X-E2E-Reset-Token";

    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _services;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<E2ETestController> _logger;

    public E2ETestController(
        IWebHostEnvironment environment,
        IConfiguration configuration,
        IServiceProvider services,
        AppDbContext dbContext,
        ILogger<E2ETestController> logger)
    {
        _environment = environment;
        _configuration = configuration;
        _services = services;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Returns the database to the seeded fixture state.
    /// </summary>
    /// <remarks>
    /// The suite calls this between runs. Without it the run inherits whatever the previous one left behind
    /// — accumulated accounts eventually hit the beta cap and the suite starts failing in ways that look
    /// like flakes.
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("reset")]
    public async Task<IActionResult> Reset(CancellationToken cancellationToken)
    {
        var guard = ValidateE2ERequest();
        if (guard != null)
        {
            return guard;
        }

        await _dbContext.Database.EnsureDeletedAsync(cancellationToken);
        await _dbContext.Database.MigrateAsync(cancellationToken);
        await DataSeeder.SeedAsync(_services);

        return Ok(new
        {
            status = "reset",
            environment = _environment.EnvironmentName,
            users = new
            {
                admin = "admin@projectk.com",
                manager = "manager1@projectk.com",
                mentor = "mentor1@projectk.com",
                member = "g1member1@projectk.com"
            }
        });
    }

    /// <summary>
    /// Returns the newest invitation token for an address, standing in for reading the email.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("invitations/by-email")]
    public async Task<IActionResult> GetLatestInvitationByEmail([FromQuery] string email, CancellationToken cancellationToken)
    {
        var guard = ValidateE2ERequest();
        if (guard != null)
        {
            return guard;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return this.Failure(ResultType.BadRequest, "EmailRequired", "Email is required.");
        }

        var normalizedEmail = email.Trim().ToUpperInvariant();
        var invitation = await _dbContext.Invitations
            .AsNoTracking()
            .Include(item => item.WaitlistEntry)
            .Include(item => item.TargetUser)
            .Where(item =>
                item.WaitlistEntry.Email.ToUpper() == normalizedEmail ||
                (item.TargetUser != null && item.TargetUser.NormalizedEmail == normalizedEmail))
            .OrderByDescending(item => item.WaitlistEntry.InvitationSentAtUtc)
            .ThenByDescending(item => item.ExpiresAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (invitation == null)
        {
            return this.Failure(ResultType.NotFound, "InvitationNotFound", "Invitation was not found.");
        }

        return Ok(new
        {
            invitation.InvitationKey,
            invitation.Token,
            invitation.WaitlistEntryKey,
            invitation.TargetUserKey,
            invitation.ExpiresAtUtc,
            invitation.UsedAtUtc,
            invitation.IsRevoked,
            Email = invitation.WaitlistEntry.Email
        });
    }

    private IActionResult? ValidateE2ERequest()
    {
        if (!_environment.IsEnvironment("E2E"))
        {
            // Deliberately bare: a structured error would confirm the endpoint exists outside E2E.
            return NotFound();
        }

        var expectedToken = _configuration["E2E:ResetToken"];
        if (string.IsNullOrWhiteSpace(expectedToken))
        {
            _logger.LogWarning("E2E endpoint requested, but E2E:ResetToken is not configured.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "E2E reset is not configured." });
        }

        if (!Request.Headers.TryGetValue(ResetTokenHeader, out var providedToken)
            || !SecretComparisonRules.Matches(providedToken.ToString(), expectedToken))
        {
            return this.Failure(ResultType.Unauthorized, "InvalidResetToken", "Invalid E2E reset token.");
        }

        return null;
    }
}
