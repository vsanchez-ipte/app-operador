using System.Net;
using System.Net.Sockets;

namespace AppOperador.Infrastructure.Http;

// En Android el handler lanza WebException, no HttpRequestException. El tiempo agotado se trata aparte.
internal static class FalloDeComunicacion
{
	public static bool Es(Exception excepcion) =>
		excepcion is HttpRequestException or WebException or SocketException or IOException;
}
