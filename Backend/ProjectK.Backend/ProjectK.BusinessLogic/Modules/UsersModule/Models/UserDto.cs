using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.UsersModule.Models;

public class UserDto
{
    public Guid UserId { get; set; }
    public Guid? KurinKey { get; set; }
    public int? KurinNumber { get; set; }

    /// <summary>
    /// The kurin's main улад. Each улад counts its own numbers, so the number alone would put
    /// 5-й курінь УПЮ and 5-й курінь УСП under one heading.
    /// </summary>
    public KurinBranch? KurinBranch { get; set; }
    public IReadOnlyList<KurinNumbering>? KurinBranches { get; set; }
    public string? KurinNamedAfter { get; set; }

    /// <summary>The kurin's край: numbers are the kurin's own per край too, so two УПЮ kurins may share one.</summary>
    public string? KurinKraiCode { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Role { get; set; }
    public bool TwoFactorEnabled { get; set; }

    /// <summary>Refused at sign-in by an administrator; every session was ended when it was applied.</summary>
    public bool IsSuspended { get; set; }
}
