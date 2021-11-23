using fiskaltrust.ifPOS.v1;
using fiskaltrust.ifPOS.v1.de;
using Microsoft.AspNetCore.Mvc;

namespace fiskaltrust.Launcher.Extensions
{
    public static class WebApplicationExtensions
    {
        public static WebApplication AddQueueEndpoints(this WebApplication app, IPOS pos)
        {
            app.MapPost("v1/echo", async (EchoRequest req) => await pos.EchoAsync(req));
            app.MapPost("v1/sign", async (ReceiptRequest req) => await pos.SignAsync(req));
            app.MapGet("v1/journal", ([FromQuery] long type, [FromQuery] long from, [FromQuery] long to) => pos.JournalAsync(new JournalRequest { ftJournalType = type, From = from, To = to }));

            return app;
        }

        public static WebApplication AddScuEndpoints(this WebApplication app, IDESSCD sscd)
        {
            app.MapPost("v1/StartTransaction", async (StartTransactionRequest req) => await sscd.StartTransactionAsync(req));
            app.MapPost("v1/UpdateTransaction", async (UpdateTransactionRequest req) => await sscd.UpdateTransactionAsync(req));
            app.MapPost("v1/FinishTransaction", async (FinishTransactionRequest req) => await sscd.FinishTransactionAsync(req));
            app.MapPost("v1/GetTseInfo", async () => await sscd.GetTseInfoAsync());
            app.MapPost("v1/SetTseState", async (TseState req) => await sscd.SetTseStateAsync(req));
            app.MapPost("v1/RegisterClientId", async (RegisterClientIdRequest req) => await sscd.RegisterClientIdAsync(req));
            app.MapPost("v1/UnregisterClientId", async (UnregisterClientIdRequest req) => await sscd.UnregisterClientIdAsync(req));
            app.MapPost("v1/ExecuteSetTseTime", async () => await sscd.ExecuteSetTseTimeAsync());
            app.MapPost("v1/ExecuteSelfTest", async () => await sscd.ExecuteSelfTestAsync());
            app.MapPost("v1/StartExportSession", async (StartExportSessionRequest req) => await sscd.StartExportSessionAsync(req));
            app.MapPost("v1/StartExportSessionByTimeStamp", async (StartExportSessionByTimeStampRequest req) => await sscd.StartExportSessionByTimeStampAsync(req));
            app.MapPost("v1/StartExportSessionByTransaction", async (StartExportSessionByTransactionRequest req) => await sscd.StartExportSessionByTransactionAsync(req));
            app.MapPost("v1/ExportData", async (ExportDataRequest req) => await sscd.ExportDataAsync(req));
            app.MapPost("v1/EndExportSession", async (EndExportSessionRequest req) => await sscd.EndExportSessionAsync(req));
            app.MapPost("v1/Echo", async (ScuDeEchoRequest req) => await sscd.EchoAsync(req));

            return app;
        }
    }
}
