using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Infrastructure.Dispositivo;

/// <summary>
/// Mide el espacio del volumen donde viven la base local y las evidencias (JTT-289 CA 8, JTT-292 CA 4).
/// </summary>
/// <remarks>
/// <para>
/// Se mide <b>sobre el directorio de datos de la app</b>, no sobre «el disco»: en Android el
/// almacenamiento interno y el externo son volúmenes distintos, y lo que importa es el que va a
/// recibir el archivo.
/// </para>
/// <para>
/// <b>Si el sistema no lo dice, se devuelve desconocido y no se inventa nada.</b> Quien decide
/// con el número —<c>ReglaEspacioParaEvidencia</c>— trata lo desconocido como «no bloquear».
/// </para>
/// </remarks>
public sealed class MedidorEspacioDispositivo : IEspacioDispositivo
{
	private readonly string _directorio;

	/// <param name="directorio">Directorio cuyo volumen se mide. Normalmente el de datos de la app.</param>
	public MedidorEspacioDispositivo(string directorio)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(directorio);
		_directorio = directorio;
	}

	/// <inheritdoc />
	public EspacioDispositivo Medir()
	{
		try
		{
			var volumen = new DriveInfo(_directorio);
			return new EspacioDispositivo(volumen.AvailableFreeSpace, volumen.TotalSize);
		}
		catch (Exception excepcion) when (
			excepcion is IOException or UnauthorizedAccessException or ArgumentException)
		{
			// Ruta que no es un volumen montado, permiso negado o sistema sin statvfs.
			return EspacioDispositivo.Desconocido;
		}
	}
}
