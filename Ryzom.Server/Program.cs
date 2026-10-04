using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Ryzom.Core.Math3D;
using Ryzom.Engine.Entities;
using Ryzom.Engine.Spatial;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Ryzom Modern MMORPG Gateway API",
        Version = "v1",
        Description = "High-throughput cloud-native spatial gateway and WebGPU streaming server for Ryzom Modern."
    });
});

// Single spatial world grid instance
builder.Services.AddSingleton(new VoxelSpatialGrid(cellSize: 32f));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseWebSockets();

// World status and metrics endpoint
app.MapGet("/api/world/status", (VoxelSpatialGrid grid) => Results.Ok(new
{
    World = "Atys",
    Status = "Online",
    SpatialIndex = "Morton-64 Flat Voxel Grid",
    Netcode = "Zero-Allocation Span BitStream",
    Timestamp = DateTime.UtcNow
}));

// WebSocket connection endpoint for real-time WebGPU browser clients
app.Map("/ws/game", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var buffer = new byte[1024 * 4];

        // Send initial welcome spawn packet
        var welcomeMsg = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            type = "Spawn",
            id = 1001,
            name = "FyrosPlayer",
            position = new { x = 0f, y = 0f, z = 0f },
            hp = 1000,
            sap = 500,
            stamina = 800
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
