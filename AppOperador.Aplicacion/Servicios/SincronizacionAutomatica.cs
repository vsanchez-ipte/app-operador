using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Servicios;

// Solo escucha el enlace y llama al sincronizador; nunca deja escapar una excepción.
public sealed class SincronizacionAutomatica : IDisposable
{
	private readonly IConnectivityService _conectividad;
	private readonly ISincronizadorIncidencias _sincronizador;
	private readonly IAuditLog _bitacora;

	private bool _escuchando;
	private bool _liberado;

	public SincronizacionAutomatica(
		IConnectivityService conectividad,
		ISincronizadorIncidencias sincronizador,
		IAuditLog bitacora)
	{
		_conectividad = conectividad;
		_sincronizador = sincronizador;
		_bitacora = bitacora;
	}

	// Para que la Cola abierta se refresque; un envío pedido por el operador no lo levanta.
	public event EventHandler<ResultadoSincronizacion>? SincronizacionTerminada;

	// Fuera del constructor: construir no debe lanzar trabajo de fondo. Idempotente.
	public void Iniciar()
	{
		if (_escuchando || _liberado)
		{
			return;
		}

		_conectividad.EnlaceCambio += AlCambiarElEnlace;
		_escuchando = true;
	}

	private void AlCambiarElEnlace(object? origen, bool hayEnlace)
	{
		if (!hayEnlace)
		{
			return;
		}

		_ = SincronizarSinPropagarFallosAsync();
	}

	// Nadie espera el resultado y no hay cancelación: cortarla dejaría registros en Enviando.
	private async Task SincronizarSinPropagarFallosAsync()
	{
		try
		{
			var resultado = await _sincronizador.EjecutarAsync();

			// Otra tanda ya lo atiende: es normal si el operador pulsó el botón a la vez.
			if (resultado.MotivoBloqueo == MotivoNoSincroniza.YaEnCurso)
			{
				return;
			}

			SincronizacionTerminada?.Invoke(this, resultado);
		}
		catch (Exception excepcion)
		{
			await _bitacora.RegistrarAsync(
				NivelAuditoria.Advertencia,
				$"Falló la sincronización automática al recuperar el enlace: {excepcion.GetType().Name}.");
		}
	}

	public void Dispose()
	{
		if (_liberado)
		{
			return;
		}

		if (_escuchando)
		{
			_conectividad.EnlaceCambio -= AlCambiarElEnlace;
			_escuchando = false;
		}

		_liberado = true;
	}
}
