using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Infrastructure.Dispositivo;

// Mide el volumen del directorio de datos: en Android el interno y el externo son distintos.
public sealed class MedidorEspacioDispositivo : IEspacioDispositivo
{
	private readonly string _directorio;

	public MedidorEspacioDispositivo(string directorio)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(directorio);
		_directorio = directorio;
	}

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
