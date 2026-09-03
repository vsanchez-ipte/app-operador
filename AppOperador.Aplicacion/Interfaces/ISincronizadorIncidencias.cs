using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Envía a Jacob lo que espera en la cola local.
/// </summary>
/// <remarks>
/// <para>
/// Existe por dos motivos concretos, no por costumbre de poner interfaz a todo:
/// </para>
/// <list type="number">
/// <item>
/// <b>Tiene dos consumidores</b> —la pantalla de Cola y la revalidación de sesión— y ninguno
/// tiene por qué conocer cómo se decide el reintento.
/// </item>
/// <item>
/// <b>Sin ella, quien dependa del envío no se puede probar</b> sin montar la base, el cliente
/// HTTP y el catálogo. <c>RevalidarSesionMovil</c> solo necesita comprobar que <i>dispara</i> la
/// sincronización y que un fallo suyo no deshace la revalidación.
/// </item>
/// </list>
/// </remarks>
public interface ISincronizadorIncidencias
{
	/// <summary>Intenta enviar lo pendiente y devuelve qué pasó.</summary>
	Task<ResultadoSincronizacion> EjecutarAsync(CancellationToken cancelacion = default);

	/// <summary>
	/// Intenta enviar <b>una sola</b> incidencia, recién capturada.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Es el envío inmediato al guardar: si Jacob la acepta, el operador la ve en la cola ya
	/// <b>sincronizada</b> y con su folio; si no hay enlace o falla, cae a la cola como pendiente
	/// y sigue el camino normal de reintentos.
	/// </para>
	/// <para>
	/// <b>No arrastra el resto de la cola a propósito.</b> El operador está parado en el
	/// incidente: hacerle esperar a que suban los registros viejos alarga la captura, y si uno
	/// de ellos falla, el aviso sobre <i>el suyo</i> deja de ser claro.
	/// </para>
	/// </remarks>
	Task<ResultadoSincronizacion> EnviarUnaAsync(
		string claveLocal,
		CancellationToken cancelacion = default);
}
