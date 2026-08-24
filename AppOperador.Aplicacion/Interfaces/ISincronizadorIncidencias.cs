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
}
