using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.CasosDeUso;

/// <summary>Desenlace de corregir un registro rechazado.</summary>
public enum ResultadoCorreccionRechazada
{
	/// <summary>Volvió a la cola como pendiente y saldrá en la siguiente tanda.</summary>
	Corregida = 0,

	/// <summary>No hay un registro fallido con esa clave a nombre del operador.</summary>
	NoEncontrada = 1,

	FaltaTipo = 2,
	FaltaSeveridad = 3,
	KilometroInvalido = 4,
	NotaInsuficiente = 5,
}

/// <summary>
/// Devuelve a la cola un registro que el CCO rechazó, con los datos corregidos (JTT-291 CA 8).
/// </summary>
/// <remarks>
/// <para>
/// <b>Un rechazo funcional no sale solo</b>: la tarjeta de la Cola lo dice desde JTT-1406
/// —«Corríjala: no saldrá sola»— y hasta ahora no había dónde. Este caso de uso es ese dónde:
/// el registro se abre en el mismo formulario de captura, se corrige y vuelve a
/// <c>Pendiente</c>, que es la única transición que <c>ReglaTransicionSincronizacion</c> admite
/// desde <c>Fallido</c>.
/// </para>
/// <para>
/// <b>Se conserva el registro, su clave local, su identificador y su evidencia</b>. Nada de
/// eso se recrea: el operador ve la misma clave en la Cola, Jacob recibe el mismo
/// identificador —que nunca llegó a existir allá, porque el rechazo ocurre antes de la
/// transacción— y las evidencias siguen atadas a él.
/// </para>
/// <para>
/// <b>No decide si el rechazo era corregible.</b> La pantalla ofrece la corrección solo en los
/// rechazos funcionales, que son los que el operador puede arreglar; pero si algún día se
/// abre también para un fallo técnico, volver a Pendiente es igual de válido y solo adelanta
/// el reintento.
/// </para>
/// </remarks>
public sealed class CorregirIncidenciaRechazada
{
	private readonly IIncidentRepository _incidencias;

	public CorregirIncidenciaRechazada(IIncidentRepository incidencias)
	{
		_incidencias = incidencias;
	}

	public async Task<ResultadoCorreccionRechazada> EjecutarAsync(
		string claveLocal,
		TipoIncidencia? tipo,
		string? kilometro,
		KilometerSource fuenteKilometro,
		SeveridadIncidencia? severidad,
		string nota,
		PosicionDispositivo? posicionGps = null,
		CancellationToken cancelacion = default)
	{
		// Las mismas comprobaciones que al convertir un borrador: lo que Jacob va a validar es
		// idéntico, y admitir aquí lo que allá se rechaza sería mandar un segundo rechazo seguro.
		if (!FormularioIncidencia.EstaCompleto(tipo, kilometro, severidad, nota, out var kilometroValido, out var motivo))
		{
			return motivo switch
			{
				MotivoFormularioIncompleto.FaltaTipo => ResultadoCorreccionRechazada.FaltaTipo,
				MotivoFormularioIncompleto.KilometroInvalido => ResultadoCorreccionRechazada.KilometroInvalido,
				MotivoFormularioIncompleto.FaltaSeveridad => ResultadoCorreccionRechazada.FaltaSeveridad,
				_ => ResultadoCorreccionRechazada.NotaInsuficiente,
			};
		}

		var corregida = await _incidencias.CorregirRechazadaAsync(
			claveLocal,
			tipo,
			kilometroValido,
			fuenteKilometro,
			severidad,
			nota.Trim(),
			posicionGps,
			cancelacion);

		return corregida
			? ResultadoCorreccionRechazada.Corregida
			: ResultadoCorreccionRechazada.NoEncontrada;
	}
}
