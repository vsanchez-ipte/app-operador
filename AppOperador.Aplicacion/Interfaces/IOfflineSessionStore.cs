using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Sesión guardada en disco, para reanudarla tras cerrar la app (JTT-1383).
/// </summary>
/// <remarks>
/// <para>
/// Es distinto de <see cref="ISessionStore"/>: aquel guarda la sesión <b>viva</b>, en
/// memoria, y se vacía al salir; este la conserva entre arranques.
/// </para>
/// <para>
/// <b>Aquí no va el token.</b> Los tokens siguen en el almacenamiento seguro de la
/// plataforma (<see cref="ITokenProvider"/>): la base local viaja en cualquier respaldo del
/// dispositivo.
/// </para>
/// </remarks>
public interface IOfflineSessionStore
{
	/// <summary>Guarda o reemplaza la sesión persistida. Solo hay una a la vez.</summary>
	Task GuardarAsync(SesionOfflinePersistida sesion, CancellationToken cancelacion = default);

	/// <summary>Sesión guardada, o <see langword="null"/> si no hay ninguna.</summary>
	Task<SesionOfflinePersistida?> ObtenerAsync(CancellationToken cancelacion = default);

	/// <summary>Borra la sesión guardada. No toca los registros pendientes.</summary>
	Task LimpiarAsync(CancellationToken cancelacion = default);
}
