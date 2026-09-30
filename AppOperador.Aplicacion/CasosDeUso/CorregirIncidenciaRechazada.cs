using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.CasosDeUso;

public enum ResultadoCorreccionRechazada
{
	Corregida = 0,

	NoEncontrada = 1,

	FaltaTipo = 2,
	FaltaSeveridad = 3,
	KilometroInvalido = 4,
	NotaInsuficiente = 5,
}

// Conserva clave, uuid y evidencias: el rechazo ocurrió antes de que Jacob creara el registro.
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
