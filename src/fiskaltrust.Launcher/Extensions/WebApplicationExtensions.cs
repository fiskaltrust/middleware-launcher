using fiskaltrust.ifPOS.v1;
using fiskaltrust.ifPOS.v1.de;
using Microsoft.AspNetCore.Mvc;
using System.IO.Pipelines;

namespace fiskaltrust.Launcher.Extensions
{
    public static class WebApplicationExtensions
    {
        private static readonly string[] _prefixes = new[] { "", "json/", "v1/", "json/v1/", "v0/", "json/v0/" };
        private static IEnumerable<RouteHandlerBuilder> MapMultiple(this WebApplication app, IEnumerable<string> urls, Func<IEndpointRouteBuilder, string, Delegate, RouteHandlerBuilder> method, Delegate callback) =>
            urls.Select(url => method(app, url, callback)).ToList();

        private static IEnumerable<RouteHandlerBuilder> MapMultiplePrefixed(this WebApplication app, IEnumerable<string> prefixes, string url, Func<IEndpointRouteBuilder, string, Delegate, RouteHandlerBuilder> method, Delegate callback) =>
            app.MapMultiple(prefixes.Select(prefix => $"{prefix}{url}"), method, callback);

        public static WebApplication AddQueueEndpoints(this WebApplication app, IPOS pos)
        {
            app.MapMultiplePrefixed(_prefixes, "Echo", EndpointRouteBuilderExtensions.MapPost, async (EchoRequest req) => await pos.EchoAsync(req));
            app.MapMultiplePrefixed(_prefixes, "Sign", EndpointRouteBuilderExtensions.MapPost, async (ReceiptRequest req) => await pos.SignAsync(req));
            app.MapMultiplePrefixed(_prefixes, "Journal", EndpointRouteBuilderExtensions.MapPost, ([FromQuery] long type, [FromQuery] long? from, [FromQuery] long? to) =>
            {
                var pipe = new Pipe();
                var journal = pos.JournalAsync(new JournalRequest { ftJournalType = type, From = from ?? 0, To = to ?? 0 });
                var _ = Task.Run(async () =>
                {
                    await journal.ForEachAwaitAsync(async b => await pipe.Writer.WriteAsync(new ReadOnlyMemory<byte>(b.Chunk.ToArray())));
                    await pipe.Writer.CompleteAsync();
                });
                return Results.Stream(pipe.Reader.AsStream());
            });

            return app;
        }

        public static WebApplication AddScuEndpoints(this WebApplication app, IDESSCD sscd)
        {
            app.MapMultiplePrefixed(_prefixes, "StartTransaction", EndpointRouteBuilderExtensions.MapPost, async (StartTransactionRequest req) => await sscd.StartTransactionAsync(req));
            app.MapMultiplePrefixed(_prefixes, "UpdateTransaction", EndpointRouteBuilderExtensions.MapPost, async (UpdateTransactionRequest req) => await sscd.UpdateTransactionAsync(req));
            app.MapMultiplePrefixed(_prefixes, "FinishTransaction", EndpointRouteBuilderExtensions.MapPost, async (FinishTransactionRequest req) => await sscd.FinishTransactionAsync(req));
            app.MapMultiplePrefixed(_prefixes, "TseInfo", EndpointRouteBuilderExtensions.MapGet, async () => await sscd.GetTseInfoAsync());
            app.MapMultiplePrefixed(_prefixes, "TseState", EndpointRouteBuilderExtensions.MapPost, async (TseState req) => await sscd.SetTseStateAsync(req));
            app.MapMultiplePrefixed(_prefixes, "RegisterClientId", EndpointRouteBuilderExtensions.MapPost, async (RegisterClientIdRequest req) => await sscd.RegisterClientIdAsync(req));
            app.MapMultiplePrefixed(_prefixes, "UnregisterClientId", EndpointRouteBuilderExtensions.MapPost, async (UnregisterClientIdRequest req) => await sscd.UnregisterClientIdAsync(req));
            app.MapMultiplePrefixed(_prefixes, "ExecuteSetTseTime", EndpointRouteBuilderExtensions.MapPost, async () => await sscd.ExecuteSetTseTimeAsync());
            app.MapMultiplePrefixed(_prefixes, "ExecuteSelfTest", EndpointRouteBuilderExtensions.MapPost, async () => await sscd.ExecuteSelfTestAsync());
            app.MapMultiplePrefixed(_prefixes, "StartExportSession", EndpointRouteBuilderExtensions.MapPost, async (StartExportSessionRequest req) => await sscd.StartExportSessionAsync(req));
            app.MapMultiplePrefixed(_prefixes, "StartExportSessionByTimeStamp", EndpointRouteBuilderExtensions.MapPost, async (StartExportSessionByTimeStampRequest req) => await sscd.StartExportSessionByTimeStampAsync(req));
            app.MapMultiplePrefixed(_prefixes, "StartExportSessionByTransaction", EndpointRouteBuilderExtensions.MapPost, async (StartExportSessionByTransactionRequest req) => await sscd.StartExportSessionByTransactionAsync(req));
            app.MapMultiplePrefixed(_prefixes, "ExportData", EndpointRouteBuilderExtensions.MapPost, async (ExportDataRequest req) => await sscd.ExportDataAsync(req));
            app.MapMultiplePrefixed(_prefixes, "EndExportSession", EndpointRouteBuilderExtensions.MapPost, async (EndExportSessionRequest req) => await sscd.EndExportSessionAsync(req));
            app.MapMultiplePrefixed(_prefixes, "Echo", EndpointRouteBuilderExtensions.MapPost, async (ScuDeEchoRequest req) => await sscd.EchoAsync(req));

            return app;
        }
    }
}
