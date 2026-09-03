using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Infrastructure.Dispositivo;

/// <summary>
/// Estado real del enlace con Jacob CCO (JTT-1391).
/// </summary>
/// <remarks>
/// <para>
/// Combina dos señales, y las dos hacen falta:
/// </para>
/// <list type="number">
///   <item>
///     <b>La red del dispositivo</b>, que dice si hay por dónde salir. Es barata y cambia
///     sola, así que se escucha en vez de preguntarla.
///   </item>
///   <item>
///     <b>Una consulta a Jacob</b>, que dice si esa red llega hasta el servidor. Sin ella,
///     el indicador mentiría en las dos situaciones que más se dan en campo: cobertura sin
///     servidor alcanzable y servidor levantado al que no se llega (CA 2).
///   </item>
/// </list>
/// <para>
/// <b>Antes de que exista sesión no hay con qué preguntar</b>, porque la sonda va
/// autenticada. En ese caso manda la red a secas: decirle al operador que no hay enlace
/// cuando ni siquiera ha intentado entrar sería un aviso sin fundamento.
/// </para>
/// <para>
/// El evento se levanta en el hilo principal: lo consumen enlaces de la interfaz, y
/// notificarlo desde el hilo del sistema haría fallar la actualización de las vistas.
/// </para>
/// </remarks>
public sealed class ServicioConectividadJacob : IConnectivityService, IDisposable
{
	private readonly IAccesoJacobClient _jacob;
	private readonly ITokenProvider _tokens;

	private bool _hayEnlace;
	private bool _liberado;

	public ServicioConectividadJacob(IAccesoJacobClient jacob, ITokenProvider tokens)
	{
		_jacob = jacob;
		_tokens = tokens;

		// Se parte de lo que diga la red. La primera sonda llega con la primera
		// comprobación explícita, para no lanzar tráfico desde el constructor.
		_hayEnlace = HayRed;

		Connectivity.Current.ConnectivityChanged += AlCambiarLaRed;
	}

	/// <inheritdoc />
	public bool HayEnlace => _hayEnlace;

	/// <inheritdoc />
	public event EventHandler<bool>? EnlaceCambio;

	/// <inheritdoc />
	public async Task<ResultadoSondeo> ComprobarAsync(CancellationToken cancelacion = default)
	{
		// Sin red no hace falta molestar al servidor: la respuesta ya se conoce.
		if (!HayRed)
		{
			return Publicar(ResultadoSondeo.SinTransporte(
				"El dispositivo declara no tener acceso a internet."));
		}

		var token = await _tokens.ObtenerAsync(cancelacion);

		// Sin sesión no hay sonda autenticada posible. Ver las notas del tipo.
		if (string.IsNullOrWhiteSpace(token))
		{
			return Publicar(ResultadoSondeo.SegunLaRed());
		}

		return Publicar(await _jacob.ComprobarEnlaceAsync(token, cancelacion));
	}

	/// <summary>Indica si el dispositivo declara tener acceso a internet.</summary>
	private static bool HayRed =>
		Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

	/// <summary>
	/// Reacciona a que la red aparezca o desaparezca.
	/// </summary>
	/// <remarks>
	/// Perder la red se publica de inmediato, sin sondear: es seguro y evita una petición
	/// que se sabe perdida. Recuperarla <b>no</b> se publica todavía, porque tener red no
	/// implica alcanzar a Jacob: primero se comprueba.
	/// </remarks>
	private void AlCambiarLaRed(object? origen, ConnectivityChangedEventArgs argumentos)
	{
		if (argumentos.NetworkAccess != NetworkAccess.Internet)
		{
			Publicar(ResultadoSondeo.SinTransporte("El dispositivo perdió el acceso a internet."));
			return;
		}

		_ = SondearSinPropagarFallosAsync();
	}

	/// <summary>
	/// Sondea sin dejar escapar excepciones.
	/// </summary>
	/// <remarks>
	/// Lo llama el sistema al recuperar la red y nadie espera el resultado, así que una
	/// excepción aquí no tendría quién la recogiera. Si el sondeo falla, el estado se queda
	/// como estaba y el operador tiene el reintento manual.
	/// </remarks>
	private async Task SondearSinPropagarFallosAsync()
	{
		try
		{
			await ComprobarAsync();
		}
		catch (Exception excepcion)
		{
			Publicar(ResultadoSondeo.SinTransporte($"Falló el sondeo automático: {excepcion.Message}"));
		}
	}

	/// <summary>
	/// Guarda el estado y avisa solo si cambió.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Avisar en cada comprobación dispararía la revalidación una y otra vez, porque quien
	/// escucha el evento la lanza al recuperar el enlace.
	/// </para>
	/// <para>
	/// El evento sigue llevando un booleano: para el indicador de la pantalla solo cuenta si
	/// se puede operar. La causa viaja en el resultado, hacia quien la pidió.
	/// </para>
	/// </remarks>
	private ResultadoSondeo Publicar(ResultadoSondeo sondeo)
	{
		if (_hayEnlace == sondeo.HayEnlace)
		{
			return sondeo;
		}

		_hayEnlace = sondeo.HayEnlace;

		var suscriptores = EnlaceCambio;
		if (suscriptores is not null)
		{
			MainThread.BeginInvokeOnMainThread(() => suscriptores(this, sondeo.HayEnlace));
		}

		return sondeo;
	}

	public void Dispose()
	{
		if (_liberado)
		{
			return;
		}

		Connectivity.Current.ConnectivityChanged -= AlCambiarLaRed;
		_liberado = true;
	}
}
