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

/// <summary>Kilómetro por GPS: lectura, fuente y avisos (JTT-1395).</summary>
public sealed partial class CapturaViewModel
{
	/// <summary>
	/// Marca que el kilómetro lo está escribiendo la lectura del GPS, no el operador.
	/// </summary>
	/// <remarks>
	/// Sin esto no se distingue quién escribió: <see cref="OnKilometroChanged(string)"/> se dispara
	/// igual cuando <see cref="RecalcularUbicacionAsync"/> asigna la lectura que cuando el operador
	/// teclea, y la fuente acabaría siempre en <see cref="KilometerSource.Manual"/>.
	/// </remarks>
	private bool _asignandoDesdeGps;

	[ObservableProperty]
	public partial string? AvisoGps { get; set; }

	/// <summary>
	/// Indica que se está pidiendo una lectura nueva al GPS. Enciende el indicador junto al KM.
	/// </summary>
	/// <remarks>
	/// La lectura tarda hasta diez segundos a propósito —una posición vieja en un vehículo en
	/// marcha son kilómetros de error—, pero eso no tiene por qué detener la pantalla: el
	/// operador ya puede elegir tipo y severidad, escribir la nota, o teclear el KM si lo
	/// sabe. Lo que se espera es un campo, no la captura.
	/// </remarks>
	[ObservableProperty]
	public partial bool BuscandoUbicacion { get; set; }

	/// <summary>
	/// Origen del kilómetro: GPS mientras la lectura sea válida, Manual en cuanto el
	/// operador lo escriba a mano.
	/// </summary>
	[ObservableProperty]
	public partial KilometerSource FuenteKilometro { get; set; }

	public bool HayAvisoGps => !string.IsNullOrEmpty(AvisoGps);

	/// <summary>
	/// Etiqueta del campo de kilómetro, con la fuente de la que salió (JTT-1393 CA 6).
	/// </summary>
	/// <remarks>
	/// Antes era el texto fijo «KM (GPS O MANUAL)», que enuncia las dos posibilidades pero no
	/// dice cuál ocurrió. El criterio pide justamente lo segundo.
	/// </remarks>
	public string EtiquetaKilometro =>
		FuenteKilometro == KilometerSource.GPS ? "KM (GPS)" : "KM (MANUAL)";

	/// <summary>Pide una lectura al GPS sin detener a quien la pide.</summary>
	/// <remarks>
	/// Lo que escape de aquí no tendría quién lo recogiera: el comando de la vista ya captura
	/// sus fallos por dentro, y lo que puede fallar es la lectura, que se traduce a un aviso.
	/// </remarks>
	private void RecalcularUbicacionSinEsperar() => _ = RecalcularUbicacionCommand.ExecuteAsync(null);

	/// <summary>
	/// Intenta completar el kilómetro con la lectura del GPS.
	/// </summary>
	/// <remarks>
	/// Si la posición no pertenece al corredor o no hay lectura válida, se avisa y queda
	/// la captura manual, tal como describe el flujo 5.3 del documento de arquitectura.
	/// </remarks>
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

		// Mientras el GPS fijaba, el operador pudo teclear el KM porque lo sabe. Eso vale más
		// que la lectura: no se pisa, y la fuente sigue siendo manual.
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

			// Una lectura anterior no puede sobrevivir como si fuera captura manual. Lo que el
			// operador haya escrito a mano sí se conserva cuando un reintento falla.
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

		// La bandera evita que el propio GPS marque el kilómetro como capturado a mano.
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

	/// <summary>
	/// Escribir el kilómetro a mano cambia su origen: deja de ser una lectura del GPS.
	/// </summary>
	/// <remarks>
	/// La condición anterior —cambiar a manual solo si además había aviso de GPS— nunca se
	/// cumplía en el caso que importa: con lectura válida no hay aviso, así que corregir a mano
	/// un kilómetro obtenido por GPS lo dejaba marcado como GPS. No se notaba porque la fuente
	/// no se mostraba en ninguna parte; al presentarla (CA 6) queda a la vista.
	/// </remarks>
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
