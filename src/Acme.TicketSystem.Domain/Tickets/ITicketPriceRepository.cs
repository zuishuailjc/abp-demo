using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Acme.TicketSystem.Tickets;

/// <summary>
/// 票种目录的仓储。
/// <para>
/// 接口放在领域层、实现放在基础设施层（EF Core），是 DDD 常见的"端口—适配器"写法：
/// 领域层只知道自己需要"按编码查票价"，不关心它是从表里查的、还是从缓存读的。
/// </para>
/// </summary>
public interface ITicketPriceRepository : IRepository<TicketPrice, Guid>
{
    /// <summary>按票种编码查目录价，找不到返回 null。</summary>
    Task<TicketPrice?> FindByCodeAsync(string ticketCode, CancellationToken cancellationToken = default);

    /// <summary>所有在售票种，按编码排序。</summary>
    Task<List<TicketPrice>> GetActiveListAsync(CancellationToken cancellationToken = default);
}
