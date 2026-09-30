using System.Diagnostics;
using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppOperador.Mobile.ViewModels;

public sealed partial class CapturaViewModel
{
	// Distingue si el KM lo escribe el GPS o el operador; sin ella la fuente acabaría siempre en Manual.
	private bool _asignandoDesdeGps;

	[ObservableProperty]
	public partial string? AvisoGps { get; set; }

	// La lectura tarda hasta diez segundos, pero solo espera el campo, no la captura.
	[ObservableProperty]
	public partial bool BuscandoUbicacion { get; set; }

	[ObservableProperty]
	public partial KilometerSource FuenteKilometro { get; set; }

	public bool HayAvisoGps => !string.IsNullOrEmpty(AvisoGps);

	public string EtiquetaKilometro =>
		FuenteKilometro == KilometerSource.GPS ? "KM (GPS)" : "KM (MANUAL)";

	// El comando ya captura sus fallos; lo que falle se traduce a un aviso.
	private void RecalcularUbicacionSinEsperar() => _ = RecalcularUbicacionCommand.ExecuteAsync(null);

	[RelayCommand]
	private async Task RecalcularUbicacionAsync()
	{
		BuscandoUbicacion = true;
		AvisoGps = null;
		var tecleadoAntes = Kilometro;
		ResultadoKilometroPorUbicacion resultado;
		try
		{
			resultado = await _obtenerKilometro.EjecutarAsync();
		}
		finally
		{
			BuscandoUbicacion = false;
		}

		// Si el operador tecleó el KM mientras el GPS fijaba, vale más que la lectura.
		if (FuenteKilometro == KilometerSource.Manual
			&& !string.IsNullOrWhiteSpace(Kilometro)
			&& Kilometro != tecleadoAntes)
		{
			return;
		}

		if (!resultado.HayKilometro)
		{
			var teniaKilometroGps = FuenteKilometro == KilometerSource.GPS;
			_posicionGps = null;
			FuenteKilometro = KilometerSource.Manual;

			// Una lectura anterior no sobrevive como manual; lo tecleado a mano sí se conserva.
			if (teniaKilometroGps)
			{
				_asignandoDesdeGps = true;
				Kilometro = string.Empty;
				_asignandoDesdeGps = false;
			}

			AvisoGps = MensajeDe(resultado.Motivo);
			return;
		}

		AvisoGps = null;
		_posicionGps = resultado.Posicion;

		// Para que el propio GPS no marque el kilómetro como capturado a mano.
		_asignandoDesdeGps = true;
		Kilometro = resultado.Kilometro!.Valor;
		_asignandoDesdeGps = false;

		FuenteKilometro = KilometerSource.GPS;
	}

	private static string MensajeDe(MotivoSinKilometro? motivo) => motivo switch
	{
		MotivoSinKilometro.ServicioNoDisponible => MensajeGpsNoDisponible,
		MotivoSinKilometro.PermisoDenegado => MensajeGpsSinPermiso,
		MotivoSinKilometro.PrecisionInsuficiente => MensajeGpsSinPrecision,
		MotivoSinKilometro.FueraDelCorredor => MensajeFueraDelCorredor,
		MotivoSinKilometro.TramoSinGeometria => MensajeTramoSinGeometria,
		_ => MensajeErrorGps,
	};

	partial void OnAvisoGpsChanged(string? value) => OnPropertyChanged(nameof(HayAvisoGps));

	partial void OnFuenteKilometroChanged(KilometerSource value) =>
		OnPropertyChanged(nameof(EtiquetaKilometro));

	// Escribir el KM a mano cambia su origen: deja de ser lectura del GPS.
	partial void OnKilometroChanged(string value)
	{
		ErrorKilometro = null;

		if (_asignandoDesdeGps)
		{
			return;
		}

		FuenteKilometro = KilometerSource.Manual;
		_posicionGps = null;
	}
}
