using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Acme.TicketSystem.Tickets;

/// <summary>
/// 已出的一张票（聚合内实体）。原项目里对应 Main + TicketNO + Account 三张表，
/// 这里合并成一条，因为一次售票的交易信息本来就是同源的。
/// </summary>
public class SoldTicket : Entity<Guid>
{
    public Guid TicketSaleId { get; private set; }

    /// <summary>票号，打印成二维码用，全系统唯一。</summary>
    public string TicketNo { get; private set; } = string.Empty;

    public string TicketCode { get; private set; } = string.Empty;

    public string TicketName { get; private set; } = string.Empty;

    /// <summary>这一张票的单价。</summary>
    public decimal Amount { get; private set; }

    /// <summary>是否已退票。</summary>
    public bool IsRefunded { get; private set; }

    public DateTime? RefundedAt { get; private set; }

    public string? RefundReason { get; private set; }

    /// <summary>是否已核销（检票）。渠道票核销后要通知 OTA，否则对方还能退。</summary>
    public bool IsValidated { get; private set; }

    public DateTime? ValidatedAt { get; private set; }

    private SoldTicket()
    {
    }

    internal SoldTicket(
        Guid id,
        Guid ticketSaleId,
        string ticketNo,
        string ticketCode,
        string ticketName,
        decimal amount)
        : base(id)
    {
        TicketSaleId = ticketSaleId;
        TicketNo = ticketNo;
        TicketCode = ticketCode;
        TicketName = ticketName;
        Amount = amount;
    }

    /// <summary>
    /// 退这一张票。状态校验放在实体自己身上，
    /// 聚合根只负责"找到票 + 调它"，不用知道退票有哪些前置条件。
    /// </summary>
    internal void Refund(string? reason, DateTime refundedAt)
    {
        if (IsRefunded)
        {
            throw new BusinessException(TicketSystemDomainErrorCodes.TicketAlreadyRefunded)
                .WithData("TicketNo", TicketNo);
        }

        IsRefunded = true;
        RefundedAt = refundedAt;
        RefundReason = reason;
    }

    /// <summary>
    /// 核销。返回 true 表示这次是首次核销（需要触发对外通知），
    /// 重复核销返回 false，调用方据此决定要不要重复通知渠道。
    /// </summary>
    internal bool Validate(DateTime validatedAt)
    {
        if (IsRefunded)
        {
            throw new BusinessException(TicketSystemDomainErrorCodes.RefundedTicketCannotBeValidated)
                .WithData("TicketNo", TicketNo);
        }

        if (IsValidated)
        {
            return false;
        }

        IsValidated = true;
        ValidatedAt = validatedAt;
        return true;
    }
}
