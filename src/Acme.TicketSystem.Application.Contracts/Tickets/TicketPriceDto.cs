namespace Acme.TicketSystem.Tickets;

/// <summary>
/// 票种目录项，给前端下拉框用。
/// </summary>
public class TicketPriceDto
{
    public string TicketCode { get; set; } = string.Empty;

    public string TicketName { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public bool IsActive { get; set; }
}
