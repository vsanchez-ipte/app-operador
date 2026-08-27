using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;

namespace AppOperador.Aplicacion.CasosDeUso;

/// <summary>
/// Adjunta un archivo a una incidencia: valida, copia y registra (JTT-1398 CA 1, 5 y 9).
/// </summary>
/// <remarks>
/// <para>
/// <b>Único dueño del criterio</b>, como <c>ConvertirBorradorEnIncidencia</c> lo es del suyo.
/// Adjuntar tiene tres pasos que solo valen en un orden, y repartirlos entre la pantalla y el
/// repositorio dejaría el orden a merced de quien llame: se valida contra los límites del
/// servidor, se copia al espacio privado, y solo entonces se registra la fila.
/// </para>
/// <para>
/// <b>Por qué ese orden y no otro.</b> Validar al final copiaría archivos que van a rechazarse
/// —quince megabytes de video en un teléfono de campo—. Registrar antes de copiar dejaría filas
/// apuntando a un archivo que quizá no llegó, que es la forma de romper la cola: al sincronizar
/// no habría qué subir y el registro no sabría que está roto.
/// </para>
/// <para>
/// <b>No decide a qué incidencia pertenece.</b> Recibe el identificador ya resuelto, porque el
/// CA 1 permite adjuntar <i>antes</i> de guardar y quién resuelve esa identidad es la pantalla.
/// </para>
/// </remarks>
public sealed class AdjuntarEvidencia
{
	private readonly IRepositorioEvidencias _evidencias;
	private readonly IAlmacenEvidencias _almacen;
	private readonly ICatalogoRepository _catalogo;
	private readonly IClock _reloj;
	private readonly IAuditLog _bitacora;

	public AdjuntarEvidencia(
		IRepositorioEvidencias evidencias,
		IAlmacenEvidencias almacen,
		ICatalogoRepository catalogo,
		IClock reloj,
		IAuditLog bitacora)
	{
		_evidencias = evidencias;
		_almacen = almacen;
		_catalogo = catalogo;
		_reloj = reloj;
		_bitacora = bitacora;
	}

	/// <summary>Intenta adjuntar el archivo elegido a la incidencia indicada.</summary>
	public async Task<ResultadoAdjuntar> EjecutarAsync(
		string incidenciaUuid,
		ArchivoElegido archivo,
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

		// La identidad se genera aquí y no en la base: es también la clave de idempotencia
		// frente al servidor, así que tiene que existir antes de que el archivo se copie y
		// sobrevivir a cualquier reintento.
		var uuid = Guid.NewGuid().ToString();
		var ruta = await _almacen.GuardarAsync(uuid, archivo, cancelacion);

		if (string.IsNullOrWhiteSpace(ruta))
		{
			// El archivo no se pudo leer: lo borraron, se revocó el acceso o el
			// almacenamiento está lleno. No se registra nada, para no dejar una fila
			// apuntando a un archivo que no existe.
			await _bitacora.RegistrarAsync(
				NivelAuditoria.Advertencia,
				"No se pudo copiar la evidencia al espacio privado de la app.",
				cancelacion);

			return ResultadoAdjuntar.Rechazada(MotivoEvidenciaRechazada.ArchivoVacio);
		}

		var evidencia = new EvidenciaAdjunta(
			uuid,
			incidenciaUuid,
			archivo.NombreOriginal,
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
}
