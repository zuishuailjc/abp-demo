using System.ComponentModel.DataAnnotations;

namespace Acme.TicketSystem.Tickets;

/// <summary>退票入参。原项目 Refund(string ticketNO) 只有票号，这里补一个原因便于记录。</summary>
public class RefundTicketInput
{
    [Required]
    [StringLength(TicketConsts.MaxTicketNoLength)]
    public string TicketNo { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Reason { get; set; }
}
