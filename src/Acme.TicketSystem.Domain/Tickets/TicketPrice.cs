using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Acme.TicketSystem.Tickets;

/// <summary>
/// 票种定价（票种目录）："某个票种现在卖多少钱"。
/// <para>
/// 这是一个<strong>独立的聚合</strong>，它描述的是配置，不是某一次交易：
/// 同一份票种目录会被成千上万张售票单引用，所以它不能被塞进 TicketSale 聚合里——
/// 否则每卖一张票就要复制一份目录，聚合会无限膨胀。
/// </para>
/// <para>
/// 它和 TicketSale 的关系是"目录 → 成交"：售票时要先读目录才知道卖多少钱，
/// 但两者生命周期不同、并发修改互不相关，所以是两个聚合，不是一个。
/// </para>
/// </summary>
public class TicketPrice : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    /// <summary>票种编码，如 ADULT。全局唯一。</summary>
    public string TicketCode { get; private set; } = string.Empty;

    /// <summary>票种名称，如 成人票。</summary>
    public string TicketName { get; private set; } = string.Empty;

    /// <summary>目录单价。</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>是否在售。停售的票种不能新卖，但历史票不受影响。</summary>
    public bool IsActive { get; private set; }

    private TicketPrice()
    {
        // EF Core
    }

    public TicketPrice(
        Guid? tenantId,
        string ticketCode,
        string ticketName,
        decimal unitPrice,
        bool isActive = true)
    {
        TenantId = tenantId;
        TicketCode = Check.NotNullOrWhiteSpace(ticketCode, nameof(ticketCode), TicketConsts.MaxTicketCodeLength);
        TicketName = Check.NotNullOrWhiteSpace(ticketName, nameof(ticketName), TicketConsts.MaxTicketNameLength);

        if (unitPrice < 0)
        {
            throw new BusinessException(TicketSystemDomainErrorCodes.TicketPriceCannotBeNegative)
                .WithData("TicketCode", ticketCode);
        }

        UnitPrice = unitPrice;
        IsActive = isActive;
    }

    /// <summary>调价。改价只影响之后的售票，已经卖出去的票按成交价记账，不受影响。</summary>
    public void ChangePrice(decimal unitPrice)
    {
        if (unitPrice < 0)
        {
            throw new BusinessException(TicketSystemDomainErrorCodes.TicketPriceCannotBeNegative)
                .WithData("TicketCode", TicketCode);
        }

        UnitPrice = unitPrice;
    }

    /// <summary>停售。已售出的票不受影响，只是不能再卖新的。</summary>
    public void Deactivate()
    {
        IsActive = false;
    }
}
