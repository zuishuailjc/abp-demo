using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Acme.TicketSystem.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Timing;

namespace Acme.TicketSystem.Tickets;

/// <summary>
/// 票号生成器实现：查当天最大票号再 +1。
/// <para>
/// 这是最容易出问题的地方，所以单拎出来当一个"被明确标注的弱点"：
/// 并发下两个请求可能拿到同一段号，靠 SoldTicket.TicketNo 的唯一索引兜底。
/// 生产环境应该换成号段表 + 悲观锁、数据库序列或雪花算法，
/// 但那些都是"基础设施细节"，不影响聚合本身的写法。
/// </para>
/// </summary>
public class TicketNumberGenerator : ITicketNumberGenerator, ITransientDependency
{
    private readonly TicketSystemDbContext _dbContext;
    private readonly IClock _clock;

    public TicketNumberGenerator(TicketSystemDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<IReadOnlyList<string>> GenerateAsync(string prefix, int count)
    {
        if (count <= 0)
        {
            return Array.Empty<string>();
        }

        var head = prefix + _clock.Now.ToString("yyyyMMdd");

        var lastTicketNo = await _dbContext.Set<SoldTicket>()
            .Where(x => x.TicketNo.StartsWith(head))
            .OrderByDescending(x => x.TicketNo)
            .Select(x => x.TicketNo)
            .FirstOrDefaultAsync();

        var start = 1;
        if (lastTicketNo != null &&
            int.TryParse(lastTicketNo.Substring(head.Length), out var lastValue))
        {
            start = lastValue + 1;
        }

        return Enumerable.Range(start, count)
            .Select(i => head + i.ToString("D5"))
            .ToList();
    }
}
