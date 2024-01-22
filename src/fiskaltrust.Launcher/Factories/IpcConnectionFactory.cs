using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Security.Principal;
using fiskaltrust.Launcher.Common.Configuration;

namespace fiskaltrust.Launcher.Factories
{
  public interface IConnectionFactory
  {
    public ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext _, CancellationToken cancellationToken);
  }

  public class IpcConnectionFactory : IConnectionFactory
  {
    private IConnectionFactory _connectionFactory;
    public IpcConnectionFactory(LauncherConfiguration configuration)
    {
      if (OperatingSystem.IsWindows())
      {
        _connectionFactory = new NamedPipesConnectionFactory(configuration.LauncherServiceUri!);
      }
      else
      {
        _connectionFactory = new UnixDomainSocketsConnectionFactory(configuration.LauncherServiceUri!);
      }
    }
    public ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext _, CancellationToken cancellationToken) => _connectionFactory.ConnectAsync(_, cancellationToken);
  }

  public class NamedPipesConnectionFactory : IConnectionFactory
  {
    private readonly string _uri;

    public NamedPipesConnectionFactory(string uri)
    {
      _uri = uri;
    }

    public async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext _, CancellationToken cancellationToken)
    {
      var clientStream = new NamedPipeClientStream(".", _uri, PipeDirection.InOut, PipeOptions.WriteThrough | PipeOptions.Asynchronous, TokenImpersonationLevel.Anonymous);
      await clientStream.ConnectAsync(cancellationToken).ConfigureAwait(false);
      return clientStream;
    }
  }

  public class UnixDomainSocketsConnectionFactory : IConnectionFactory
  {
    private readonly EndPoint _endPoint;

    public UnixDomainSocketsConnectionFactory(string uri)
    {
      _endPoint = new UnixDomainSocketEndPoint(uri);
    }

    public async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext _, CancellationToken cancellationToken)
    {
      var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
      await socket.ConnectAsync(_endPoint, cancellationToken).ConfigureAwait(false);
      return new NetworkStream(socket, ownsSocket: true);
    }
  }
}