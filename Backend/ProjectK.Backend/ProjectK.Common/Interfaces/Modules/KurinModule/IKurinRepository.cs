using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Interfaces.Modules.KurinModule;

public interface IKurinRepository : IBaseEntityRepository<Kurin>
{
    /// <summary>
    /// A kurin number is unique within its край and гілка only, so a lookup by number names both;
    /// without a гілка it is УПЮ, where every number lived before the older гілки came.
    /// </summary>
    Task<Kurin?> GetByNumberAsync(string kraiCode, int number, CancellationToken token = default, KurinBranch branch = KurinBranch.UPYu);

    /// <summary>Whether a УПЮ kurin of this край has the number.</summary>
    Task<bool> ExistsAsync(string kraiCode, int number, CancellationToken token = default);

    /// <summary>
    /// Whether another kurin of the край already holds the number in this гілка, in any of its гілки
    /// rather than only its main one. <paramref name="exceptKurinKey"/> leaves out the kurin asking.
    /// </summary>
    Task<bool> IsNumberTakenAsync(string kraiCode, KurinBranch branch, int number, Guid? exceptKurinKey = null, CancellationToken token = default);

    /// <summary>The kurins of one край, by number; підготовчі last.</summary>
    Task<IReadOnlyList<Kurin>> GetInKraiAsync(string kraiCode, CancellationToken token = default);
}
