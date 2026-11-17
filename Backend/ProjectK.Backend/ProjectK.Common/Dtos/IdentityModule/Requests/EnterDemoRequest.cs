using System.Text.Json.Serialization;
using ProjectK.Common.Enums.IdentityModule;

namespace ProjectK.Common.Dtos.IdentityModule.Requests;

/// <summary>Body of <c>POST api/demo/login</c>: the seat to take in the demo kurin.</summary>
public sealed record EnterDemoRequest([property: JsonRequired] DemoSeat Seat);
