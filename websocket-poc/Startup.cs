using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;

namespace websocket_poc
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            //how frequently to send "ping" frames to the client to ensure proxies keep the connection open.
            //The default is two minutes
            var webSocketOptions = new WebSocketOptions()
            {
                KeepAliveInterval = TimeSpan.FromSeconds(120),
                ReceiveBufferSize = 4 * 1024
            };

            app.UseWebSockets(webSocketOptions);

            var connections = new Dictionary<string, Connection>();


            app.Use(async (httpContext, next) =>
            {
                if (httpContext.Request.Path == "/ws")
                {
                    if (httpContext.WebSockets.IsWebSocketRequest)
                    {
                        WebSocket webSocket = await httpContext.WebSockets.AcceptWebSocketAsync();
                        await Echo(httpContext, webSocket, connections);
                    }
                    else
                    {
                        httpContext.Response.StatusCode = 400;
                    }
                }
                else
                {
                    await next();
                }

            });

            app.Run(async (httpContext) =>
            {
                await httpContext.Response.WriteAsync("Websocket is Running!");
            });
        }

        private async Task Echo(HttpContext httpContext, WebSocket webSocket, Dictionary<string, Connection> connections)
        {
            string userName = "";
            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            while (!result.CloseStatus.HasValue)
            {
                byte[] byteArray = new ArraySegment<byte>(buffer, 0, result.Count).ToArray();

                var data = GetDataFromByteArray(byteArray);

                //TODO implement Strategy pattern
                if (data.TryGetValue("eventType", out string action))
                {
                    switch (action)
                    {
                        case "USER_CONNECT":
                            userName = data["userName"];
                            await AddConnection(httpContext, webSocket, connections, userName);
                            break;

                        case "USER_MESSAGE":
                            string destination = data["destination"];
                            var connection = connections.Where(connection => connection.Key.Equals(destination)).Select(connection => connection.Value).FirstOrDefault();
                            await connection.WebSocket.SendAsync(byteArray, result.MessageType, result.EndOfMessage, CancellationToken.None);
                            break;
                        default:
                            break;
                    }
                }
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }

            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None)
                .ContinueWith(task =>
                {
                    if (task.IsCompleted)
                    {
                        RemoveConnection(userName, connections);
                    }

                }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        private async Task AddConnection(HttpContext httpContext, WebSocket webSocket, Dictionary<string, Connection> connections, string userName)
        {
            await Task.Factory.StartNew(() =>
            {
                if (!connections.ContainsKey(userName))
                {
                    connections.Add(userName, new Connection(httpContext.Connection.Id, webSocket));
                }
            });
        }

        private Dictionary<string, string> GetDataFromByteArray(byte[] byteArray)
        {
            var readOnlySpan = new ReadOnlySpan<byte>(byteArray);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(readOnlySpan);
        }

        private void RemoveConnection(string userName, Dictionary<string, Connection> connections)
        {
            if (connections.ContainsKey(userName))
            {
                connections.Remove(userName);
            }
        }


    }
}
