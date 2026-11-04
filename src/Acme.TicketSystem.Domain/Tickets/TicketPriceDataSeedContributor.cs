using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace Acme.TicketSystem.Tickets;

/// <summary>
/// 票种目录的种子数据。
/// <para>
/// 没有目录就没法定价，所以第一次初始化时要先把几个常见票种放进去。
/// 幂等：已存在就不重复插入，可以反复跑。
/// </para>
/// </summary>
public class TicketPriceDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly ITicketPriceRepository _ticketPriceRepository;
    private readonly ICurrentTenant _currentTenant;

    public TicketPriceDataSeedContributor(
        ITicketPriceRepository ticketPriceRepository,
        ICurrentTenant currentTenant)
    {
        _ticketPriceRepository = ticketPriceRepository;
        _currentTenant = currentTenant;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        // 目录是租户自己的事，这里只给 host 塞一份初始目录
        if (context.TenantId.HasValue)
        {
            return;
        }

        var existing = await _ticketPriceRepository.GetListAsync();
        var existingCodes = new HashSet<string>();

        foreach (var item in existing)
        {
            existingCodes.Add(item.TicketCode);
        }

        var defaults = new[]
        {
            (Code: "ADULT", Name: "成人票", Price: 100m),
            (Code: "STUDENT", Name: "学生票", Price: 50m),
            (Code: "FAMILY", Name: "家庭票", Price: 180m),
            (Code: "VIP", Name: "贵宾票", Price: 300m)
        };

        foreach (var item in defaults)
        {
            if (existingCodes.Contains(item.Code))
            {
                continue;
            }

            await _ticketPriceRepository.InsertAsync(
                new TicketPrice(_currentTenant.Id, item.Code, item.Name, item.Price),
                autoSave: true);
        }
    }
}
