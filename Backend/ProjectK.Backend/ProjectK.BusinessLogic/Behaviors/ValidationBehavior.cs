using System.Reflection;
using FluentValidation;
using MediatR;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Behaviors;

/// <summary>
/// Runs every <see cref="IValidator{T}"/> registered for the request before the handler.
/// On failure it short-circuits with a <see cref="ServiceResult{T}"/> of
/// <see cref="ResultType.BadRequest"/> — so validation errors flow through the same
/// result-to-HTTP mapping as the rest of the app instead of surfacing as exceptions.
/// A request with no registered validator passes straight through.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const string ValidationErrorCode = "ValidationFailed";

    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next(cancellationToken);
        }

        var context = new ValidationContext<TRequest>(request);

        // Run all validators for this request type in parallel, then collect every failure.
        var results = await Task.WhenAll(
            _validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        var failures = results
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count == 0)
        {
            return await next(cancellationToken);
        }

        var message = string.Join("; ", failures.Select(failure => failure.ErrorMessage));

        // TResponse is ServiceResult<TData>, but TData is only known at runtime here, so we
        // build the failure through the static ServiceResult<TData>.Failure factory via reflection.
        if (TryBuildServiceResultFailure(message, out var failureResponse))
        {
            return failureResponse;
        }

        // Fallback for the rare request whose response is not a ServiceResult<T>.
        throw new ValidationException(failures);
    }

    private static bool TryBuildServiceResultFailure(string message, out TResponse response)
    {
        response = default!;

        var responseType = typeof(TResponse);
        if (!responseType.IsGenericType ||
            responseType.GetGenericTypeDefinition() != typeof(ServiceResult<>))
        {
            return false;
        }

        var failureMethod = responseType.GetMethod(
            nameof(ServiceResult<object>.Failure),
            BindingFlags.Public | BindingFlags.Static);

        if (failureMethod is null)
        {
            return false;
        }

        response = (TResponse)failureMethod.Invoke(
            null,
            [ResultType.BadRequest, ValidationErrorCode, message])!;
        return true;
    }
}
