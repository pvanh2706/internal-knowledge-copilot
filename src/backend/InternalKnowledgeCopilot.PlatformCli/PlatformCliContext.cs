using Amazon.DynamoDBv2;
using Amazon.Runtime.CredentialManagement;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;
using InternalKnowledgeCopilot.PlatformCli.Authority;
using InternalKnowledgeCopilot.PlatformCli.Preflight;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace InternalKnowledgeCopilot.PlatformCli;

// Điểm mở rộng chỉ dùng được từ code (test harness), không qua tham số dòng lệnh hay cấu hình.
public sealed class PlatformCliHostOptions
{
    public string? EnvironmentName { get; init; }

    public IReadOnlyDictionary<string, string?>? Configuration { get; init; }

    // (settings, forMutation) → client. Test dùng client giả; production luôn tạo AmazonDynamoDBClient.
    public Func<ProvisioningAuthoritySettings, bool, IAmazonDynamoDB>? DynamoDbClientFactory { get; init; }

    public Func<IProvisioningAuthorityMutator, IProvisioningAuthorityMutator>? MutatorDecorator { get; init; }
}

public sealed class CliConfigurationException(string message) : Exception(message);

// Composition root của CLI: chỉ tạo đúng những gì command cần (DbContext, reader, mutator). Không có IHost, Kestrel,
// middleware, DevelopmentSeeder, migration hay hosted service.
public sealed class PlatformCliContext : IDisposable
{
    private readonly PlatformCliHostOptions hostOptions;
    private readonly List<IDisposable> owned = [];
    private IProvisioningAuthorityReader? reader;

    private PlatformCliContext(string environmentName, string appDirectory, string databasePath, ProvisioningAuthoritySettings? authority, PlatformCliHostOptions hostOptions)
    {
        EnvironmentName = environmentName;
        AppDirectory = appDirectory;
        DatabasePath = databasePath;
        Authority = authority;
        this.hostOptions = hostOptions;
    }

    public string EnvironmentName { get; }

    public string AppDirectory { get; }

    public string DatabasePath { get; }

    public ProvisioningAuthoritySettings? Authority { get; }

    public static PlatformCliContext Create(string? appDirectory, PlatformCliHostOptions hostOptions)
    {
        var environmentName = hostOptions.EnvironmentName
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Production";
        var directory = Path.GetFullPath(appDirectory ?? AppContext.BaseDirectory);
        if (!Directory.Exists(directory))
        {
            throw new CliConfigurationException("app_directory_not_found");
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(directory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .AddEnvironmentVariables()
            .AddInMemoryCollection(hostOptions.Configuration ?? new Dictionary<string, string?>())
            .Build();

        // Cùng mặc định với API; đường dẫn tương đối được hiểu theo thư mục ứng dụng (thư mục làm việc của API trên IIS).
        var configuredPath = configuration.GetValue<string>("Database:SqlitePath");
        var databasePath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(directory, "data", "internal-knowledge-copilot.db")
            : Path.GetFullPath(configuredPath, directory);

        ProvisioningAuthoritySettings? authority;
        try
        {
            authority = ProvisioningAuthorityConfigurationValidator.Validate(
                configuration.GetSection(ProvisioningAuthorityOptions.SectionName).Get<ProvisioningAuthorityOptions>() ?? new ProvisioningAuthorityOptions(),
                environmentName,
                databasePath);
        }
        catch (InvalidOperationException ex)
        {
            throw new CliConfigurationException(ex.Message);
        }

        return new PlatformCliContext(environmentName, directory, databasePath, authority, hostOptions);
    }

    public bool DatabaseExists => File.Exists(DatabasePath);

    // ReadOnly do SQLite cưỡng chế (Mode=ReadOnly) cho status/recovery-check; ReadWrite không bao giờ tạo file mới.
    public AppDbContext CreateDbContext(bool readOnly)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWrite,
            Pooling = false,
        }.ToString();
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options);
    }

    public async Task<PlatformDatabaseInspection?> InspectAsync(CancellationToken cancellationToken = default)
    {
        if (!DatabaseExists)
        {
            return null;
        }

        await using var db = CreateDbContext(readOnly: true);
        return await PlatformDatabaseInspector.InspectAsync(db, cancellationToken);
    }

    public IProvisioningAuthorityReader GetReader()
    {
        var settings = Authority ?? throw new CliConfigurationException("provisioning_authority_not_configured");
        return reader ??= settings.Kind switch
        {
            ProvisioningAuthorityKind.LocalSeal => new LocalSealProvisioningAuthorityReader(settings),
            _ => Own(new DynamoDbProvisioningAuthorityReader(CreateDynamoDbClient(settings, forMutation: false), settings)),
        };
    }

    public IProvisioningAuthorityMutator CreateMutator()
    {
        var settings = Authority ?? throw new CliConfigurationException("provisioning_authority_not_configured");
        IProvisioningAuthorityMutator mutator = settings.Kind switch
        {
            ProvisioningAuthorityKind.LocalSeal => new LocalSealProvisioningAuthorityMutator(settings),
            _ => Own(new DynamoDbProvisioningAuthorityMutator(CreateDynamoDbClient(settings, forMutation: true), settings)),
        };
        return hostOptions.MutatorDecorator?.Invoke(mutator) ?? mutator;
    }

    public void Dispose()
    {
        foreach (var item in owned)
        {
            item.Dispose();
        }
    }

    // Ghi authority luôn dùng OperatorProfile tường minh; không bao giờ rơi về default chain (trên host đó là role
    // read-only của runtime). Đọc dùng OperatorProfile nếu có, nếu không thì default chain.
    private IAmazonDynamoDB CreateDynamoDbClient(ProvisioningAuthoritySettings settings, bool forMutation)
    {
        if (hostOptions.DynamoDbClientFactory is { } factory)
        {
            return factory(settings, forMutation);
        }

        var config = DynamoDbAuthorityClientFactory.CreateConfig(settings);
        if (settings.DynamoDbOperatorProfile is null)
        {
            return forMutation
                ? throw new CliConfigurationException("ProvisioningAuthority:DynamoDb:OperatorProfile is required for authority mutations.")
                : new AmazonDynamoDBClient(config);
        }

        if (!new CredentialProfileStoreChain().TryGetAWSCredentials(settings.DynamoDbOperatorProfile, out var credentials))
        {
            throw new CliConfigurationException("operator_profile_not_found");
        }

        return new AmazonDynamoDBClient(credentials, config);
    }

    private T Own<T>(T item)
        where T : IDisposable
    {
        owned.Add(item);
        return item;
    }
}
