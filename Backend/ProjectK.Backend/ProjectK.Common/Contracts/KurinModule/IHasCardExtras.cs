namespace ProjectK.Common.Contracts.KurinModule;

/// <summary>A shape that carries the values of the край's own card fields.</summary>
public interface IHasCardExtras
{
    IDictionary<string, string> CardExtras { get; set; }
}
