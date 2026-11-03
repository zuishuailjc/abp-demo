using Microsoft.AspNetCore.Builder;
using Acme.TicketSystem;
using Volo.Abp.AspNetCore.TestBase;

var builder = WebApplication.CreateBuilder();
await builder.RunAbpModuleAsync<TicketSystemWebTestModule>();

public partial class Program
{
}
