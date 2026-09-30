using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Infrastructure.Dispositivo;

// Red del dispositivo más sonda a Jacob. Sin sesión viva manda la red: un token vencido daría 401.
public sealed class ServicioConectividadJacob : IConnectivityService, IDisposable
{
	private readonly IAccesoJacobClient _jacob;
	private readonly ITokenProvider _tokens;
	private readonly ISessionStore _sesiones;

	private bool _hayEnlace;
	private bool _liberado;

	public ServicioConectividadJacob(IAccesoJacobClient jacob, ITokenProvider tokens, ISessionStore sesiones)
	{
		_jacob = jacob;
		_tokens = tokens;
		_sesiones = sesiones;

		// Sin sondear desde el constructor: la primera sonda llega con la primera comprobación.
		_hayEnlace = HayRed;

		Connectivity.Current.ConnectivityChanged += AlCambiarLaRed;
	}

	public bool HayEnlace => _hayEnlace;

	public event EventHandler<bool>? EnlaceCambio;

	public async Task<ResultadoSondeo> ComprobarAsync(CancellationToken cancelacion = default)
	{
		if (!HayRed)
		{
			return Publicar(ResultadoSondeo.SinTransporte(
				"El dispositivo declara no tener acceso a internet."));
		}

		// Sin sesión viva no hay sonda autenticada; el token guardado puede ser de una sesión vencida.
		if (_sesiones.Actual is null)
		{
			return Publicar(ResultadoSondeo.SegunLaRed());
		}

		var token = await _tokens.ObtenerAsync(cancelacion);
		if (string.IsNullOrWhiteSpace(token))
		{
			return Publicar(ResultadoSondeo.SinSesion());
		}

		return Publicar(await _jacob.ComprobarEnlaceAsync(token, cancelacion));
	}

	public void AnotarIntercambio(bool jacobRespondio) =>
		Publicar(ResultadoSondeo.Observado(jacobRespondio));

	private static bool HayRed =>
		Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

	// Perder la red se publica de inmediato; recuperarla, solo después de sondear.
	private void AlCambiarLaRed(object? origen, ConnectivityChangedEventArgs argumentos)
	{
		if (argumentos.NetworkAccess != NetworkAccess.Internet)
		{
			Publicar(ResultadoSondeo.SinTransporte("El dispositivo perdió el acceso a internet."));
			return;
		}

		_ = SondearSinPropagarFallosAsync();
	}

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

	// Solo avisa si cambió: cada aviso dispara la revalidación. En el hilo principal, por las vistas.
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
