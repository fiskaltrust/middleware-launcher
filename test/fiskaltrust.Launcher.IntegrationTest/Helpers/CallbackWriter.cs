using System.Text;

namespace fiskaltrust.Launcher.IntegrationTest.Helpers
{
    public class CallbackWriter : TextWriter
    {
        private readonly Action<string> _callback;

        public CallbackWriter(Action<string> callback) { _callback = callback; }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            _callback(value.ToString());
        }

        public override void Write(string? value)
        {
            _callback(value ?? "");
        }

        public override void Write(char[] buffer, int index, int count)
        {
            _callback(buffer.Skip(index).Take(count).Aggregate("", (acc, c) => acc + c));
        }

        public override void Write(char[]? buffer)
        {
            _callback(buffer?.Aggregate("", (acc, c) => acc + c) ?? "");
        }

        public override void Write(ReadOnlySpan<char> buffer)
        {
            _callback(buffer.ToString());
        }

        public override void Write(bool value)
        {
            _callback(value.ToString());
        }

        public override void Write(int value)
        {
            _callback(value.ToString());
        }

        public override void Write(uint value)
        {
            _callback(value.ToString());
        }

        public override void Write(long value)
        {
            _callback(value.ToString());
        }

        public override void Write(ulong value)
        {
            _callback(value.ToString());
        }

        public override void Write(float value)
        {
            _callback(value.ToString());
        }

        public override void Write(double value)
        {
            _callback(value.ToString());
        }

        public override void Write(decimal value)
        {
            _callback(value.ToString());
        }

        public override void Write(object? value)
        {
            _callback(value?.ToString() ?? "");
        }

        public override void Write(StringBuilder? value)
        {
            _callback(value?.ToString() ?? "");
        }

        public override void Write(string format, object? arg0)
        {
            _callback(string.Format(format, arg0));
        }

        public override void Write(string format, object? arg0, object? arg1)
        {
            _callback(string.Format(format, arg0, arg1));
        }

        public override void Write(string format, object? arg0, object? arg1, object? arg2)
        {
            _callback(string.Format(format, arg0, arg1, arg2));
        }

        public override void Write(string format, params object?[] arg)
        {
            _callback(string.Format(format, arg));
        }

        public override Task WriteAsync(char value)
        {
            Write(value);
            return Task.CompletedTask;
        }

        public override Task WriteAsync(string? value)
        {
            Write(value);
            return Task.CompletedTask;
        }

        public override Task WriteAsync(StringBuilder? value, CancellationToken cancellationToken = default)
        {
            Write(value);
            return Task.CompletedTask;
        }

        public override Task WriteAsync(char[] buffer, int index, int count)
        {
            Write(buffer, index, count);
            return Task.CompletedTask;
        }

        public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
        {
            Write(buffer);
            return Task.CompletedTask;
        }

        public override void WriteLine()
        {
            _callback(NewLine);
        }

        public override void WriteLine(char value)
        {
            _callback(value + NewLine);
        }

        public override void WriteLine(char[]? buffer)
        {
            _callback(buffer?.Aggregate("", (acc, c) => acc + c) + NewLine);
        }

        public override void WriteLine(char[] buffer, int index, int count)
        {
            _callback(buffer.Skip(index).Take(count).Aggregate("", (acc, c) => acc + c) + NewLine);
        }

        public override void WriteLine(ReadOnlySpan<char> buffer)
        {
            _callback(buffer.ToString() + NewLine);
        }

        public override void WriteLine(bool value)
        {
            _callback(value.ToString() + NewLine);
        }

        public override void WriteLine(int value)
        {
            _callback(value.ToString() + NewLine);
        }

        public override void WriteLine(uint value)
        {
            _callback(value.ToString() + NewLine);
        }

        public override void WriteLine(long value)
        {
            _callback(value.ToString() + NewLine);
        }

        public override void WriteLine(ulong value)
        {
            _callback(value.ToString() + NewLine);
        }

        public override void WriteLine(float value)
        {
            _callback(value.ToString() + NewLine);
        }

        public override void WriteLine(double value)
        {
            _callback(value.ToString() + NewLine);
        }

        public override void WriteLine(decimal value)
        {
            _callback(value.ToString() + NewLine);
        }

        public override void WriteLine(string? value)
        {
            _callback(value?.ToString() + NewLine);
        }

        public override void WriteLine(StringBuilder? value)
        {
            _callback(value?.ToString() + NewLine);
        }

        public override void WriteLine(object? value)
        {
            _callback(value?.ToString() + NewLine);
        }

        public override void WriteLine(string format, object? arg0)
        {
            _callback(string.Format(format, arg0));
        }

        public override void WriteLine(string format, object? arg0, object? arg1)
        {
            _callback(string.Format(format, arg0, arg1));
        }

        public override void WriteLine(string format, object? arg0, object? arg1, object? arg2)
        {
            _callback(string.Format(format, arg0, arg1, arg2));
        }

        public override void WriteLine(string format, params object?[] arg)
        {
            _callback(string.Format(format, arg));
        }

        public override Task WriteLineAsync(char value)
        {
            Write(value);
            return Task.CompletedTask;
        }

        public override Task WriteLineAsync(string? value)
        {
            Write(value);
            return Task.CompletedTask;
        }

        public override Task WriteLineAsync(StringBuilder? value, CancellationToken cancellationToken = default)
        {
            Write(value);
            return Task.CompletedTask;
        }

        public override Task WriteLineAsync(char[] buffer, int index, int count)
        {
            Write(buffer, index, count);
            return Task.CompletedTask;
        }

        public override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
        {
            Write(buffer);
            return Task.CompletedTask;
        }

        public override Task WriteLineAsync()
        {
            WriteLine();
            return Task.CompletedTask;
        }
    }
}