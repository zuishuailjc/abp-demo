using Volo.Abp.Settings;

namespace Acme.TicketSystem.Settings;

public class TicketSystemSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        //Define your own settings here. Example:
        //context.Add(new SettingDefinition(TicketSystemSettings.MySetting1));
    }
}
