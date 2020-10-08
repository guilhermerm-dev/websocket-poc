using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

            //how frequently to send "ping" frames to the client to ensure proxies keep the connection open. The default is two minutes
            var webSocketOptions = new WebSocketOptions()
            {
                KeepAliveInterval = TimeSpan.FromSeconds(120),
                ReceiveBufferSize = 4 * 1024
            };

            app.UseWebSockets(webSocketOptions);

            app.Use(async (httpContext, next) =>
            {
                if (httpContext.Request.Path == "/ws")
                {
                    if (httpContext.WebSockets.IsWebSocketRequest)
                    {
                        WebSocket webSocket = await httpContext.WebSockets.AcceptWebSocketAsync();
                        await Echo(httpContext, webSocket);
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

        private async Task Echo(HttpContext httpContext, WebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            while (!result.CloseStatus.HasValue)
            {
                await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType, result.EndOfMessage, CancellationToken.None);

                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }
    }
}
