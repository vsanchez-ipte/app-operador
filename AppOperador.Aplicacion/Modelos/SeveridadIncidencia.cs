namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Nivel de severidad del catálogo de Jacob (JTT-1394).
/// </summary>
/// <remarks>
/// <para>
/// <b>Sustituye al enum <c>Gravedad</c>, y no es una traducción mecánica.</b> La app tenía
/// cuatro niveles inventados —Baja, Media, Alta, Crítica— y Jacob tiene tres, con Guid propio.
/// No hay forma de mapear cuatro en tres sin inventar una equivalencia, y lo inventado no lo
/// entendería quien lea la incidencia en el CCO. Por eso la app deja de tener niveles propios y
/// adopta los del servidor.
/// </para>
/// <para>
/// <b>Se conservan nombre y orden del momento de capturar</b>, no solo el identificador: una
/// incidencia capturada sin conexión puede sincronizarse días después, y para entonces el nivel
/// pudo renombrarse. El histórico tiene que seguir diciendo lo que el operador vio.
/// </para>
/// </remarks>
/// <param name="Id">Identificador del nivel en Jacob. Es lo que viaja al sincronizar.</param>
/// <param name="Nivel">Texto que ve el operador: «Crítico», «Advertencia», «Información».</param>
/// <param name="Orden">
/// Posición en la escala, donde <b>menor es más grave</b>. De aquí sale la prioridad de
/// sincronización, en vez de una tabla de equivalencias por nombre que se rompería el día que
/// alguien renombre un nivel.
/// </param>
/// <param name="Hexadecimal">
/// Color con el que se pinta la insignia, en formato <c>#RRGGBB</c>. Viene del catálogo para que
/// el operador y el CCO vean el mismo color para el mismo nivel.
/// </param>
public sealed record SeveridadIncidencia(Guid Id, string Nivel, int Orden, string Hexadecimal)
{
	public override string ToString() => Nivel;
}
