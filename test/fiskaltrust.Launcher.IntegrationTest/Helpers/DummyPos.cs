using fiskaltrust.ifPOS.v1;

namespace fiskaltrust.Launcher.IntegrationTest.Helpers
{
    internal class DummyPos : IPOS
    {
        public IAsyncResult BeginEcho(string message, AsyncCallback callback, object state) => throw new NotImplementedException();

        public IAsyncResult BeginJournal(long ftJournalType, long from, long to, AsyncCallback callback, object state) => throw new NotImplementedException();

        public IAsyncResult BeginSign(ifPOS.v0.ReceiptRequest data, AsyncCallback callback, object state) => throw new NotImplementedException();

        public string Echo(string message) => message;

        public Task<EchoResponse> EchoAsync(EchoRequest message) => Task.FromResult(new EchoResponse { Message = message.Message });

        public string EndEcho(IAsyncResult result) => throw new NotImplementedException();

        public Stream EndJournal(IAsyncResult result) => throw new NotImplementedException();

        public ifPOS.v0.ReceiptResponse EndSign(IAsyncResult result) => throw new NotImplementedException();

        public Stream Journal(long ftJournalType, long from, long to) => throw new NotImplementedException();

        public IAsyncEnumerable<JournalResponse> JournalAsync(JournalRequest request) => throw new NotImplementedException();

        public ifPOS.v0.ReceiptResponse Sign(ifPOS.v0.ReceiptRequest data) => throw new NotImplementedException();

        public Task<ReceiptResponse> SignAsync(ReceiptRequest request) => throw new NotImplementedException();
    }
}