using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AppStorageOptions>(builder.Configuration.GetSection(AppStorageOptions.SectionName));
builder.Services.Configure<ChromaOptions>(builder.Configuration.GetSection(ChromaOptions.SectionName));
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var sqlitePath = builder.Configuration.GetValue<string>("Database:SqlitePath")
    ?? Path.Combine(AppContext.BaseDirectory, "data", "internal-knowledge-copilot.db");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(sqlitePath))!);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite($"Data Source={sqlitePath}");
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseHttpsRedirection();
app.MapControllers();

app.Run();

public partial class Program;
