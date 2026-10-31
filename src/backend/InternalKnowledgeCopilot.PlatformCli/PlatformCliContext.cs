using Amazon.DynamoDBv2;
using Amazon.Runtime;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;
using InternalKnowledgeCopilot.PlatformCli.Authority;
using InternalKnowledgeCopilot.PlatformCli.Preflight;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace InternalKnowledgeCopilot.PlatformCli;

// Điểm mở rộng chỉ dùng được từ code (test/test harness), không qua tham số dòng lệnh, biến môi trường hay cấu hình.
public sealed class PlatformCliHostOptions
{
    public string? EnvironmentName { get; init; }

    public IReadOnlyDictionary<string, string?>? Configuration { get; init; }

    // Client DynamoDB chỉ để ĐỌC authority (đường read-only giống runtime). CLI không có client ghi DynamoDB nào.
    public Func<ProvisioningAuthoritySettings, IAmazonDynamoDB>? DynamoDbReadClientFactory { get; init; }

    // Transport tới Authority Service (test thay bằng gateway giả trong tiến trình) và credential operator (test thay cho profile MFA).
    public HttpMessageHandler? AuthorityServiceHandler { get; init; }

    public Func<AWSCredentials>? OperatorCredentials { get; init; }

    public Func<IAuthorityTransitionClient, IAuthorityTransitionClient>? TransitionClientDecorator { get; init; }

    // C-IR-03: kiểm tra nâng quyền KHÔNG phụ thuộc tên môi trường. Mặc định Environment.IsPrivilegedProcess; chỉ code test mới thay.
    public Func<bool>? PrivilegeCheck { get; init; }

    // Điểm tiêm lỗi cho test: chạy bên trong transaction SQLite trước commit / ngay sau commit (trước khi activate authority).
    public Func<AppDbContext, Task>? BeforeSqliteCommit { get; init; }

    public Func<Task>? AfterSqliteCommit { get; init; }
}

public sealed class CliConfigurationException(string message) : Exception(message);

// Composition root của CLI: chỉ tạo đúng những gì command cần (DbContext, reader, transition client). Không có IHost, Kestrel,
// middleware, DevelopmentSeeder, migration hay hosted service.
public sealed class PlatformCliContext : IDisposable
{
    private static readonly TimeSpan AuthorityServiceTimeout = TimeSpan.FromSeconds(15);

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

    public PlatformCliHostOptions HostOptions => hostOptions;

    public bool IsPrivilegedOperator => (hostOptions.PrivilegeCheck ?? (() => Environment.IsPrivilegedProcess))();

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

    // Đọc authority luôn qua đường read-only của runtime (DynamoDB: default credential chain của host = role runtime chỉ GetItem).
    public IProvisioningAuthorityReader GetReader()
    {
        var settings = Authority ?? throw new CliConfigurationException("provisioning_authority_not_configured");
        return reader ??= settings.Kind switch
        {
            ProvisioningAuthorityKind.LocalSeal => new LocalSealProvisioningAuthorityReader(settings),
            _ => Own(new DynamoDbProvisioningAuthorityReader(
                hostOptions.DynamoDbReadClientFactory?.Invoke(settings) ?? DynamoDbAuthorityClientFactory.CreateRuntimeClient(settings),
                settings)),
        };
    }

    // DynamoDb: mọi thay đổi authority đi qua Authority Service (route operation) với credential assume-role có MFA của operator.
    // Không bao giờ rơi về default credential chain hay ghi DynamoDB trực tiếp. LocalSeal: chỉ Development/Testing.
    public IAuthorityTransitionClient CreateTransitionClient(IOperatorConsole console)
    {
        var settings = Authority ?? throw new CliConfigurationException("provisioning_authority_not_configured");
        IAuthorityTransitionClient client;
        if (settings.Kind == ProvisioningAuthorityKind.LocalSeal)
        {
            client = new LocalSealTransitionClient(settings);
        }
        else
        {
            var endpoint = settings.AuthorityServiceEndpoint
                ?? throw new CliConfigurationException("ProvisioningAuthority:AuthorityService:Endpoint is required for authority operations.");
            var credentials = hostOptions.OperatorCredentials?.Invoke()
                ?? (settings.DynamoDbOperatorProfile is { } profile
                    ? OperatorCredentialProvider.Create(profile, console)
                    : throw new CliConfigurationException("ProvisioningAuthority:DynamoDb:OperatorProfile is required for authority operations."));
            var http = Own(hostOptions.AuthorityServiceHandler is { } handler
                ? new HttpClient(handler, disposeHandler: false)
                : new HttpClient());
            http.Timeout = AuthorityServiceTimeout;
            client = new AuthorityServiceHttpClient(http, EnsureTrailingSlash(endpoint), credentials, settings.DynamoDbRegion!, TimeProvider.System);
        }

        return hostOptions.TransitionClientDecorator?.Invoke(client) ?? client;
    }

    public void Dispose()
    {
        foreach (var item in owned)
        {
            item.Dispose();
        }
    }

    private static Uri EnsureTrailingSlash(Uri endpoint)
    {
        return endpoint.AbsoluteUri.EndsWith('/') ? endpoint : new Uri(endpoint.AbsoluteUri + "/");
    }

    private T Own<T>(T item)
        where T : IDisposable
    {
        owned.Add(item);
        return item;
    }
}
