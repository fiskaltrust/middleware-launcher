using fiskaltrust.ifPOS.v1;
using fiskaltrust.ifPOS.v1.de;
using Microsoft.AspNetCore.Mvc;
using System.IO.Pipelines;
using System.Linq;

namespace fiskaltrust.Launcher.Extensions
{
    public static class WebApplicationExtensions
    {
        public static WebApplication AddQueueEndpoints(this WebApplication app, IPOS pos)
        {
            app.MapPost("v2/Echo", async (EchoRequest req) => await pos.EchoAsync(req));
            app.MapPost("v2/Sign", async (ReceiptRequest req) => await pos.SignAsync(req));
            app.MapGet("v2/Journal", ([FromQuery] long type, [FromQuery] long? from, [FromQuery] long? to) =>
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
            app.MapPost("v2/StartTransaction", async (StartTransactionRequest req) => await sscd.StartTransactionAsync(req));
            app.MapPost("v2/UpdateTransaction", async (UpdateTransactionRequest req) => await sscd.UpdateTransactionAsync(req));
            app.MapPost("v2/FinishTransaction", async (FinishTransactionRequest req) => await sscd.FinishTransactionAsync(req));
            app.MapPost("v2/GetTseInfo", async () => await sscd.GetTseInfoAsync());
            app.MapPost("v2/SetTseState", async (TseState req) => await sscd.SetTseStateAsync(req));
            app.MapPost("v2/RegisterClientId", async (RegisterClientIdRequest req) => await sscd.RegisterClientIdAsync(req));
            app.MapPost("v2/UnregisterClientId", async (UnregisterClientIdRequest req) => await sscd.UnregisterClientIdAsync(req));
            app.MapPost("v2/ExecuteSetTseTime", async () => await sscd.ExecuteSetTseTimeAsync());
            app.MapPost("v2/ExecuteSelfTest", async () => await sscd.ExecuteSelfTestAsync());
            app.MapPost("v2/StartExportSession", async (StartExportSessionRequest req) => await sscd.StartExportSessionAsync(req));
            app.MapPost("v2/StartExportSessionByTimeStamp", async (StartExportSessionByTimeStampRequest req) => await sscd.StartExportSessionByTimeStampAsync(req));
            app.MapPost("v2/StartExportSessionByTransaction", async (StartExportSessionByTransactionRequest req) => await sscd.StartExportSessionByTransactionAsync(req));
            app.MapPost("v2/ExportData", async (ExportDataRequest req) => await sscd.ExportDataAsync(req));
            app.MapPost("v2/EndExportSession", async (EndExportSessionRequest req) => await sscd.EndExportSessionAsync(req));
            app.MapPost("v2/Echo", async (ScuDeEchoRequest req) => await sscd.EchoAsync(req));

            return app;
        }
    }
}
