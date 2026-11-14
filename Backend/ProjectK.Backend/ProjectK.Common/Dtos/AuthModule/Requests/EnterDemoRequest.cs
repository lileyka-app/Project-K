using System.Text.Json.Serialization;
using ProjectK.Common.Enums.AuthModule;

namespace ProjectK.Common.Dtos.AuthModule.Requests;

/// <summary>Body of <c>POST api/demo/login</c>: the seat to take in the demo kurin.</summary>
public sealed record EnterDemoRequest([property: JsonRequired] DemoSeat Seat);
