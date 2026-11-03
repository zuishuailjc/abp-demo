using System.Collections.Generic;

namespace Acme.TicketSystem.Tickets;

/// <summary>交易流水查询条件。</summary>
public class LedgerQueryInput
{
    /// <summary>按票号模糊查，空表示全部。</summary>
    public string? TicketNo { get; set; }

    public int SkipCount { get; set; }

    public int MaxResultCount { get; set; } = 20;
}

/// <summary>流水列表 + 汇总。借方=Charge，贷方=Credit。</summary>
public class LedgerResultDto
{
    public List<TicketLedgerEntryDto> Items { get; set; } = new();

    public int TotalCount { get; set; }

    /// <summary>Charge 合计（正负相抵后应为 0）。</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Credit 合计（正负相抵后应为 0）。</summary>
    public decimal TotalCredit { get; set; }
}
