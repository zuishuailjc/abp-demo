using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Acme.TicketSystem.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Acme.TicketSystem.Tickets;

/// <summary>
/// ITicketSaleRepository 的 EF Core 实现。
/// 明细行、票、账务流水永远跟聚合根一起加载，所以默认就把 details 带上。
/// 少了任何一个 Include，退票时都会因为"集合是空的"而失败。
/// </summary>
public class EfCoreTicketSaleRepository
    : EfCoreRepository<TicketSystemDbContext, TicketSale, Guid>, ITicketSaleRepository
{
    public EfCoreTicketSaleRepository(IDbContextProvider<TicketSystemDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public override async Task<IQueryable<TicketSale>> WithDetailsAsync()
    {
        return (await GetQueryableAsync())
            .Include(x => x.Lines)
            .Include(x => x.Tickets)
            .Include(x => x.LedgerEntries);
    }

    public async Task<TicketSale?> FindByTicketNoAsync(
        string ticketNo,
        CancellationToken cancellationToken = default)
    {
        var query = await WithDetailsAsync();

        return await query.FirstOrDefaultAsync(
            x => x.Tickets.Any(t => t.TicketNo == ticketNo),
            cancellationToken);
    }
}
