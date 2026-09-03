namespace AppOperador.Domain.Reglas;

/// <summary>
/// Los dos umbrales que deciden si una lectura de GPS sirve para poner el kilómetro
/// (JTT-1395 CA 5, 6 y 12).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Los dos valores están asumidos, no entregados.</b> <b>DA-12</b> pide «tolerancia máxima
/// respecto al corredor» y sigue sin contestarse. Se eligen aquí, en un solo sitio y con el
/// razonamiento escrito, en vez de esparcirlos por la pantalla: el día que Producto los fije,
/// **cambiarlos es tocar estas dos constantes y sus pruebas**.
/// </para>
/// <para>
/// <b>De dónde salen los números.</b> Se midió el KMZ del corredor el 24-ago: las paletas de
/// kilometraje —que están al borde de la carretera— quedan a <b>15 m de mediana y 27 m como
/// máximo</b> de la traza. La traza es <b>una sola línea para una vía de dos cuerpos</b>, así que
/// el cuerpo contrario suma unas decenas de metros, y a eso hay que añadirle el error normal de
/// un GPS de teléfono a cielo abierto.
/// </para>
/// </remarks>
public static class ReglaToleranciaCorredor
{
	/// <summary>
	/// Cuánto puede separarse una lectura de la traza y seguir contando como «en el corredor».
	/// </summary>
	/// <remarks>
	/// <b>60 m: 27 de la peor paleta, más el cuerpo contrario, más el error del GPS.</b>
	///
	/// Subirlo mucho tiene un riesgo concreto: la carretera libre corre en paralelo a la de cuota
	/// en varios tramos, y una tolerancia generosa le daría kilómetro de corredor a un vehículo
	/// que va por la libre. Bajarlo tiene el riesgo contrario y peor de leer: rechazar a alguien
	/// que sí está en la autopista, que es lo que pasaría con los 50 m de un primer impulso.
	/// </remarks>
	public const double ToleranciaLateralMetros = 60;

	/// <summary>
	/// Precisión peor que ésta y la lectura no se usa (CA 12).
	/// </summary>
	/// <remarks>
	/// <b>50 m.</b> Un teléfono a cielo abierto da entre 5 y 20 m; pasar de 50 significa que está
	/// resolviendo por antenas de telefonía y no por satélite, y esa lectura puede caer a cientos
	/// de metros. Aceptarla daría un kilómetro creíble y equivocado, **que es el peor resultado
	/// posible**: el operador no tiene forma de notarlo y nadie va a dudar de un dato marcado
	/// como GPS.
	///
	/// No se compara contra <see cref="ToleranciaLateralMetros"/> aunque los dos se midan en
	/// metros: uno dice cuánto puede fallar la lectura y el otro cuánto puede alejarse de la
	/// carretera. Atarlos obligaría a mover los dos para corregir uno.
	/// </remarks>
	public const double PrecisionMaximaMetros = 50;

	/// <summary>Indica si la precisión reportada permite fiarse de la lectura.</summary>
	public static bool PrecisionAceptable(double precisionMetros) =>
		!double.IsNaN(precisionMetros)
		&& precisionMetros >= 0
		&& precisionMetros <= PrecisionMaximaMetros;

	/// <summary>Indica si una lectura cae dentro del corredor, dada su desviación de la traza.</summary>
	public static bool DentroDelCorredor(double desviacionMetros) =>
		!double.IsNaN(desviacionMetros)
		&& desviacionMetros >= 0
		&& desviacionMetros <= ToleranciaLateralMetros;
}
