using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.CasosDeUso;

public enum ResultadoConversionBorrador
{
	Convertido = 0,

	NoEncontrado = 1,

	FaltaTipo = 2,

	FaltaSeveridad = 3,

	KilometroInvalido = 4,

	NotaInsuficiente = 5,
}

public sealed class ConvertirBorradorEnIncidencia
{
	private readonly IIncidentRepository _incidencias;

	public ConvertirBorradorEnIncidencia(IIncidentRepository incidencias)
	{
		_incidencias = incidencias;
	}

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
		// Las comprobaciones son las mismas que al corregir un rechazado; viven en un solo sitio.
		if (!FormularioIncidencia.EstaCompleto(tipo, kilometro, severidad, nota, out var kilometroValido, out var motivo))
		{
			return motivo switch
			{
				MotivoFormularioIncompleto.FaltaTipo => ResultadoConversionBorrador.FaltaTipo,
				MotivoFormularioIncompleto.KilometroInvalido => ResultadoConversionBorrador.KilometroInvalido,
				MotivoFormularioIncompleto.FaltaSeveridad => ResultadoConversionBorrador.FaltaSeveridad,
				_ => ResultadoConversionBorrador.NotaInsuficiente,
			};
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
