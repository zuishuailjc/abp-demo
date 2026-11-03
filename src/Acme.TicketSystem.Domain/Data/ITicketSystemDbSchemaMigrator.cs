using System.Threading.Tasks;

namespace Acme.TicketSystem.Data;

public interface ITicketSystemDbSchemaMigrator
{
    Task MigrateAsync();
}
