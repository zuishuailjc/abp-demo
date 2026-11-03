using System.ComponentModel.DataAnnotations;

namespace Acme.TicketSystem.Tickets;

/// <summary>窗口核销入参。</summary>
public class CheckInInput
{
    [Required]
    [StringLength(TicketConsts.MaxTicketNoLength)]
    public string TicketNo { get; set; } = string.Empty;
}
