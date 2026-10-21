using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectK.Common.Models.Enums;

public enum ResultType
{
    Success,
    Created,
    NotFound,
    BadRequest,
    Unauthorized,
    Forbidden,
    Conflict,
    InternalServerError,
    UnprocessableEntity,
    TooManyRequests
}
