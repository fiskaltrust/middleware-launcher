using fiskaltrust.ifPOS.v1;
using fiskaltrust.ifPOS.v1.at;
using fiskaltrust.ifPOS.v1.de;
using fiskaltrust.ifPOS.v1.it;
using Microsoft.AspNetCore.Mvc;
using System.IO.Pipelines;

namespace fiskaltrust.Launcher.Extensions
{
    public static class WebApplicationExtensions
    {
        private static readonly string[] _prefixesV0 = new[] { "v0/", "json/v0/" };
        private static readonly string[] _prefixesV1 = new[] { "", "json/", "v1/", "json/v1/" };
        private static readonly string[] _prefixes = _prefixesV0.Concat(_prefixesV1).ToArray();


        private static IEnumerable<RouteHandlerBuilder> MapMultiple(this WebApplication app, IEnumerable<string> urls, Func<IEndpointRouteBuilder, string, Delegate, RouteHandlerBuilder> method, Delegate callback) =>
            urls.Select(url => method(app, url, callback)).ToList();

        private static IEnumerable<RouteHandlerBuilder> MapMultiplePrefixed(this WebApplication app, IEnumerable<string> prefixes, string url, Func<IEndpointRouteBuilder, string, Delegate, RouteHandlerBuilder> method, Delegate callback) =>
            app.MapMultiple(prefixes.Select(prefix => $"{prefix}{url}"), method, callback);

