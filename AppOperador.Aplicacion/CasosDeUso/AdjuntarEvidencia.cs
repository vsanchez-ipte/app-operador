using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;

namespace AppOperador.Aplicacion.CasosDeUso;

// El orden importa: validar, comprobar espacio, copiar y solo entonces registrar la fila.
public sealed class AdjuntarEvidencia
{
	private readonly IRepositorioEvidencias _evidencias;
	private readonly IAlmacenEvidencias _almacen;
	private readonly ICatalogoRepository _catalogo;
	private readonly IClock _reloj;
	private readonly IAuditLog _bitacora;
	private readonly IEspacioDispositivo _espacio;

	public AdjuntarEvidencia(
		IRepositorioEvidencias evidencias,
		IAlmacenEvidencias almacen,
		ICatalogoRepository catalogo,
		IClock reloj,
		IAuditLog bitacora,
		IEspacioDispositivo espacio)
	{
		_espacio = espacio;
		_evidencias = evidencias;
		_almacen = almacen;
		_catalogo = catalogo;
		_reloj = reloj;
		_bitacora = bitacora;
	}

	public async Task<ResultadoAdjuntar> EjecutarAsync(
		string incidenciaUuid,
		ArchivoElegido archivo,
		string? claveLocal = null,
		CancellationToken cancelacion = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(incidenciaUuid);
		ArgumentNullException.ThrowIfNull(archivo);

		var limites = (await _catalogo.ObtenerAsync(cancelacion)).LimitesEvidencia;
		var yaAdjuntas = await _evidencias.ContarDeIncidenciaAsync(incidenciaUuid, cancelacion);

		var motivo = ReglaEvidenciaAdmisible.Comprobar(
			limites, archivo.TipoMime, archivo.Bytes, yaAdjuntas);

		if (motivo != MotivoEvidenciaRechazada.Ninguno)
		{
			return ResultadoAdjuntar.Rechazada(motivo);
		}

		// Antes de copiar: una copia a medias en un disco lleno se subiría como si estuviera entera.
		if (!ReglaEspacioParaEvidencia.Cabe(_espacio.Medir().BytesLibres, archivo.Bytes))
		{
			await _bitacora.RegistrarAsync(
				NivelAuditoria.Advertencia,
				$"Evidencia rechazada por falta de espacio en el dispositivo ({archivo.Bytes} bytes).",
				cancelacion);

			return ResultadoAdjuntar.Rechazada(MotivoEvidenciaRechazada.SinEspacio);
		}

		// Se genera aquí porque también es la clave de idempotencia con el servidor.
		var uuid = Guid.NewGuid().ToString();
		var ruta = await _almacen.GuardarAsync(uuid, archivo, cancelacion);

		if (string.IsNullOrWhiteSpace(ruta))
		{
			await _bitacora.RegistrarAsync(
				NivelAuditoria.Advertencia,
				"No se pudo copiar la evidencia al espacio privado de la app.",
				cancelacion);

			return ResultadoAdjuntar.Rechazada(MotivoEvidenciaRechazada.ArchivoVacio);
		}

		var evidencia = new EvidenciaAdjunta(
			uuid,
			incidenciaUuid,
			NombreParaMostrar(archivo, claveLocal),
			archivo.TipoMime,
			archivo.Bytes,
			ruta,
			EstadoSincronizacion.Pendiente);

		await _evidencias.AgregarAsync(evidencia, cancelacion);

		await _bitacora.RegistrarAsync(
			NivelAuditoria.Info,
			$"Evidencia adjuntada a la incidencia {incidenciaUuid}.",
			cancelacion);

		return ResultadoAdjuntar.Aceptada(evidencia);
	}

	// Solo se renombra lo capturado con la cámara: lo elegido ya trae un nombre que dice más.
	private string NombreParaMostrar(ArchivoElegido archivo, string? claveLocal) =>
		archivo.Origen is OrigenEvidencia.Camara or OrigenEvidencia.Video
			? NombreEvidencia.Componer(claveLocal, _reloj.UtcAhora, archivo.NombreOriginal)
			: archivo.NombreOriginal;
}
