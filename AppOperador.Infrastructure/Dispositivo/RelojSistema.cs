using AppOperador.Aplicacion.Interfaces;

namespace AppOperador.Infrastructure.Dispositivo;

/// <summary>
/// Reloj real del dispositivo.
/// </summary>
/// <remarks>
/// <para>
/// Es el <b>único</b> punto de la app que consulta <see cref="DateTime.UtcNow"/>. Todo lo
/// demás recibe el instante por parámetro, que es lo que hace comprobables las reglas de
/// vigencia.
/// </para>
/// <para>
/// Va junto a <see cref="RelojMonotonicoDispositivo"/>: los dos son fuentes de tiempo del
/// dispositivo y se usan juntos para medir la ventana offline sin quedar a merced de que
/// alguien cambie la hora (JTT-1383).
/// </para>
/// </remarks>
public sealed class RelojSistema : IClock
{
	public DateTime UtcAhora => DateTime.UtcNow;
}
