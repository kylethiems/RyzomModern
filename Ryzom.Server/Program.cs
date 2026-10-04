using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ryzom.Core.Persistence;
using Ryzom.Engine.Ecology;
using Ryzom.Engine.Spatial;
using Ryzom.Engine.Stanzas;
using Ryzom.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Ryzom Modern MMORPG Spatial Gateway API",
        Version = "v1",
        Description = "High-throughput cloud-native spatial gateway, EF Core 8 persistence, and WebGPU streaming server for Ryzom Modern."
    });
});

// EF Core In-Memory database
builder.Services.AddDbContext<RyzomDbContext>(options =>
    options.UseInMemoryDatabase("RyzomDb"));

// Single spatial world grid instance & ecology simulation
builder.Services.AddSingleton(new VoxelSpatialGrid(cellSize: 32f));
builder.Services.AddSingleton<EcologySimulation>();
builder.Services.AddHostedService<WorldTickHostedService>();

var app = builder.Build();

// Ensure Database Seeded
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RyzomDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseWebSockets();

// World status and telemetry endpoint
app.MapGet("/api/world/status", (VoxelSpatialGrid grid) => Results.Ok(new
{
    World = "Atys",
    Status = "Online",
    SpatialIndex = "Morton-64 Flat Voxel Grid",
    Netcode = "Zero-Allocation Span BitStream",
    TickFrequency = "20 Hz (50ms)",
    Timestamp = DateTime.UtcNow
}));

// Live Ecology Herds query
app.MapGet("/api/world/herds", (EcologySimulation ecology) => Results.Ok(
    ecology.Herds.Select(h => new
    {
        h.Species,
        h.TerritoryRadius,
        Count = h.Members.Count,
        Living = h.Members.Count(m => m.IsAlive),
        Center = new { h.TerritoryCenter.X, h.TerritoryCenter.Y, h.TerritoryCenter.Z }
    })
));

// Characters REST API
app.MapGet("/api/characters", async (RyzomDbContext db) =>
    Results.Ok(await db.Characters.Include(c => c.Inventory).AsNoTracking().ToListAsync()));

app.MapGet("/api/characters/{id:int}", async (int id, RyzomDbContext db) =>
{
    var character = await db.Characters.Include(c => c.Inventory).FirstOrDefaultAsync(c => c.Id == id);
    return character != null ? Results.Ok(character) : Results.NotFound();
});

// Stanza Spell Compiler API
app.MapPost("/api/stanzas/compile", (StanzaRecipe recipe) =>
{
    try
    {
        var stanza = StanzaGrammarEngine.CompileRecipe(recipe);
        return Results.Ok(stanza);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// WebSocket game connection endpoint
app.Map("/ws/game", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var buffer = new byte[1024 * 4];

        var welcomeMsg = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            type = "Spawn",
            id = 1001,
            name = "FyrosPlayer",
            position = new { x = 120.5f, y = 15.0f, z = 85.25f },
            hp = 1200,
            sap = 600,
            stamina = 950
        }));

        await webSocket.SendAsync(
            new ArraySegment<byte>(welcomeMsg),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None);

        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
        }
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
    }
});

app.Run();

public partial class Program { }
