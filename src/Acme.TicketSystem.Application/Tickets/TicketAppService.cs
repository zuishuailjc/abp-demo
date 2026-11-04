using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Acme.TicketSystem.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;

namespace Acme.TicketSystem.Tickets;

/// <summary>
/// 票务应用服务：票务模块的所有用例都在这里，售票和退票是它的两个方法。
/// 聚合根 TicketSale 只是内部模型，API 的命名跟着"模块"走：/api/app/ticket/*。
/// </summary>
[Authorize(TicketSystemPermissions.Tickets.Default)]
public class TicketAppService : TicketSystemAppService, ITicketAppService
{
    private readonly ITicketSaleRepository _ticketSaleRepository;
    private readonly ITicketNumberGenerator _ticketNumberGenerator;
    private readonly TicketPricingDomainService _ticketPricingDomainService;

    public TicketAppService(
        ITicketSaleRepository ticketSaleRepository,
        ITicketNumberGenerator ticketNumberGenerator,
        TicketPricingDomainService ticketPricingDomainService)
    {
        _ticketSaleRepository = ticketSaleRepository;
        _ticketNumberGenerator = ticketNumberGenerator;
        _ticketPricingDomainService = ticketPricingDomainService;
    }

    /// <summary>窗口售票：POST /api/app/ticket/sell</summary>
    [Authorize(TicketSystemPermissions.Tickets.Sell)]
    public async Task<TicketSaleDto> SellAsync(SellTicketsInput input)
    {
        // 定价交给领域服务：它要读票种目录和优惠活动，
        // 这些都不属于这张售票单，所以规则不能塞进聚合，只能放领域服务。
        // 客户端传过来的只有"要哪个票、几张"，没有价格。
        var pricing = await _ticketPricingDomainService.CalculateAsync(
            input.Lines.Select(x => new PricingLineRequest(x.TicketCode, x.Quantity)).ToList());

        var sale = new TicketSale(
            CurrentTenant.Id,
            GenerateSaleNo(),
            input.StoreId,
            CurrentUser.UserName ?? "system",
            Clock.Now,
            input.CustomerName,
            input.CustomerPhone);

        // 聚合只负责记账：价格已经定好，它只保证"卖多少钱就记多少钱"
        foreach (var quote in pricing.Quotes)
        {
            sale.AddLine(quote.TicketCode, quote.TicketName, quote.UnitPrice, quote.Quantity);
        }

        // 取号和出票分开：聚合不关心票号从哪来，也就方便了单测
        var ticketNumbers = await _ticketNumberGenerator.GenerateAsync("T", sale.TotalQuantity);
        sale.IssueTickets(ticketNumbers);

        await _ticketSaleRepository.InsertAsync(sale, autoSave: true);

        var dto = ObjectMapper.Map<TicketSale, TicketSaleDto>(sale);

        // 原价和优惠是定价时的信息，聚合自己不知道，所以单独回填
        dto.OriginalAmount = pricing.OriginalAmount;
        dto.DiscountAmount = pricing.DiscountAmount;

        return dto;
    }

    /// <summary>票种目录（只读）：GET /api/app/ticket/prices</summary>
    [Authorize(TicketSystemPermissions.Tickets.Default)]
    public async Task<List<TicketPriceDto>> GetPricesAsync()
    {
        var prices = await _ticketPricingDomainService.GetActivePricesAsync();

        return ObjectMapper.Map<List<TicketPrice>, List<TicketPriceDto>>(prices);
    }

    /// <summary>
    /// 窗口核销：POST /api/app/ticket/check-in
    /// 注意：这里只负责改本地状态，并发布 TicketValidatedEvent 领域事件。
    /// 外部系统的通知由事件订阅方去做，核销本身不会因为外部接口不通而失败。
    /// </summary>
    [Authorize(TicketSystemPermissions.Tickets.Default)]
    public async Task<TicketSaleDto> CheckInAsync(CheckInInput input)
    {
        var sale = await _ticketSaleRepository.FindByTicketNoAsync(input.TicketNo)
                   ?? throw new BusinessException(TicketSystemDomainErrorCodes.TicketNotFound)
                       .WithData("TicketNo", input.TicketNo);

        sale.ValidateTicket(input.TicketNo, Clock.Now);

        await _ticketSaleRepository.UpdateAsync(sale, autoSave: true);

        return ObjectMapper.Map<TicketSale, TicketSaleDto>(sale);
    }

    /// <summary>按票号退票：POST /api/app/ticket/refund</summary>
    [Authorize(TicketSystemPermissions.Tickets.Refund)]
    public async Task<TicketSaleDto> RefundAsync(RefundTicketInput input)
    {
        // 票号 -> 所属售票单（聚合根），退票必须整单加载后才能判断"全退/部分退"
        var sale = await _ticketSaleRepository.FindByTicketNoAsync(input.TicketNo);

        if (sale == null)
        {
            throw new BusinessException(TicketSystemDomainErrorCodes.TicketNotFound)
                .WithData("TicketNo", input.TicketNo);
        }

        sale.RefundTicket(input.TicketNo, input.Reason, Clock.Now);

        await _ticketSaleRepository.UpdateAsync(sale, autoSave: true);

        return ObjectMapper.Map<TicketSale, TicketSaleDto>(sale);
    }

/// <summary>交易流水列表：直接从聚合的流水分支展平查询，只读。</summary>
    [Authorize(TicketSystemPermissions.Tickets.Default)]
    public async Task<LedgerResultDto> GetLedgerAsync(LedgerQueryInput input)
    {
        var saleQuery = await _ticketSaleRepository.GetQueryableAsync();

        // 聚合内集合展平成一张 "表"，等价于 SQL 里的 JOIN
        var query = saleQuery.SelectMany(sale => sale.LedgerEntries);

        if (!string.IsNullOrWhiteSpace(input.TicketNo))
        {
            var ticketNo = input.TicketNo!.Trim();
            query = query.Where(x => x.TicketNo.Contains(ticketNo));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);

        var amounts = await AsyncExecuter.ToListAsync(query.Select(x => x.Amount));
        var credits = await AsyncExecuter.ToListAsync(query.Select(x => x.Credit));

        var items = await AsyncExecuter.ToListAsync(
            query
                .OrderByDescending(x => x.CreatedAt)
                .Skip(Math.Max(0, input.SkipCount))
                .Take(input.MaxResultCount <= 0 ? 20 : Math.Min(input.MaxResultCount, 200))
                .Select(x => new TicketLedgerEntryDto
                {
                    TicketNo = x.TicketNo,
                    EntryType = x.EntryType.ToString(),
                    Quantity = x.Quantity,
                    Amount = x.Amount,
                    Balance = x.Balance,
                    Credit = x.Credit,
                    CreatedAt = x.CreatedAt
                }));

        return new LedgerResultDto
        {
            Items = items,
            TotalCount = totalCount,
            TotalAmount = amounts.Sum(),
            TotalCredit = credits.Sum()
        };
    }

    private string GenerateSaleNo() => $"S{Clock.Now:yyyyMMddHHmmssfff}";
}
