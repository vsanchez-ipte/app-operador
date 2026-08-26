namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Estado de la comunicación con Jacob CCO, tal como se le presenta al operador.
/// </summary>
/// <remarks>
/// <para>
/// Son los cuatro que fija JTT-1386 CA 2, ni uno más. No es lo mismo que
/// <see cref="CausaSinEnlace"/>: aquella explica <b>por qué</b> falló un sondeo concreto y
/// alimenta el detalle bajo el aviso; esta dice <b>cómo está</b> la comunicación ahora mismo y
/// es lo que se pinta en la insignia.
/// </para>
/// <para>
/// <see cref="CausaSinEnlace.SesionRechazada"/> no tiene estado propio y es deliberado: cuando
/// Jacob niega la sesión, la app expulsa al acceso (JTT-1384), así que no hay pantalla donde
/// mostrarlo. El detalle del aviso sí lo distingue mientras dura.
/// </para>
/// </remarks>
public enum EstadoComunicacion
{
	/// <summary>
	/// Hay comunicación con Jacob CCO.
	/// </summary>
	/// <remarks>
	/// Exige comunicación <b>exitosa</b>, no que el dispositivo tenga red (CA 10 y CA 11).
	/// </remarks>
	EnLinea,

	/// <summary>No se alcanza a Jacob CCO y se trabaja con la ventana offline.</summary>
	SinConexion,

	/// <summary>Hay una comprobación o revalidación en curso.</summary>
	Revalidando,

	/// <summary>Jacob CCO contestó, pero con un error que no depende del operador.</summary>
	ErrorDeServicio,
}
