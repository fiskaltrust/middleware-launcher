using System.Text;

namespace fiskaltrust.Launcher.Middlewares
{
    public class RequestResponseLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;
        public RequestResponseLoggingMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
        {
            _next = next;
            _logger = loggerFactory.CreateLogger<RequestResponseLoggingMiddleware>();
        }
        public async Task Invoke(HttpContext context)
        {
            await LogRequest(context.Request);
            await LogResponse(context);
        }

        private async Task LogRequest(HttpRequest request)
        {
            request.EnableBuffering();

            var buffer = new byte[Convert.ToInt32(request.ContentLength)];

            await request.Body.ReadAsync(new Memory<byte>(buffer));

            _logger.LogDebug("Http Request Information: Schema \"{Scheme}\" Host = \"{Host}\" Path = \"{Path}\" QueryString = \"{QueryString}\" Body = {body}",
                request.Scheme, request.Host, request.Path, request.QueryString, buffer.Length == 0 ? null : Encoding.UTF8.GetString(buffer));
            request.Body.Position = 0;
        }

        private async Task LogResponse(HttpContext context)
        {
            var originalBodyStream = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            await _next.Invoke(context);

            string text;
            if (!context.Response.ContentType.Contains("octet-stream"))
            {
                responseBody.Position = 0;

                var len = Convert.ToInt32(context.Response.ContentLength ?? responseBody.Length);
                var buffer = new byte[len];
                
                await responseBody.ReadAsync(new Memory<byte>(buffer));

                text = Encoding.UTF8.GetString(buffer);
            }
            else
            {
                text = "[stream]";
            }
            responseBody.Position = 0;


            await responseBody.CopyToAsync(originalBodyStream);
            context.Response.Body = originalBodyStream;

            _logger.LogDebug("Http Response Information: StatusCode \"{StatusCode}\" Body {text}",
                context.Response.StatusCode, text);
        }
    }
}
