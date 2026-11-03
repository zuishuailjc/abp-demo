using System.Threading.Tasks;
using Acme.TicketSystem.Tickets;
using Volo.Abp.Application.Services;

namespace Acme.TicketSystem.Tickets;

/// <summary>
/// 票务应用服务。一个"模块"一个服务，售票/退票都是它的方法：
///   SellAsync   → POST /api/app/ticket/sell
///   RefundAsync → POST /api/app/ticket/refund
/// 聚合根仍叫 TicketSale（一次售票单），但它只是内部模型，不是 API 的命名依据。
/// </summary>
public interface ITicketAppService : IApplicationService
{
    /// <summary>窗口售票。</summary>
    Task<TicketSaleDto> SellAsync(SellTicketsInput input);

    /// <summary>按票号退票（一次退一张）。</summary>
    Task<TicketSaleDto> RefundAsync(RefundTicketInput input);

    /// <summary>窗口核销。首次核销会触发 TicketValidatedEvent 领域事件。</summary>
    Task<TicketSaleDto> CheckInAsync(CheckInInput input);

    /// <summary>交易流水列表（只读查询）。GET /api/app/ticket/ledger</summary>
    Task<LedgerResultDto> GetLedgerAsync(LedgerQueryInput input);
}
