namespace Acme.TicketSystem.Tickets;

/// <summary>售票模块的长度约束，集中在一处便于校验和 EF 映射保持一致。</summary>
public static class TicketConsts
{
    public const int MaxSaleNoLength = 32;
    public const int MaxStoreIdLength = 32;
    public const int MaxSoldByLength = 64;
    public const int MaxCustomerNameLength = 50;
    public const int MaxCustomerPhoneLength = 15;
    public const int MaxTicketCodeLength = 32;
    public const int MaxTicketNameLength = 64;
    public const int MaxTicketNoLength = 32;
}
