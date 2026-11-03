using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Acme.TicketSystem.Tickets;

/// <summary>
/// 售票聚合的仓储。只暴露聚合根，明细行和票由聚合自己维护。
/// </summary>
public interface ITicketSaleRepository : IRepository<TicketSale, Guid>
{
    /// <summary>
    /// 按票号找它所属的售票单（同时加载明细和票）。
    /// 退票只拿到一个票号，必须先回到聚合根才能做状态变更。
    /// </summary>
    Task<TicketSale?> FindByTicketNoAsync(string ticketNo, CancellationToken cancellationToken = default);

    
}
