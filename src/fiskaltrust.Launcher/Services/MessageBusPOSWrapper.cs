using fiskaltrust.ifPOS.v0;

namespace fiskaltrust.Launcher.Services
{
    public class MessageBusPOSWrapper : ifPOS.v1.IPOS
    {
        private readonly MessageBusClient _client;
        private readonly IPOS _queue;

        public MessageBusPOSWrapper(MessageBusClient messageBusService, IPOS queue)
        {
            _client = messageBusService;
            _queue = queue;
        }

        public IAsyncResult BeginEcho(string message, AsyncCallback callback, object state)
        {
            return _queue.BeginEcho(message, callback, state);
        }

        public IAsyncResult BeginJournal(long ftJournalType, long from, long to, AsyncCallback callback, object state)
        {
            return _queue.BeginJournal(ftJournalType, from, to, callback, state);
        }

        public IAsyncResult BeginSign(ReceiptRequest data, AsyncCallback callback, object state)
        {
            return _queue.BeginSign(data, callback, state);
        }

        public string Echo(string message)
        {
            return _queue.Echo(message);
        }

        public async Task<ifPOS.v1.EchoResponse> EchoAsync(ifPOS.v1.EchoRequest message)
        {
            return await EchoAsync(message);
        }

        public string EndEcho(IAsyncResult result)
        {
            return _queue.EndEcho(result);
        }

        public Stream EndJournal(IAsyncResult result)
        {
            return _queue.EndJournal(result);
        }

        public ReceiptResponse EndSign(IAsyncResult result)
        {
            return _queue.EndSign(result);
        }

        public Stream Journal(long ftJournalType, long from, long to)
        {
            return _queue.Journal(ftJournalType, from, to);
        }

        public IAsyncEnumerable<ifPOS.v1.JournalResponse> JournalAsync(ifPOS.v1.JournalRequest request)
        {
            return JournalAsync(request);
        }

        public ReceiptResponse Sign(ReceiptRequest data)
        {
            var response = Sign(data);
            Task.Run(() => _client.PublishSignAsync(data.ftCashBoxID, response.ftQueueID, data, response)).Wait();
            return response;
        }

        public async Task<ifPOS.v1.ReceiptResponse> SignAsync(ifPOS.v1.ReceiptRequest request)
        {
            var response = await SignAsync(request);
            await _client.PublishSignAsync(request.ftCashBoxID, response.ftQueueID, request, response);
            return response;
        }
    }
}
