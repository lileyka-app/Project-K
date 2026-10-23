using ProjectK.Common.Models.Members;
using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using ProjectK.API.Models.Requests;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;

namespace ProjectK.API.Models.Requests;

public class UpsertMemberRequest : IHasParentContacts
{
    public Guid? MemberKey { get; set; }
    public Guid? GroupKey { get; set; }
    public Guid? KurinKey { get; set; }
    public bool CreateUserAccount { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    /// <summary>
    /// Left out by a form whose writer may not see the contacts; the stored address then stays.
    /// Whether one is needed at all is the profile write's to say, not the binder's.
    /// </summary>
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public DateOnly DateOfBirth { get; set; }
    /// <summary>Left out keeps what is stored; sent empty clears it.</summary>
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Address { get; set; }

    /// <summary>Left out keeps what is stored; sent empty clears it.</summary>
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? School { get; set; }

    /// <summary>Parents' contacts: left out keeps what is stored; sent empty clears it.</summary>
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? MotherFirstName { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? MotherLastName { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? MotherPhone { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? FatherFirstName { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? FatherLastName { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? FatherPhone { get; set; }
    public ICollection<PlastLevelHistoryDto> PlastLevelHistories { get; set; } = [];

    /// <summary>Values of the край's own card fields, by key, sent as <c>cardExtras[key]</c>.</summary>
    public Dictionary<string, string>? CardExtras { get; set; }
    public ICollection<LeadershipHistory> LeadershipHistories { get; set; } = [];
    public bool? RemoveProfilePhoto { get; set; }
    public IFormFile? Blob { get; set; }
}
