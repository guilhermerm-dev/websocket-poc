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
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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

            var connections = new Dictionary<string, WebSocket>();


            app.Use(async (httpContext, next) =>
            {
                if (httpContext.Request.Path == "/ws")
                {
                    if (httpContext.WebSockets.IsWebSocketRequest)
                    {
                        WebSocket webSocket = await httpContext.WebSockets.AcceptWebSocketAsync();
                        await AddConnection(httpContext, webSocket, connections);
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


        private async Task AddConnection(HttpContext httpContext, WebSocket webSocket, Dictionary<string, WebSocket> connections)
        {
            await Task.Factory.StartNew(() =>
            {
                var currentThread = Thread.CurrentThread.ManagedThreadId;
                if (!connections.ContainsKey(httpContext.Connection.Id))
                {
                    connections.Add(httpContext.Connection.Id, webSocket);
                }
            });
        }


        private async Task Echo(HttpContext httpContext, WebSocket webSocket, Dictionary<string, WebSocket> connections)
        {
            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            while (!result.CloseStatus.HasValue)
            {
                byte[] byteArray = new ArraySegment<byte>(buffer, 0, result.Count).ToArray();

                var data = GetDataFromByteArray(byteArray);

                //TODO mplement logic to decide which command to call

                var destinationWebsocket = connections.Select(connection => connection)
                    .Where(connection => !connection.Key.Equals(httpContext.Connection.Id)).FirstOrDefault().Value; 

                await destinationWebsocket.SendAsync(byteArray, result.MessageType, result.EndOfMessage, CancellationToken.None);

                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }

            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None)
                .ContinueWith(task =>
                {
                    if (task.IsCompleted)
                    {
                        RemoveConnection(httpContext.Connection.Id, connections);
                    }

                }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        private object GetDataFromByteArray(byte[] byteArray)
        {
            var readOnlySpan = new ReadOnlySpan<byte>(byteArray);
            return JsonSerializer.Deserialize<Data>(readOnlySpan);
        }

        private void RemoveConnection(string httpContextId, Dictionary<string, WebSocket> connections)
        {
            if (connections.ContainsKey(httpContextId))
            {
                connections.Remove(httpContextId);
            }
        }


    }
}
