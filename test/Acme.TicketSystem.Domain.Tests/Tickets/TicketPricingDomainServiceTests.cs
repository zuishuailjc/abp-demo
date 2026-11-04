using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Acme.TicketSystem.Tickets;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Acme.TicketSystem.Tickets;

/// <summary>
/// 定价领域服务的纯单元测试。
/// <para>
/// 和 <see cref="Domains.TicketSaleTests"/> 一样：不连库、不启 DI 容器，
/// 唯一的依赖"票种目录仓储"用 NSubstitute 打桩。
/// </para>
/// <para>
/// 这正是把定价做成领域服务的好处之一：它没有状态、不碰数据库，
/// 规则可以一条条单独断言；而如果定价是聚合里的方法，测它就得先把整张单建出来。
/// </para>
/// </summary>
public class TicketPricingDomainServiceTests
{
    private readonly ITicketPriceRepository _repository = Substitute.For<ITicketPriceRepository>();

    private TicketPricingDomainService CreateService()
    {
        return new TicketPricingDomainService(_repository);
    }

    private void GivenPrice(string code, string name, decimal unitPrice, bool isActive = true)
    {
        var price = new TicketPrice(null, code, name, unitPrice, isActive);

        _repository
            .FindByCodeAsync(code, Arg.Any<CancellationToken>())
            .Returns(price);
    }

    [Fact]
    public async Task 单价应当来自票种目录而不是客户端()
    {
        GivenPrice("ADULT", "成人票", 100m);

        var service = CreateService();

        // 买 1 张（100 元，没到满 200 的门槛，所以单价就是目录价）
        var result = await service.CalculateAsync(new[]
        {
            new PricingLineRequest("ADULT", 1)
        });

        var quote = result.Quotes.Single();

        quote.OriginalUnitPrice.ShouldBe(100m);
        quote.UnitPrice.ShouldBe(100m);
        quote.Quantity.ShouldBe(1);
        result.PayableAmount.ShouldBe(100m);
    }

    [Fact]
    public async Task 票种不存在时应当抛出业务异常()
    {
        // 仓储默认返回 null：目录里没有这个票种
        var service = CreateService();

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await service.CalculateAsync(new[]
            {
                new PricingLineRequest("NOT_EXIST", 1)
            }));

        ex.Code.ShouldBe(TicketSystemDomainErrorCodes.TicketPriceNotFound);
    }

    [Fact]
    public async Task 票种已停售时也应当拒绝()
    {
        GivenPrice("ADULT", "成人票", 100m, isActive: false);

        var service = CreateService();

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await service.CalculateAsync(new[]
            {
                new PricingLineRequest("ADULT", 1)
            }));

        ex.Code.ShouldBe(TicketSystemDomainErrorCodes.TicketPriceNotFound);
    }

    [Fact]
    public async Task 达到门槛时整单应当享受优惠并按比例摊到各行()
    {
        GivenPrice("ADULT", "成人票", 100m);
        GivenPrice("STUDENT", "学生票", 50m);

        var service = CreateService();

        // 原价 2×100 + 1×50 = 250，满 200 减 30，应付 220
        var result = await service.CalculateAsync(new[]
        {
            new PricingLineRequest("ADULT", 2),
            new PricingLineRequest("STUDENT", 1)
        });

        result.OriginalAmount.ShouldBe(250m);
        result.DiscountAmount.ShouldBe(30m);
        result.PayableAmount.ShouldBe(220m);

        // 优惠按各行原价比例摊：200 的行摊 24，50 的行摊 6
        var adult = result.Quotes[0];
        var student = result.Quotes[1];

        adult.UnitPrice.ShouldBe(88m);
        student.UnitPrice.ShouldBe(44m);

        adult.Amount.ShouldBe(176m);
        student.Amount.ShouldBe(44m);
    }

    [Fact]
    public async Task 未达到门槛时不应有优惠()
    {
        GivenPrice("STUDENT", "学生票", 50m);

        var service = CreateService();

        var result = await service.CalculateAsync(new[]
        {
            new PricingLineRequest("STUDENT", 1)
        });

        result.OriginalAmount.ShouldBe(50m);
        result.DiscountAmount.ShouldBe(0m);
        result.PayableAmount.ShouldBe(50m);
        result.Quotes.Single().UnitPrice.ShouldBe(50m);
    }

    [Fact]
    public async Task 应付金额必须等于各行成交单价乘张数之和()
    {
        GivenPrice("ADULT", "成人票", 33.33m);
        GivenPrice("STUDENT", "学生票", 10.07m);

        var service = CreateService();

        // 用会触发舍入的价格组合，验证分摊后账还是平的
        var result = await service.CalculateAsync(new[]
        {
            new PricingLineRequest("ADULT", 3),
            new PricingLineRequest("STUDENT", 2)
        });

        result.PayableAmount.ShouldBe(result.Quotes.Sum(x => x.Amount));

        // 原价 - 优惠 = 应付
        (result.OriginalAmount - result.DiscountAmount).ShouldBe(result.PayableAmount);
    }

    [Fact]
    public async Task 张数必须大于零()
    {
        GivenPrice("ADULT", "成人票", 100m);

        var service = CreateService();

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await service.CalculateAsync(new[]
            {
                new PricingLineRequest("ADULT", 0)
            }));

        ex.Code.ShouldBe(TicketSystemDomainErrorCodes.TicketQuantityMustBePositive);
    }

    [Fact]
    public async Task 空明细应当被拒绝()
    {
        var service = CreateService();

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await service.CalculateAsync(new List<PricingLineRequest>()));

        ex.Code.ShouldBe(TicketSystemDomainErrorCodes.TicketLinesCannotBeEmpty);
    }
}
