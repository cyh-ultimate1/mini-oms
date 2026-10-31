using Microsoft.AspNetCore.SignalR;

namespace MiniOMS.WebApi;

/// <summary>
/// the SignalR endpoint browsers connect to. It stays empty, because the server only pushes to clients.
/// </summary>
public class OrdersHub : Hub { }
