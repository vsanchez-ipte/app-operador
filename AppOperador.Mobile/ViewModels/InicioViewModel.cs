using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppOperador.Mobile.ViewModels;

/// <summary>
/// Tablero de Inicio: identidad, estado de enlace y los cuatro indicadores operativos.
/// </summary>
/// <remarks>
/// Aquí no se captura nada: es solo lectura del estado local.
/// </remarks>
public sealed partial class InicioViewModel : ObservableObject
{
	// Lo que muestra la tarjeta «KM actual» mientras el GPS fija y cuando no hay lectura útil.
	private const string LeyendoKilometro = "…";
	private const string SinKilometro = "-";

	private readonly ISessionStore _sesiones;
	private readonly IConnectivityService _conectividad;
	private readonly ISyncQueueService _cola;
	private readonly IClock _reloj;
	private readonly CapacidadesDeLaSesion _capacidades;
	private readonly ObtenerKilometroPorUbicacion _kilometro;

	// Una lectura por vez: la pantalla reaparece más rápido de lo que el GPS fija posición.
	private bool _leyendoKilometro;

	[ObservableProperty]
	public partial ResumenOperativo Resumen { get; set; }

	[ObservableProperty]
	public partial string HoraActualizacion { get; set; }

	/// <summary>Kilómetro del corredor en el que está el vehículo ahora, o un guion si no hay lectura.</summary>
	[ObservableProperty]
	public partial string KilometroActual { get; set; }

	public InicioViewModel(
		ISessionStore sesiones,
		IConnectivityService conectividad,
		ISyncQueueService cola,
		IClock reloj,
		CapacidadesDeLaSesion capacidades,
		ObtenerKilometroPorUbicacion kilometro,
		EstadoEnlaceViewModel enlace)
	{
		Enlace = enlace;
		_sesiones = sesiones;
		_conectividad = conectividad;
		_cola = cola;
		_reloj = reloj;
		_capacidades = capacidades;
		_kilometro = kilometro;
		_conectividad.EnlaceCambio += (_, _) => NotificarEstadoEnlace();

		Resumen = ResumenOperativo.Vacio;
		HoraActualizacion = string.Empty;
		KilometroActual = SinKilometro;
	}

	/// <summary>Aviso de modo offline, común a todas las pantallas (JTT-1383 CA 8).</summary>
	public EstadoEnlaceViewModel Enlace { get; }

	/// <summary>Cuenta del operador que tiene la sesión abierta.</summary>
	public string Operador => _sesiones.Actual?.Operador ?? "-";

	/// <summary>
	/// Rol funcional con el que ingresó el operador (JTT-1386 CA 1).
	/// </summary>
	/// <remarks>
	/// Sale de la sesión, como el resto de lo que se muestra aquí (CA 9). Antes la pantalla
	/// llevaba un «Operador de campo» escrito en el XAML que parecía el rol y no lo era: quien
	/// ingresara con otro rol veía igualmente ese texto.
	/// </remarks>
	public string Rol => _sesiones.Actual?.Rol ?? "-";

	/// <summary>
	/// Unidad con la que el operador está trabajando (JTT-1381 CA 13).
	/// </summary>
	/// <remarks>
	/// Se muestra la <b>clave</b>, no el identificador técnico: al operador le sirve el número
	/// económico que lleva pintado la unidad, no el uuid que usa Jacob.
	/// </remarks>
	public string UnidadVehicular => _sesiones.Actual?.UnidadVehicular ?? "-";

	/// <summary>
	/// Si la sesión autoriza registrar incidencias, que es lo que decide si la tarjeta muestra
	/// la insignia «CAPTURA».
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>La insignia estaba escrita a mano en el XAML</b>, heredada de la maqueta: decía
	/// «CAPTURA» siempre, incluso con un operador que no puede capturar. Puesta junto al nombre
	/// y al rol se lee como una afirmación sobre ese operador, así que afirmaba algo falso justo
	/// donde más se cree.
	/// </para>
	/// <para>
	/// Se oculta en vez de sustituirse por otro texto: un «SOLO LECTURA» sería un literal que
	/// Producto no ha fijado. Su ausencia, con el aviso que Captura sí da (JTT-1404 CA 5),
	/// alcanza para contar lo que pasa.
	/// </para>
	/// </remarks>
	public bool PuedeCapturar => _capacidades.Puede(CapacidadOperador.RegistrarIncidencia);

	public bool HayEnlace => _conectividad.HayEnlace;

	/// <summary>Refresca los indicadores del tablero.</summary>
	public async Task ActualizarAsync()
	{
		var pendientes = await _cola.ContarPendientesAsync();
		var registros = await _cola.ObtenerRegistrosAsync();

		Resumen = new ResumenOperativo(
			PendientesSincronizar: pendientes,
			Avisos: 0,
			EvidenciaLocal: registros.Count(r => r.Clase == ClaseRegistro.Evidencia));

		// El kilómetro va aparte y sin esperarlo: la lectura del GPS tarda hasta diez segundos
		// a propósito, y los contadores no tienen por qué aparecer después de ella.
		_ = LeerKilometroAsync();

		// La comparación es en UTC; al operador se le presenta su hora local (DA-10).
		HoraActualizacion = _reloj.UtcAhora.ToLocalTime().ToString("HH:mm:ss");
		OnPropertyChanged(nameof(Operador));
		OnPropertyChanged(nameof(Rol));
		OnPropertyChanged(nameof(UnidadVehicular));
		// Va con la identidad y no con el enlace: las capacidades llegan en la misma
		// instantánea de sesión, y cambian cuando cambia ella.
		OnPropertyChanged(nameof(PuedeCapturar));
	}

	/// <summary>
	/// Sitúa al vehículo en el corredor con la lectura del GPS de este momento.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Antes el indicador tomaba el kilómetro del último registro en cola, es decir, lo que el
	/// operador escribió en su última incidencia: si lo tecleó mal, Inicio repetía el error, y
	/// ahí se quedaba aunque el vehículo llevara horas en otro punto. «KM actual» promete dónde
	/// está, no dónde dijo estar.
	/// </para>
	/// <para>
	/// Sin lectura útil se muestra el guion y no el motivo: la tarjeta no tiene sitio para
	/// explicarlo, y la captura, que sí lo tiene, lo dice al abrirse.
	/// </para>
	/// </remarks>
	private async Task LeerKilometroAsync()
	{
		if (_leyendoKilometro)
		{
			return;
		}

		_leyendoKilometro = true;
		KilometroActual = LeyendoKilometro;
		try
		{
			var resultado = await _kilometro.EjecutarAsync();
			KilometroActual = resultado.HayKilometro ? resultado.Kilometro!.Valor : SinKilometro;
		}
		catch (Exception)
		{
			// Nadie espera esta tarea: lo que escape de aquí no tendría quién lo recogiera y
			// en el hilo de interfaz cierra la app. El caso de uso ya traduce a motivo lo que
			// puede fallar; esto cubre lo que no previó.
			KilometroActual = SinKilometro;
		}
		finally
		{
			_leyendoKilometro = false;
		}
	}

	// Los botones "Simular enlace" y "Simular fallo" de la maqueta son ayudas de
	// demostración para los desarrolladores, no funcionalidad del producto. No se
	// implementan aquí: obligarían al ViewModel a conocer un simulador concreto.

	private void NotificarEstadoEnlace() => OnPropertyChanged(nameof(HayEnlace));
}
