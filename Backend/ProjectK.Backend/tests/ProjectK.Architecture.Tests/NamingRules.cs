using System.Reflection;
using FluentValidation;
using MediatR;

namespace ProjectK.Architecture.Tests;

/// <summary>
/// One naming style for the MediatR surface (DEBT-06). Two conventions used to coexist — the
/// documented <c>…Command</c>/<c>…CommandHandler</c> in some modules and a bare
/// <c>UpsertMemberCommand</c>/<c>UpsertMemberCommandHandler</c> in the kurin module — and every new slice
/// picked whichever its neighbour used.
/// </summary>
public class NamingRules
{
    private static readonly Assembly BusinessLogic = typeof(ProjectK.BusinessLogic.Behaviors.ValidationBehavior<,>).Assembly;

    private static IEnumerable<Type> ConcreteTypes() =>
        BusinessLogic.GetTypes().Where(type => type.IsClass && !type.IsAbstract);

    private static Type? ImplementedGeneric(Type type, Type openGeneric) =>
        type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGeneric);

    [Fact]
    public void EveryRequest_ShouldEndInCommandOrQuery()
    {
        var offenders = ConcreteTypes()
            .Where(type => ImplementedGeneric(type, typeof(IRequestHandler<,>)) is not null)
            .Select(type => ImplementedGeneric(type, typeof(IRequestHandler<,>))!.GetGenericArguments()[0].Name)
            .Where(name => !name.EndsWith("Command", StringComparison.Ordinal) && !name.EndsWith("Query", StringComparison.Ordinal))
            .Distinct()
            .ToList();

        Assert.True(offenders.Count == 0, "Requests without a Command/Query suffix: " + string.Join(", ", offenders));
    }

    [Fact]
    public void EveryHandler_ShouldBeNamedAfterItsRequest()
    {
        var offenders = ConcreteTypes()
            .Select(type => (type, request: ImplementedGeneric(type, typeof(IRequestHandler<,>))?.GetGenericArguments()[0]))
            .Where(pair => pair.request is not null && pair.type.Name != pair.request!.Name + "Handler")
            .Select(pair => $"{pair.type.Name} handles {pair.request!.Name}")
            .ToList();

        Assert.True(offenders.Count == 0, "Handlers not named <Request>Handler: " + string.Join(", ", offenders));
    }

    [Fact]
    public void EveryValidator_ShouldBeNamedAfterItsRequest()
    {
        var offenders = ConcreteTypes()
            .Select(type => (type, validated: ImplementedGeneric(type, typeof(IValidator<>))?.GetGenericArguments()[0]))
            .Where(pair => pair.validated is not null
                && ImplementedGeneric(pair.validated!, typeof(IRequest<>)) is not null
                && pair.type.Name != pair.validated!.Name + "Validator")
            .Select(pair => $"{pair.type.Name} validates {pair.validated!.Name}")
            .ToList();

        Assert.True(offenders.Count == 0, "Validators not named <Request>Validator: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// A request, its handler and its validator are leaves: nothing derives from them, and saying so
    /// keeps a second handler from slipping in as a subclass of the first (POL-17).
    /// </summary>
    [Fact]
    public void EveryRequestHandlerAndValidator_ShouldBeSealed()
    {
        var derivedFrom = SourceTree.All
            .SelectMany(assembly => assembly.GetTypes())
            .Select(type => type.BaseType)
            .Where(baseType => baseType is not null)
            .Select(baseType => baseType!.IsGenericType ? baseType.GetGenericTypeDefinition() : baseType)
            .ToHashSet();

        var offenders = ConcreteTypes()
            .Where(type => !type.IsSealed && !type.IsNested && !derivedFrom.Contains(type))
            .Where(type => ImplementedGeneric(type, typeof(IRequestHandler<,>)) is not null
                || ImplementedGeneric(type, typeof(IRequestHandler<>)) is not null
                || ImplementedGeneric(type, typeof(INotificationHandler<>)) is not null
                || ImplementedGeneric(type, typeof(IValidator<>)) is not null
                || typeof(IBaseRequest).IsAssignableFrom(type))
            .Select(type => type.FullName!);

        Baseline.None(offenders, "MediatR requests, handlers and validators that are not sealed (POL-17):");
    }

    [Fact]
    public void EveryDomainEventHandler_ShouldEndInEventHandler()
    {
        var offenders = ConcreteTypes()
            .Where(type => ImplementedGeneric(type, typeof(INotificationHandler<>)) is not null)
            .Where(type => !type.Name.EndsWith("EventHandler", StringComparison.Ordinal))
            .Select(type => type.Name)
            .ToList();

        Assert.True(offenders.Count == 0, "Domain event handlers without the EventHandler suffix: " + string.Join(", ", offenders));
    }
}
