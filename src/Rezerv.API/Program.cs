using Hangfire;
using Hangfire.MemoryStorage;
using Rezerv.API.Middleware;
using Rezerv.Application.Services;
using Rezerv.Infrastructure;
using Rezerv.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHangfire(c => c.UseMemoryStorage());
builder.Services.AddHangfireServer();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    await DbSeeder.SeedAsync(db);
}

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.RoutePrefix = "";
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Rezerv API v1");
});
app.UseHangfireDashboard("/hangfire");
RecurringJob.AddOrUpdate<WaitlistService>("expire-ended-waitlists", s => s.ExpireEndedAsync(), "*/5 * * * *");
app.MapControllers();
app.Run();