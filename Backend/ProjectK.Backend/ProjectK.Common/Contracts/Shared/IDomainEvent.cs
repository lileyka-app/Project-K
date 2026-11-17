namespace ProjectK.Common.Contracts.Shared;

/// <summary>
/// Something that happened in the domain, stated as a fact and carrying everything a listener needs
/// to react to it. An event never says what should be done about it — that is the listener's call.
/// </summary>
public interface IDomainEvent
{
}
