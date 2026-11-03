using System;
using Volo.Abp.Domain.Entities;

namespace Acme.TicketSystem.Tickets;

/// <summary>
/// 售票明细行（聚合内实体）：一个票种 + 单价 + 购买张数。
/// 只有聚合根能创建和修改它，外部拿不到 setter。
/// </summary>
public class TicketSaleLine : Entity<Guid>
{
    /// <summary>所属售票单（EF 外键）。</summary>
    public Guid TicketSaleId { get; private set; }

    public string TicketCode { get; private set; } = string.Empty;

    public string TicketName { get; private set; } = string.Empty;

    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    /// <summary>小计。计算属性，不落库（在 DbContext 里 Ignore）。</summary>
    public decimal Amount => UnitPrice * Quantity;

    private TicketSaleLine()
    {
    }

    internal TicketSaleLine(
        Guid id,
        Guid ticketSaleId,
        string ticketCode,
        string ticketName,
        decimal unitPrice,
        int quantity)
        : base(id)
    {
        TicketSaleId = ticketSaleId;
        TicketCode = ticketCode;
        TicketName = ticketName;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }
}
