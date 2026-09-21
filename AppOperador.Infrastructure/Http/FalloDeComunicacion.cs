using System.Net;
using System.Net.Sockets;

namespace AppOperador.Infrastructure.Http;

/// <summary>
/// Qué excepciones significan «no se pudo hablar con Jacob CCO», sea cual sea el handler.
/// </summary>
/// <remarks>
/// <para>
/// Los clientes atrapaban solo <see cref="HttpRequestException"/>, que es lo que lanza el
/// handler administrado de .NET. <b>En Android el handler es otro</b>
/// (<c>AndroidMessageHandler</c>) y un socket cerrado, un servidor caído o un nombre que no
/// resuelve llegan como <see cref="WebException"/>. Con el API apagado, tocar «Ingresar»
/// cerraba la app: la excepción subía hasta el comando en el hilo principal.
/// </para>
/// <para>
/// Un solo sitio para la lista, porque son cuatro clientes y nueve <c>catch</c>: si aparece
/// otra excepción de transporte se agrega aquí y no en nueve lugares. El tiempo de espera de
/// <c>HttpClient</c> queda fuera a propósito: llega como <see cref="TaskCanceledException"/>
/// y cada cliente ya lo distingue, porque en el registro «tiempo agotado» y «conexión
/// fallida» apuntan a causas distintas.
/// </para>
/// </remarks>
internal static class FalloDeComunicacion
{
	/// <summary>Indica si la excepción es un fallo de transporte, no una respuesta ni un error del cliente.</summary>
	public static bool Es(Exception excepcion) =>
		excepcion is HttpRequestException or WebException or SocketException or IOException;
}
