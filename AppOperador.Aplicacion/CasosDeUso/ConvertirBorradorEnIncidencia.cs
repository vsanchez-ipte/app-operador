using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.CasosDeUso;

/// <summary>
/// Motivo por el que un borrador no se pudo convertir en incidencia (JTT-1399 CA 9).
/// </summary>
/// <remarks>
/// Se devuelve un motivo y no un booleano porque la pantalla tiene que decirle al operador
/// <b>qué le falta</b>. Un «no se pudo» a secas frente a un formulario de seis campos obliga a
/// adivinar, y se captura en carretera.
/// </remarks>
public enum ResultadoConversionBorrador
{
	/// <summary>El borrador pasó a la cola como incidencia pendiente.</summary>
	Convertido = 0,

	/// <summary>No existe, ya no es borrador, o es de otro operador.</summary>
	NoEncontrado = 1,

	/// <summary>Falta elegir el tipo de incidencia.</summary>
	FaltaTipo = 2,

	/// <summary>Falta elegir la severidad.</summary>
	FaltaSeveridad = 3,

	/// <summary>El kilómetro está vacío o no tiene la forma <c>000+000</c>.</summary>
	KilometroInvalido = 4,

	/// <summary>El tipo exige descripción y la nota no alcanza.</summary>
	NotaInsuficiente = 5,
}

/// <summary>
/// Convierte un borrador en incidencia ejecutando las validaciones de envío (JTT-1399 CA 8 y 9).
/// </summary>
/// <remarks>
/// <para>
/// <b>Existe para que el CA 9 tenga un solo dueño.</b> El criterio dice que al convertir se
/// ejecutan «todas las validaciones»; las mismas que aplica el formulario al guardar una
/// incidencia nueva. Con la regla repartida entre la pantalla y el repositorio, el segundo
/// camino acaba validando de menos —y lo que se cuela no se descubre al capturar, sino al
/// sincronizar, cuando el operador ya no está frente al hecho.
/// </para>
/// <para>
/// El caso de uso <b>no toca la base directamente</b>: valida y delega en
/// <see cref="IIncidentRepository.ConvertirBorradorAsync"/>, que es quien sabe de filas. Aquí
/// vive la regla; allá, la persistencia.
/// </para>
/// </remarks>
public sealed class ConvertirBorradorEnIncidencia
{
	private readonly IIncidentRepository _incidencias;

	public ConvertirBorradorEnIncidencia(IIncidentRepository incidencias)
	{
		_incidencias = incidencias;
	}

	/// <summary>
	/// Valida el contenido y, si pasa, deja el borrador como incidencia pendiente.
	/// </summary>
	/// <param name="claveLocal">Clave <c>LOC-######</c> del borrador.</param>
	/// <param name="tipo">Tipo elegido. Nulo si el operador aún no lo eligió.</param>
	/// <param name="kilometro">Kilómetro tal como está escrito, sin validar.</param>
	/// <param name="fuenteKilometro">Si el kilómetro lo dio el GPS o lo escribió el operador.</param>
	/// <param name="severidad">Nivel elegido. Nulo si aún no se eligió.</param>
	/// <param name="nota">Nota capturada.</param>
	/// <remarks>
	/// El orden de las comprobaciones es el del formulario, de arriba abajo, para que el primer
	/// motivo que se reporte sea el primer campo que al operador le falta llenar.
	/// </remarks>
	public async Task<ResultadoConversionBorrador> EjecutarAsync(
		string claveLocal,
		TipoIncidencia? tipo,
		string? kilometro,
		KilometerSource fuenteKilometro,
		SeveridadIncidencia? severidad,
		string nota,
		PosicionDispositivo? posicionGps = null,
		CancellationToken cancelacion = default)
	{
		if (tipo is null)
		{
			return ResultadoConversionBorrador.FaltaTipo;
		}

		if (!Kilometer.IntentarCrear(kilometro, out var kilometroValido))
		{
			return ResultadoConversionBorrador.KilometroInvalido;
		}

		if (severidad is null)
		{
			return ResultadoConversionBorrador.FaltaSeveridad;
		}

		if (!ReglaNotaIncidencia.EsSuficiente(tipo.ExigeDescripcion, nota))
		{
			return ResultadoConversionBorrador.NotaInsuficiente;
		}

		var convertido = await _incidencias.ConvertirBorradorAsync(
			claveLocal,
			tipo,
			kilometroValido,
			fuenteKilometro,
			severidad,
			nota.Trim(),
			posicionGps,
			cancelacion);

		return convertido
			? ResultadoConversionBorrador.Convertido
			: ResultadoConversionBorrador.NoEncontrado;
	}
}
