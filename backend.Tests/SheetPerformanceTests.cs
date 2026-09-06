using System.Data.Common;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DnDSheetApi.Domain.Entities;
using DnDSheetApi.Infrastructure.Data;
using DnDSheetApi.Infrastructure.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace DnDSheetApi.Tests;

public class SheetPerformanceTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public SheetPerformanceTests(TestApp app) => _app = app;

    [Fact]
    public async Task Reads_fetch_only_needed_columns_and_do_not_track_sheets()
    {
        using var client = _app.CreateClient(); // Apply migrations to the isolated database.
        var commands = new CommandCapture();
        await using var db = _app.CreateDb(commands);
        var repository = new SheetRepository(db);
        var sheet = await SeedAsync(db);
        db.ChangeTracker.Clear();

        var summaries = await repository.GetAllAsync();
        Assert.Contains(summaries, s => s.Id == sheet.Id && s.CharacterName == sheet.CharacterName);
        Assert.DoesNotContain("SheetData", commands.LastCommand);
        Assert.DoesNotContain("EditPasswordHash", commands.LastCommand);

        Assert.Equal(sheet.EditPasswordHash, await repository.GetEditPasswordHashAsync(sheet.Id));
        Assert.DoesNotContain("SheetData", commands.LastCommand);
        Assert.DoesNotContain("CharacterName", commands.LastCommand);

        var details = await repository.GetByIdAsync(sheet.Id);
        Assert.Equal(sheet.SheetData, details!.SheetData);
        Assert.DoesNotContain("EditPasswordHash", commands.LastCommand);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Password_reset_preserves_data_and_revokes_writes_already_in_flight()
    {
        using var client = _app.CreateClient();
        var commands = new CommandCapture();
        await using var db = _app.CreateDb(commands);
        var sheet = await SeedAsync(db);
        db.ChangeTracker.Clear();
        var repository = new SheetRepository(db);
        var previouslyVerifiedHash = await repository.GetEditPasswordHashAsync(sheet.Id);
        var newHash = BCrypt.Net.BCrypt.HashPassword("new-password");

        Assert.True(await repository.ResetPasswordAsync(sheet.Id, newHash, DateTime.UtcNow));
        Assert.DoesNotContain("SheetData", commands.LastCommand);
        Assert.False(await repository.UpdateDataAsync(sheet.Id, previouslyVerifiedHash!, "{}", DateTime.UtcNow));
        Assert.False(await repository.DeleteAsync(sheet.Id, previouslyVerifiedHash!));
        Assert.Equal(sheet.SheetData, (await repository.GetByIdAsync(sheet.Id))!.SheetData);
        Assert.True(await repository.UpdateDataAsync(sheet.Id, newHash, "{\"hp\":42}", DateTime.UtcNow));
        var saved = await db.CharacterSheets.AsNoTracking().SingleAsync(s => s.Id == sheet.Id);
        Assert.Equal(newHash, saved.EditPasswordHash);
        Assert.Equal(sheet.CharacterName, saved.CharacterName);
        Assert.Equal(sheet.CreatedAt, saved.CreatedAt);
        Assert.Equal(42, JsonDocument.Parse(saved.SheetData).RootElement.GetProperty("hp").GetInt32());
        Assert.True(await repository.DeleteAsync(sheet.Id, newHash));
        Assert.Null(await repository.GetByIdAsync(sheet.Id));
    }

    [Fact]
    public async Task Api_preserves_authentication_password_checks_and_response_contract()
    {
        using var client = _app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/sheets")).StatusCode);
        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip");
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username = "admin", password = "mestrejohn" });
        login.EnsureSuccessStatusCode();
        Assert.Empty(login.Content.Headers.ContentEncoding);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.AcceptEncoding.Clear();

        var created = await client.PostAsJsonAsync("/api/sheets", new { characterName = "  Test Ranger  ", editPassword = "test-password" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var summary = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Test Ranger", summary.GetProperty("characterName").GetString());
        AssertProperties(summary, "id", "characterName", "createdAt", "updatedAt");
        var id = summary.GetProperty("id").GetInt32();
        var list = await client.GetFromJsonAsync<JsonElement>("/api/sheets");
        AssertProperties(list.EnumerateArray().Single(s => s.GetProperty("id").GetInt32() == id),
            "id", "characterName", "createdAt", "updatedAt");

        using var publicClient = _app.CreateClient();
        var details = await publicClient.GetFromJsonAsync<JsonElement>($"/api/sheets/{id}");
        AssertProperties(details, "id", "characterName", "sheetData", "createdAt", "updatedAt");
        Assert.Equal(HttpStatusCode.Unauthorized, (await publicClient.PostAsJsonAsync($"/api/sheets/{id}/verify-password", new { editPassword = "test-password" })).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/sheets/{id}/verify-password", new { editPassword = "wrong" })).StatusCode);
        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip");
        var verified = await client.PostAsJsonAsync($"/api/sheets/{id}/verify-password", new { editPassword = "test-password" });
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        Assert.Empty(verified.Content.Headers.ContentEncoding);
        client.DefaultRequestHeaders.AcceptEncoding.Clear();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/sheets/{id}", new { editPassword = "wrong", sheetData = "{\"hp\":1}" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/sheets/{id}", new { editPassword = "test-password", sheetData = "{\"hp\":20}" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/sheets/{id}/reset-password-direct", new { newPassword = "new-password" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/sheets/{id}/verify-password", new { editPassword = "test-password" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/sheets/{id}/verify-password", new { editPassword = "new-password" })).StatusCode);
        details = await publicClient.GetFromJsonAsync<JsonElement>($"/api/sheets/{id}");
        Assert.Equal(20, JsonDocument.Parse(details.GetProperty("sheetData").GetString()!).RootElement.GetProperty("hp").GetInt32());

        foreach (var password in new[] { "wrong", "new-password" })
        {
            using var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/sheets/{id}") { Content = JsonContent.Create(new { editPassword = password }) };
            Assert.Equal(password == "wrong" ? HttpStatusCode.Forbidden : HttpStatusCode.OK, (await client.SendAsync(delete)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await publicClient.GetAsync($"/api/sheets/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/sheets/{id}/reset-password-direct", new { newPassword = "another-password" })).StatusCode);
    }

    [Theory]
    [InlineData("gzip")]
    [InlineData("br")]
    public async Task Large_sheet_compression_is_lossless_over_https(string encoding)
    {
        using var client = _app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        await using var db = _app.CreateDb();
        var sheet = await SeedAsync(db);
        var plain = await client.GetByteArrayAsync($"/api/sheets/{sheet.Id}");
        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd(encoding);
        var response = await client.GetAsync($"/api/sheets/{sheet.Id}");
        response.EnsureSuccessStatusCode();
        Assert.Contains(encoding, response.Content.Headers.ContentEncoding);
        Assert.Contains("Accept-Encoding", response.Headers.Vary);
        var compressed = await response.Content.ReadAsByteArrayAsync();
        Assert.True(compressed.Length < plain.Length);
        using var input = new MemoryStream(compressed);
        using Stream decoder = encoding == "gzip" ? new GZipStream(input, CompressionMode.Decompress) : new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        await decoder.CopyToAsync(output);
        Assert.Equal(plain, output.ToArray());
    }

    [Fact]
    public async Task Restart_with_current_schema_preserves_existing_sheets()
    {
        using var client = _app.CreateClient();
        await using var db = _app.CreateDb();
        var sheet = await SeedAsync(db);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());

        using var restartedApp = new TestApp();
        using var restartedClient = restartedApp.CreateClient();
        var response = await restartedClient.GetFromJsonAsync<JsonElement>($"/api/sheets/{sheet.Id}");
        Assert.Equal(sheet.CharacterName, response.GetProperty("characterName").GetString());
        Assert.Equal(sheet.SheetData, response.GetProperty("sheetData").GetString());
    }

    private static void AssertProperties(JsonElement element, params string[] names) =>
        Assert.Equal(names.Order(), element.EnumerateObject().Select(p => p.Name).Order());

    private static async Task<CharacterSheet> SeedAsync(AppDbContext db)
    {
        var bytes = new byte[500 * 1024];
        new Random(42).NextBytes(bytes);
        var sheet = new CharacterSheet
        {
            CharacterName = "Large test sheet",
            EditPasswordHash = BCrypt.Net.BCrypt.HashPassword("test-password"),
            SheetData = JsonSerializer.Serialize(new { image = "data:image/png;base64," + Convert.ToBase64String(bytes) }),
            // PostgreSQL timestamp precision is microseconds.
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        db.CharacterSheets.Add(sheet);
        await db.SaveChangesAsync();
        await db.Entry(sheet).ReloadAsync();
        return sheet;
    }
}

public sealed class TestApp : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public TestApp()
    {
        var configured = Environment.GetEnvironmentVariable("DND_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set DND_TEST_CONNECTION to a disposable local PostgreSQL database named dnd_test_*.");
        var connection = new NpgsqlConnectionStringBuilder(configured);
        if (connection.Host is not ("localhost" or "127.0.0.1") || connection.Database?.StartsWith("dnd_test_", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("Tests only accept localhost/127.0.0.1 and a database named dnd_test_*.");
        _connectionString = connection.ConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../backend")));
        builder.ConfigureLogging(logging => logging.ClearProviders().AddConsole());
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = _connectionString,
            ["Jwt:ExpireHours"] = "1",
            ["Logging:LogLevel:Default"] = "Warning"
        }));
    }

    public AppDbContext CreateDb(CommandCapture? capture = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_connectionString);
        if (capture != null) options.AddInterceptors(capture);
        return new AppDbContext(options.Options);
    }
}

public sealed class CommandCapture : DbCommandInterceptor
{
    public string LastCommand { get; private set; } = "";
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        LastCommand = command.CommandText;
        return ValueTask.FromResult(result);
    }
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        LastCommand = command.CommandText;
        return ValueTask.FromResult(result);
    }
}