        public static WebApplication AddQueueEndpoints(this WebApplication app, IPOS pos)
        {
            app.MapMultiplePrefixed(_prefixesV1, "Echo", EndpointRouteBuilderExtensions.MapPost, async (ifPOS.v1.EchoRequest req) => await pos.EchoAsync(req));
            app.MapMultiplePrefixed(_prefixesV0, "Echo", EndpointRouteBuilderExtensions.MapPost, async ([FromBody] string message) => (await pos.EchoAsync(new ifPOS.v1.EchoRequest { Message = message })).Message);
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

        public static WebApplication AddScuDeEndpoints(this WebApplication app, IDESSCD sscd)
        {
            app.MapMultiplePrefixed(_prefixesV1, "StartTransaction", EndpointRouteBuilderExtensions.MapPost, async (StartTransactionRequest req) => await sscd.StartTransactionAsync(req));
            app.MapMultiplePrefixed(_prefixesV1, "UpdateTransaction", EndpointRouteBuilderExtensions.MapPost, async (UpdateTransactionRequest req) => await sscd.UpdateTransactionAsync(req));
            app.MapMultiplePrefixed(_prefixesV1, "FinishTransaction", EndpointRouteBuilderExtensions.MapPost, async (FinishTransactionRequest req) => await sscd.FinishTransactionAsync(req));
            app.MapMultiplePrefixed(_prefixesV1, "TseInfo", EndpointRouteBuilderExtensions.MapGet, async () => await sscd.GetTseInfoAsync());
            app.MapMultiplePrefixed(_prefixesV1, "TseState", EndpointRouteBuilderExtensions.MapPost, async (TseState req) => await sscd.SetTseStateAsync(req));
            app.MapMultiplePrefixed(_prefixesV1, "RegisterClientId", EndpointRouteBuilderExtensions.MapPost, async (RegisterClientIdRequest req) => await sscd.RegisterClientIdAsync(req));
            app.MapMultiplePrefixed(_prefixesV1, "UnregisterClientId", EndpointRouteBuilderExtensions.MapPost, async (UnregisterClientIdRequest req) => await sscd.UnregisterClientIdAsync(req));
            app.MapMultiplePrefixed(_prefixesV1, "ExecuteSetTseTime", EndpointRouteBuilderExtensions.MapPost, async () => await sscd.ExecuteSetTseTimeAsync());
            app.MapMultiplePrefixed(_prefixesV1, "ExecuteSelfTest", EndpointRouteBuilderExtensions.MapPost, async () => await sscd.ExecuteSelfTestAsync());
            app.MapMultiplePrefixed(_prefixesV1, "StartExportSession", EndpointRouteBuilderExtensions.MapPost, async (StartExportSessionRequest req) => await sscd.StartExportSessionAsync(req));
            app.MapMultiplePrefixed(_prefixesV1, "StartExportSessionByTimeStamp", EndpointRouteBuilderExtensions.MapPost, async (StartExportSessionByTimeStampRequest req) => await sscd.StartExportSessionByTimeStampAsync(req));
            app.MapMultiplePrefixed(_prefixesV1, "StartExportSessionByTransaction", EndpointRouteBuilderExtensions.MapPost, async (StartExportSessionByTransactionRequest req) => await sscd.StartExportSessionByTransactionAsync(req));
            app.MapMultiplePrefixed(_prefixesV1, "ExportData", EndpointRouteBuilderExtensions.MapPost, async (ExportDataRequest req) => await sscd.ExportDataAsync(req));
            app.MapMultiplePrefixed(_prefixesV1, "EndExportSession", EndpointRouteBuilderExtensions.MapPost, async (EndExportSessionRequest req) => await sscd.EndExportSessionAsync(req));
            app.MapMultiplePrefixed(_prefixesV1, "Echo", EndpointRouteBuilderExtensions.MapPost, async (ScuDeEchoRequest req) => await sscd.EchoAsync(req));

            return app;
        }


        public static WebApplication AddScuItEndpoints(this WebApplication app, IITSSCD sscd)
        {
            app.MapMultiplePrefixed(_prefixesV1, "GetDeviceInfo", EndpointRouteBuilderExtensions.MapGet, async () => await sscd.GetDeviceInfoAsync());
            app.MapMultiplePrefixed(_prefixesV1, "Echo", EndpointRouteBuilderExtensions.MapPost, async (ScuItEchoRequest req) => await sscd.EchoAsync(req));
            app.MapMultiplePrefixed(_prefixesV1, "FiscalReceiptInvoice", EndpointRouteBuilderExtensions.MapPost, async (FiscalReceiptInvoice req) => await sscd.FiscalReceiptInvoiceAsync(req));
            app.MapMultiplePrefixed(_prefixesV1, "FiscalReceiptRefund", EndpointRouteBuilderExtensions.MapPost, async (FiscalReceiptRefund req) => await sscd.FiscalReceiptRefundAsync(req));
            app.MapMultiplePrefixed(_prefixesV1, "ExecuteDailyClosing", EndpointRouteBuilderExtensions.MapPost, async (DailyClosingRequest req) => await sscd.ExecuteDailyClosingAsync(req));
            app.MapMultiplePrefixed(_prefixesV1, "ProcessReceipt", EndpointRouteBuilderExtensions.MapPost, async (ProcessRequest req) => await sscd.ProcessReceiptAsync(req));
            app.MapMultiplePrefixed(_prefixesV1, "GetRTInfo", EndpointRouteBuilderExtensions.MapGet, async () => await sscd.GetRTInfoAsync());
            return app;
        }


        public static WebApplication AddScuAtEndpoints(this WebApplication app, IATSSCD ssat)
        {
            app.MapMultiplePrefixed(_prefixesV1, "Certificate", EndpointRouteBuilderExtensions.MapGet, async () => await ssat.CertificateAsync());
            app.MapMultiplePrefixed(_prefixesV1, "ZDA", EndpointRouteBuilderExtensions.MapGet, async () => await ssat.ZdaAsync());
            app.MapMultiplePrefixed(_prefixesV1, "Sign", EndpointRouteBuilderExtensions.MapPost, async (SignRequest req) => await ssat.SignAsync(req));
            app.MapMultiplePrefixed(_prefixesV1, "Echo", EndpointRouteBuilderExtensions.MapPost, async (ifPOS.v1.at.EchoRequest req) => await ssat.EchoAsync(req));
            return app;
        }
    }
}
