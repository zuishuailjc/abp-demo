using System;

namespace Acme.TicketSystem.Tickets;

/// <summary>
/// 票核销事件。核销成功后抛出，由订阅方监听。
/// <para>
/// 用领域事件而不是直接调用别的模块，是为了让 Tickets 聚合不认识任何外部系统：
/// 将来要接外部渠道时，加一个 ILocalEventHandler 监听即可，聚合本身不用改。
/// </para>
/// </summary>
public class TicketValidatedEvent
{
    public Guid TicketSaleId { get; }

    public string TicketNo { get; }

    public DateTime ValidatedAt { get; }

    public TicketValidatedEvent(Guid ticketSaleId, string ticketNo, DateTime validatedAt)
    {
        TicketSaleId = ticketSaleId;
        TicketNo = ticketNo;
        ValidatedAt = validatedAt;
    }
}
