using fiskaltrust.ifPOS.v1.de;

namespace fiskaltrust.Launcher.IntegrationTest.Helpers
{
    internal class DummyDeSscd : IDESSCD
    {
        public Task<ScuDeEchoResponse> EchoAsync(ScuDeEchoRequest request) => Task.FromResult(new ScuDeEchoResponse { Message = request.Message });

        public Task<EndExportSessionResponse> EndExportSessionAsync(EndExportSessionRequest request) => throw new NotImplementedException();

        public Task ExecuteSelfTestAsync() => throw new NotImplementedException();

        public Task ExecuteSetTseTimeAsync() => throw new NotImplementedException();

        public Task<ExportDataResponse> ExportDataAsync(ExportDataRequest request) => throw new NotImplementedException();

        public Task<FinishTransactionResponse> FinishTransactionAsync(FinishTransactionRequest request) => throw new NotImplementedException();

        public Task<TseInfo> GetTseInfoAsync() => throw new NotImplementedException();

        public Task<RegisterClientIdResponse> RegisterClientIdAsync(RegisterClientIdRequest request) => throw new NotImplementedException();

        public Task<TseState> SetTseStateAsync(TseState state) => throw new NotImplementedException();

        public Task<StartExportSessionResponse> StartExportSessionAsync(StartExportSessionRequest request) => throw new NotImplementedException();

        public Task<StartExportSessionResponse> StartExportSessionByTimeStampAsync(StartExportSessionByTimeStampRequest request) => throw new NotImplementedException();

        public Task<StartExportSessionResponse> StartExportSessionByTransactionAsync(StartExportSessionByTransactionRequest request) => throw new NotImplementedException();

        public Task<StartTransactionResponse> StartTransactionAsync(StartTransactionRequest request) => throw new NotImplementedException();

        public Task<UnregisterClientIdResponse> UnregisterClientIdAsync(UnregisterClientIdRequest request) => throw new NotImplementedException();

        public Task<UpdateTransactionResponse> UpdateTransactionAsync(UpdateTransactionRequest request) => throw new NotImplementedException();
    }
}
