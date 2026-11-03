using Acme.TicketSystem.Tickets;
using AutoMapper;

namespace Acme.TicketSystem;

public class TicketSystemApplicationAutoMapperProfile : Profile
{
    public TicketSystemApplicationAutoMapperProfile()
    {
        /* You can configure your AutoMapper mapping configuration here.
         * Alternatively, you can split your mapping configurations
         * into multiple profile classes for a better organization. */

        // 售票：聚合 -> DTO，Lines/Tickets 名字一致，AutoMapper 自己会展开
        CreateMap<TicketSale, TicketSaleDto>();
        CreateMap<TicketSaleLine, TicketSaleLineDto>();
        CreateMap<SoldTicket, SoldTicketDto>();
        CreateMap<TicketLedgerEntry, TicketLedgerEntryDto>().ForMember(d => d.EntryType, o => o.MapFrom(s => s.EntryType.ToString()));
	}
}
