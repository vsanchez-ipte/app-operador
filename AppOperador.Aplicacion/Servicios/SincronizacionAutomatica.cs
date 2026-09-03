using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Servicios;

/// <summary>
/// Envía lo pendiente en cuanto vuelve el enlace con Jacob, sin que nadie lo pida (JTT-1406).
/// </summary>
/// <remarks>
/// <para>
/// <b>Existe porque hasta ahora lo pendiente solo salía si el operador pulsaba el botón.</b> En
/// campo el teléfono va en la bolsa: se pierde la señal en un tramo, se recupera dos kilómetros
/// después y nadie está mirando la pantalla. Sin este enganche, lo capturado se queda en la cola
/// hasta que alguien abra la aplicación y se acuerde. La pantalla de Cola ya prometía
/// «se enviará al recuperar la señal»; esto es lo que hace que sea verdad.
/// </para>
/// <para>
/// <b>No decide nada por su cuenta.</b> No mira la red, no cuenta registros y no consulta la
/// sesión: para eso están <see cref="IConnectivityService"/>, que solo anuncia enlace cuando la
/// sonda autenticada a Jacob contestó —de ahí que tener internet no baste para disparar el envío,
/// CA 2—, y <see cref="ISincronizadorIncidencias"/>, que comprueba permiso, sesión y enlace antes
/// de tocar la cola y devuelve a Pendiente lo que quedó a medio enviar antes de recorrerla, que es
/// el estado consistente del CA 9.
/// </para>
/// <para>
/// Su único trabajo es escuchar y llamar, y hacerlo sin dejar escapar una excepción.
/// </para>
/// </remarks>
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

	/// <summary>
	/// Avisa de que terminó una sincronización que nadie pidió.
	/// </summary>
	/// <remarks>
	/// Lo consume la pantalla de Cola: si el envío ocurre solo mientras el operador la tiene
	/// abierta, la lista se quedaría enseñando como pendiente algo que ya salió. Un envío que el
	/// operador sí pidió no lo levanta —quien lo pidió ya tiene el resultado en la mano—.
	/// </remarks>
	public event EventHandler<ResultadoSincronizacion>? SincronizacionTerminada;

	/// <summary>
	/// Empieza a escuchar el enlace.
	/// </summary>
	/// <remarks>
	/// <b>Separado del constructor a propósito</b>, por lo mismo que
	/// <c>ServicioConectividadJacob</c> no sondea desde el suyo: construir un servicio no debería
	/// poder lanzar tráfico ni trabajo de fondo. Es idempotente, así que llamarlo dos veces no
	/// deja dos suscripciones.
	/// </remarks>
	public void Iniciar()
	{
		if (_escuchando || _liberado)
		{
			return;
		}

		_conectividad.EnlaceCambio += AlCambiarElEnlace;
		_escuchando = true;
	}

	/// <summary>
	/// Reacciona a que el enlace aparezca o desaparezca.
	/// </summary>
	/// <remarks>
	/// Perderlo no requiere nada: lo capturado ya está guardado y la cola lo conserva. Solo
	/// recuperarlo da trabajo.
	/// </remarks>
	private void AlCambiarElEnlace(object? origen, bool hayEnlace)
	{
		if (!hayEnlace)
		{
			return;
		}

		_ = SincronizarSinPropagarFallosAsync();
	}

	/// <summary>
	/// Sincroniza sin dejar escapar excepciones.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Lo dispara un evento del sistema y <b>nadie espera el resultado</b>: una excepción aquí no
	/// tendría quién la recogiera y se llevaría por delante el proceso. Si el envío falla, cada
	/// registro conserva su propio estado y su reintento, y al operador le queda el botón.
	/// </para>
	/// <para>
	/// <b>No se pasa cancelación</b> porque no hay a quién obedecer: el disparo no cuelga de
	/// ninguna pantalla, y cancelarlo a media tanda dejaría registros en Enviando que solo
	/// recuperaría la siguiente.
	/// </para>
	/// </remarks>
	private async Task SincronizarSinPropagarFallosAsync()
	{
		try
		{
			var resultado = await _sincronizador.EjecutarAsync();

			// Que ya hubiera una tanda en marcha es el desenlace normal aquí, no una anomalía:
			// recuperar la señal y pulsar el botón a la vez es justo lo que hace un operador que
			// está esperando a que salga lo suyo. La otra tanda se está ocupando.
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
