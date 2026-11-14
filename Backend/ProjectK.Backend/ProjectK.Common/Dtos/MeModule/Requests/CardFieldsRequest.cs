using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Dtos.MeModule.Requests;

/// <summary>Body of <c>PUT api/me/card/fields</c>.</summary>
public sealed record CardFieldsRequest(CardField Fields);
