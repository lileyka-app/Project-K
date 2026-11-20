using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Records.DuesModule;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Services;

/// <summary>A quarter's price for one person, with the пільга of the край that set it, if one did.</summary>
public sealed record DuesPrice(DuesAmount Amount, DuesConcessionGrant? Grant, DuesAmount WithoutGrants);
