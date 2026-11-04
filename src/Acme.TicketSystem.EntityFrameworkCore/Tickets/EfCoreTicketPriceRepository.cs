using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Acme.TicketSystem.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Acme.TicketSystem.Tickets;

/// <summary>
/// 票种目录仓储的 EF Core 实现。
/// <para>
/// 接口在 Domain 层，这里是适配器：领域层只知道"按编码查票价"，
/// 不关心底层是 SQL 表、缓存还是远程配置中心。
/// </para>
/// </summary>
public class EfCoreTicketPriceRepository
    : EfCoreRepository<TicketSystemDbContext, TicketPrice, Guid>, ITicketPriceRepository
{
    public EfCoreTicketPriceRepository(IDbContextProvider<TicketSystemDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<TicketPrice?> FindByCodeAsync(string ticketCode, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();

        return await dbSet
            .FirstOrDefaultAsync(x => x.TicketCode == ticketCode, GetCancellationToken(cancellationToken));
    }

    public async Task<List<TicketPrice>> GetActiveListAsync(CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();

        return await dbSet
            .Where(x => x.IsActive)
            .OrderBy(x => x.TicketCode)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }
}
