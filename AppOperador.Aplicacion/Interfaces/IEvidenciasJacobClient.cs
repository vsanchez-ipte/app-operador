using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Sube evidencias al canal móvil de Jacob CCO (JTT-1398 CA 11).
/// </summary>
/// <remarks>
/// <para>
/// Va aparte de <see cref="IIncidenciasJacobClient"/> aunque compartan endpoint base: la
/// evidencia viaja en <c>multipart/form-data</c> y no en JSON, un archivo por llamada, y
/// mezclarlas obligaría a que el cliente de incidencias supiera de flujos y de límites de
/// memoria que no le tocan.
/// </para>
/// <para>
/// <b>La incidencia tiene que existir del otro lado.</b> El servidor valida el <c>uuid</c> de la
/// ruta; una evidencia que salga antes recibe <c>appevidencias.incidencia.noexiste</c>, y lo que
/// hay que reintentar entonces es el registro, no ella.
/// </para>
/// </remarks>
public interface IEvidenciasJacobClient
{
	/// <summary>Sube un archivo ya guardado en el espacio privado.</summary>
	/// <param name="incidenciaUuid">La incidencia a la que documenta, ya registrada en Jacob.</param>
	/// <param name="rutaArchivo">Dónde está el archivo dentro del espacio privado.</param>
	/// <param name="nombreOriginal">Nombre con el que viaja; el servidor decide el tipo por contenido.</param>
	/// <param name="accessToken">Token de la sesión. Exige <c>APP_OPERADOR_CAPTURA</c>.</param>
	Task<ResultadoEnvioEvidencia> SubirAsync(
		string incidenciaUuid,
		string rutaArchivo,
		string nombreOriginal,
		string accessToken,
		CancellationToken cancelacion = default);
}
