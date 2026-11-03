namespace Acme.TicketSystem.Permissions;

public static class TicketSystemPermissions
{
    public const string GroupName = "TicketSystem";

    //Add your own permission names. Example:
    //public const string MyPermission1 = GroupName + ".MyPermission1";

    /// <summary>
    /// 售票相关权限
    /// </summary>
    public static class Tickets
    {
        public const string Default = GroupName + ".Tickets";
        public const string Sell = Default + ".Sell";
        public const string Refund = Default + ".Refund";
    }

}
